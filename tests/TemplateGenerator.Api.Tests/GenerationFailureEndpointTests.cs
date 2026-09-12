using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// O que o cliente recebe quando o defeito é do <em>servidor</em>: template com conflito de
/// caminho, marcador inexistente, caminho fora da raiz.
/// </summary>
/// <remarks>
/// A pergunta que este teste responde é se a recusa chega como erro ou como um ZIP truncado com
/// <c>200</c> em cima. O motor resolve o pacote inteiro na memória antes de escrever o primeiro
/// byte (RNF-03), então a resposta ainda está intacta quando a falha acontece — e o cliente
/// recebe <c>problem+json</c>, não um arquivo que só falha ao ser aberto.
/// </remarks>
public sealed class GenerationFailureEndpointTests
{
    private sealed class DefectiveTemplateFactory : GeneratorApiFactory
    {
        protected override void Configure(IServiceCollection services) =>
            services.AddSingleton<IGenerationEngine>(new DefectiveEngine());
    }

    private sealed class DefectiveEngine : IGenerationEngine
    {
        public Task WriteArchiveAsync(
            GenerationRequest request,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            throw new TemplatePathConflictException(
                "src/Program.cs",
                "architecture/simple",
                "auth/none");
    }

    [Fact]
    public async Task Defeito_de_template_responde_erro_e_nao_um_ZIP_truncado()
    {
        using DefectiveTemplateFactory factory = new();
        using HttpClient client = factory.CreateClient();

        Dictionary<string, object?> configuration = new(StringComparer.Ordinal)
        {
            [CatalogFields.ProjectName] = "Acme.Billing.Api",
            [CatalogFields.Architecture] = "simple",
            [CatalogFields.Database] = "none",
            [CatalogFields.Authentication] = "none",
            [CatalogFields.Swagger] = true,
            [CatalogFields.DotnetVersion] = "net10.0",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/templates", UriKind.Relative),
            configuration,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // E nada de `Content-Disposition`: nenhum navegador deve oferecer isto como download.
        Assert.Null(response.Content.Headers.ContentDisposition);
    }
}
