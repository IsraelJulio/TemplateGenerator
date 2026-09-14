using System.Text.Json;
using TemplateGenerator.Generation;
using TemplateGenerator.Matrix.Tests.Layer3;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer2;

/// <summary>
/// Camada 2 dos fragmentos de banco de T05: <c>dotnet build</c> das combinações com banco, com um
/// cache de pacotes compartilhado, e a prova que só o <em>restore</em> dá — a dependência do
/// provider aparece resolvida no <c>project.assets.json</c> do projeto de persistência
/// (docs/quality/test-strategy.md, camada 2).
/// </summary>
/// <remarks>
/// <para>
/// É a metade <strong>transitiva</strong> de RF-20 que a camada 1 não alcança e
/// <see cref="Layer1.PackageReferenceMatrixTests"/> nomeia como sendo daqui: "sem restore não há
/// grafo". A camada 1 confere o que está <em>escrito</em> no <c>.csproj</c>; esta confere que o
/// grafo <em>resolve</em> — o provider do banco escolhido, e o EF Core que ele arrasta, entram no
/// conjunto restaurado.
/// </para>
/// <para>
/// O recorte é o conjunto que cobre os pares do eixo <c>database</c> com os demais: cada banco
/// (<c>sqlite</c>, <c>postgresql</c>) em cada arquitetura (<c>simple</c>, <c>clean</c>), com
/// <c>swagger</c> alternado entre eles para cobrir também o par banco×swagger. São as combinações
/// com banco entrando no conjunto pairwise.
/// </para>
/// <para>
/// <strong>Um <c>NUGET_PACKAGES</c> compartilhado é obrigatório</strong> (test-strategy.md): sem
/// ele, cada combinação restauraria o mundo de novo. O diretório é estável entre as invocações da
/// teoria, então o primeiro restore paga a rede e os seguintes reaproveitam.
/// </para>
/// <para>
/// <strong>Nada é pulado.</strong> Se o restore ou o build falharem, o teste falha com a saída do
/// comando na mensagem (via <see cref="DatabaseRuntime"/>).
/// </para>
/// </remarks>
[Trait("Camada", "2")]
public sealed class DatabaseBuildMatrixTests
{
    private const string ProjectName = "Camada2.Banco";

    /// <summary>Cache de pacotes compartilhado por toda a teoria — pago uma vez, reaproveitado.</summary>
    private static readonly string _sharedPackages = Path.Combine(
        Path.GetTempPath(),
        "templategenerator-layer2-nuget");

    /// <summary>
    /// O provider que cada banco deve deixar resolvido no grafo, com a versão exata do fragmento.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Version)> _providerByDatabase =
        new(StringComparer.Ordinal)
        {
            ["sqlite"] = ("Microsoft.EntityFrameworkCore.Sqlite", "10.0.12"),
            ["postgresql"] = ("Npgsql.EntityFrameworkCore.PostgreSQL", "10.0.3"),
        };

    /// <summary>
    /// O conjunto pairwise das combinações com banco: banco × arquitetura, com swagger alternado.
    /// </summary>
    private static readonly (string Architecture, string Database, bool Swagger)[] _set =
    [
        ("simple", "sqlite", true),
        ("clean", "sqlite", false),
        ("simple", "postgresql", false),
        ("clean", "postgresql", true),
    ];

    public static TheoryData<string, string, bool> DatabaseCombinations
    {
        get
        {
            TheoryData<string, string, bool> data = [];

            foreach ((string architecture, string database, bool swagger) in _set)
            {
                data.Add(architecture, database, swagger);
            }

            return data;
        }
    }

    [Fact]
    public void O_conjunto_cobre_os_dois_bancos_nas_duas_arquiteturas()
    {
        // Assert de sanidade do recorte: um conjunto que perdesse um banco ou uma arquitetura
        // deixaria de cobrir o par, e o build passaria a afirmar menos do que diz.
        List<(string Architecture, string Database)> pairs =
        [
            .. _set.Select(row => (row.Architecture, row.Database)),
        ];

        Assert.Contains(("simple", "sqlite"), pairs);
        Assert.Contains(("clean", "sqlite"), pairs);
        Assert.Contains(("simple", "postgresql"), pairs);
        Assert.Contains(("clean", "postgresql"), pairs);

        Assert.Contains(pairs, pair => pair.Database == "sqlite");
        Assert.Contains(pairs, pair => pair.Database == "postgresql");
    }

    [Theory]
    [MemberData(nameof(DatabaseCombinations))]
    public async Task A_combinacao_com_banco_compila_e_o_provider_resolve_no_grafo(
        string architecture,
        string database,
        bool swagger)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using DatabaseRuntime runtime = await DatabaseRuntime.ExtractAsync(
            new GenerationRequest(ProjectName, architecture, database, "none", swagger, "net10.0"),
            cancellationToken);

        // Cache compartilhado — obrigatório na camada 2.
        runtime.Environment["NUGET_PACKAGES"] = _sharedPackages;

        // `dotnet build` do projeto Web API restaura o grafo inteiro (na Clean, também o
        // Infrastructure que hospeda o provider) e falha o teste com a saída se algo quebrar.
        await runtime.BuildAsync(cancellationToken);

        (string providerName, string providerVersion) = _providerByDatabase[database];

        string assets = PersistenceAssetsFile(runtime.Root);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assets));

        // `project.assets.json` lista o que o restore resolveu, com chave "Nome/Versão". Encontrar o
        // provider ali é a prova transitiva: o `.csproj` pediu, e o grafo entregou, na versão certa.
        bool resolved = document.RootElement
            .GetProperty("libraries")
            .EnumerateObject()
            .Any(library => string.Equals(
                library.Name,
                $"{providerName}/{providerVersion}",
                StringComparison.Ordinal));

        Assert.True(
            resolved,
            $"{architecture}/{database}: '{providerName}/{providerVersion}' não aparece resolvido " +
            $"em '{assets}'. O restore não trouxe o provider que o '.csproj' declara.");
    }

    /// <summary>
    /// O <c>obj/project.assets.json</c> do projeto que hospeda a persistência — o que contém o
    /// <c>AppDbContext.cs</c>. Na Simples é o projeto único; na Clean é o <c>Infrastructure</c>.
    /// </summary>
    private static string PersistenceAssetsFile(string root)
    {
        string context = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "AppDbContext.cs", SearchOption.AllDirectories)
            .Single();

        // Sobe da pasta do arquivo até a pasta que tem o `.csproj`.
        string? directory = Path.GetDirectoryName(context);

        while (directory is not null &&
            Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Any() ==
                false)
        {
            directory = Path.GetDirectoryName(directory);
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Não achei o '.csproj' do projeto de persistência a partir de '{context}'.");
        }

        string assets = Path.Combine(directory, "obj", "project.assets.json");

        if (!File.Exists(assets))
        {
            throw new InvalidOperationException(
                $"O restore não gerou '{assets}'. O build do projeto de persistência não " +
                "aconteceu, e a prova transitiva desta camada não tem o que ler.");
        }

        return assets;
    }
}
