namespace TemplateGenerator.Api.Endpoints;

/// <summary>
/// Corpo de <c>GET /api/health</c>: o mínimo que prova que a plataforma está no ar.
/// </summary>
/// <remarks>
/// Deliberadamente pobre. Um health que reporta versão, dependências ou tempo de atividade vira
/// superfície de informação para quem não deveria tê-la, e o gerador não tem dependência externa
/// nenhuma a reportar — é <em>stateless</em>, sem banco e sem sessão.
/// </remarks>
/// <param name="Status">Sempre <c>"ok"</c>: se não estivesse, não haveria resposta.</param>
public sealed record HealthResponse(string Status);
