using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 do cruzamento <c>authentication × swagger</c> (T08b): o documento OpenAPI declara o
/// esquema <em>bearer</em> — e a interface ganha o botão <em>Authorize</em> — se e somente se há
/// Swagger <strong>e</strong> autenticação, e isso não custa pacote nenhum.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Como o cruzamento chega ao <c>Program.cs</c>.</strong> <c>auth/identity</c> e
/// <c>auth/jwt</c> contribuem a configuração do documento e os <c>using</c> que ela pede;
/// <c>swagger/enabled</c>, que é o eixo seguinte, lê as duas contribuições para trás (ADR-0015,
/// decisão 1). Com <c>auth = none</c> elas resolvem vazias e a chamada fica
/// <c>AddOpenApi()</c>, sem lambda; com <c>swagger = false</c> ninguém as lê. Não há condicional em
/// template nenhum (ADR-0011, item 9) — por isso a afirmação é sobre o texto gerado, nas 32.
/// </para>
/// <para>
/// <strong>O que esta camada não prova:</strong> que o documento servido em runtime traz o
/// esquema, e que o requisito cai só nas operações protegidas. Isso é a camada 3, em
/// <c>SwaggerRoutes</c>, que lê o <c>/openapi/v1.json</c> da aplicação no ar.
/// </para>
/// <para>
/// <strong>Assert de sanidade, obrigatório</strong> (ADR-0008, ADR-0011, ADR-0015): os dois lados
/// do "se e somente se" precisam existir na matriz, ou os teoremas abaixo passariam por vacuidade.
/// </para>
/// </remarks>
public sealed class OpenApiSecurityMatrixTests
{
    private const string None = "none";
    private const string Identity = "identity";
    private const string Jwt = "jwt";

    /// <summary>Os dois pacotes de ADR-0001 — e os únicos que a opção Swagger pode acrescentar.</summary>
    private static readonly string[] _swaggerPackages =
        ["Microsoft.AspNetCore.OpenApi", "Swashbuckle.AspNetCore"];

    /// <summary>
    /// O que só existe no <c>Program.cs</c> quando o documento declara segurança: o registro do
    /// esquema, o requisito por operação e os dois <c>using</c> que eles pedem.
    /// </summary>
    private static readonly string[] _securityCode =
    [
        "using Microsoft.AspNetCore.Authorization;",
        "using Microsoft.OpenApi;",
        "options.AddDocumentTransformer(",
        "new OpenApiSecurityScheme",
        "Type = SecuritySchemeType.Http,",
        "Scheme = \"bearer\",",
        "options.AddOperationTransformer(",
        "new OpenApiSecurityRequirement",
    ];

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    public static TheoryData<string, string, string, bool> AvailableWithSwaggerCombinations =>
        GenerationMatrixTests.AvailableWithSwaggerCombinations;

    [Fact]
    public void A_matriz_produz_os_dois_lados_do_cruzamento()
    {
        // Com segurança: Swagger e cada uma das autenticações. Sem: Swagger sem autenticação, e
        // cada autenticação sem Swagger. Faltando um deles, uma metade do "se e somente se" para
        // de ser conferida em silêncio.
        foreach (string authentication in (string[])[Identity, Jwt])
        {
            Assert.Contains(
                Combinations.Available,
                request => request.Swagger && request.Authentication == authentication);
            Assert.Contains(
                Combinations.Available,
                request => !request.Swagger && request.Authentication == authentication);
        }

        Assert.Contains(
            Combinations.Available,
            request => request.Swagger && request.Authentication == None);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_Program_declara_seguranca_no_documento_se_e_somente_se_ha_Swagger_e_autenticacao(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string program = Program(package);
        bool expected = swagger && authentication != None;

        foreach (string code in _securityCode)
        {
            Assert.True(
                program.Contains(code, StringComparison.Ordinal) == expected,
                $"{GeneratedPackage.Describe(package.Request)}: o Program.cs " +
                $"{(expected ? "não traz" : "traz")} '{code}'. O esquema bearer entra no documento " +
                "se e somente se swagger = true e authentication ≠ none (T08b, critérios 1 e 3).");
        }

        // A forma da chamada nos três casos. Sem segurança, `AddOpenApi()` sem lambda — e não um
        // `AddOpenApi(options => { })` vazio, que é o invólucro órfão que a regra 7b de ADR-0011
        // existe para impedir. Sem Swagger, chamada nenhuma.
        if (!swagger)
        {
            Assert.DoesNotContain("AddOpenApi", program, StringComparison.Ordinal);
        }
        else if (expected)
        {
            Assert.Contains("builder.Services.AddOpenApi(options =>\n{\n", program, StringComparison.Ordinal);
            Assert.Contains("\n});\n", program, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("builder.Services.AddOpenApi();\n", program, StringComparison.Ordinal);
            Assert.DoesNotContain("OpenApi;", program, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(AvailableWithSwaggerCombinations))]
    public async Task O_requisito_vai_so_onde_o_endpoint_exige_autorizacao(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério 2 de T08b, no código: o requisito não é aplicado a todas as operações em bloco,
        // mas só às que carregam metadado de autorização sem `AllowAnonymous`. É o que deixa
        // `/health` e `/auth/register|login|refresh` sem cadeado no documento. A prova de que o
        // filtro acerta as rotas certas é da camada 3.
        if (authentication == None)
        {
            return;
        }

        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string program = Program(package);

        Assert.Contains(
            "metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any()",
            program,
            StringComparison.Ordinal);
        Assert.Contains("ActionDescriptor.EndpointMetadata", program, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AvailableWithSwaggerCombinations))]
    public async Task O_formato_JWT_e_declarado_so_quando_o_token_e_JWT(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // O token do Identity é bearer próprio, opaco (generated-projects.md, "identity"); o do
        // JWT é JWT. O esquema diz a verdade sobre cada um: `bearerFormat: JWT` no segundo, e
        // nada no primeiro — declará-lo no Identity mandaria a pessoa decodificar um token que não
        // é decodificável.
        if (authentication == None)
        {
            return;
        }

        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string scheme = SchemeBlock(Program(package));

        switch (authentication)
        {
            case Identity:
                Assert.DoesNotContain("BearerFormat", scheme, StringComparison.Ordinal);
                break;
            case Jwt:
                Assert.Contains("BearerFormat = \"JWT\",", scheme, StringComparison.Ordinal);
                break;
            default:
                Assert.Fail(
                    $"authentication = {authentication} declara segurança no documento, e este " +
                    "teste não sabe se o token dela é JWT. Diga aqui qual é o formato.");
                break;
        }
    }

    [Theory]
    [MemberData(nameof(AvailableWithSwaggerCombinations))]
    public async Task O_Swagger_acrescenta_exatamente_os_dois_pacotes_de_ADR_0001(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério 3 de T08b, por IGUALDADE: o conjunto de PackageReference da combinação com
        // Swagger é o da gêmea sem Swagger mais os dois pacotes de ADR-0001, e nada mais. Como a
        // gêmea tem a mesma autenticação, um pacote trazido pela configuração de segurança do
        // documento — o Microsoft.OpenApi direto, por exemplo — derruba este teste nas combinações
        // autenticadas. Sobre as 16 com Swagger, cada uma contra a sua gêmea, as 32 são lidas.
        GeneratedPackage withSwagger = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);
        GeneratedPackage withoutSwagger = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger: false),
            TestContext.Current.CancellationToken);

        string[] expected =
        [
            .. Declared(withoutSwagger).Concat(_swaggerPackages).Order(StringComparer.Ordinal),
        ];

        Assert.Equal(expected, Declared(withSwagger));
    }

    /// <summary>Todas as PackageReference do pacote, por nome, em ordem ordinal.</summary>
    private static string[] Declared(GeneratedPackage package) =>
    [
        .. PackageLayout.ProjectFiles(package)
            .SelectMany(path => PackageLayout.PackageReferences(package, path))
            .Select(reference => reference.Name)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>O <c>Program.cs</c> do projeto Web API.</summary>
    private static string Program(GeneratedPackage package)
    {
        string? web = PackageLayout.WebProjectDirectory(package);

        Assert.NotNull(web);

        return package.Read(web + "Program.cs");
    }

    /// <summary>
    /// O inicializador do esquema, de <c>new OpenApiSecurityScheme</c> até o <c>};</c> que o
    /// fecha — para a afirmação sobre <c>BearerFormat</c> não escorregar para o resto do arquivo.
    /// </summary>
    private static string SchemeBlock(string program)
    {
        int start = program.IndexOf("new OpenApiSecurityScheme", StringComparison.Ordinal);

        Assert.True(start >= 0, "O Program.cs não registra 'new OpenApiSecurityScheme'.");

        int end = program.IndexOf("};", start, StringComparison.Ordinal);

        Assert.True(end > start, "O inicializador de 'OpenApiSecurityScheme' não fecha.");

        return program[start..end];
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");
}
