namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// A configuração está <strong>correta</strong> e o motor para ela não existe: um dos valores que
/// ela seleciona não tem template (ADR-0012).
/// </summary>
/// <remarks>
/// <para>
/// Não confundir com <see cref="GenerationRequestRejectedException"/>. Lá a configuração está
/// errada e a pessoa consegue consertá-la — é <c>400</c>. Aqui ela passou pela validação, pertence
/// ao catálogo e satisfaz as restrições; quem está incompleto é o servidor — é <c>501</c>.
/// </para>
/// <para>
/// A Api recusa <em>antes</em> de chegar ao motor, porque a resposta de recusa não pode sair com
/// cabeçalho de download em cima. Esta exceção é o outro lado da imposição, e sem ela
/// <strong>quem chama o motor direto continuaria recebendo o pacote defeituoso</strong> — e quem
/// chama o motor direto é a camada 1 inteira, que é justamente quem precisa provar a recusa sem
/// subir HTTP. Os dois lados perguntam ao mesmo <see cref="TemplateAvailability"/>: o que se
/// duplica é a imposição, não a verdade.
/// </para>
/// </remarks>
public sealed class GenerationNotAvailableException : InvalidOperationException
{
    /// <summary>
    /// Cria a exceção para os campos cujo valor escolhido não tem template.
    /// </summary>
    /// <param name="fields">Os campos, na ordem do catálogo.</param>
    /// <param name="reason">A razão, a mesma que o catálogo publica.</param>
    public GenerationNotAvailableException(IReadOnlyList<string> fields, string reason)
        : base(BuildMessage(fields, reason))
    {
        Fields = fields;
        Reason = reason;
    }

    /// <summary>
    /// Os campos que causaram a recusa, na ordem do catálogo. É o que vira o membro <c>errors</c>
    /// do <c>ProblemDetails</c> — uma entrada por campo, com a mesma frase.
    /// </summary>
    public IReadOnlyList<string> Fields { get; }

    /// <summary>A razão publicada, palavra por palavra.</summary>
    public string Reason { get; }

    private static string BuildMessage(IReadOnlyList<string> fields, string reason)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return "Esta combinação ainda não gera projeto. " +
            $"Campos sem template: {string.Join(", ", fields)}. {reason}";
    }
}
