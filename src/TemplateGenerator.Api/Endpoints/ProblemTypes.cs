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

    /// <summary>
    /// Limite de requisições por origem ou de gerações simultâneas atingido (RNF-04).
    /// </summary>
    /// <remarks>
    /// A resposta carrega <c>Retry-After</c>. A configuração não tem defeito nenhum: o pedido
    /// chegou na hora errada e vale repetir.
    /// </remarks>
    public const string TooManyRequests =
        "https://templategenerator.local/problems/too-many-requests";

    // Havia aqui `GenerationNotImplemented`, o 501 que a Api respondia enquanto o motor de
    // geração não existia (T01/T02). O motor entrou em T03 e o `POST /api/templates` passou a
    // responder `200 application/zip` — não existe mais estado em que a Api afirme "válido, mas
    // não implementado". Ver docs/architecture/http-contract.md, "Estado transitório: 501".
}
