using System.Text.Json;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1, item "todo arquivo obrigatório presente"
/// (docs/quality/test-strategy.md; a lista é de docs/architecture/generated-projects.md,
/// seção "Conteúdo obrigatório de todo ZIP"), e a <strong>linha 1</strong> da tabela do item 5 de
/// ADR-0012: para <em>toda</em> combinação disponível, o pacote traz o conteúdo obrigatório.
/// </summary>
/// <remarks>
/// <para>
/// <strong>O escopo é a disponibilidade, e ele está nos dados.</strong> Uma combinação
/// indisponível não produz pacote nenhum — o motor recusa (ADR-0012, item 4) —, então cobrar dela
/// o conteúdo obrigatório não é rigor, é pedir o impossível. O recorte vem de
/// <see cref="Combinations.Available"/>, derivado de <c>TemplateAvailability</c>: quando T05
/// escrever <c>database/sqlite</c>, as combinações novas entram aqui sozinhas e passam a ser
/// cobradas sem ninguém editar este arquivo.
/// </para>
/// <para>
/// Um teste que recebesse as 32 e desistisse na primeira linha apareceria <em>verde</em> para as
/// que não examinou, e um verde que não afirma nada é a forma mais barata de perder uma
/// verificação sem ninguém notar — o modo de falha que ADR-0008 e ADR-0010 mandam vigiar. O
/// tamanho de cada recorte é conferido por
/// <c>GenerationMatrixTests.Nenhum_recorte_da_matriz_chega_vazio_a_uma_teoria</c>, porque uma
/// teoria sem dados também não falha: ela simplesmente não roda.
/// </para>
/// <para>
/// <strong>Os caminhos são derivados do pacote, por papel</strong> — ver
/// <see cref="PackageLayout"/>. A lista de obrigações vale para as duas arquiteturas, e cada uma
/// põe os arquivos em lugares diferentes: na Simples há um projeto de código chamado exatamente
/// <c>&lt;projectName&gt;</c>, na Clean há quatro irmãos com sufixo de camada. Reescrever a regra
/// de nomes aqui criaria uma segunda cópia dela dentro do próprio verificador; quem a fixa por
/// teste é <see cref="ZipStructureContractTests"/>.
/// </para>
/// <para>
/// <strong>O que esta camada não prova:</strong> que o projeto de testes do pacote
/// <em>passa</em>. "Um projeto de testes com testes básicos que passam" exige executá-los, e isso
/// é camada 3 (<see cref="Layer3.GeneratedProjectRuntimeTests"/>). Aqui só se afirma que o projeto
/// existe, que referencia projeto de código do pacote e que traz ao menos um arquivo de teste.
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
    /// O que todo projeto <strong>Web API</strong> traz, relativo à pasta dele. O diretório é
    /// descoberto pelo <c>Program.cs</c>, e não pelo nome (<see cref="PackageLayout"/>).
    /// </summary>
    private static readonly string[] _requiredInWebProject =
    [
        "appsettings.json",
        "appsettings.Development.json",

        // generated-projects.md: ele FIXA a porta que o README e o `requests.http` citam. Sem ele
        // o projeto sobe numa porta escolhida pelo runtime e todo exemplo de chamada do pacote
        // aponta para o lugar errado, contra RF-21.
        "Properties/launchSettings.json",
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

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
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
    [MemberData(nameof(AvailableCombinations))]
    public async Task Toda_combinacao_disponivel_traz_todo_o_conteudo_obrigatorio(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // ADR-0012, item 5, linha 1. Falha quando um fragmento acendeu sem estar completo — que é
        // o limite nomeado da derivação: "contribui pelo menos um arquivo" não é "está completo".
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string name = Combinations.ProjectName;
        string? webProject = PackageLayout.WebProjectDirectory(package);

        Assert.True(
            webProject is not null,
            $"{GeneratedPackage.Describe(package.Request)}: o pacote não tem exatamente um " +
            "'Program.cs'. Sem o projeto Web API identificado, metade da lista de " +
            "generated-projects.md não tem onde ser cobrada. O pacote traz: " +
            string.Join(", ", package.Paths));

        List<string> required =
        [
            .. _alwaysRequired,
            "README.md",

            // A `.sln` leva o nome do projeto e nada mais é concatenado a ela, nas duas
            // arquiteturas — é o único caminho da lista que não depende de papel.
            $"{name}.sln",
            .. _requiredInWebProject.Select(path => webProject + path),
        ];

        string[] missing =
        [
            .. required.Where(path => !package.Contains(path)).Order(StringComparer.Ordinal),
        ];

        Assert.True(
            missing.Length == 0,
            $"{GeneratedPackage.Describe(package.Request)}: faltam " +
            $"{string.Join(", ", missing)}. O pacote traz: " + string.Join(", ", package.Paths));

        // "Projeto(s) de código conforme a arquitetura" e "um projeto de testes": a quantidade
        // varia por arquitetura, a existência não.
        Assert.True(
            PackageLayout.SourceProjects(package).Count > 0,
            $"{GeneratedPackage.Describe(package.Request)}: nenhum '.csproj' sob 'src/'.");

        Assert.Single(PackageLayout.TestProjects(package));
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_projeto_de_testes_referencia_projeto_de_codigo_do_pacote_e_traz_teste(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // A regra é POR PAPEL, e o que mudou em T04 é o que ela não pode afirmar. Na Simples o
        // projeto de testes referencia o projeto único; na Clean ele referencia `Application`, e
        // `Domain` chega por transitividade. Fixar `Tests → Application` congelaria o acidente de
        // hoje (generated-projects.md, "O projeto de testes fica fora do diagrama"): o primeiro
        // teste que precisasse de outro projeto derrubaria a suíte por ter razão.
        //
        // O que sobra afirmável, e é o que importa: o projeto de testes declara pelo menos uma
        // referência, e toda referência dele aponta para um projeto de produção QUE EXISTE no
        // pacote. Um `Include` para um caminho inexistente é a forma que a regra de nomes assume
        // quando erra, e ela não restaura na máquina de quem recebe.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string testProject = Assert.Single(PackageLayout.TestProjects(package));
        string testDirectory = testProject[..(testProject.LastIndexOf('/') + 1)];

        // "Um projeto de testes com testes básicos" — um `.csproj` sozinho, sem nenhum `.cs` ao
        // lado, cumpriria a letra e não o propósito.
        string[] testSources =
        [
            .. package.Paths.Where(path =>
                path.StartsWith(testDirectory, StringComparison.Ordinal) &&
                path.EndsWith(".cs", StringComparison.Ordinal)),
        ];

        Assert.True(
            testSources.Length > 0,
            $"{GeneratedPackage.Describe(package.Request)}: '{testProject}' não tem nenhum " +
            "arquivo '.cs' ao lado. Um projeto de testes vazio não é 'testes básicos que passam' " +
            "(generated-projects.md).");

        IReadOnlyList<string> references = PackageLayout.ProjectReferences(package, testProject);

        Assert.True(
            references.Count > 0,
            $"{GeneratedPackage.Describe(package.Request)}: '{testProject}' não declara nenhuma " +
            "'ProjectReference'. Um projeto de testes que não enxerga o código não testa nada.");

        string[] dangling =
        [
            .. references
                .Where(reference => !package.Contains(reference))
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            dangling.Length == 0,
            $"{GeneratedPackage.Describe(package.Request)}: '{testProject}' referencia " +
            $"{string.Join(", ", dangling)}, que o pacote não traz. O projeto não restaura na " +
            "máquina de quem recebe.");

        Assert.All(
            references,
            reference => Assert.StartsWith(
                PackageLayout.SourceDirectory,
                reference,
                StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
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
    [MemberData(nameof(AvailableCombinations))]
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
