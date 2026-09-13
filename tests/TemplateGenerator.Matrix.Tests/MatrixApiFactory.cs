using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TemplateGenerator.Api.Generation;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// A Api geradora hospedada in-process para a única afirmação da camada 1 que precisa de HTTP:
/// que <c>POST /api/templates</c> recusa toda combinação indisponível com <c>501</c>
/// (ADR-0012, item 5, linha 4).
/// </summary>
/// <remarks>
/// <para>
/// <strong>O limite por origem é afrouxado aqui, e só aqui.</strong> A matriz faz uma requisição
/// por combinação — 32 — e o default de RNF-04 é 30 por janela de 60 segundos; sem o ajuste, as
/// últimas combinações receberiam <c>429</c> e o teste falharia por um motivo que não é o assunto
/// dele. O limite continua existindo e continua sendo verificado onde ele <em>é</em> o assunto:
/// <c>GenerationLimitsEndpointTests</c>, em Api.Tests.
/// </para>
/// <para>
/// O ajuste é por serviço, e não por arquivo de configuração, porque as opções são lidas de
/// <c>IOptions</c> a cada requisição — uma fonte de configuração acrescentada pelo host de teste
/// entraria tarde demais para ser vista no registro.
/// </para>
/// </remarks>
internal sealed class MatrixApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Folga suficiente para a matriz inteira caber na janela, com margem para ela crescer.
    /// </summary>
    private const int RequestsPerWindow = 1_000;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureTestServices(services =>
            services.Configure<GenerationLimits>(limits =>
                limits.RequestsPerWindow = RequestsPerWindow));
    }
}
