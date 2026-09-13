using __ProjectName__.Api.Models;

namespace __ProjectName__.Api.Endpoints;

/// <summary>O endpoint de saúde.</summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Mapeia <c>GET /health</c>, que é <strong>público em qualquer combinação</strong> (RF-12).
    /// </summary>
    /// <remarks>
    /// O <c>AllowAnonymous</c> é o que garante isso: mesmo quando a geração incluir autenticação,
    /// a saúde continua respondendo sem token — é ela que um balanceador consulta.
    /// </remarks>
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
            .AllowAnonymous()
            .WithName("Health");

        return routes;
    }
}
