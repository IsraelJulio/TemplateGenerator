using System.Net;
using System.Text.Json;
using TemplateGenerator.Generation.Catalog;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Contrato de <c>GET /api/template-options</c> (docs/architecture/http-contract.md), verificado
/// contra o JSON que sai pela rede — não contra os objetos C# que o produziram.
/// </summary>
/// <remarks>
/// A diferença importa: o frontend lê JSON. Um campo com nome errado, um <c>default</c>
/// serializado como texto onde deveria ser booleano ou uma restrição virada em código não
/// apareceriam numa asserção sobre o objeto.
/// </remarks>
public sealed class TemplateOptionsEndpointTests : IClassFixture<GeneratorApiFactory>
{
    private readonly GeneratorApiFactory _factory;

    public TemplateOptionsEndpointTests(GeneratorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Responde_200_em_json()
    {
        using HttpResponseMessage response = await GetCatalogResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Traz_a_versao_do_conjunto_de_templates()
    {
        using JsonDocument catalog = await GetCatalogAsync();

        string? version = catalog.RootElement.GetProperty("templateVersion").GetString();

        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public async Task Traz_os_campos_configuraveis_da_matriz()
    {
        using JsonDocument catalog = await GetCatalogAsync();

        string[] keys =
        [
            .. catalog.RootElement
                .GetProperty("fields")
                .EnumerateObject()
                .Select(field => field.Name),
        ];

        string[] expected =
        [
            CatalogFields.Architecture,
            CatalogFields.Database,
            CatalogFields.Authentication,
            CatalogFields.Swagger,
            CatalogFields.DotnetVersion,
        ];

        Assert.Equal(expected, keys);
    }

    [Theory]
    [InlineData(CatalogFields.Architecture, "Arquitetura", "simple", 2)]
    [InlineData(CatalogFields.Database, "Banco", "none", 3)]
    [InlineData(CatalogFields.Authentication, "Autenticação", "none", 3)]
    [InlineData(CatalogFields.DotnetVersion, "Versão .NET", "net10.0", 1)]
    public async Task Campo_de_escolha_traz_rotulo_padrao_e_valores(
        string key,
        string label,
        string defaultValue,
        int valueCount)
    {
        using JsonDocument catalog = await GetCatalogAsync();

        JsonElement field = catalog.RootElement.GetProperty("fields").GetProperty(key);

        Assert.Equal(label, field.GetProperty("label").GetString());
        Assert.Equal(defaultValue, field.GetProperty("default").GetString());

        // `type` também no campo de escolha: o cliente não deve deduzi-lo pela ausência de
        // `values` (docs/architecture/http-contract.md, "Regras de serialização", item 3).
        Assert.Equal(CatalogFieldTypes.Choice, field.GetProperty("type").GetString());

        JsonElement values = field.GetProperty("values");

        Assert.Equal(valueCount, values.GetArrayLength());

        Assert.All(
            values.EnumerateArray(),
            option =>
            {
                Assert.False(string.IsNullOrWhiteSpace(option.GetProperty("value").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(option.GetProperty("label").GetString()));
            });

        Assert.Contains(
            values.EnumerateArray(),
            option => option.GetProperty("value").GetString() == defaultValue);
    }

    [Fact]
    public async Task Swagger_chega_como_interruptor_booleano()
    {
        using JsonDocument catalog = await GetCatalogAsync();

        JsonElement field = catalog.RootElement.GetProperty("fields").GetProperty(CatalogFields.Swagger);

        Assert.Equal("Swagger", field.GetProperty("label").GetString());
        Assert.Equal(CatalogFieldTypes.Boolean, field.GetProperty("type").GetString());

        // Booleano de verdade, não a string "true": é o que a tela usa para desenhar um toggle.
        Assert.Equal(JsonValueKind.True, field.GetProperty("default").ValueKind);
        Assert.False(field.TryGetProperty("values", out _));
    }

    [Fact]
    public async Task A_restricao_de_Identity_vem_como_dado_na_lista_constraints()
    {
        using JsonDocument catalog = await GetCatalogAsync();

        JsonElement constraint = Assert.Single(
            catalog.RootElement.GetProperty("constraints").EnumerateArray(),
            candidate => candidate.GetProperty("id").GetString()
                == TemplateCatalog.IdentityRequiresDatabaseId);

        string[] when =
        [
            .. constraint
                .GetProperty("when")
                .GetProperty(CatalogFields.Authentication)
                .EnumerateArray()
                .Select(value => value.GetString()!),
        ];

        Assert.Equal<string>(["identity"], when);

        string[] required =
        [
            .. constraint
                .GetProperty("requires")
                .GetProperty(CatalogFields.Database)
                .EnumerateArray()
                .Select(value => value.GetString()!),
        ];

        Assert.Equal<string>(["sqlite", "postgresql"], required);

        Assert.Equal(
            "O Identity nativo precisa de um banco para persistir os usuários.",
            constraint.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Todo_campo_declara_type_no_json()
    {
        using JsonDocument catalog = await GetCatalogAsync();

        Assert.All(
            catalog.RootElement.GetProperty("fields").EnumerateObject(),
            field =>
            {
                Assert.True(
                    field.Value.TryGetProperty("type", out JsonElement type),
                    $"O campo '{field.Name}' não declarou 'type'.");

                Assert.False(string.IsNullOrWhiteSpace(type.GetString()));
            });
    }

    [Fact]
    public async Task When_e_requires_saem_sempre_como_vetor()
    {
        // Mesmo com um único valor aceito. Um formato que muda conforme a quantidade obriga todo
        // cliente a tratar dois casos, e mudaria sozinho no dia em que a regra ganhar um segundo
        // valor (docs/architecture/http-contract.md, "Regras de serialização", item 4).
        using JsonDocument catalog = await GetCatalogAsync();

        Assert.All(
            catalog.RootElement.GetProperty("constraints").EnumerateArray(),
            constraint =>
            {
                foreach (string side in (string[])["when", "requires"])
                {
                    Assert.All(
                        constraint.GetProperty(side).EnumerateObject(),
                        term => Assert.Equal(JsonValueKind.Array, term.Value.ValueKind));
                }
            });
    }

    [Fact]
    public async Task Toda_restricao_e_legivel_sem_conhecer_a_regra_especifica()
    {
        // O ponto do contrato: acrescentar uma restrição não pode exigir mudança no frontend.
        // Quem lê o catálogo consegue avaliar qualquer regra só com id, when, requires e message.
        using JsonDocument catalog = await GetCatalogAsync();

        JsonElement constraints = catalog.RootElement.GetProperty("constraints");

        Assert.Equal(JsonValueKind.Array, constraints.ValueKind);
        Assert.NotEqual(0, constraints.GetArrayLength());

        Assert.All(
            constraints.EnumerateArray(),
            constraint =>
            {
                Assert.False(string.IsNullOrWhiteSpace(constraint.GetProperty("id").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(constraint.GetProperty("message").GetString()));
                Assert.Equal(JsonValueKind.Object, constraint.GetProperty("when").ValueKind);
                Assert.Equal(JsonValueKind.Object, constraint.GetProperty("requires").ValueKind);
            });
    }

    private async Task<HttpResponseMessage> GetCatalogResponseAsync()
    {
        using HttpClient client = _factory.CreateClient();

        return await client.GetAsync(
            new Uri("/api/template-options", UriKind.Relative),
            TestContext.Current.CancellationToken);
    }

    private async Task<JsonDocument> GetCatalogAsync()
    {
        using HttpResponseMessage response = await GetCatalogResponseAsync();

        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
