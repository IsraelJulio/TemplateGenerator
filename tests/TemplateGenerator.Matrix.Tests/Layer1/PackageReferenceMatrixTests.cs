using System.Text.RegularExpressions;
using System.Xml.Linq;
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

    public static TheoryData<string, string, string, bool> ValidCombinations =>
        GenerationMatrixTests.ValidCombinations;

    /// <summary>
    /// As combinações da Simples, para as afirmações que só valem onde existe fragmento escrito.
    /// Ver <see cref="Combinations.Simple"/>.
    /// </summary>
    public static TheoryData<string, string, string, bool> SimpleCombinations =>
        GenerationMatrixTests.SimpleCombinations;

    /// <summary>As combinações da Simples com Swagger marcado.</summary>
    public static TheoryData<string, string, string, bool> SimpleWithSwaggerCombinations =>
        GenerationMatrixTests.SimpleWithSwaggerCombinations;

    [Fact]
    public async Task A_matriz_produz_csproj_e_PackageReference_para_conferir()
    {
        // ADR-0011, "Assert de sanidade, obrigatório". Sem isto, toda afirmação de RF-20 e RNF-06
        // desta classe passaria por vacuidade no dia em que a geração parasse de emitir `.csproj`
        // ou o marcador `__ApiPackageReferences__` parasse de ser preenchido.
        int projects = 0;
        int references = 0;

        foreach (GenerationRequest request in Combinations.Valid)
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
    [MemberData(nameof(ValidCombinations))]
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
    [MemberData(nameof(ValidCombinations))]
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
    [MemberData(nameof(SimpleWithSwaggerCombinations))]
    public async Task Com_swagger_ligado_o_projeto_web_declara_os_dois_pacotes(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // A contraprova do teste acima: sem ela, um `__ApiPackageReferences__` que nunca fosse
        // preenchido faria a verificação de RF-20 passar sempre.
        //
        // O escopo está nos DADOS, não num `return`: Clean ainda não tem fragmento (T04), e um
        // `return` antecipado faria este nome aparecer verde para as 16 combinações de Clean sem
        // ter olhado nenhuma. Quando T04 escrever o fragmento, troque por `ValidCombinations` e
        // apague o filtro de Swagger — `Os_recortes_da_matriz_tem_o_tamanho_que_afirmam` é quem
        // garante que o recorte não encolheu sozinho até lá.
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
    [MemberData(nameof(SimpleCombinations))]
    public async Task A_arquitetura_simples_traz_o_projeto_web_e_o_de_testes(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // O fragmento de Clean é de T04, então o escopo é dado pelos DADOS e não por um `return`:
        // assim este nome nunca aparece verde por uma combinação de Clean que ele não examinou.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string name = Combinations.ProjectName;

        string[] expected =
        [
            $"src/{name}/{name}.csproj",
            $"tests/{name}.Tests/{name}.Tests.csproj",
        ];

        // Também é aqui que a regra de nomes de generated-projects.md é cobrada: `Matriz.Exemplo.Api`
        // produz `src/Matriz.Exemplo.Api/`, e não `src/Matriz.Exemplo.Api.Api/`. Nada é concatenado.
        Assert.Equal(expected, ProjectFiles(package));
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
    [
        .. XDocument.Parse(package.Read(path)).Root!
            .Descendants()
            .Where(element =>
                element.Name.LocalName.Equals("PackageReference", StringComparison.Ordinal))
            .Select(element => (
                Name: element.Attribute("Include")?.Value ?? string.Empty,
                Version: element.Attribute("Version")?.Value)),
    ];

    [GeneratedRegex(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactVersion();
}
