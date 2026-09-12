namespace TemplateGenerator.Generation.Validation;

/// <summary>
/// O resultado de uma validação: os erros agrupados por campo, na ordem em que apareceram.
/// </summary>
/// <remarks>
/// O formato é o mesmo do membro <c>errors</c> do <c>ProblemDetails</c> (RFC 9457) que a Api
/// devolve, de propósito: a Api transporta, não reinterpreta.
/// </remarks>
public sealed class ValidationResult
{
    private ValidationResult(IReadOnlyDictionary<string, IReadOnlyList<string>> errors)
    {
        Errors = errors;
    }

    /// <summary>Resultado sem nenhum erro.</summary>
    public static ValidationResult Valid { get; } =
        new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    /// <summary>Mensagens por campo, na ordem em que foram produzidas.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; }

    /// <summary>Verdadeiro quando nenhum erro foi encontrado.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Agrupa as falhas por campo, sem repetir mensagem idêntica no mesmo campo.</summary>
    public static ValidationResult From(IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        OrderedDictionary<string, List<string>> grouped = new(StringComparer.Ordinal);

        foreach (ValidationFailure failure in failures)
        {
            if (!grouped.TryGetValue(failure.Field, out List<string>? messages))
            {
                messages = [];
                grouped[failure.Field] = messages;
            }

            if (!messages.Contains(failure.Message, StringComparer.Ordinal))
            {
                messages.Add(failure.Message);
            }
        }

        if (grouped.Count == 0)
        {
            return Valid;
        }

        OrderedDictionary<string, IReadOnlyList<string>> errors = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, List<string>> entry in grouped)
        {
            errors[entry.Key] = entry.Value;
        }

        return new ValidationResult(errors);
    }
}
