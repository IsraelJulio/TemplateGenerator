namespace __ProjectName__.Models;

/// <summary>A resposta de <c>GET /health</c>.</summary>
/// <param name="Status">Sempre <c>"ok"</c> quando a aplicação está no ar.</param>
public sealed record HealthResponse(string Status);
