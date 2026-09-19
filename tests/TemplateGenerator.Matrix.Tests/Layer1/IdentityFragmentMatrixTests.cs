using System.Text.RegularExpressions;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Validation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 do fragmento <c>auth/identity</c> de T06: o pacote de armazenamento do Identity, a
/// ausência de segredo de assinatura, o CRUD protegido, a saúde pública, os arquivos de identidade
/// e as duas frases que o README precisa dizer — mais a recusa de <c>identity</c> sem banco.
/// </summary>
/// <remarks>
/// <para>
/// Tudo aqui é estático (docs/quality/test-strategy.md, camada 1): gera o ZIP e olha dentro, sem
/// <c>restore</c> e sem <c>build</c>. Cadastrar, entrar e renovar de verdade é da camada 3.
/// </para>
/// <para>
/// <strong>Por que as afirmações são "se e somente se".</strong> Metade de cada critério de T06 é
/// negativa — "não exige segredo JWT", "<c>/health</c> continua público", "nenhuma dependência de
/// opção não marcada" (RF-20). Uma verificação que só olhasse as combinações com Identity deixaria
/// passar exatamente o defeito mais provável de um eixo novo: o fragmento vazando para as
/// combinações que não o escolheram.
/// </para>
/// <para>
/// <strong>Assert de sanidade, obrigatório</strong> (ADR-0008, ADR-0011, ADR-0015): se a matriz
/// deixasse de produzir combinação com Identity — ou deixasse de produzir combinação sem —, os
/// teoremas desta classe passariam por vacuidade. Um verificador que para de verificar em silêncio
/// é pior que nenhum.
/// </para>
/// <para>
/// <strong>O que saiu daqui em T07, e por quê.</strong> Duas afirmações desta classe — "o CRUD
/// exige token" e "todo comando de CRUD do README leva token" — não eram sobre o Identity: são
/// sobre <em>haver autenticação</em> (RF-14, RF-21). Elas calculavam o esperado como
/// <c>authentication == "identity"</c>, o que só coincidia com a verdade enquanto o Identity era a
/// única autenticação escrita; com <c>auth/jwt</c> escrito, as 24 invocações de <c>jwt</c> caíram.
/// As duas mudaram de casa para <see cref="AuthenticationMatrixTests"/>, com a condição certa e
/// com o nome do que afirmam. Aqui ficou o que só vale para o Identity — o pacote de
/// armazenamento, a ausência de segredo de assinatura, os endpoints nativos, as tabelas e as
/// frases próprias do README.
/// </para>
/// </remarks>
public sealed partial class IdentityFragmentMatrixTests
{
    /// <summary>O valor de <c>authentication</c> do Identity nativo.</summary>
    private const string Identity = "identity";

    /// <summary>O pacote de armazenamento do Identity, com a versão exata do fragmento.</summary>
    private const string IdentityPackage = "Microsoft.AspNetCore.Identity.EntityFrameworkCore";

    /// <summary>
    /// A versão fixada no fragmento. Ela acompanha a linha do EF Core que o provedor mais
    /// conservador da matriz fixa, e <strong>não</strong> a versão mais nova disponível — subir só
    /// este pacote traria uma segunda linha do mesmo assembly para a compilação do PostgreSQL, cujo
    /// design time está em 10.0.4 (ADR-0015, "A versão do <c>dotnet-ef</c> é a mesma nos dois
    /// bancos"). Comparar ao literal é o que faz esse acoplamento aparecer no dia em que ele mudar.
    /// </summary>
    private const string IdentityPackageVersion = "10.0.4";

    /// <summary>
    /// O que denunciaria um esquema de token próprio no lugar do Identity nativo: um segredo de
    /// assinatura, um emissor, uma audiência ou o pacote de validação de JWT de terceiros.
    /// </summary>
    /// <remarks>
    /// É o critério de aceite 2 de T06 escrito como verificação: o Identity nativo emite
    /// <em>bearer tokens próprios</em>, e por isso não existe chave a configurar. Se alguém
    /// acrescentar um `Jwt:Key` ao <c>appsettings.json</c> para "facilitar", isto cai.
    /// </remarks>
    private static readonly string[] _jwtMarkers =
    [
        "signingkey",
        "issuersigningkey",
        "jwtbearer",
        "\"jwt\"",
        "\"authority\"",
        "\"audience\"",
    ];

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Fact]
    public async Task A_matriz_produz_combinacao_com_Identity_e_combinacao_sem_Identity()
    {
        int withIdentity = 0;
        int withoutIdentity = 0;
        int protectedCruds = 0;

        foreach (GenerationRequest request in Combinations.Available)
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(
                request,
                TestContext.Current.CancellationToken);

            if (!IsIdentity(request))
            {
                withoutIdentity++;
                continue;
            }

            withIdentity++;

            if (ItemEndpoints(package).Contains("RequireAuthorization", StringComparison.Ordinal))
            {
                protectedCruds++;
            }
        }

        Assert.True(
            withIdentity > 0,
            "A matriz não produziu nenhuma combinação com 'authentication = identity'. Enquanto " +
            "isso for verdade, todo teste 'para cada combinação com Identity…' desta classe passa " +
            "por vacuidade (ADR-0011, 'Assert de sanidade, obrigatório').");

        Assert.True(
            withoutIdentity > 0,
            "A matriz não produziu nenhuma combinação sem Identity. A metade negativa de cada " +
            "afirmação — o fragmento não vaza para quem não o escolheu — passaria por vacuidade.");

        Assert.True(
            protectedCruds > 0,
            "Nenhuma combinação com Identity protegeu o CRUD. O teorema de RF-14 passaria por " +
            "vacuidade.");
    }

    [Fact]
    public void Identity_sem_banco_nao_e_combinacao_valida_da_matriz()
    {
        // Critério de aceite 5 de T06 na origem: a restrição `identity-requires-database` do
        // catálogo. A recusa em si é ProblemDetails 400 da Api, afirmada em
        // TemplateCreationEndpointTests; aqui o que se afirma é que ela vale ANTES de qualquer
        // composição — nenhuma combinação da matriz junta Identity com `database = none`, então
        // nenhum fragmento de `auth/identity` precisa se defender de um pacote sem EF Core.
        Assert.DoesNotContain(
            Combinations.Valid,
            request => IsIdentity(request) &&
                string.Equals(request.Database, "none", StringComparison.Ordinal));

        GenerationRequest invalid = new(
            Combinations.ProjectName,
            Combinations.SimpleArchitecture,
            "none",
            Identity,
            true,
            "net10.0");

        ValidationResult result = GenerationRequestValidator.Validate(TemplateCatalog.Current, invalid);

        Assert.False(result.IsValid);

        Assert.Contains(
            TemplateCatalog.Current.Constraints
                .Single(constraint => string.Equals(
                    constraint.Id,
                    TemplateCatalog.IdentityRequiresDatabaseId,
                    StringComparison.Ordinal))
                .Message,
            result.Errors[CatalogFields.Authentication]);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_pacote_do_Identity_esta_no_projeto_de_persistencia_se_e_somente_se_ha_Identity(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        (string Path, string? Version)[] declared =
        [
            .. PackageLayout.ProjectFiles(package)
                .SelectMany(path => PackageLayout
                    .PackageReferences(package, path)
                    .Where(reference => string.Equals(
                        reference.Name,
                        IdentityPackage,
                        StringComparison.Ordinal))
                    .Select(reference => (Path: path, reference.Version))),
        ];

        if (!string.Equals(authentication, Identity, StringComparison.Ordinal))
        {
            // RF-20: a dependência de uma opção não marcada não entra em projeto nenhum.
            Assert.True(
                declared.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: " +
                $"{string.Join(", ", declared.Select(entry => entry.Path))} declara " +
                $"'{IdentityPackage}' sem Identity escolhido (RF-20).");

            return;
        }

        (string Path, string? Version) single = Assert.Single(declared);

        Assert.True(
            string.Equals(single.Version, IdentityPackageVersion, StringComparison.Ordinal),
            $"{GeneratedPackage.Describe(package.Request)}: '{IdentityPackage}' está na versão " +
            $"'{single.Version ?? "(ausente)"}', e o fragmento fixa '{IdentityPackageVersion}' " +
            "(RNF-06).");

        // Ele mora no projeto que hospeda o `DbContext` da identidade, e não no de composição: em
        // Clean, pô-lo no `Api` faria a `Infrastructure` deixar de compilar, e pô-lo no `Domain`
        // seria referência arquitetural indevida.
        Assert.Equal(PersistenceProject(package), single.Path);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Com_Identity_nenhum_arquivo_do_pacote_pede_segredo_de_assinatura(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério de aceite 2 de T06: o Identity nativo emite bearer tokens próprios e NÃO exige
        // segredo JWT. A afirmação é sobre o pacote inteiro, e não só sobre o `appsettings.json`:
        // uma chave de assinatura em `Program.cs` valeria o mesmo defeito.
        if (!string.Equals(authentication, Identity, StringComparison.Ordinal))
        {
            return;
        }

        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in package.Paths)
        {
            string content = package.Read(path).ToLowerInvariant();

            string[] found =
            [
                .. _jwtMarkers.Where(marker => content.Contains(marker, StringComparison.Ordinal)),
            ];

            Assert.True(
                found.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' menciona " +
                $"{string.Join(", ", found)}. O Identity nativo não tem segredo de assinatura, " +
                "emissor nem audiência a configurar — quem tem é a opção JWT.");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Os_endpoints_nativos_e_as_tabelas_do_Identity_existem_se_e_somente_se_ha_Identity(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério de aceite 1 de T06: os endpoints são os NATIVOS. O que a camada 1 consegue
        // afirmar é que quem os mapeia é `MapIdentityApi`, e não rota escrita à mão — e que as
        // tabelas de usuário têm migração própria, por provider, no projeto de persistência.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        bool expected = string.Equals(authentication, Identity, StringComparison.Ordinal);

        string program = package.Read(package.Paths.Single(path =>
            path.EndsWith("/Program.cs", StringComparison.Ordinal)));

        Assert.True(
            program.Contains("MapIdentityApi<AppUser>()", StringComparison.Ordinal) == expected,
            $"{GeneratedPackage.Describe(package.Request)}: 'Program.cs' " +
            $"{(expected ? "não chama" : "chama")} 'MapIdentityApi', e authentication = " +
            $"{authentication}.");

        foreach (string suffix in (string[])
        [
            "/Identity/AppUser.cs",
            "/Identity/AppIdentityDbContext.cs",
            "/Identity/IdentityRegistration.cs",
            "/Identity/Migrations/20260101000100_IdentitySchema.cs",
            "/Identity/Migrations/20260101000100_IdentitySchema.Designer.cs",
            "/Identity/Migrations/AppIdentityDbContextModelSnapshot.cs",
        ])
        {
            Assert.True(
                package.Paths.Any(path => path.EndsWith(suffix, StringComparison.Ordinal)) == expected,
                $"{GeneratedPackage.Describe(package.Request)}: '{suffix}' " +
                $"{(expected ? "não está" : "está")} no pacote, e authentication = " +
                $"{authentication}.");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_README_ensina_a_migracao_da_identidade_com_dois_caminhos_que_existem_no_pacote(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // O mesmo teorema de ADR-0015 aplicado ao segundo `DbContext`: como o projeto passa a ter
        // dois, cada comando nomeia o seu, e os caminhos lidos do eixo `architecture` precisam
        // resolver para projetos que existem no pacote.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");

        if (!string.Equals(authentication, Identity, StringComparison.Ordinal))
        {
            Assert.DoesNotContain("AppIdentityDbContext", readme, StringComparison.Ordinal);
            return;
        }

        Match command = IdentityDatabaseUpdate().Match(readme);

        Assert.True(
            command.Success,
            $"{GeneratedPackage.Describe(package.Request)}: o README não traz 'dotnet ef " +
            "database update --project <x> --startup-project <y> --context AppIdentityDbContext'.");

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

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_README_diz_que_o_token_nao_e_JWT_e_que_email_exige_configuracao_adicional(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critérios de aceite 6 e 7 de T06, e eles são textuais de propósito: o valor da frase
        // está em ela estar escrita. Sem a primeira, a pessoa procura uma chave de assinatura que
        // não existe; sem a segunda, ela conta com um e-mail que nunca sai.
        if (!string.Equals(authentication, Identity, StringComparison.Ordinal))
        {
            return;
        }

        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        // A comparação é sobre o texto com os espaços colapsados: onde o parágrafo quebra a linha é
        // formatação, e prender o teste à largura da coluna faria uma reformatação inocente falhar
        // sem que a frase tivesse mudado.
        string readme = Whitespace().Replace(package.Read("README.md"), " ");

        foreach (string sentence in (string[])
        [
            "bearer token próprio do ASP.NET Core Identity",
            "**Não é um JWT de terceiros.**",
            "**não existe segredo de assinatura para configurar**",
            "**enviar a mensagem exige configurar um serviço de e-mail, e isso está fora do fluxo " +
            "garantido deste projeto**",
        ])
        {
            Assert.Contains(sentence, readme, StringComparison.Ordinal);
        }
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    private static bool IsIdentity(GenerationRequest request) =>
        string.Equals(request.Authentication, Identity, StringComparison.Ordinal);

    private static string ItemEndpoints(GeneratedPackage package) =>
        package.Read(package.Paths.Single(path =>
            path.EndsWith("/ItemEndpoints.cs", StringComparison.Ordinal)));

    /// <summary>
    /// O <c>.csproj</c> do projeto que hospeda a persistência: aquele cuja pasta contém o
    /// <c>AppDbContext.cs</c> — o mesmo critério de <see cref="DatabaseFragmentMatrixTests"/>,
    /// achado pelo arquivo e não pelo nome, para valer nas duas arquiteturas sem um <c>if</c>.
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
        @"dotnet ef database update --project (?<project>\S+) --startup-project (?<startup>\S+) --context AppIdentityDbContext",
        RegexOptions.CultureInvariant)]
    private static partial Regex IdentityDatabaseUpdate();

    /// <summary>Qualquer sequência de espaço em branco, para colapsar a quebra de linha do README.</summary>
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
