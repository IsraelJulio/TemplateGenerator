using System.Text.Json;
using System.Text.RegularExpressions;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 do fragmento <c>auth/jwt</c> de T07: o pacote de validação de bearer no projeto de
/// composição, <c>Jwt:Authority</c> e <c>Jwt:Audience</c> vazios no <c>appsettings.json</c>, as
/// quatro conferências de RF-19 escritas uma a uma com a tolerância de relógio zerada, o passo do
/// README que aponta os dois para o provedor existente — e, dos dois lados, a
/// <strong>ausência</strong>: nenhum provedor embutido, nenhum segredo, nenhuma dependência de
/// banco.
/// </summary>
/// <remarks>
/// <para>
/// Tudo aqui é estático (docs/quality/test-strategy.md, camada 1): gera o ZIP e olha dentro, sem
/// <c>restore</c> e sem <c>build</c>. Aceitar um token e recusar os quatro casos de rejeição é da
/// camada 3, em <see cref="Layer3.JwtRuntimeTests"/>, contra o emissor OIDC in-process de ADR-0006.
/// </para>
/// <para>
/// <strong>O que só esta camada consegue afirmar.</strong> A camada 3 exercita <em>duas</em>
/// combinações de <c>jwt</c>; as doze existem, e o defeito mais provável de um eixo novo é o
/// fragmento vazar para quem não o escolheu ou faltar em quem o escolheu. Por isso toda afirmação
/// abaixo é "se e somente se", sobre as 32 combinações — e não sobre as 12 com <c>jwt</c>.
/// </para>
/// <para>
/// <strong>Assert de sanidade, obrigatório</strong> (ADR-0008, ADR-0011, ADR-0015):
/// <see cref="A_matriz_produz_JWT_com_cada_banco_e_combinacao_sem_JWT"/> é a condição de validade
/// de todo o resto. Um verificador que para de verificar em silêncio é pior que nenhum.
/// </para>
/// <para>
/// O que vale para <em>qualquer</em> autenticação — o CRUD protegido, a saúde pública, os comandos
/// do README com token — está em <see cref="AuthenticationMatrixTests"/>, e não aqui: repetir ali
/// e aqui a mesma afirmação criaria duas cópias que divergiriam.
/// </para>
/// </remarks>
public sealed partial class JwtFragmentMatrixTests
{
    /// <summary>O valor de <c>authentication</c> da validação de JWT externo.</summary>
    private const string Jwt = "jwt";

    /// <summary>O valor de <c>authentication</c> sem autenticação nenhuma.</summary>
    private const string NoAuthentication = "none";

    /// <summary>O validador de bearer do ASP.NET Core, com a versão exata do fragmento (RNF-06).</summary>
    private const string JwtBearerPackage = "Microsoft.AspNetCore.Authentication.JwtBearer";

    /// <summary>A versão fixada no fragmento.</summary>
    private const string JwtBearerPackageVersion = "10.0.12";

    /// <summary>
    /// O que denunciaria um provedor de identidade <strong>embutido</strong> no pacote — o oposto
    /// exato do que esta opção é.
    /// </summary>
    /// <remarks>
    /// Critério de aceite 1 de T07, escrito como verificação: a aplicação gerada <em>confere</em>
    /// token de um emissor externo e não emite nenhum. Um servidor de identidade no pacote traria
    /// chave de assinatura, armazenamento de usuário e emissão — e a combinação deixaria de
    /// funcionar sem banco, que é o critério 5.
    /// </remarks>
    private static readonly string[] _embeddedProviderPackages =
    [
        "Duende.IdentityServer",
        "IdentityServer4",
        "OpenIddict",
        "Microsoft.AspNetCore.Identity",
    ];

    /// <summary>
    /// Material de segredo que não pode estar em arquivo nenhum do pacote (verificação 4 de T07).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A lista é de <strong>material</strong>, não de palavras: o README fala de
    /// <c>client_secret</c> de propósito, para ensinar a pedir o token, e o <c>Program.cs</c> chama
    /// <c>ValidateIssuerSigningKey</c>, que é a conferência e não a chave. Procurar "secret" ou
    /// "signingkey" como substring acusaria os dois e o teste viraria ruído — e ruído se desliga.
    /// </para>
    /// <para>
    /// O que está aqui é o que só aparece quando alguém <em>embutiu</em> uma chave: um bloco PEM,
    /// uma chave simétrica construída em código, credenciais de assinatura, ou uma chave lida da
    /// configuração (<c>Jwt:Key</c>, o atalho que a opção JWT convida a tomar e que esta
    /// combinação não tem).
    /// </para>
    /// </remarks>
    private static readonly string[] _secretMarkers =
    [
        "-----begin",
        "symmetricsecuritykey",
        "signingcredentials",
        "new rsasecuritykey",
        "jwt:key",
        "jwt__key",
    ];

    /// <summary>Extensões de arquivo que carregam chave ou certificado.</summary>
    private static readonly string[] _keyFileExtensions =
        [".pem", ".key", ".pfx", ".p12", ".jks", ".snk", ".p8", ".jwk", ".der"];

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Fact]
    public void A_matriz_produz_JWT_com_cada_banco_e_combinacao_sem_JWT()
    {
        // Sanidade, e ela carrega o critério de aceite 5 na origem: `jwt` precisa aparecer com
        // TODO valor de `database`, `none` inclusive. Se a matriz deixasse de produzir a
        // combinação sem banco, "funciona com qualquer banco" passaria a ser afirmado por um
        // conjunto que não a contém — e nenhuma teoria ficaria vermelha.
        string[] databases =
        [
            .. Combinations.Available
                .Where(IsJwt)
                .Select(request => request.Database)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal<string>(["none", "postgresql", "sqlite"], databases);

        Assert.Equal(
            12,
            Combinations.Available.Count(IsJwt));

        Assert.Contains(Combinations.Available, request => !IsJwt(request));
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_pacote_de_bearer_esta_no_projeto_de_composicao_se_e_somente_se_ha_JWT(
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
                        JwtBearerPackage,
                        StringComparison.Ordinal))
                    .Select(reference => (Path: path, reference.Version))),
        ];

        if (!string.Equals(authentication, Jwt, StringComparison.Ordinal))
        {
            // RF-20: a dependência de uma opção não marcada não entra em projeto nenhum.
            Assert.True(
                declared.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: " +
                $"{string.Join(", ", declared.Select(entry => entry.Path))} declara " +
                $"'{JwtBearerPackage}' sem JWT escolhido (RF-20).");

            return;
        }

        (string Path, string? Version) single = Assert.Single(declared);

        Assert.True(
            string.Equals(single.Version, JwtBearerPackageVersion, StringComparison.Ordinal),
            $"{GeneratedPackage.Describe(package.Request)}: '{JwtBearerPackage}' está na versão " +
            $"'{single.Version ?? "(ausente)"}', e o fragmento fixa '{JwtBearerPackageVersion}' " +
            "(RNF-06).");

        // Ele mora no projeto de COMPOSIÇÃO — o que tem o `Program.cs` —, porque é lá que
        // `AddJwtBearer` é chamado. Em Clean, pô-lo no `Infrastructure` faria a composição não
        // compilar, e é o tipo de engano que só aparece na camada 2 se ninguém afirmar isto aqui.
        Assert.Equal(WebProject(package), single.Path);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhum_pacote_de_provedor_de_identidade_entra_com_JWT(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério de aceite 1, metade negativa: "sem provedor embutido". A aplicação confere o
        // token que chega e não emite nenhum — nenhum servidor de identidade, nenhum
        // armazenamento de usuário. É o que separa esta opção de `auth/identity`, e é por isso que
        // ela vale sem banco.
        if (!string.Equals(authentication, Jwt, StringComparison.Ordinal))
        {
            return;
        }

        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string project in PackageLayout.ProjectFiles(package))
        {
            foreach ((string name, string? _) in PackageLayout.PackageReferences(package, project))
            {
                Assert.DoesNotContain(
                    _embeddedProviderPackages,
                    forbidden => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_appsettings_traz_Authority_e_Audience_vazios_se_e_somente_se_ha_JWT(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério de aceite 1: as duas chaves existem em `appsettings.json`, e nascem VAZIAS. Um
        // endereço de exemplo apontaria para lugar nenhum e daria a impressão de configuração
        // pronta; o vazio é o que faz a pessoa ir ao passo do README. E são só essas duas: uma
        // terceira chave sob `Jwt` seria o segredo que esta opção não tem.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string settingsPath = WebProjectDirectory(package) + "appsettings.json";

        using JsonDocument settings = JsonDocument.Parse(package.Read(settingsPath));

        bool expected = string.Equals(authentication, Jwt, StringComparison.Ordinal);

        bool present = settings.RootElement.TryGetProperty("Jwt", out JsonElement jwt);

        Assert.True(
            present == expected,
            $"{GeneratedPackage.Describe(package.Request)}: '{settingsPath}' " +
            $"{(expected ? "não traz" : "traz")} a seção 'Jwt', e authentication = " +
            $"{authentication} (RF-20).");

        if (!expected)
        {
            return;
        }

        Assert.Equal<string>(
            ["Audience", "Authority"],
            [.. jwt.EnumerateObject().Select(entry => entry.Name).Order(StringComparer.Ordinal)]);

        Assert.Equal(string.Empty, jwt.GetProperty("Authority").GetString());
        Assert.Equal(string.Empty, jwt.GetProperty("Audience").GetString());
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task As_quatro_conferencias_de_RF19_estao_escritas_com_folga_de_relogio_zerada(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // RF-19 na composição, lido no código gerado. As quatro linhas estão escritas uma a uma em
        // vez de herdadas do default, e é isso que este teste fixa: herdadas, elas desapareceriam
        // do arquivo e ninguém que lesse o `Program.cs` saberia dizer o que o projeto confere.
        //
        // `ClockSkew = TimeSpan.Zero` é o que a camada 3 vai cobrar em runtime: sem ele, a
        // biblioteca aceitaria por cinco minutos um token já vencido, e o caso "expirado" de RF-19
        // passaria a depender de o teste esperar cinco minutos para ser verdade.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string program = package.Read(WebProjectDirectory(package) + "Program.cs");

        bool expected = string.Equals(authentication, Jwt, StringComparison.Ordinal);

        foreach (string line in (string[])
        [
            "options.Authority = builder.Configuration[\"Jwt:Authority\"];",
            "options.Audience = builder.Configuration[\"Jwt:Audience\"];",
            "options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();",
            "options.TokenValidationParameters.ValidateIssuerSigningKey = true;",
            "options.TokenValidationParameters.ValidateIssuer = true;",
            "options.TokenValidationParameters.ValidateAudience = true;",
            "options.TokenValidationParameters.ValidateLifetime = true;",
            "options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;",
            ".AddJwtBearer(options =>",
        ])
        {
            Assert.True(
                program.Contains(line, StringComparison.Ordinal) == expected,
                $"{GeneratedPackage.Describe(package.Request)}: 'Program.cs' " +
                $"{(expected ? "não traz" : "traz")} `{line}`, e authentication = " +
                $"{authentication}.");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_README_ensina_a_apontar_Authority_e_Audience_se_e_somente_se_ha_JWT(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério de aceite 7. E a metade de ADR-0015 que ele exercita: o caminho do
        // `appsettings.json` citado no passo vem do eixo ANTERIOR, por `__ApiProjectDir__`. Se o
        // marcador não resolvesse, o README mandaria editar um arquivo com `__` no nome; se
        // resolvesse errado, mandaria editar um arquivo que o pacote não tem. As duas falhas são
        // invisíveis para quem só confere que a frase está lá.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");

        if (!string.Equals(authentication, Jwt, StringComparison.Ordinal))
        {
            Assert.DoesNotContain("Jwt__Authority", readme, StringComparison.Ordinal);
            Assert.DoesNotContain("Jwt:Authority", readme, StringComparison.Ordinal);

            return;
        }

        foreach (string sentence in (string[])
        [
            "## Autenticação",
            "### 1. Apontar `Authority` e `Audience` para o seu provedor",
            "export Jwt__Authority=",
            "export Jwt__Audience=",
            "$env:Jwt__Authority = ",
            "$env:Jwt__Audience = ",
            "/.well-known/openid-configuration",
        ])
        {
            Assert.Contains(sentence, readme, StringComparison.Ordinal);
        }

        Match edit = AppSettingsToEdit().Match(readme);

        Assert.True(
            edit.Success,
            $"{GeneratedPackage.Describe(package.Request)}: o README não diz em qual " +
            "'appsettings.json' preencher Authority e Audience.");

        string path = edit.Groups["path"].Value;

        Assert.False(
            path.Contains("__", StringComparison.Ordinal),
            $"{GeneratedPackage.Describe(package.Request)}: o caminho do 'appsettings.json' no " +
            $"README ainda tem um marcador não resolvido ('{path}').");

        Assert.True(
            package.Contains(path),
            $"{GeneratedPackage.Describe(package.Request)}: o README manda preencher '{path}', " +
            "que não é um arquivo do pacote.");

        Assert.Equal(WebProjectDirectory(package) + "appsettings.json", path);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Com_JWT_nenhum_arquivo_do_pacote_carrega_chave_ou_certificado(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Verificação 4 de T07, do lado do pacote entregue: o ZIP que a pessoa baixa não pode
        // trazer segredo nenhum. Não há o que ocultar aqui — a aplicação só confere assinatura com
        // a chave PÚBLICA que o provedor publica no JWKS —, e é justamente por isso que uma chave
        // aparecendo no pacote seria um defeito grave e silencioso.
        if (!string.Equals(authentication, Jwt, StringComparison.Ordinal))
        {
            return;
        }

        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in package.Paths)
        {
            Assert.DoesNotContain(
                _keyFileExtensions,
                extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

            string content = package.Read(path).ToLowerInvariant();

            string[] found =
            [
                .. _secretMarkers.Where(marker =>
                    content.Contains(marker, StringComparison.Ordinal)),
            ];

            Assert.True(
                found.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' traz " +
                $"{string.Join(", ", found)}. Esta opção valida token de um provedor externo com " +
                "a chave pública dele — não existe segredo a versionar no pacote.");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_JWT_nao_acrescenta_nem_remove_arquivo_do_ZIP_em_banco_nenhum(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério de aceite 5, e a prova é estrutural em vez de textual: a árvore do pacote com
        // `jwt` é IGUAL à do mesmo pacote com `none`. Disso segue que `auth/jwt` é composição pura
        // — ele não traz `DbContext`, migração, projeto de persistência nem arquivo de identidade
        // —, e portanto não tem como depender do banco escolhido. Uma dependência de banco
        // apareceria aqui como um arquivo a mais em `sqlite`/`postgresql` e a menos em `none`.
        //
        // A afirmação contrária — que o CONTEÚDO muda — está nas teorias acima, que leem o
        // `Program.cs`, o `.csproj` e o `appsettings.json`. Sem elas, esta passaria com um
        // fragmento vazio.
        if (!string.Equals(authentication, Jwt, StringComparison.Ordinal))
        {
            return;
        }

        GeneratedPackage comJwt = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        GeneratedPackage semAutenticacao = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, NoAuthentication, swagger),
            TestContext.Current.CancellationToken);

        Assert.Equal<string>(
            [.. semAutenticacao.Paths.Order(StringComparer.Ordinal)],
            [.. comJwt.Paths.Order(StringComparer.Ordinal)]);

        // E a contraprova de que os dois pacotes não são o mesmo arquivo: se fossem, a igualdade
        // acima seria trivial e o fragmento poderia estar fazendo nada.
        Assert.NotEqual(semAutenticacao.Sha256, comJwt.Sha256);
    }

    private static bool IsJwt(GenerationRequest request) =>
        string.Equals(request.Authentication, Jwt, StringComparison.Ordinal);

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    /// <summary>
    /// O diretório do projeto de composição dentro do pacote, com <c>/</c> no fim — achado pelo
    /// <c>Program.cs</c>, e não pelo nome, para valer nas duas arquiteturas.
    /// </summary>
    private static string WebProjectDirectory(GeneratedPackage package) =>
        PackageLayout.WebProjectDirectory(package)
            ?? throw new InvalidOperationException(
                $"O pacote de {GeneratedPackage.Describe(package.Request)} não tem exatamente um " +
                "'Program.cs'.");

    /// <summary>O <c>.csproj</c> do projeto de composição.</summary>
    private static string WebProject(GeneratedPackage package)
    {
        string directory = WebProjectDirectory(package);

        return PackageLayout.SourceProjects(package).Single(project =>
            project.StartsWith(directory, StringComparison.Ordinal));
    }

    /// <summary>
    /// O caminho do <c>appsettings.json</c> que o passo 1 do README manda preencher.
    /// </summary>
    [GeneratedRegex(
        @"Preencha os dois em `(?<path>[^`]+/appsettings\.json)`",
        RegexOptions.CultureInvariant)]
    private static partial Regex AppSettingsToEdit();
}
