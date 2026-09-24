using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// O provedor OIDC de teste de <strong>ADR-0006</strong>: um emissor hospedado
/// <strong>in-process</strong> pelo próprio conjunto de testes, que expõe
/// <c>/.well-known/openid-configuration</c> e um JWKS, assina com uma chave RSA
/// <strong>gerada nesta execução</strong> e emite, sob demanda, o token válido e os quatro tokens
/// que RF-19 manda recusar.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Sem container</strong> (ADR-0005): não há Keycloak, não há Duende, não há imagem a
/// baixar. O emissor são dois <c>GET</c> de JSON, e é tudo de que o <c>JwtBearer</c> precisa para
/// descobrir o emissor e as chaves públicas.
/// </para>
/// <para>
/// <strong>Por que Kestrel e não <c>WebApplicationFactory</c>.</strong> ADR-0006 nomeia a
/// <c>WebApplicationFactory</c> como o meio de hospedar, e ela serve enquanto quem consome o
/// emissor vive no mesmo processo — o <c>TestServer</c> dela é um pipeline em memória, sem socket.
/// Aqui quem consome é a <em>aplicação gerada</em>, que a camada 3 sobe como
/// <strong>processo separado</strong> (é o que separa esta camada da 1 e da 2: código compilado,
/// executando de verdade). Um pipeline em memória é inalcançável de outro processo, então o
/// emissor escuta uma porta de <em>loopback</em> com Kestrel. O que a decisão protege continua
/// inteiro: in-process, sem container, chave gerada no teste e nunca versionada.
/// </para>
/// <para>
/// <strong>Os tokens são montados aqui, à mão</strong> — cabeçalho, corpo e assinatura RSA sobre
/// <c>Base64Url</c> —, e não pela biblioteca que a aplicação usa para validá-los. É deliberado:
/// produzir e conferir com a mesma implementação faria um par de defeitos simétricos passar
/// despercebido, que é a objeção de ADR-0006 a "mockar o handler" levada até o fim. O custo é
/// meia dúzia de linhas de codificação; o ganho é que o <c>401</c> e o <c>200</c> desta suíte
/// falam de interoperabilidade real.
/// </para>
/// <para>
/// <strong>Nada é gravado em disco.</strong> As duas chaves vivem na memória do processo de teste e
/// morrem com ele. É o critério de aceite 6 de T07: chave gerada no teste, nunca versionada — e
/// <c>InProcessOidcIssuerTests</c> é quem o afirma como teste, em vez de deixá-lo como intenção.
/// </para>
/// </remarks>
public sealed class InProcessOidcIssuer : IAsyncDisposable
{
    /// <summary>A audiência que o projeto gerado recebe em <c>Jwt:Audience</c>.</summary>
    public const string Audience = "camada3.jwt.api";

    /// <summary>O identificador da chave publicada no JWKS.</summary>
    private const string KeyId = "camada3-rsa";

    private readonly WebApplication _app;
    private readonly RSA _publicada;
    private readonly RSA _intrusa;

    private InProcessOidcIssuer(WebApplication app, string authority, RSA publicada, RSA intrusa)
    {
        _app = app;
        _publicada = publicada;
        _intrusa = intrusa;
        Authority = authority;
    }

    /// <summary>
    /// O endereço base do emissor, <strong>sem barra no fim</strong> — o valor de
    /// <c>Jwt:Authority</c> e o <c>iss</c> anunciado no documento de descoberta.
    /// </summary>
    public string Authority { get; }

    /// <summary>O JWKS publicado, como o projeto gerado o lê.</summary>
    public string Jwks { get; private set; } = string.Empty;

    /// <summary>O documento de descoberta publicado.</summary>
    public string Discovery { get; private set; } = string.Empty;

    /// <summary>Sobe o emissor numa porta livre de <c>loopback</c>.</summary>
    public static async Task<InProcessOidcIssuer> StartAsync(CancellationToken cancellationToken)
    {
        RSA publicada = RSA.Create(2048);

        // A segunda chave é o caso "assinatura inválida" de ADR-0006: ela NUNCA entra no JWKS. Ela
        // usa o mesmo `kid` da publicada de propósito — assim a aplicação encontra a chave que o
        // cabeçalho aponta e recusa o token pela CONFERÊNCIA CRIPTOGRÁFICA, e não por não achar
        // chave nenhuma. Com um `kid` desconhecido, o teste passaria mesmo que a verificação de
        // assinatura estivesse desligada.
        RSA intrusa = RSA.Create(2048);

        string jwks = JwksOf(publicada);

        // O documento de descoberta só pode ser montado DEPOIS do `Start`, porque ele anuncia o
        // `issuer` e o `issuer` é o endereço que o Kestrel acabou de bindar. O `handler` lê esta
        // variável capturada; ela é preenchida antes de esta função retornar, e antes disso
        // ninguém no mundo conhece a porta para chegar aqui.
        string discovery = string.Empty;

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();

        WebApplication app = builder.Build();

        // Porta 0: quem escolhe é o sistema operacional, no ato de bindar, e o socket nunca é
        // solto no meio do caminho. Ver `EnderecoBindado`.
        app.Urls.Add("http://127.0.0.1:0");

        app.MapGet(
            "/.well-known/openid-configuration",
            () => Results.Content(discovery, "application/json"));

        app.MapGet("/.well-known/jwks.json", () => Results.Content(jwks, "application/json"));

        await app.StartAsync(cancellationToken);

        string authority = EnderecoBindado(app);

        discovery = DiscoveryOf(authority);

        return new InProcessOidcIssuer(app, authority, publicada, intrusa)
        {
            Jwks = jwks,
            Discovery = discovery,
        };
    }

    /// <summary>
    /// Um token que passa nas quatro conferências: assinado com a chave do JWKS, com o
    /// <c>iss</c> do emissor, a audiência configurada e validade de dez minutos.
    /// </summary>
    public string TokenValido() => Token(
        _publicada,
        Authority,
        Audience,
        DateTimeOffset.UtcNow.AddMinutes(10));

    /// <summary>
    /// Caso 1 de RF-19 — <strong>assinatura inválida</strong>: tudo certo, exceto a chave, que é a
    /// segunda, ausente do JWKS.
    /// </summary>
    public string TokenComAssinaturaInvalida() => Token(
        _intrusa,
        Authority,
        Audience,
        DateTimeOffset.UtcNow.AddMinutes(10));

    /// <summary>
    /// Caso 2 de RF-19 — <strong>emissor errado</strong>: assinado pela chave legítima, mas com um
    /// <c>iss</c> diferente do que o <c>Authority</c> anuncia.
    /// </summary>
    public string TokenComEmissorErrado() => Token(
        _publicada,
        "https://outro-emissor.exemplo/realms/impostor",
        Audience,
        DateTimeOffset.UtcNow.AddMinutes(10));

    /// <summary>
    /// Caso 3 de RF-19 — <strong>audiência errada</strong>: emitido para outra API.
    /// </summary>
    public string TokenComAudienciaErrada() => Token(
        _publicada,
        Authority,
        "outra-api",
        DateTimeOffset.UtcNow.AddMinutes(10));

    /// <summary>
    /// Caso 4 de RF-19 — <strong>expirado</strong>: <c>exp</c> trinta segundos no passado.
    /// </summary>
    /// <remarks>
    /// Trinta segundos, e não uma hora, é o número que <em>afirma</em> a
    /// <c>ClockSkew = TimeSpan.Zero</c> da composição: a folga padrão da biblioteca é de cinco
    /// minutos, e com ela este token seria <strong>aceito</strong>. Se alguém tirar a linha do
    /// <c>Program.cs</c>, o caso 4 fica vermelho.
    /// </remarks>
    public string TokenExpirado() => Token(
        _publicada,
        Authority,
        Audience,
        DateTimeOffset.UtcNow.AddSeconds(-30));

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();

        _publicada.Dispose();
        _intrusa.Dispose();
    }

    /// <summary>O JWKS de uma chave: só a parte <strong>pública</strong>, que é o que se publica.</summary>
    public static string JwksOf(RSA key)
    {
        ArgumentNullException.ThrowIfNull(key);

        RSAParameters parameters = key.ExportParameters(includePrivateParameters: false);

        JsonObject jwks = new()
        {
            ["keys"] = new JsonArray(
                new JsonObject
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = "RS256",
                    ["kid"] = KeyId,
                    ["n"] = Base64Url.EncodeToString(parameters.Modulus!),
                    ["e"] = Base64Url.EncodeToString(parameters.Exponent!),
                }),
        };

        return jwks.ToJsonString();
    }

    private static string DiscoveryOf(string authority) =>
        new JsonObject
        {
            // O `issuer` é igual ao `Authority` porque é assim que a descoberta OIDC amarra os
            // dois: é este valor que o `JwtBearer` compara com o `iss` de cada token.
            ["issuer"] = authority,
            ["jwks_uri"] = $"{authority}/.well-known/jwks.json",
            ["authorization_endpoint"] = $"{authority}/connect/authorize",
            ["token_endpoint"] = $"{authority}/connect/token",
            ["response_types_supported"] = new JsonArray("code"),
            ["subject_types_supported"] = new JsonArray("public"),
            ["id_token_signing_alg_values_supported"] = new JsonArray("RS256"),
        }.ToJsonString();

    /// <summary>
    /// Monta e assina um JWS compacto: <c>base64url(header).base64url(payload).base64url(assinatura)</c>.
    /// </summary>
    private static string Token(
        RSA key,
        string issuer,
        string audience,
        DateTimeOffset expiresAt)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string header = Segment(new JsonObject
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = KeyId,
        });

        string payload = Segment(new JsonObject
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = "pessoa-de-teste",
            ["jti"] = Guid.NewGuid().ToString("N"),

            // `nbf` um minuto atrás: com `ClockSkew` zerado, um `nbf` no instante exato da emissão
            // pode cair do lado errado do relógio do processo da aplicação e recusar um token que
            // deveria valer. Isso tornaria o teste intermitente por um motivo que não é o assunto
            // dele — o assunto é `exp`, e esse está no caso 4.
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
        });

        byte[] signed = Encoding.ASCII.GetBytes($"{header}.{payload}");

        byte[] signature = key.SignData(
            signed,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{header}.{payload}.{Base64Url.EncodeToString(signature)}";
    }

    private static string Segment(JsonNode node) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            node.ToJsonString(new JsonSerializerOptions { WriteIndented = false })));

    /// <summary>
    /// O endereço que o Kestrel <strong>efetivamente</strong> bindou, lido depois do
    /// <c>StartAsync</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>O defeito que esta função existe para não ter.</strong> A versão anterior escolhia
    /// a porta antes de subir: um <c>TcpListener</c> na porta 0, lia a porta que o sistema deu,
    /// <strong>soltava o socket</strong> e só depois mandava o Kestrel bindar aquele número. Entre
    /// soltar e rebindar existe uma janela, e a camada 3 executa em paralelo vários processos que
    /// também reservam porta dinâmica — é a definição de corrida, e ela aparece exatamente onde
    /// apareceu: na suíte inteira, sob carga, e nunca com o teste isolado.
    /// </para>
    /// <para>
    /// Aqui o socket <strong>nunca é solto</strong>: <c>http://127.0.0.1:0</c> vai para o Kestrel,
    /// que binda e devolve o endereço real em <c>IServerAddressesFeature</c> — que é o que
    /// <see cref="WebApplication.Urls"/> expõe. Não há janela porque não há intervalo entre
    /// escolher e possuir.
    /// </para>
    /// <para>
    /// A conferência de que a porta deixou de ser <c>0</c> não é zelo: se um dia o servidor parar
    /// de reescrever a coleção de endereços, o <c>Authority</c> sairia daqui como
    /// <c>http://127.0.0.1:0</c> e o sintoma seria de novo um <em>timeout</em> de cliente, longe
    /// da causa. Falhar aqui, na construção, é o que mantém a causa perto do efeito.
    /// </para>
    /// </remarks>
    private static string EnderecoBindado(WebApplication app)
    {
        string[] addresses = [.. app.Urls];

        if (addresses.Length != 1)
        {
            throw new InvalidOperationException(
                "O emissor OIDC de teste bindou " + addresses.Length + " endereço(s) " +
                $"({string.Join(", ", addresses)}), e precisa ser exatamente um: o `Authority` que " +
                "a aplicação gerada recebe é um só.");
        }

        string authority = addresses[0].TrimEnd('/');

        if (authority.EndsWith(":0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O servidor devolveu '{authority}' depois do Start: a porta continua sendo 0, " +
                "então ele não reescreveu o endereço com a porta que bindou. O `Authority` daqui " +
                "seria inalcançável e o sintoma apareceria longe da causa, como um timeout de " +
                "cliente.");
        }

        return authority;
    }
}
