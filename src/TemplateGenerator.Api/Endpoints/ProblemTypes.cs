namespace TemplateGenerator.Api.Endpoints;

/// <summary>
/// Os identificadores do membro <c>type</c> do <c>ProblemDetails</c> (RFC 9457).
/// </summary>
/// <remarks>
/// São URIs de identificação, não endereços a serem buscados. O domínio <c>.local</c> é
/// deliberado: deixa explícito que nada aqui é resolvível na internet e que o valor serve para
/// o cliente distinguir uma classe de erro da outra, como manda docs/architecture/http-contract.md.
/// </remarks>
public static class ProblemTypes
{
    /// <summary>Configuração recusada pela validação do servidor.</summary>
    public const string InvalidConfiguration =
        "https://templategenerator.local/problems/invalid-configuration";

    /// <summary>Configuração válida, mas o motor de geração ainda não existe.</summary>
    public const string GenerationNotImplemented =
        "https://templategenerator.local/problems/generation-not-implemented";
}
