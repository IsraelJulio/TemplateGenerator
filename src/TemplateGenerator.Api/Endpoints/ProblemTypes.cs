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

    /// <summary>
    /// A configuração é válida e o servidor não tem template para ela (ADR-0012).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>É o mesmo URI que existiu até T02</strong>, e isso é deliberado: o significado — "a
    /// configuração passou pela validação, quem está incompleto é o servidor" — é o mesmo, com
    /// escopo menor. Antes era "o motor de geração ainda não existe"; agora é "esta combinação
    /// ainda não gera projeto". Um URI novo faria um cliente antigo tratar como desconhecido um
    /// caso que ele já sabia tratar.
    /// </para>
    /// <para>
    /// <c>501</c> e não <c>400</c>: a escolha da pessoa está certa e não há nada que ela possa
    /// consertar. Dizer o contrário repetiria, num lugar novo, o erro que o contrato já recusou uma
    /// vez.
    /// </para>
    /// </remarks>
    public const string GenerationNotImplemented =
        "https://templategenerator.local/problems/generation-not-implemented";
}
