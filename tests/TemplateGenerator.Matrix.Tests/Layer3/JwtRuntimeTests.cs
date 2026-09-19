using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Camada 3 do eixo <c>auth/jwt</c> (T07): extrai o pacote, compila, sobe a aplicação apontando
/// <c>Jwt:Authority</c> e <c>Jwt:Audience</c> para o emissor OIDC in-process de ADR-0006, e
/// exercita <strong>um token de verdade</strong> — o que passa e os quatro que RF-19 manda
/// recusar.
/// </summary>
/// <remarks>
/// <para>
/// <strong>O que esta camada acrescenta e nenhuma outra prova.</strong> A camada 1 lê o
/// <c>Program.cs</c> e afirma que as quatro conferências estão <em>escritas</em>; a camada 2
/// afirma que o projeto <em>compila</em>. Só aqui um token é assinado, enviado e conferido pelo
/// <c>JwtBearer</c> de verdade, contra as chaves públicas que a aplicação foi buscar sozinha no
/// <c>jwks_uri</c> do emissor. A diferença é concreta: uma composição em que
/// <c>ValidateAudience</c> estivesse desligado passa nas camadas 1 e 2 — a linha continua escrita,
/// o projeto continua compilando — e falha aqui, em
/// <see cref="Audiencia_errada_responde_401_RF19"/>.
/// </para>
/// <para>
/// <strong>Um teste por caso de rejeição</strong>, e não um teste com quatro asserts. Num teste
/// único, o primeiro <c>Assert</c> que falhasse esconderia os três seguintes, e o relatório diria
/// "a rejeição de JWT falhou" quando o que se precisa saber é <em>qual</em> das quatro
/// conferências parou de valer.
/// </para>
/// <para>
/// <strong>Por que o motivo do 401 é conferido, e não só o status.</strong> Quatro tokens
/// diferentes recusados com o mesmo <c>401</c> podem ser quatro conferências funcionando — ou uma
/// só falha comum a todos: um <c>Authority</c> inalcançável, um JWKS vazio, um relógio absurdo.
/// Nesse cenário os quatro testes ficariam verdes sem que nenhuma das quatro conferências
/// estivesse sendo exercitada, e o <c>200</c> do token válido seria o único sinal — um sinal que um
/// erro de configuração do <em>teste</em> também derrubaria. Por isso cada caso lê o
/// <c>WWW-Authenticate</c> da resposta e cobra que a recusa fale da conferência certa.
/// </para>
/// <para>
/// <strong>Nada é pulado.</strong> Se o pacote não compilar, se a aplicação não subir ou se o
/// emissor não responder, o teste <strong>falha</strong> com a saída do comando colada
/// (<see cref="DatabaseRuntime"/>). Um <c>Skip</c> aqui apagaria a única camada que olha
/// comportamento.
/// </para>
/// </remarks>
public abstract class JwtRuntimeTests(JwtRuntimeFixture fixture)
{
    private readonly JwtRuntimeFixture _fixture = fixture;

    /// <summary>Critério de aceite 2 de T07: token válido acessa o CRUD protegido.</summary>
    /// <remarks>
    /// É também a contraprova dos quatro testes de rejeição: sem ela, uma aplicação que respondesse
    /// <c>401</c> a <em>tudo</em> — porque o <c>Authority</c> não resolve, porque o JWKS veio vazio
    /// — passaria nos quatro e a suíte ficaria verde sem nunca ter validado um token.
    /// </remarks>
    [Fact]
    public async Task Token_valido_do_provedor_externo_acessa_o_CRUD_protegido()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpClient autenticado = _fixture.Client(_fixture.Issuer.TokenValido());

        using (HttpResponseMessage created = await autenticado.PostAsJsonAsync(
            "items",
            new { title = "Item autenticado por JWT externo" },
            cancellationToken))
        {
            Log($"POST /items (Bearer válido) -> {(int)created.StatusCode} " +
                $"Location={created.Headers.Location}");

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("/items/1", created.Headers.Location?.ToString());
        }

        using (HttpResponseMessage list = await autenticado.GetAsync("items", cancellationToken))
        {
            string body = await list.Content.ReadAsStringAsync(cancellationToken);

            Log($"GET /items (Bearer válido) -> {(int)list.StatusCode} {body}");

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            using JsonDocument json = JsonDocument.Parse(body);

            Assert.Equal(1, json.RootElement.GetArrayLength());

            Assert.Equal(
                "Item autenticado por JWT externo",
                json.RootElement[0].GetProperty("title").GetString());
        }

        using (HttpResponseMessage one = await autenticado.GetAsync("items/1", cancellationToken))
        {
            Log($"GET /items/1 (Bearer válido) -> {(int)one.StatusCode}");

            Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        }

        using (HttpResponseMessage replaced = await autenticado.PutAsJsonAsync(
            "items/1",
            new { title = "Item renomeado" },
            cancellationToken))
        {
            Log($"PUT /items/1 (Bearer válido) -> {(int)replaced.StatusCode}");

            Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        }

        using (HttpResponseMessage removed = await autenticado.DeleteAsync(
            "items/1",
            cancellationToken))
        {
            Log($"DELETE /items/1 (Bearer válido) -> {(int)removed.StatusCode}");

            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }
    }

    /// <summary>RF-19, caso 1: assinatura feita com uma chave ausente do JWKS.</summary>
    [Fact]
    public Task Assinatura_invalida_responde_401_RF19() => RecusaAsync(
        "assinatura inválida",
        _fixture.Issuer.TokenComAssinaturaInvalida(),
        ["signature", "key"]);

    /// <summary>RF-19, caso 2: <c>iss</c> diferente do emissor que o <c>Authority</c> anuncia.</summary>
    [Fact]
    public Task Emissor_errado_responde_401_RF19() => RecusaAsync(
        "emissor errado",
        _fixture.Issuer.TokenComEmissorErrado(),
        ["issuer"]);

    /// <summary>RF-19, caso 3: <c>aud</c> diferente do <c>Audience</c> configurado.</summary>
    [Fact]
    public Task Audiencia_errada_responde_401_RF19() => RecusaAsync(
        "audiência errada",
        _fixture.Issuer.TokenComAudienciaErrada(),
        ["audience"]);

    /// <summary>
    /// RF-19, caso 4: <c>exp</c> trinta segundos no passado — recusado <strong>sem folga de
    /// relógio</strong>.
    /// </summary>
    /// <remarks>
    /// Trinta segundos é o número que afirma <c>ClockSkew = TimeSpan.Zero</c>: com a folga padrão
    /// de cinco minutos da biblioteca, este token seria aceito e este teste ficaria vermelho.
    /// </remarks>
    [Fact]
    public Task Token_expirado_responde_401_RF19_sem_folga_de_relogio() => RecusaAsync(
        "expirado há 30 segundos",
        _fixture.Issuer.TokenExpirado(),
        ["expired", "lifetime"]);

    /// <summary>
    /// Critério de aceite 4 de T07: sem token o CRUD inteiro responde <c>401</c>, e <c>/health</c>
    /// continua público (RF-12).
    /// </summary>
    [Fact]
    public async Task Sem_token_todo_o_CRUD_responde_401_e_a_saude_continua_publica()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpClient anonimo = _fixture.Client();

        using (HttpResponseMessage health = await anonimo.GetAsync("health", cancellationToken))
        {
            string body = await health.Content.ReadAsStringAsync(cancellationToken);

            Log($"GET /health (sem Authorization) -> {(int)health.StatusCode} {body}");

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Contains("\"status\":\"ok\"", body, StringComparison.Ordinal);
        }

        // TODO verbo, e não só o `GET`: `RequireAuthorization` no grupo cobre os cinco de uma vez,
        // mas é o grupo que precisa estar protegido — um endpoint mapeado fora dele passaria
        // despercebido se o teste olhasse uma rota só.
        foreach ((HttpMethod method, string route) in CrudSemToken())
        {
            using HttpRequestMessage message = new(method, route);

            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                message.Content = JsonContent.Create(new { title = "Não deveria entrar" });
            }

            using HttpResponseMessage denied = await anonimo.SendAsync(message, cancellationToken);

            Log($"{method} /{route} (sem Authorization) -> {(int)denied.StatusCode}");

            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
    }

    /// <summary>RF-20 em runtime: Swagger presente ou ausente conforme a seleção.</summary>
    /// <remarks>
    /// As duas combinações desta camada alternam o eixo, como manda a estratégia de testes — uma
    /// com <c>swagger = true</c> e outra com <c>false</c> —, de modo que os dois estados sejam
    /// exercitados <em>em execução</em>, e não só no <c>.csproj</c>.
    /// </remarks>
    [Fact]
    public async Task Swagger_responde_ou_nao_conforme_a_selecao()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpClient anonimo = _fixture.Client();

        using HttpResponseMessage openapi = await anonimo.GetAsync(
            "openapi/v1.json",
            cancellationToken);

        Log($"GET /openapi/v1.json (swagger={_fixture.Swagger}) -> {(int)openapi.StatusCode}");

        Assert.Equal(
            _fixture.Swagger ? HttpStatusCode.OK : HttpStatusCode.NotFound,
            openapi.StatusCode);
    }

    /// <summary>
    /// Envia <paramref name="token"/> ao CRUD protegido e cobra <c>401</c> — e que o
    /// <c>WWW-Authenticate</c> da recusa nomeie a conferência que falhou.
    /// </summary>
    private async Task RecusaAsync(
        string caso,
        string token,
        IReadOnlyList<string> esperadoNoMotivo)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpClient client = _fixture.Client(token);

        using HttpResponseMessage response = await client.GetAsync("items", cancellationToken);

        string motivo = string.Join(
            " ",
            response.Headers.WwwAuthenticate.Select(header => header.ToString()));

        Log($"GET /items (Bearer com {caso}) -> {(int)response.StatusCode} " +
            $"WWW-Authenticate: {motivo}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // O corpo não pode vazar o motivo para quem chamou — o `WWW-Authenticate` é o canal certo
        // — mas o teste precisa dele para saber que recusou pelo motivo certo.
        Assert.True(
            esperadoNoMotivo.Any(termo =>
                motivo.Contains(termo, StringComparison.OrdinalIgnoreCase)),
            $"{_fixture.Descricao}: o token com {caso} foi recusado com 401, mas o " +
            $"'WWW-Authenticate' não menciona nenhum de [{string.Join(", ", esperadoNoMotivo)}] — " +
            $"ele diz '{motivo}'. Um 401 pelo motivo errado (Authority inalcançável, JWKS vazio) " +
            "deixaria os quatro casos de RF-19 verdes sem que nenhuma das quatro conferências " +
            "estivesse sendo exercitada.");
    }

    private static IEnumerable<(HttpMethod Method, string Route)> CrudSemToken() =>
    [
        (HttpMethod.Get, "items"),
        (HttpMethod.Get, "items/1"),
        (HttpMethod.Post, "items"),
        (HttpMethod.Put, "items/1"),
        (HttpMethod.Delete, "items/1"),
    ];

    private static void Log(string line) =>
        TestContext.Current.TestOutputHelper?.WriteLine(line);
}

/// <summary>
/// A linha <c>clean + sqlite + jwt</c> da tabela da camada 3 (docs/quality/test-strategy.md), com
/// Swagger <strong>ligado</strong>: a validação de JWT e os quatro casos de rejeição de RF-19, com
/// banco de verdade e migração aplicada pelos comandos do README.
/// </summary>
public sealed class CleanComSqliteEJwt : JwtRuntimeFixture
{
    protected override GenerationRequest Request { get; } =
        new("Camada3.Jwt", "clean", "sqlite", "jwt", true, "net10.0");
}

/// <summary>
/// A linha <c>simple + none + jwt</c> da tabela da camada 3, com Swagger
/// <strong>desligado</strong>: <strong>JWT sem banco nenhum</strong> — o critério de aceite 5 de
/// T07 em execução, e não só na árvore do pacote.
/// </summary>
public sealed class SimpleSemBancoEJwt : JwtRuntimeFixture
{
    protected override GenerationRequest Request { get; } =
        new("Camada3.Jwt", "simple", "none", "jwt", false, "net10.0");
}

/// <inheritdoc cref="CleanComSqliteEJwt"/>
[Trait("Camada", "3")]
public sealed class JwtRuntimeCleanSqliteTests(CleanComSqliteEJwt fixture)
    : JwtRuntimeTests(fixture), IClassFixture<CleanComSqliteEJwt>;

/// <inheritdoc cref="SimpleSemBancoEJwt"/>
[Trait("Camada", "3")]
public sealed class JwtRuntimeSimpleSemBancoTests(SimpleSemBancoEJwt fixture)
    : JwtRuntimeTests(fixture), IClassFixture<SimpleSemBancoEJwt>;
