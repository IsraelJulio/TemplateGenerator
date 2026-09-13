using System.Text.Json;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1, item "todo arquivo obrigatório presente"
/// (docs/quality/test-strategy.md; a lista é de docs/architecture/generated-projects.md,
/// seção "Conteúdo obrigatório de todo ZIP").
/// </summary>
/// <remarks>
/// <para>
/// A lista se divide em duas: o que vale para <strong>qualquer</strong> combinação, porque vem do
/// fragmento <c>common</c> ou do próprio motor, e o que depende da arquitetura, porque o caminho
/// muda com ela. A primeira metade é cobrada nas 32; a segunda recebe
/// <see cref="SimpleCombinations"/> — o fragmento de Clean é um diretório vazio até T04.
/// </para>
/// <para>
/// <strong>O escopo está nos dados, não num <c>return</c>.</strong> Um teste que recebe as 32 e
/// desiste na primeira linha aparece <em>verde</em> para as 16 combinações que não examinou, e um
/// verde que não afirma nada é a forma mais barata de perder uma verificação sem ninguém notar —
/// o modo de falha que ADR-0008 e ADR-0010 mandam vigiar. O tamanho de cada recorte é conferido
/// por <c>GenerationMatrixTests.Os_recortes_da_matriz_tem_o_tamanho_que_afirmam</c>, porque uma
/// teoria sem dados também não falha: ela simplesmente não roda.
/// </para>
/// <para>
/// <strong>O que esta camada não prova:</strong> que o projeto de testes do pacote
/// <em>passa</em>. "Um projeto de testes com testes básicos que passam" exige executá-los, e isso
/// é camada 3 (<see cref="Layer3.GeneratedProjectRuntimeTests"/>). Aqui só se afirma que o projeto
/// existe, que referencia o projeto de código e que traz ao menos um arquivo de teste.
/// </para>
/// </remarks>
public sealed class RequiredContentMatrixTests
{
    /// <summary>
    /// O que todo ZIP traz, em qualquer combinação: vem de <c>common</c> ou do motor, e o caminho
    /// não depende de eixo nenhum.
    /// </summary>
    private static readonly string[] _alwaysRequired =
    [
        ".editorconfig",
        ".gitignore",
        "global.json",
        "requests.http",
        GenerationManifest.Path,
    ];

    /// <summary>
    /// Palavras que denunciam credencial real em arquivo de configuração. A exigência de
    /// generated-projects.md é "<c>appsettings.json</c> e <c>appsettings.Development.json</c>
    /// <strong>sem credenciais reais</strong>"; um reconhecedor textual não prova ausência de
    /// segredo, mas pega a forma em que um segredo aparece por descuido — uma cadeia de conexão
    /// colada de um ambiente de verdade.
    /// </summary>
    private static readonly string[] _secretMarkers =
    [
        "password=",
        "pwd=",
        "apikey",
        "api_key",
        "clientsecret",
        "client_secret",
        "secretkey",
        "accesskey",
        "-----begin",
    ];

    public static TheoryData<string, string, string, bool> ValidCombinations =>
        GenerationMatrixTests.ValidCombinations;

    /// <summary>
    /// As combinações da Simples. O escopo dos testes desta classe que dependem de fragmento
    /// escrito está <strong>nos dados</strong>, e não num <c>return</c> antecipado — ver
    /// <see cref="Combinations.Simple"/>.
    /// </summary>
    public static TheoryData<string, string, string, bool> SimpleCombinations =>
        GenerationMatrixTests.SimpleCombinations;

    [Theory]
    [MemberData(nameof(ValidCombinations))]
    public async Task Toda_combinacao_traz_o_conteudo_que_nao_depende_de_eixo(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string required in _alwaysRequired)
        {
            Assert.True(
                package.Contains(required),
                $"{GeneratedPackage.Describe(package.Request)}: falta '{required}', que " +
                "generated-projects.md exige de todo ZIP. O pacote traz: " +
                string.Join(", ", package.Paths));
        }
    }

    [Theory]
    [MemberData(nameof(SimpleCombinations))]
    public async Task A_arquitetura_simples_traz_todo_o_conteudo_obrigatorio(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Escopo declarado, e declarado nos dados: o fragmento de Clean é um diretório vazio até
        // T04, e um pacote sem `.sln` e sem projeto não tem como cumprir a lista. Recortar os
        // dados, em vez de sair por um `return`, é o que impede este nome de aparecer verde para
        // as 16 combinações de Clean que ele não examinou.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string name = Combinations.ProjectName;

        // generated-projects.md, "Conteúdo obrigatório de todo ZIP", com os caminhos que a regra
        // de nomes da arquitetura Simples determina: o projeto Web API se chama exatamente
        // `<ProjectName>` — nada é acrescentado.
        string[] required =
        [
            .. _alwaysRequired,
            "README.md",
            $"{name}.sln",
            $"src/{name}/{name}.csproj",
            $"src/{name}/appsettings.json",
            $"src/{name}/appsettings.Development.json",
            $"tests/{name}.Tests/{name}.Tests.csproj",
        ];

        string[] missing =
        [
            .. required.Where(path => !package.Contains(path)).Order(StringComparer.Ordinal),
        ];

        Assert.True(
            missing.Length == 0,
            $"{GeneratedPackage.Describe(package.Request)}: faltam " +
            $"{string.Join(", ", missing)}. O pacote traz: " + string.Join(", ", package.Paths));
    }

    [Theory]
    [MemberData(nameof(SimpleCombinations))]
    public async Task O_projeto_de_testes_referencia_o_projeto_de_codigo_e_traz_teste(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Escopo de T04, nos dados, como acima.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string name = Combinations.ProjectName;
        string testProject = $"tests/{name}.Tests/{name}.Tests.csproj";

        // "Um projeto de testes com testes básicos" — um `.csproj` sozinho, sem nenhum `.cs` ao
        // lado, cumpriria a letra e não o propósito.
        string[] testSources =
        [
            .. package.Paths.Where(path =>
                path.StartsWith($"tests/{name}.Tests/", StringComparison.Ordinal) &&
                path.EndsWith(".cs", StringComparison.Ordinal)),
        ];

        Assert.True(
            testSources.Length > 0,
            $"{GeneratedPackage.Describe(package.Request)}: '{testProject}' não tem nenhum " +
            "arquivo '.cs' ao lado. Um projeto de testes vazio não é 'testes básicos que passam' " +
            "(generated-projects.md).");

        Assert.Contains(
            $"src\\{name}\\{name}.csproj",
            package.Read(testProject),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ValidCombinations))]
    public async Task Nenhum_appsettings_carrega_credencial(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string[] settings =
        [
            .. package.Paths.Where(path =>
                path.EndsWith("appsettings.json", StringComparison.Ordinal) ||
                path.EndsWith("appsettings.Development.json", StringComparison.Ordinal)),
        ];

        foreach (string path in settings)
        {
            string content = package.Read(path).ToLowerInvariant();

            string[] found =
            [
                .. _secretMarkers.Where(marker => content.Contains(marker, StringComparison.Ordinal)),
            ];

            Assert.True(
                found.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' parece carregar " +
                $"credencial ({string.Join(", ", found)}). generated-projects.md exige " +
                "appsettings sem credenciais reais, e AGENTS.md proíbe segredo em arquivo " +
                "versionado, inclusive nos ZIPs gerados.");
        }
    }

    [Theory]
    [MemberData(nameof(ValidCombinations))]
    public async Task O_global_json_fixa_o_SDK_com_versao_exata(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        using JsonDocument global = JsonDocument.Parse(package.Read("global.json"));

        string? version = global.RootElement
            .GetProperty("sdk")
            .GetProperty("version")
            .GetString();

        // "global.json fixando o SDK" só vale se a versão for uma versão, e não um curinga.
        Assert.Matches(@"^\d+\.\d+\.\d+$", version ?? string.Empty);
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");
}
