using System.Text.RegularExpressions;
using System.Xml.Linq;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 dos fragmentos de banco de T05: o conjunto de <c>PackageReference</c> por combinação
/// com banco, o manifesto de ferramenta <c>.config/dotnet-tools.json</c>, o passo de migração
/// escrito no README, e a verificação <strong>negativa</strong> que o ADR-0015 exige — nenhum
/// arquivo do pacote chama <c>Database.Migrate(</c>.
/// </summary>
/// <remarks>
/// <para>
/// Tudo aqui é estático (docs/quality/test-strategy.md, camada 1): gera o ZIP e olha dentro, sem
/// <c>restore</c> e sem <c>build</c>. A execução real — aplicar a migração, CRUD, reinício — é da
/// camada 3, em <see cref="Layer3"/>.
/// </para>
/// <para>
/// <strong>O que cada versão exata defende.</strong> Os providers e o pacote de design time do EF
/// Core andam em pares que <em>não coincidem</em>: o Npgsql fixa o EF Core numa versão e o provider
/// do SQLite noutra, e o pacote de design time acompanha a do EF Core que cada provider exige
/// (ADR-0015 e os comentários dos próprios fragmentos). Um teste que só conferisse "é versão exata"
/// (RNF-06, já coberto em <see cref="PackageReferenceMatrixTests"/>) deixaria passar o dia em que
/// alguém adiantasse o design time do PostgreSQL para a versão do SQLite e as duas versões do mesmo
/// assembly passassem a disputar a compilação. Por isso aqui a versão é comparada ao <em>literal</em>.
/// </para>
/// <para>
/// <strong>Assert de sanidade, obrigatório</strong> (ADR-0011, ADR-0015, "O que defende esta
/// decisão, executável"): se a matriz deixasse de produzir combinação com banco, toda afirmação
/// "para cada combinação com banco…" desta classe passaria por vacuidade. Um verificador que para
/// de verificar em silêncio é pior que nenhum.
/// </para>
/// </remarks>
public sealed partial class DatabaseFragmentMatrixTests
{
    /// <summary>O valor de <c>database</c> quando não há banco — nenhuma migração, nenhum provider.</summary>
    private const string NoDatabase = "none";

    /// <summary>Caminho do manifesto de ferramenta dentro do ZIP (raiz do pacote).</summary>
    private const string DotnetToolsPath = ".config/dotnet-tools.json";

    /// <summary>O provider de EF Core que cada banco declara, com a versão exata do fragmento.</summary>
    private static readonly Dictionary<string, (string Name, string Version)> _providerByDatabase =
        new(StringComparer.Ordinal)
        {
            ["sqlite"] = ("Microsoft.EntityFrameworkCore.Sqlite", "10.0.12"),
            ["postgresql"] = ("Npgsql.EntityFrameworkCore.PostgreSQL", "10.0.3"),
        };

    /// <summary>
    /// O pacote de design time do EF Core, cuja versão acompanha a do EF Core que cada provider
    /// exige — e por isso difere entre os bancos.
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Version)> _designByDatabase =
        new(StringComparer.Ordinal)
        {
            ["sqlite"] = ("Microsoft.EntityFrameworkCore.Design", "10.0.12"),
            ["postgresql"] = ("Microsoft.EntityFrameworkCore.Design", "10.0.4"),
        };

    /// <summary>Todo nome de provider e o design time, para a checagem de ausência com banco = none.</summary>
    private static readonly string[] _allDatabasePackages =
    [
        .. _providerByDatabase.Values.Select(pair => pair.Name),
        _designByDatabase.Values.First().Name,
    ];

    /// <summary>A versão do <c>dotnet-ef</c> fixada no manifesto de ferramenta.</summary>
    private const string DotnetEfVersion = "10.0.12";

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Fact]
    public async Task A_matriz_produz_combinacao_com_banco_e_combinacao_sem_banco()
    {
        // ADR-0015, "Assert de sanidade obrigatório": sem isto, no dia em que a matriz deixasse de
        // acender `sqlite`/`postgresql`, ou deixasse de trazer `none`, os teoremas abaixo passariam
        // sem olhar nada — uns por não terem combinação com banco, o de `none` por não ter `none`.
        int withDatabase = 0;
        int withoutDatabase = 0;
        int readmesWithMigration = 0;
        int toolManifests = 0;

        foreach (GenerationRequest request in Combinations.Available)
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(
                request,
                TestContext.Current.CancellationToken);

            if (string.Equals(request.Database, NoDatabase, StringComparison.Ordinal))
            {
                withoutDatabase++;
                continue;
            }

            withDatabase++;

            if (package.Contains(DotnetToolsPath))
            {
                toolManifests++;
            }

            if (Readme(package).Contains("dotnet ef database update", StringComparison.Ordinal))
            {
                readmesWithMigration++;
            }
        }

        Assert.True(
            withDatabase > 0,
            "A matriz não produziu nenhuma combinação com banco. Enquanto isso for verdade, todo " +
            "teste 'para cada combinação com banco…' desta classe passa por vacuidade " +
            "(ADR-0015, 'Assert de sanidade obrigatório').");

        Assert.True(
            withoutDatabase > 0,
            "A matriz não produziu nenhuma combinação com 'database = none'. A verificação de que " +
            "o pacote de banco NÃO entra sem banco passaria por vacuidade.");

        Assert.True(
            toolManifests > 0,
            $"Nenhuma combinação com banco trouxe '{DotnetToolsPath}'. O teorema do manifesto de " +
            "ferramenta passaria por vacuidade.");

        Assert.True(
            readmesWithMigration > 0,
            "Nenhum README de combinação com banco trouxe 'dotnet ef database update'. O teorema " +
            "do passo de migração passaria por vacuidade.");
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_provedor_de_banco_certo_esta_no_projeto_de_persistencia_com_versao_exata(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        if (!_providerByDatabase.TryGetValue(database, out (string Name, string Version) provider))
        {
            // database = none: nenhum provider de EF, em projeto nenhum (RF-20).
            foreach (string path in PackageLayout.ProjectFiles(package))
            {
                string[] leaked =
                [
                    .. PackageLayout.PackageReferences(package, path)
                        .Select(reference => reference.Name)
                        .Where(name => _allDatabasePackages.Contains(name, StringComparer.Ordinal)),
                ];

                Assert.True(
                    leaked.Length == 0,
                    $"{GeneratedPackage.Describe(package.Request)}: '{path}' declara " +
                    $"{string.Join(", ", leaked)} sem banco escolhido (RF-20).");
            }

            return;
        }

        string persistenceProject = PersistenceProject(package);

        (string Name, string? Version) declared = PackageLayout
            .PackageReferences(package, persistenceProject)
            .SingleOrDefault(reference =>
                string.Equals(reference.Name, provider.Name, StringComparison.Ordinal));

        Assert.True(
            declared.Name is not null,
            $"{GeneratedPackage.Describe(package.Request)}: o projeto de persistência " +
            $"'{persistenceProject}' não declara '{provider.Name}'. É o provider que a opção " +
            $"'database = {database}' exige.");

        Assert.True(
            string.Equals(declared.Version, provider.Version, StringComparison.Ordinal),
            $"{GeneratedPackage.Describe(package.Request)}: '{provider.Name}' está na versão " +
            $"'{declared.Version ?? "(ausente)"}', e o fragmento fixa '{provider.Version}'.");

        // O provider do OUTRO banco não pode aparecer em lugar nenhum do pacote.
        string otherProvider = _providerByDatabase
            .Single(pair => !string.Equals(pair.Key, database, StringComparison.Ordinal))
            .Value.Name;

        foreach (string path in PackageLayout.ProjectFiles(package))
        {
            Assert.DoesNotContain(
                otherProvider,
                PackageLayout.PackageReferences(package, path).Select(reference => reference.Name),
                StringComparer.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_design_time_do_EF_esta_no_projeto_de_API_como_privado_com_versao_exata(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        if (!_designByDatabase.TryGetValue(database, out (string Name, string Version) design))
        {
            // Sem banco, o design time do EF não entra em projeto nenhum. Já coberto pelo laço de
            // ausência do teste do provider, mas afirmado aqui também para que este teorema não
            // dependa da leitura do outro para a sua contraparte de `none`.
            return;
        }

        string? webProject = PackageLayout.WebProjectDirectory(package);

        Assert.NotNull(webProject);

        string apiProject = PackageLayout.ProjectFiles(package)
            .Single(path => path.StartsWith(webProject, StringComparison.Ordinal));

        XElement declared = XDocument.Parse(package.Read(apiProject)).Root!
            .Descendants()
            .Single(element =>
                element.Name.LocalName.Equals("PackageReference", StringComparison.Ordinal) &&
                string.Equals(
                    element.Attribute("Include")?.Value,
                    design.Name,
                    StringComparison.Ordinal));

        Assert.Equal(design.Version, declared.Attribute("Version")?.Value);

        // O design time é uma dependência de ferramenta, não do produto: não pode escapar por
        // transitividade nem ir para o pacote publicado.
        Assert.Equal(
            "all",
            declared.Attribute("PrivateAssets")?.Value,
            ignoreCase: true);

        // E ele mora só no projeto de subida (`--startup-project`, ADR-0015). Num projeto de
        // domínio ou de aplicação seria referência arquitetural indevida.
        foreach (string path in PackageLayout.ProjectFiles(package)
            .Where(path => !path.StartsWith(webProject, StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(
                design.Name,
                PackageLayout.PackageReferences(package, path).Select(reference => reference.Name),
                StringComparer.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_manifesto_de_ferramenta_existe_se_e_somente_se_ha_banco_com_versao_literal(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // ADR-0015: `.config/dotnet-tools.json` fixa o `dotnet-ef` para que a migração seja
        // `dotnet tool restore` + `dotnet ef database update`, e NÃO `dotnet tool install --global`.
        // Ele existe se e somente se há migração a aplicar — num pacote sem EF Core seria a
        // dependência de opção não marcada que RF-20 recusa.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        bool hasDatabase = !string.Equals(database, NoDatabase, StringComparison.Ordinal);

        Assert.True(
            package.Contains(DotnetToolsPath) == hasDatabase,
            $"{GeneratedPackage.Describe(package.Request)}: '{DotnetToolsPath}' " +
            $"{(package.Contains(DotnetToolsPath) ? "está presente" : "está ausente")}, mas " +
            $"database = {database}. O manifesto existe se e somente se há banco (ADR-0015).");

        if (!hasDatabase)
        {
            return;
        }

        using System.Text.Json.JsonDocument manifest =
            System.Text.Json.JsonDocument.Parse(package.Read(DotnetToolsPath));

        string? version = manifest.RootElement
            .GetProperty("tools")
            .GetProperty("dotnet-ef")
            .GetProperty("version")
            .GetString();

        Assert.True(
            string.Equals(version, DotnetEfVersion, StringComparison.Ordinal),
            $"{GeneratedPackage.Describe(package.Request)}: o manifesto fixa 'dotnet-ef' em " +
            $"'{version}', e a versão literal esperada é '{DotnetEfVersion}' (RNF-06, ADR-0015).");
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhum_arquivo_do_pacote_chama_Database_Migrate(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // ADR-0015, decisão 2, verificação negativa: o projeto gerado NÃO aplica migração ao subir.
        // A migração é passo do README, com `dotnet ef database update`. Um `Database.Migrate(` de
        // volta em qualquer arquivo — `Program.cs`, `PersistenceRegistration.cs` — devolveria a
        // chamada de banco ao arranque que a decisão tirou.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in package.Paths)
        {
            Assert.False(
                package.Read(path).Contains("Database.Migrate(", StringComparison.Ordinal),
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' contém " +
                "'Database.Migrate('. O projeto gerado não migra na subida — a migração é passo do " +
                "README (ADR-0015, decisão 2).");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_README_ensina_ef_database_update_com_dois_caminhos_que_existem_no_pacote(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // ADR-0015, "O que defende esta decisão, executável": em toda combinação com banco, o README
        // traz `dotnet ef database update` com `--project` e `--startup-project`, e os dois caminhos
        // EXISTEM no pacote — a prova de que os marcadores lidos de eixo anterior resolveram para
        // projetos reais, e não sobraram como `__...__` no texto.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = Readme(package);

        if (string.Equals(database, NoDatabase, StringComparison.Ordinal))
        {
            Assert.DoesNotContain("dotnet ef database update", readme, StringComparison.Ordinal);
            return;
        }

        Match command = EfDatabaseUpdate().Match(readme);

        Assert.True(
            command.Success,
            $"{GeneratedPackage.Describe(package.Request)}: o README não traz " +
            "'dotnet ef database update --project <x> --startup-project <y>' (ADR-0015).");

        foreach ((string flag, string path) in (ValueTuple<string, string>[])
        [
            ("--project", command.Groups["project"].Value),
            ("--startup-project", command.Groups["startup"].Value),
        ])
        {
            Assert.False(
                path.Contains("__", StringComparison.Ordinal),
                $"{GeneratedPackage.Describe(package.Request)}: o caminho de '{flag}' no README " +
                $"ainda tem um marcador não resolvido ('{path}').");

            Assert.True(
                package.Paths.Any(entry =>
                    entry.StartsWith(path + "/", StringComparison.Ordinal) &&
                    entry.EndsWith(".csproj", StringComparison.Ordinal)),
                $"{GeneratedPackage.Describe(package.Request)}: o caminho de '{flag}' no README " +
                $"('{path}') não é um projeto que existe no pacote.");
        }
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    private static string Readme(GeneratedPackage package) => package.Read("README.md");

    /// <summary>
    /// O <c>.csproj</c> do projeto que hospeda a persistência: aquele cuja pasta contém o
    /// <c>AppDbContext.cs</c>. Na Simples é o projeto único; na Clean é o <c>Infrastructure</c>.
    /// Achado pelo arquivo, e não pelo nome, para valer nas duas arquiteturas sem um <c>if</c>.
    /// </summary>
    private static string PersistenceProject(GeneratedPackage package)
    {
        string context = package.Paths.Single(path =>
            path.EndsWith("/AppDbContext.cs", StringComparison.Ordinal));

        return PackageLayout.SourceProjects(package).Single(project =>
            context.StartsWith(
                project[..(project.LastIndexOf('/') + 1)],
                StringComparison.Ordinal));
    }

    [GeneratedRegex(
        @"dotnet ef database update --project (?<project>\S+) --startup-project (?<startup>\S+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex EfDatabaseUpdate();
}
