using TemplateGenerator.Generation.Validation;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// O motor foi chamado com uma configuração que a validação recusa.
/// </summary>
/// <remarks>
/// <para>
/// Não é a forma normal de recusar uma configuração: quem fala HTTP valida antes e responde
/// <c>ProblemDetails</c> 400 (docs/architecture/http-contract.md). Esta exceção é a última linha
/// de defesa, para que o motor não gere pacote a partir de entrada não validada quando for
/// chamado direto — pelos testes da matriz, por exemplo.
/// </para>
/// <para>
/// Ela carrega o <see cref="ValidationResult"/> inteiro para que quem a capture consiga montar a
/// mesma resposta endereçada ao campo, sem revalidar.
/// </para>
/// </remarks>
public sealed class GenerationRequestRejectedException : ArgumentException
{
    /// <summary>Cria a exceção a partir de um resultado de validação inválido.</summary>
    public GenerationRequestRejectedException(ValidationResult validation)
        : base(BuildMessage(validation))
    {
        Validation = validation;
    }

    /// <summary>O resultado da validação que motivou a recusa.</summary>
    public ValidationResult Validation { get; }

    private static string BuildMessage(ValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        IEnumerable<string> lines = validation.Errors
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}: {string.Join(" ", entry.Value)}");

        return "O motor de geração recebeu uma configuração inválida. " +
            "Valide antes de chamar (GenerationRequestValidator). " +
            string.Join(" | ", lines);
    }
}
