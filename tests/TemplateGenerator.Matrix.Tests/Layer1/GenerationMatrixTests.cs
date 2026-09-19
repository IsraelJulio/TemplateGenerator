using System.Text.Json;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 da matriz: gera o ZIP das combinações válidas e inspeciona o conteúdo.
/// <strong>Sem <c>restore</c>, sem <c>build</c></strong> (docs/quality/test-strategy.md).
/// </summary>
/// <remarks>
/// <para>
/// O que está aqui é o que vale para <em>qualquer</em> combinação, com ou sem template de
/// produção escrito: que a geração não quebra, que ela é determinística, que nenhum caminho sai
/// da raiz e que o manifesto corresponde ao pedido.
/// </para>
/// <para>
/// As verificações de <em>conteúdo</em> — arquivo obrigatório presente, nenhuma dependência de
/// opção não marcada no <c>.csproj</c>, referências de projeto conforme o diagrama — dependem dos
/// fragmentos e são do papel <c>qa</c>, sobre <see cref="GeneratedPackage"/>.
/// </para>
/// </remarks>
public sealed class GenerationMatrixTests
{
    public static TheoryData<string, string, string, bool> ValidCombinations =>
        DataFrom(Combinations.Valid);

    /// <summary>
    /// Só as combinações que geram projeto, para os testes que olham <em>dentro</em> do pacote.
    /// Ver <see cref="Combinations.Available"/>: o recorte é derivado de
    /// <c>TemplateAvailability</c>, e existe para que nenhum teste apareça verde por uma
    /// combinação em que ele desistiu logo na primeira linha.
    /// </summary>
    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        DataFrom(Combinations.Available);

    /// <summary>As combinações disponíveis <strong>com</strong> Swagger marcado.</summary>
    public static TheoryData<string, string, string, bool> AvailableWithSwaggerCombinations =>
        DataFrom([.. Combinations.Available.Where(request => request.Swagger)]);

    private static TheoryData<string, string, string, bool> DataFrom(
        IReadOnlyList<GenerationRequest> requests)
    {
        TheoryData<string, string, string, bool> data = [];

        foreach (GenerationRequest request in requests)
        {
            data.Add(
                request.Architecture,
                request.Database,
                request.Authentication,
                request.Swagger);
        }

        return data;
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    [Fact]
    public void A_matriz_tem_as_32_combinacoes_validas()
    {
        // O número está na definição de pronto e na estratégia de testes. Aqui ele é calculado a
        // partir do catálogo: se a fórmula mudar, isto falha antes de a documentação envelhecer.
        Assert.Equal(32, Combinations.Valid.Count);
    }

    [Fact]
    public void Nenhum_recorte_da_matriz_chega_vazio_a_uma_teoria()
    {
        // Guarda dos recortes, e não decoração. Um teste escopado às combinações disponíveis
        // recebe `AvailableCombinations`; no dia em que esse recorte devolvesse zero linha —
        // porque a derivação de disponibilidade quebrou, porque o catálogo perdeu um campo,
        // porque o filtro errou —, o xUnit não falharia: uma teoria sem dados simplesmente não
        // roda, e a suíte ficaria verde com metade da camada 1 desligada. É o mesmo modo de falha
        // que o assert de sanidade de ADR-0011 vigia do lado do conteúdo.
        //
        // Os tamanhos NÃO são constantes aqui, com a única exceção das 32, que vêm da matriz do
        // produto. Escrever "4 disponíveis" seria a lista à mão que ADR-0012 recusa, com outro
        // nome: ela quebraria em T05 por um motivo que é sucesso, não regressão.
        Assert.Equal(32, ValidCombinations.Count);

        Assert.NotEmpty(AvailableCombinations);
        Assert.NotEmpty(AvailableWithSwaggerCombinations);

        // O recorte dos INDISPONÍVEIS não tem recorte de teoria aqui, e desde T07 não tem nem
        // dados: com `auth/jwt` escrito, todo eixo passou a ter fragmento e
        // `Combinations.Unavailable` esvaziou — as 32 válidas estão disponíveis. Exigir que ele
        // tenha linha seria escrever aqui uma dívida que já venceu. Quem vigia que esse vazio
        // continua sendo o vazio LEGÍTIMO — todo fragmento escrito, e não a derivação quebrada —
        // é `AvailabilityMatrixTests`, nos dois sentidos.
        Assert.NotEmpty(Combinations.Clean);

        Assert.All(
            Combinations.Clean,
            request => Assert.Equal(Combinations.CleanArchitecture, request.Architecture));

        Assert.All(
            Combinations.Clean,
            request => Assert.Contains(request, Combinations.Available));
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task A_combinacao_gera_um_pacote_sem_conflito_de_fragmento(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // É aqui que um conflito de caminho entre dois fragmentos do template de PRODUÇÃO
        // aparece: a exceção do motor vira falha desta camada, e não precedência silenciosa em
        // tempo de execução (docs/architecture/generation-engine.md).
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(package.Paths);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Gerar_duas_vezes_produz_o_mesmo_SHA256(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GenerationRequest request = Request(architecture, database, authentication, swagger);

        GeneratedPackage first = await GeneratedPackage.GenerateAsync(
            request,
            TestContext.Current.CancellationToken);

        GeneratedPackage second = await GeneratedPackage.GenerateAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(first.Sha256, second.Sha256);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhum_caminho_sai_da_raiz_e_nenhum_caminho_se_repete(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        Assert.All(package.Paths, path =>
        {
            Assert.True(
                ArchivePath.IsValid(path, out string? error),
                $"{GeneratedPackage.Describe(package.Request)}: {error}");
        });

        Assert.Equal(
            package.Paths.Count,
            package.Paths.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_manifesto_corresponde_a_combinacao_pedida(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GenerationRequest request = Request(architecture, database, authentication, swagger);

        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            request,
            TestContext.Current.CancellationToken);

        using JsonDocument manifest = JsonDocument.Parse(package.Read(GenerationManifest.Path));

        Assert.Equal(
            TemplateCatalog.CurrentVersion,
            manifest.RootElement.GetProperty("templateVersion").GetString());

        JsonElement options = manifest.RootElement.GetProperty("options");

        Assert.Equal(request.ProjectName, options.GetProperty(CatalogFields.ProjectName).GetString());
        Assert.Equal(architecture, options.GetProperty(CatalogFields.Architecture).GetString());
        Assert.Equal(database, options.GetProperty(CatalogFields.Database).GetString());
        Assert.Equal(authentication, options.GetProperty(CatalogFields.Authentication).GetString());
        Assert.Equal(swagger, options.GetProperty(CatalogFields.Swagger).GetBoolean());
    }

    [Fact]
    public async Task Duas_combinacoes_diferentes_nao_produzem_o_mesmo_pacote()
    {
        // Contraprova do determinismo: se o hash fosse igual para tudo, os testes acima passariam
        // sem medir nada.
        List<string> hashes = [];

        foreach (GenerationRequest request in Combinations.Available)
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(
                request,
                TestContext.Current.CancellationToken);

            hashes.Add(package.Sha256);
        }

        Assert.Equal(hashes.Count, hashes.Distinct(StringComparer.Ordinal).Count());
    }
}
