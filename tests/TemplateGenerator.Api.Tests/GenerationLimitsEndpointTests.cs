using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TemplateGenerator.Api.Generation;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Os dois limites de RNF-04: requisições por origem e gerações simultâneas, ambos respondendo
/// <c>429</c> em <c>ProblemDetails</c> com <c>Retry-After</c>.
/// </summary>
/// <remarks>
/// Cada teste usa a própria instância de host. O limitador guarda a contagem da janela dentro do
/// processo hospedado; compartilhar o host faria um teste contar as requisições do outro.
/// </remarks>
public sealed class GenerationLimitsEndpointTests
{
    private static readonly Uri _route = new("/api/templates", UriKind.Relative);

    /// <summary>Um host com um pedido por janela — o limite por origem atinge no segundo.</summary>
    private sealed class OneRequestPerWindowFactory : GeneratorApiFactory
    {
        protected override void Configure(IServiceCollection services) =>
            services.Configure<GenerationLimits>(limits =>
            {
                limits.RequestsPerWindow = 1;
                limits.WindowSeconds = 60;
                limits.MaxConcurrentGenerations = 8;
            });
    }

    /// <summary>
    /// Um host com uma geração simultânea e um motor que só devolve quando o teste mandar.
    /// </summary>
    /// <remarks>
    /// É o que torna o teste de concorrência determinístico. Disparar N requisições e torcer para
    /// que duas se cruzem produziria um teste que passa quando a máquina está lenta e falha
    /// quando está rápida — ou o contrário, o que é pior.
    /// </remarks>
    private sealed class SingleConcurrentGenerationFactory : GeneratorApiFactory
    {
        public BlockingGenerationEngine Engine { get; } = new();

        protected override void Configure(IServiceCollection services)
        {
            services.Configure<GenerationLimits>(limits =>
            {
                limits.MaxConcurrentGenerations = 1;
                limits.RequestsPerWindow = 1000;
                limits.RetryAfterSeconds = 7;
            });

            services.AddSingleton<IGenerationEngine>(Engine);
        }
    }

    /// <summary>Um motor que avisa quando entrou e espera liberação para terminar.</summary>
    internal sealed class BlockingGenerationEngine : IGenerationEngine
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public void Release() => _release.TrySetResult();

        public async Task WriteArchiveAsync(
            GenerationRequest request,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            _entered.TrySetResult();

            await _release.Task.WaitAsync(cancellationToken);

            await destination.WriteAsync(new byte[] { 0x50, 0x4B, 0x05, 0x06 }, cancellationToken);
        }
    }

    private static Dictionary<string, object?> ValidConfiguration() =>
        new(StringComparer.Ordinal)
        {
            [CatalogFields.ProjectName] = "Acme.Billing.Api",
            [CatalogFields.Architecture] = "simple",
            [CatalogFields.Database] = "none",
            [CatalogFields.Authentication] = "none",
            [CatalogFields.Swagger] = true,
            [CatalogFields.DotnetVersion] = "net10.0",
        };

    private static Task<HttpResponseMessage> PostAsync(HttpClient client) =>
        client.PostAsJsonAsync(_route, ValidConfiguration(), TestContext.Current.CancellationToken);

    private static async Task AssertTooManyRequestsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // Sem `Retry-After` o cliente não tem o que fazer além de tentar de novo na hora, que é
        // exatamente o comportamento que o limite quer evitar.
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.NotNull(response.Headers.RetryAfter!.Delta);
        Assert.True(
            response.Headers.RetryAfter.Delta!.Value > TimeSpan.Zero,
            "Retry-After precisa ser positivo.");

        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            "https://templategenerator.local/problems/too-many-requests",
            problem.RootElement.GetProperty("type").GetString());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "Limite de geração atingido",
            problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Segunda_requisicao_na_mesma_janela_responde_429_com_Retry_After()
    {
        using OneRequestPerWindowFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage first = await PostAsync(client);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using HttpResponseMessage second = await PostAsync(client);

        await AssertTooManyRequestsAsync(second);
    }

    [Fact]
    public async Task O_limite_nao_alcanca_o_health_nem_o_catalogo()
    {
        // O health existe para o cliente distinguir "API fora do ar" de "rota ausente". Limitá-lo
        // faria a plataforma parecer morta justamente sob carga.
        using OneRequestPerWindowFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage generation = await PostAsync(client);

        Assert.Equal(HttpStatusCode.OK, generation.StatusCode);

        using HttpResponseMessage rejected = await PostAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        foreach (string route in (string[])["/api/health", "/api/template-options"])
        {
            using HttpResponseMessage response = await client.GetAsync(
                new Uri(route, UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Geracao_simultanea_acima_do_limite_responde_429_com_Retry_After()
    {
        using SingleConcurrentGenerationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        // A primeira entra no motor e fica lá, segurando a única permissão de concorrência.
        Task<HttpResponseMessage> inFlight = PostAsync(client);

        await factory.Engine.Entered.WaitAsync(
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);

        using HttpResponseMessage rejected = await PostAsync(client);

        await AssertTooManyRequestsAsync(rejected);

        // O valor configurado aparece quando o limitador não sabe dizer quanto falta — é o caso
        // da concorrência, que depende de a outra requisição terminar.
        Assert.Equal(7, (int)rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds);

        factory.Engine.Release();

        using HttpResponseMessage first = await inFlight;

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    }
}
