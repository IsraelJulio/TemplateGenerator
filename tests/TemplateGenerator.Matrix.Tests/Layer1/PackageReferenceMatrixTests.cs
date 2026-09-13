using System.Text.RegularExpressions;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 sobre o que está <strong>literalmente escrito</strong> nos <c>.csproj</c> gerados:
/// versão exata (RNF-06) e ausência da dependência de uma opção não marcada (RF-20).
/// </summary>
/// <remarks>
/// <para>
/// É a metade de RF-20 que a camada 1 consegue provar sozinha, e é ela que ADR-0011
/// (docs/decisions/adr-0011-contribuicao-por-marcador.md, seção "Como isto sobrevive a ADR-0008")
/// usa para justificar que o <c>PackageReference</c> continue literal dentro do <c>.csproj</c>
/// em vez de migrar para um <c>.props</c> importado.
/// Dependência <em>transitiva</em> não é assunto daqui: sem restore não há grafo, e essa metade é
/// da camada 2.
/// </para>
/// <para>
/// <strong>O assert de sanidade é obrigatório</strong>, pela mesma razão de ADR-0008: um
/// <c>.csproj</c> que deixasse de ser gerado, ou um marcador de contribuição que deixasse de ser
/// preenchido, fariam todo teste "não contém X" passar por vacuidade. Um verificador que
/// silenciosamente para de verificar é pior que nenhum.
/// </para>
/// </remarks>
public sealed partial class PackageReferenceMatrixTests
{
    /// <summary>Os dois pacotes de Swagger, que só existem quando a opção está marcada (ADR-0001).</summary>
    private static readonly string[] _swaggerPackages =
    [
        "Microsoft.AspNetCore.OpenApi",
        "Swashbuckle.AspNetCore",
    ];

    /// <summary>
    /// As combinações que geram pacote. Ver <see cref="Combinations.Available"/>: o recorte é
    /// derivado de <c>TemplateAvailability</c>, e não de uma lista do que já foi escrito.
    /// </summary>
    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    /// <summary>As combinações disponíveis com Swagger marcado.</summary>
    public static TheoryData<string, string, string, bool> AvailableWithSwaggerCombinations =>
        GenerationMatrixTests.AvailableWithSwaggerCombinations;

    [Fact]
    public async Task A_matriz_produz_csproj_e_PackageReference_para_conferir()
    {
        // ADR-0011, "Assert de sanidade, obrigatório". Sem isto, toda afirmação de RF-20 e RNF-06
        // desta classe passaria por vacuidade no dia em que a geração parasse de emitir `.csproj`
        // ou o marcador `__ApiPackageReferences__` parasse de ser preenchido.
        int projects = 0;
        int references = 0;

        foreach (GenerationRequest request in Combinations.Available)
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(
                request,
                TestContext.Current.CancellationToken);

            foreach (string path in ProjectFiles(package))
            {
                projects++;
                references += PackageReferences(package, path).Count;
            }
        }

        Assert.True(
            projects > 0,
            "Nenhum '.csproj' foi encontrado em nenhuma das combinações da matriz. Enquanto isso " +
            "for verdade, todo teste de dependência desta classe passa sem olhar nada " +
            "(ADR-0011, 'Assert de sanidade, obrigatório').");

        Assert.True(
            references > 0,
            "A matriz inteira não produziu uma única 'PackageReference' para conferir. O marcador " +
            "'__ApiPackageReferences__' provavelmente deixou de ser preenchido " +
            "(ADR-0011, 'Assert de sanidade, obrigatório').");
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Toda_versao_declarada_e_exata(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // RNF-06: versão exata, sem intervalo (`[1.0,2.0)`) e sem curinga (`10.*`). O `.csproj`
        // gerado é de outro repositório e não herda o gerenciamento central da plataforma, então
        // a única garantia possível é a que está escrita nele.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in ProjectFiles(package))
        {
            foreach ((string name, string? version) in PackageReferences(package, path))
            {
                Assert.True(
                    version is not null && ExactVersion().IsMatch(version),
                    $"{GeneratedPackage.Describe(package.Request)}: em '{path}', o pacote " +
                    $"'{name}' está com a versão '{version ?? "(ausente)"}'. RNF-06 exige versão " +
                    "exata, no formato '10.0.12'.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Com_swagger_desligado_nenhum_csproj_declara_os_pacotes_de_Swagger(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        if (swagger)
        {
            return;
        }

        // RF-20, na metade que a camada 1 prova: a dependência não está escrita no projeto. Os
        // dois pacotes são verificados, e não só o Swashbuckle — ADR-0001 acrescentou dois, e um
        // teste que olhasse só um deixaria o outro passar.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in ProjectFiles(package))
        {
            string[] declared =
            [
                .. PackageReferences(package, path)
                    .Select(reference => reference.Name)
                    .Where(name => _swaggerPackages.Contains(name, StringComparer.Ordinal)),
            ];

            Assert.True(
                declared.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' declara " +
                $"{string.Join(", ", declared)} com Swagger desmarcado (RF-20, ADR-0001).");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableWithSwaggerCombinations))]
    public async Task Com_swagger_ligado_o_projeto_web_declara_os_dois_pacotes(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // A contraprova do teste acima: sem ela, um `__ApiPackageReferences__` que nunca fosse
        // preenchido faria a verificação de RF-20 passar sempre.
        //
        // O escopo está nos DADOS, e agora ele é a disponibilidade: uma combinação recusada não
        // tem `.csproj` para declarar pacote nenhum. O filtro de Swagger continua porque a
        // afirmação é sobre a posição LIGADA do interruptor.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string[] declared =
        [
            .. ProjectFiles(package)
                .SelectMany(path => PackageReferences(package, path))
                .Select(reference => reference.Name),
        ];

        Assert.All(_swaggerPackages, expected =>
            Assert.Contains(expected, declared, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Os_dois_pacotes_de_Swagger_entram_no_projeto_Web_API_e_em_nenhum_outro(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // RF-20 tem um lado que a Clean torna verificável e a Simples não conseguia mostrar: com
        // quatro projetos irmãos, "a dependência da opção entra no projeto certo" deixa de ser
        // trivialmente verdadeira. Um `Swashbuckle` que caísse em `Domain` seria, além de RF-20
        // mal cumprido, a violação do critério de aceite 2 — e nenhum teste de "não contém"
        // rodando sobre a Simples teria como acusá-lo.
        //
        // O projeto Web API é achado pelo `Program.cs`, e não pelo sufixo do nome
        // (`PackageLayout`): é a única definição que vale nas duas arquiteturas.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string? webProject = PackageLayout.WebProjectDirectory(package);

        Assert.NotNull(webProject);

        foreach (string path in ProjectFiles(package))
        {
            string[] declared =
            [
                .. PackageReferences(package, path)
                    .Select(reference => reference.Name)
                    .Where(name => _swaggerPackages.Contains(name, StringComparer.Ordinal)),
            ];

            Assert.True(
                declared.Length == 0 || path.StartsWith(webProject, StringComparison.Ordinal),
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' declara " +
                $"{string.Join(", ", declared)}, e não é o projeto Web API ('{webProject}'). A " +
                "dependência de uma opção entra no projeto de composição — em Clean, pô-la em " +
                "'Domain' ou 'Application' é referência arquitetural indevida " +
                "(generated-projects.md, 'Clean Architecture').");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Toda_combinacao_disponivel_traz_projeto_de_codigo_e_projeto_de_testes(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // A quantidade de projetos de código varia por arquitetura — um na Simples, quatro na
        // Clean —, e por isso este teste afirma a EXISTÊNCIA, não a lista. Quem fixa a lista
        // exata, com a regra de nomes junto, é `ZipStructureContractTests`, comparando a árvore
        // inteira do pacote real contra o contrato versionado.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(PackageLayout.SourceProjects(package));
        Assert.Single(PackageLayout.TestProjects(package));

        // Todo `.csproj` do pacote está sob `src/` ou sob `tests/`. Um projeto solto na raiz, ou
        // numa pasta terceira, é forma de pacote que nenhum documento descreve.
        Assert.All(
            ProjectFiles(package),
            path => Assert.True(
                path.StartsWith(PackageLayout.SourceDirectory, StringComparison.Ordinal) ||
                path.StartsWith(PackageLayout.TestsDirectory, StringComparison.Ordinal),
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' não está sob 'src/' " +
                "nem sob 'tests/'."));
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    /// <summary>Os caminhos de <c>.csproj</c> do pacote, em ordem ordinal.</summary>
    private static IReadOnlyList<string> ProjectFiles(GeneratedPackage package) =>
    [
        .. package.Paths
            .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// As <c>PackageReference</c> declaradas em um <c>.csproj</c>, lidas como XML.
    /// </summary>
    /// <remarks>
    /// Ler como XML, e não com expressão regular, é o que permite afirmar "declarada" com
    /// segurança: um nome de pacote citado dentro de um comentário não conta, e é justamente o
    /// tipo de menção que uma auditoria por <c>grep</c> confundiria com a dependência.
    /// </remarks>
    private static IReadOnlyList<(string Name, string? Version)> PackageReferences(
        GeneratedPackage package,
        string path) =>
        PackageLayout.PackageReferences(package, path);

    [GeneratedRegex(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactVersion();
}
