using System.Net;
using System.Text.Json;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Contrato de <c>GET /api/health</c>: o sinal de vida da PLATAFORMA geradora.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que o cliente distinga "API fora do ar" de "rota ausente". Sem ele, a única
/// forma de descobrir que a API caiu é falhando o próprio <c>GET /api/template-options</c> — e
/// os dois estados chegam na tela como o mesmo erro.
/// </para>
/// <para>
/// Não confundir com o <c>/health</c> dos projetos GERADOS (RF-12), que é conteúdo de template
/// e assunto de T03.
/// </para>
/// </remarks>
public sealed class HealthEndpointTests : IClassFixture<GeneratorApiFactory>
{
    private static readonly Uri _route = new("/api/health", UriKind.Relative);

    private readonly GeneratorApiFactory _factory;

    public HealthEndpointTests(GeneratorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Responde_200_com_json()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            _route,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Nao_exige_autenticacao_nem_cabecalho()
    {
        // O gerador é stateless e não identifica ninguém: uma requisição nua tem que bastar.
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, _route);

        using HttpResponseMessage response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Uma_rota_inexistente_sob_api_continua_404()
    {
        // É este contraste que dá valor ao health: 200 aqui e 404 ali são estados diferentes,
        // e o cliente precisa poder distingui-los.
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/nao-existe", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
