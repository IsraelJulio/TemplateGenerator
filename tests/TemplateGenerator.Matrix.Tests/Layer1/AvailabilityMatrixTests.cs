using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// O <strong>outro sentido</strong> de ADR-0012, sobre a matriz inteira: para toda combinação
/// indisponível, o motor recusa e a Api responde <c>501</c> — e a conta fecha com as disponíveis.
/// São as linhas 3 a 6 da tabela do item 5 da decisão.
/// </summary>
/// <remarks>
/// <para>
/// Só o primeiro sentido — "toda combinação disponível traz o conteúdo obrigatório" — deixaria a
/// derivação envelhecer sem ninguém notar: bastaria ela responder "indisponível" para tudo e a
/// camada 1 inteira ficaria verde sem abrir um pacote. É a lição de ADR-0008 e ADR-0010, pela
/// quarta vez, e é o que a <see cref="A_derivacao_nao_responde_sempre_a_mesma_coisa"/> vigia.
/// </para>
/// <para>
/// <strong>A forma da resposta <c>501</c> não é assunto daqui</strong> — <c>type</c>, <c>title</c>,
/// ausência de <c>detail</c>, a frase igual à do catálogo: isso é
/// <c>GenerationNotImplementedEndpointTests</c>, sobre um repositório de fragmentos controlado, e
/// lá ela sobrevive a T08. Aqui se afirma o que só a matriz consegue afirmar: que a recusa vale
/// para <em>todas</em> as combinações que a derivação de PRODUÇÃO marca como indisponíveis, que
/// ela nomeia o campo certo, e que nenhuma delas sai com cabeçalho de download.
/// </para>
/// </remarks>
public sealed class AvailabilityMatrixTests : IDisposable
{
    private static readonly Uri _route = new("/api/templates", UriKind.Relative);

    private readonly MatrixApiFactory _factory = new();

    public static TheoryData<string, string, string, bool> UnavailableCombinations =>
        GenerationMatrixTests.UnavailableCombinations;

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    public void Dispose() => _factory.Dispose();

    [Theory]
    [MemberData(nameof(UnavailableCombinations))]
    public async Task Combinacao_indisponivel_e_recusada_pelo_motor(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // ADR-0012, item 5, linha 3: o MOTOR recusa — exceção, não pacote. Sem este lado, quem
        // chama o motor direto continuaria recebendo o pacote defeituoso, e quem chama o motor
        // direto é a camada 1 inteira. Nenhum HTTP aqui, de propósito: a recusa é do motor.
        GenerationRequest request = Request(architecture, database, authentication, swagger);

        GenerationNotAvailableException failure =
            await Assert.ThrowsAsync<GenerationNotAvailableException>(
                () => GeneratedPackage.GenerateAsync(
                    request,
                    TestContext.Current.CancellationToken));

        Assert.NotEmpty(failure.Fields);
        Assert.Equal(TemplateAvailability.UnavailableReason, failure.Reason);

        // Os campos que a exceção nomeia são exatamente os que a derivação marca — não um
        // subconjunto, não "pelo menos um".
        Assert.Equal(
            TemplateAvailability.Current.UnavailableFields(request),
            failure.Fields);
    }

    [Theory]
    [MemberData(nameof(UnavailableCombinations))]
    public async Task Combinacao_indisponivel_responde_501_sem_cabecalho_de_download(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // ADR-0012, item 5, linha 4. Falha quando a recusa entrou tarde demais no pipeline: o
        // `GeneratedArchiveResult` escreve `application/zip` e `Content-Disposition: attachment`
        // como primeira coisa que faz, então uma recusa depois dele sai com cabeçalho de download
        // em cima — e é o cabeçalho, não o status, que manda o navegador salvar o arquivo.
        GenerationRequest request = Request(architecture, database, authentication, swagger);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _route,
            Body(request),
            TestContext.Current.CancellationToken);

        string description = GeneratedPackage.Describe(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.NotImplemented,
            $"{description}: a Api respondeu {(int)response.StatusCode}, e a combinação está " +
            "indisponível. ADR-0012: nunca 200.");

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        Assert.Null(response.Content.Headers.ContentDisposition);

        Assert.DoesNotContain(
            "Content-Disposition",
            response.Headers.Concat(response.Content.Headers).Select(header => header.Key));

        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        string[] fields =
        [
            .. problem.RootElement
                .GetProperty("errors")
                .EnumerateObject()
                .Select(entry => entry.Name),
        ];

        // O campo em `errors` é o que a tela posiciona *inline*. Ele vem da mesma derivação que a
        // recusa do motor, e a ordem é a dos campos no catálogo.
        Assert.Equal(TemplateAvailability.Current.UnavailableFields(request), fields);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Combinacao_disponivel_responde_200_com_o_pacote(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // A contraprova dos dois testes acima, e ela não é zelo: sem ela, uma Api que respondesse
        // 501 a tudo passaria em ambos. É a metade "existe pelo menos uma disponível" do assert de
        // sanidade, cobrada combinação a combinação.
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _route,
            Body(Request(architecture, database, authentication, swagger)),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
    }

    [Fact]
    public void Disponiveis_mais_indisponiveis_sao_a_matriz_inteira()
    {
        // ADR-0012, item 5, linha 5, primeira metade. Falha quando a derivação e a recusa
        // discordam — uma combinação que não estivesse em nenhum dos dois conjuntos sumiria da
        // camada 1 sem que nenhum teste ficasse vermelho.
        Assert.Equal(
            Combinations.Valid.Count,
            Combinations.Available.Count + Combinations.Unavailable.Count);

        Assert.Empty(Combinations.Available.Intersect(Combinations.Unavailable));

        // Igualdade de conjunto nos dois sentidos: uma combinação que sumisse dos dois recortes
        // deixaria a soma certa só se outra aparecesse duas vezes, e a contagem acima não pegaria.
        GenerationRequest[] union =
        [
            .. Combinations.Available.Concat(Combinations.Unavailable),
        ];

        Assert.Empty(Combinations.Valid.Except(union));
        Assert.Empty(union.Except(Combinations.Valid));
    }

    [Fact]
    public void A_derivacao_por_combinacao_coincide_com_a_derivacao_por_valor()
    {
        // ADR-0012, item 5, linha 5, segunda metade: o conjunto de disponíveis calculado pela
        // derivação (R3, `UnavailableFields`) coincide com o calculado combinação a combinação,
        // valor por valor, a partir de `IsAvailable`.
        //
        // NÃO é uma cópia da regra: `UnavailableFields` percorre os campos do catálogo e devolve
        // quais falharam; aqui a mesma pergunta é feita pelo outro lado — todo valor que a
        // combinação seleciona está disponível? Se as duas respostas divergirem, uma das duas
        // está errada, e hoje nada acusaria qual.
        TemplateAvailability availability = TemplateAvailability.Current;

        foreach (GenerationRequest request in Combinations.Valid)
        {
            bool byValue = TemplateCatalog.Current.Fields.Keys.All(field =>
            {
                CatalogValue? value = request.ValueOf(field);

                return value is null || availability.IsAvailable(field, value.Value);
            });

            bool byCombination = availability.UnavailableFields(request).Count == 0;

            Assert.True(
                byValue == byCombination,
                $"{GeneratedPackage.Describe(request)}: a derivação por valor diz " +
                $"'{(byValue ? "disponível" : "indisponível")}' e a derivação por combinação diz " +
                $"'{(byCombination ? "disponível" : "indisponível")}'. As duas saem do mesmo " +
                "TemplateAvailability e não podem divergir.");

            Assert.Equal(byCombination, Combinations.Available.Contains(request));
        }
    }

    [Fact]
    public void A_derivacao_nao_responde_sempre_a_mesma_coisa()
    {
        // ADR-0012, item 5, linha 6 — o assert de sanidade, e ele não é zelo: sem ele, uma
        // derivação que devolvesse "tudo indisponível" faria as linhas 1 e 2 passarem sem olhar
        // pacote nenhum, e uma que devolvesse "tudo disponível" faria as linhas 3 e 4 passarem sem
        // recusar nada. Um verificador que silenciosamente para de verificar é pior que nenhum.
        Assert.True(
            Combinations.Available.Count > 0,
            "Nenhuma combinação da matriz está disponível. Enquanto isso for verdade, toda " +
            "afirmação sobre o CONTEÚDO do pacote passa por vacuidade: não há pacote para olhar.");

        // A segunda metade TEM data de validade, e ela é condicionada de propósito: quando o
        // último fragmento for escrito (T08) não haverá mais combinação indisponível, e esta
        // exigência precisa cair junto — sozinha, sem ninguém precisar lembrar dela.
        string[] emptyFragments = EmptyFragments();

        if (emptyFragments.Length == 0)
        {
            Assert.Empty(Combinations.Unavailable);

            return;
        }

        Assert.True(
            Combinations.Unavailable.Count > 0,
            "Todo fragmento do repositório contribui alguma coisa, mas ainda existem diretórios " +
            $"de eixo vazios ({string.Join(", ", emptyFragments)}). Ou a derivação parou de " +
            "derivar, ou a varredura de fragmentos vazios deste teste ficou desalinhada dela — " +
            "nos dois casos, 'toda combinação indisponível é recusada' virou verdade por " +
            "vacuidade e as linhas 3 e 4 de ADR-0012 não estão sendo verificadas.");
    }

    /// <summary>
    /// Os eixos de fragmento que <strong>não têm arquivo nenhum</strong> no repositório embutido.
    /// </summary>
    /// <remarks>
    /// É a condição de validade da segunda metade do assert de sanidade, e ela é lida do
    /// repositório e não de uma lista: um diretório que só tenha <c>.gitkeep</c> chega aqui com
    /// zero arquivos porque o <c>.csproj</c> o exclui do <c>EmbeddedResource</c> (ADR-0012, R1.2).
    /// </remarks>
    private static string[] EmptyFragments() =>
    [
        .. TemplateAxes.All
            .Where(axis => EmbeddedTemplateSource.Default.Read(axis).Count == 0)
            .Order(StringComparer.Ordinal),
    ];

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    private static Dictionary<string, object?> Body(GenerationRequest request) =>
        new(StringComparer.Ordinal)
        {
            [CatalogFields.ProjectName] = request.ProjectName,
            [CatalogFields.Architecture] = request.Architecture,
            [CatalogFields.Database] = request.Database,
            [CatalogFields.Authentication] = request.Authentication,
            [CatalogFields.Swagger] = request.Swagger,
            [CatalogFields.DotnetVersion] = request.DotnetVersion,
        };
}
