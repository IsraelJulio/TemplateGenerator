using System.Net;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Verifica que a Api sobe. Os testes de contrato dos dois endpoints
/// (docs/architecture/http-contract.md) estão em <see cref="TemplateOptionsEndpointTests"/> e
/// <see cref="TemplateCreationEndpointTests"/>.
/// </summary>
public sealed class ApplicationStartupTests : IClassFixture<GeneratorApiFactory>
{
    private readonly GeneratorApiFactory _factory;

    public ApplicationStartupTests(GeneratorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Aplicacao_sobe_e_responde()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Não há rota na raiz — toda rota da plataforma vive sob /api. O que se prova aqui é que
        // o host subiu e respondeu, em vez de estourar na composição.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
