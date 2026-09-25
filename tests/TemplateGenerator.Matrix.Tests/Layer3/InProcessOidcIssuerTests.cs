using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// O critério de aceite 6 de T07 e a verificação 4 escritos como teste: o emissor OIDC roda
/// <strong>in-process, sem container</strong>, a chave de assinatura é <strong>gerada nesta
/// execução</strong>, e <strong>nenhuma chave está versionada</strong> no repositório.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Por que isto é teste e não só um comando no relatório.</strong> Uma varredura executada
/// à mão vale para o dia em que foi executada. A forma mais provável de uma chave entrar no
/// repositório é alguém, no futuro, "facilitar" a montagem do emissor gravando um PEM ao lado do
/// teste — e essa pessoa não vai reler o relatório de T07. O teste vai estar vermelho na frente
/// dela. O comando continua no relatório, como evidência da execução; este teste é o que sustenta
/// a afirmação daqui para a frente.
/// </para>
/// <para>
/// <strong>Sem o <c>Trait</c> de camada 3</strong>, de propósito: não há pacote gerado, não há
/// <c>build</c> e não há aplicação subindo. São milissegundos, e por isso a afirmação roda no
/// conjunto rápido, em todo commit — que é onde uma chave recém-versionada precisa ser pega.
/// </para>
/// </remarks>
public sealed class InProcessOidcIssuerTests
{
    /// <summary>Extensões que carregam chave privada ou certificado.</summary>
    private static readonly string[] _keyFileExtensions =
        [".pem", ".key", ".pfx", ".p12", ".jks", ".snk", ".p8", ".jwk", ".der", ".cer", ".crt"];

    /// <summary>Diretórios que não são conteúdo versionado do repositório.</summary>
    private static readonly string[] _ignoredDirectories =
        [".git", "bin", "obj", "node_modules", "dist", ".angular", ".vs", "TestResults"];

    /// <summary>O começo de qualquer bloco PEM.</summary>
    /// <remarks>
    /// Montado em duas partes, e não escrito inteiro: um literal completo faria <em>este</em>
    /// arquivo ser o primeiro achado da varredura. A primeira execução do teste o acusou, o que é
    /// a prova barata de que a varredura enxerga o que diz enxergar.
    /// </remarks>
    private static readonly string _pemBlock = "-----" + "BEGIN";

    [Fact]
    public async Task O_emissor_responde_descoberta_e_JWKS_por_HTTP_de_verdade()
    {
        // É o que a `WebApplicationFactory` não daria (ver InProcessOidcIssuer): a aplicação
        // gerada roda em OUTRO processo e vai buscar estes dois documentos por socket. Se um deles
        // não respondesse, os quatro casos de rejeição de RF-19 ficariam verdes pelo motivo
        // errado — tudo 401 porque o emissor não existe.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using InProcessOidcIssuer issuer = await InProcessOidcIssuer.StartAsync(
            cancellationToken);

        // Sem proxy, e isto não é ajuste de conveniência. O `HttpClient` padrão resolve o proxy do
        // sistema, e numa máquina de empresa isso pode significar WPAD e um gateway que ACEITA a
        // conexão e não responde — que é exatamente o sintoma que derrubou este teste na suíte
        // inteira: conexão estabelecida, leitura abortada aos dez segundos. O que o teste afirma é
        // que o EMISSOR responde em `127.0.0.1`; mandar a chamada pela rede da empresa para chegar
        // ao próprio processo não faz parte da afirmação, e só acrescenta uma dependência externa
        // num teste que ADR-0005 existe para manter local.
        using HttpClientHandler direto = new() { UseProxy = false };

        using HttpClient client = new(direto) { Timeout = TimeSpan.FromSeconds(10) };

        using JsonDocument discovery = JsonDocument.Parse(await BaixarAsync(
            client,
            issuer,
            "/.well-known/openid-configuration",
            cancellationToken));

        // O `issuer` anunciado É o `Authority` configurado: é essa igualdade que o `JwtBearer`
        // usa para decidir o caso "emissor errado" de RF-19.
        Assert.Equal(issuer.Authority, discovery.RootElement.GetProperty("issuer").GetString());

        string jwksUri = discovery.RootElement.GetProperty("jwks_uri").GetString()!;

        Assert.StartsWith(issuer.Authority, jwksUri, StringComparison.Ordinal);

        using JsonDocument jwks = JsonDocument.Parse(await BaixarAsync(
            client,
            issuer,
            jwksUri[issuer.Authority.Length..],
            cancellationToken));

        JsonElement key = Assert.Single(jwks.RootElement.GetProperty("keys").EnumerateArray());

        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("n").GetString()));

        // Só a parte pública é publicada. Um `d` aqui seria a chave privada servida na rede.
        Assert.False(key.TryGetProperty("d", out _));
    }

    [Fact]
    public async Task A_chave_de_assinatura_e_gerada_a_cada_execucao()
    {
        // ADR-0006: "chave RSA gerada no próprio teste". Dois emissores precisam ter chaves
        // DIFERENTES — se fossem iguais, a chave viria de uma constante em algum lugar, e uma
        // constante é um segredo versionado com outro nome.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using InProcessOidcIssuer primeiro = await InProcessOidcIssuer.StartAsync(
            cancellationToken);

        await using InProcessOidcIssuer segundo = await InProcessOidcIssuer.StartAsync(
            cancellationToken);

        Assert.NotEqual(Modulus(primeiro.Jwks), Modulus(segundo.Jwks));
        Assert.NotEqual(primeiro.Authority, segundo.Authority);

        // E a chave "intrusa" — a do caso "assinatura inválida" — não pode estar no JWKS, ou o
        // caso 1 de RF-19 seria recusado por outro motivo que não a assinatura.
        using RSA qualquer = RSA.Create(2048);

        Assert.NotEqual(Modulus(InProcessOidcIssuer.JwksOf(qualquer)), Modulus(primeiro.Jwks));
    }

    [Fact]
    public void Nenhuma_chave_nem_certificado_esta_versionado_no_repositorio()
    {
        // Verificação 4 de T07, sobre a árvore de trabalho inteira. Duas formas de o segredo
        // entrar: um arquivo de chave, pego pela extensão; e uma chave colada dentro de um arquivo
        // de texto, pega pelo cabeçalho PEM. A segunda é a que um `.gitignore` não pega.
        List<string> offenders = [];
        int scanned = 0;

        foreach (string file in Files(RepositoryLayout.Root))
        {
            scanned++;

            string relative = Path.GetRelativePath(RepositoryLayout.Root, file)
                .Replace(Path.DirectorySeparatorChar, '/');

            if (_keyFileExtensions.Any(extension =>
                file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add($"{relative} (extensão de chave ou certificado)");

                continue;
            }

            if (Text(file) is string content &&
                content.Contains(_pemBlock, StringComparison.Ordinal))
            {
                offenders.Add($"{relative} (bloco PEM)");
            }
        }

        // Sanidade: se a varredura não encontrasse arquivo nenhum — raiz errada, filtro invertido —
        // a afirmação abaixo passaria sem olhar nada, que é o modo de falha que ADR-0011 nomeia.
        Assert.True(
            scanned > 100,
            $"A varredura leu só {scanned} arquivo(s) sob '{RepositoryLayout.Root}'. O " +
            "repositório tem muito mais que isso, então o filtro quebrou e a afirmação abaixo " +
            "passaria por vacuidade.");

        Assert.True(
            offenders.Count == 0,
            "Há chave ou certificado versionado no repositório, e não pode haver (T07, " +
            "verificação 4; ADR-0006, 'chave gerada no próprio teste, nunca versionada'):" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// Baixa um documento do emissor e, quando a chamada não completa, diz <strong>qual</strong>
    /// das duas coisas aconteceu: ninguém escutando naquele endereço, ou alguém escutando e a
    /// resposta não vindo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A distinção existe porque as duas já aconteceram e o sintoma delas é o mesmo — uma
    /// <c>SocketException</c> de operação abortada, dez segundos depois, sem dizer nem o endereço
    /// que o teste tentou. Foi assim que a corrida de porta desta classe apareceu: uma falha
    /// mensal, sob carga, com uma mensagem que não apontava para lugar nenhum.
    /// </para>
    /// <para>
    /// Isto <strong>não afrouxa</strong> nada: o prazo continua o mesmo e a asserção continua a
    /// mesma. O que muda é que a falha passa a nomear a causa em vez de deixá-la para quem for
    /// investigar depois.
    /// </para>
    /// </remarks>
    private static async Task<string> BaixarAsync(
        HttpClient client,
        InProcessOidcIssuer issuer,
        string rota,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetStringAsync(
                new Uri($"{issuer.Authority}{rota}"),
                cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"O emissor OIDC de teste não respondeu '{rota}' em '{issuer.Authority}'. " +
                $"Sondagem TCP direta da porta: {Sondar(new Uri(issuer.Authority))}. " +
                $"Pool de threads no momento da falha: {ThreadPool.ThreadCount} thread(s), " +
                $"{ThreadPool.PendingWorkItemCount} item(ns) pendente(s).",
                failure);
        }
    }

    /// <summary>
    /// O que acontece ao abrir uma conexão TCP crua em <paramref name="address"/>, com as três
    /// respostas <strong>distinguidas</strong>.
    /// </summary>
    /// <remarks>
    /// "Recusada" e "não respondeu a tempo" significam coisas opostas — endereço errado contra
    /// servidor ocupado — e a primeira versão desta sondagem devolvia <c>false</c> para as duas.
    /// Ela chegou a afirmar "a porta NÃO aceita conexão" sobre uma porta que o próprio rastro de
    /// pilha mostrava conectada. Uma sondagem que confunde as duas causas é pior que nenhuma: ela
    /// aponta com confiança para o lugar errado.
    /// </remarks>
    private static string Sondar(Uri address)
    {
        try
        {
            using TcpClient probe = new();

            return probe.ConnectAsync(address.Host, address.Port).Wait(TimeSpan.FromSeconds(2))
                ? "conexão ACEITA (há servidor escutando; o endereço está certo e ele não " +
                  "respondeu a tempo)"
                : "conexão NÃO COMPLETOU em 2s (nem aceita nem recusada — travou)";
        }
        catch (AggregateException erro) when (erro.InnerException is SocketException socket)
        {
            return $"conexão RECUSADA ({socket.SocketErrorCode}) — não há servidor neste endereço";
        }
    }

    /// <summary>O módulo da única chave de um JWKS.</summary>
    private static string Modulus(string jwks)
    {
        using JsonDocument document = JsonDocument.Parse(jwks);

        return document.RootElement
            .GetProperty("keys")
            .EnumerateArray()
            .Single()
            .GetProperty("n")
            .GetString()!;
    }

    /// <summary>Todo arquivo do repositório, fora dos diretórios de build e de dependência.</summary>
    private static IEnumerable<string> Files(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            yield return file;
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (_ignoredDirectories.Contains(
                Path.GetFileName(child),
                StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string file in Files(child))
            {
                yield return file;
            }
        }
    }

    /// <summary>O conteúdo de um arquivo de texto, ou <c>null</c> quando ele é binário ou ilegível.</summary>
    private static string? Text(string file)
    {
        try
        {
            FileInfo info = new(file);

            // Um PEM é texto e é pequeno. Arquivos grandes são binário ou dado de teste, e lê-los
            // inteiros tornaria esta varredura cara sem acrescentar cobertura.
            if (info.Length > 2 * 1024 * 1024)
            {
                return null;
            }

            return File.ReadAllText(file);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
