using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Validation;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Validation;

/// <summary>
/// A validação soberana de <c>POST /api/templates</c>, exercitada sem subir a Api.
/// </summary>
/// <remarks>
/// Estes testes valem justamente porque <c>TemplateGenerator.Generation</c> não conhece HTTP:
/// as 32 combinações passam por aqui sem um único <c>HttpClient</c>
/// (docs/architecture/platform.md).
/// </remarks>
public sealed class GenerationRequestValidatorTests
{
    private static readonly TemplateOptionsCatalog _catalog = TemplateCatalog.Current;

    private static GenerationRequest Request(
        string projectName = "Acme.Billing.Api",
        string architecture = "simple",
        string database = "none",
        string authentication = "none",
        bool swagger = true,
        string dotnetVersion = "net10.0") =>
        new(projectName, architecture, database, authentication, swagger, dotnetVersion);

    [Fact]
    public void Aceita_configuracao_valida()
    {
        ValidationResult result = GenerationRequestValidator.Validate(_catalog, Request());

        Assert.True(result.IsValid, string.Join(" | ", result.Errors.SelectMany(entry => entry.Value)));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    public void Jwt_funciona_com_qualquer_banco(string database)
    {
        // docs/product/option-matrix.md: a validação do token não depende de persistência.
        ValidationResult result = GenerationRequestValidator.Validate(
            _catalog,
            Request(authentication: "jwt", database: database));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Identity_sem_banco_e_recusado_no_campo_authentication()
    {
        ValidationResult result = GenerationRequestValidator.Validate(
            _catalog,
            Request(authentication: "identity", database: "none"));

        Assert.False(result.IsValid);

        // O erro precisa chegar no campo que a pessoa acabou de mexer, não em "o formulário".
        string[] messages = [.. result.Errors[CatalogFields.Authentication]];

        Assert.Contains(
            messages,
            message => message.Contains("precisa de um banco", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    public void Identity_com_banco_persistente_e_aceito(string database)
    {
        ValidationResult result = GenerationRequestValidator.Validate(
            _catalog,
            Request(authentication: "identity", database: database));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(CatalogFields.Architecture, "hexagonal")]
    [InlineData(CatalogFields.Database, "oracle")]
    [InlineData(CatalogFields.Authentication, "oauth")]
    [InlineData(CatalogFields.DotnetVersion, "net9.0")]
    public void Valor_fora_do_catalogo_e_recusado_no_proprio_campo(string field, string value)
    {
        GenerationRequest request = field switch
        {
            CatalogFields.Architecture => Request(architecture: value),
            CatalogFields.Database => Request(database: value),
            CatalogFields.Authentication => Request(authentication: value),
            _ => Request(dotnetVersion: value),
        };

        ValidationResult result = GenerationRequestValidator.Validate(_catalog, request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors[field],
            message => message.Contains($"'{value}'", StringComparison.Ordinal));
    }

    [Fact]
    public void Campo_de_texto_vazio_e_cobrado_como_obrigatorio()
    {
        ValidationResult result = GenerationRequestValidator.Validate(
            _catalog,
            Request(database: string.Empty));

        Assert.Contains(
            result.Errors[CatalogFields.Database],
            message => message.Contains("Informe um valor", StringComparison.Ordinal));
    }

    [Fact]
    public void Campo_omitido_pelo_cliente_e_cobrado_como_obrigatorio()
    {
        // `swagger` chega como `false` tanto quando foi enviado assim quanto quando foi omitido;
        // quem desserializou é quem sabe a diferença e a informa.
        ValidationResult result = GenerationRequestValidator.Validate(
            _catalog,
            Request(swagger: false),
            [CatalogFields.Swagger]);

        Assert.Contains(
            result.Errors[CatalogFields.Swagger],
            message => message.Contains("Informe um valor", StringComparison.Ordinal));
    }

    [Fact]
    public void Nome_de_projeto_invalido_e_recusado_no_campo_projectName()
    {
        ValidationResult result = GenerationRequestValidator.Validate(
            _catalog,
            Request(projectName: "../etc/passwd"));

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey(CatalogFields.ProjectName));
    }

    [Fact]
    public void A_matriz_tem_exatamente_trinta_e_duas_combinacoes_validas()
    {
        // docs/product/option-matrix.md: 2 × 3 × 3 × 2 = 36 brutas, menos 4 que quebram
        // identity-requires-database. Se este número mudar, a definição de pronto e a estratégia
        // de testes mudam junto — por isso ele é verificado aqui, contra o catálogo real.
        string[] architectures = ValuesOf(CatalogFields.Architecture);
        string[] databases = ValuesOf(CatalogFields.Database);
        string[] authentications = ValuesOf(CatalogFields.Authentication);

        int valid = 0;
        int invalid = 0;

        foreach (string architecture in architectures)
        {
            foreach (string database in databases)
            {
                foreach (string authentication in authentications)
                {
                    foreach (bool swagger in (bool[])[true, false])
                    {
                        ValidationResult result = GenerationRequestValidator.Validate(
                            _catalog,
                            Request(
                                architecture: architecture,
                                database: database,
                                authentication: authentication,
                                swagger: swagger));

                        if (result.IsValid)
                        {
                            valid++;
                        }
                        else
                        {
                            invalid++;
                        }
                    }
                }
            }
        }

        Assert.Equal(36, valid + invalid);
        Assert.Equal(32, valid);
        Assert.Equal(4, invalid);
    }

    private static string[] ValuesOf(string field) =>
        [.. _catalog.Fields[field].Values!.Select(option => option.Value)];
}
