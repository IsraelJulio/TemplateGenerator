using Microsoft.AspNetCore.Mvc.Testing;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Hospeda a API geradora in-process para os testes de integração.
/// </summary>
/// <remarks>
/// Sem container (ADR-0005) e sem banco: o gerador é stateless, então não há estado a preparar
/// nem a limpar entre testes.
/// </remarks>
public sealed class GeneratorApiFactory : WebApplicationFactory<Program>
{
}
