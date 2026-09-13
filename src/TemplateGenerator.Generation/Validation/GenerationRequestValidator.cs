using TemplateGenerator.Generation.Catalog;

namespace TemplateGenerator.Generation.Validation;

/// <summary>
/// A validação soberana de <c>POST /api/templates</c>: nome do projeto, pertinência ao catálogo
/// e restrições de compatibilidade.
/// </summary>
/// <remarks>
/// <para>
/// Roda <em>antes</em> de qualquer byte ser escrito (docs/architecture/generation-engine.md,
/// seção "Validação"). O que o frontend valida é conveniência; quem decide é isto.
/// </para>
/// <para>
/// Nenhum valor de opção e nenhuma regra de compatibilidade está escrita aqui: tudo vem do
/// <see cref="TemplateOptionsCatalog"/>. Uma restrição nova é um item a mais no catálogo, e este
/// arquivo não muda.
/// </para>
/// </remarks>
public static class GenerationRequestValidator
{
    /// <summary>
    /// Valida <paramref name="request"/> contra <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">O catálogo vigente.</param>
    /// <param name="request">A configuração pedida.</param>
    /// <param name="omittedFields">
    /// Campos que o corpo da requisição não trouxe e que, portanto, não podem ser distinguidos
    /// de um valor legítimo depois da desserialização — o caso do booleano <c>swagger</c>, que
    /// chega como <c>false</c> tanto quando foi enviado assim quanto quando foi omitido. Quem
    /// desserializa é quem sabe; por isso a informação entra por parâmetro.
    /// </param>
    public static ValidationResult Validate(
        TemplateOptionsCatalog catalog,
        GenerationRequest request,
        IEnumerable<string>? omittedFields = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        HashSet<string> omitted = new(omittedFields ?? [], StringComparer.Ordinal);

        List<ValidationFailure> failures =
        [
            .. ProjectNameValidator
                .Validate(request.ProjectName)
                .Select(message => new ValidationFailure(CatalogFields.ProjectName, message)),
        ];

        Dictionary<string, CatalogValue> selection = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, TemplateField> entry in catalog.Fields)
        {
            CatalogValue? value = omitted.Contains(entry.Key)
                ? null
                : request.ValueOf(entry.Key);

            if (value is null)
            {
                failures.Add(new ValidationFailure(
                    entry.Key,
                    $"Informe um valor para o campo {entry.Value.Label}."));

                continue;
            }

            if (!entry.Value.Accepts(value.Value))
            {
                failures.Add(new ValidationFailure(entry.Key, NotInCatalog(entry.Value, value.Value)));

                continue;
            }

            selection[entry.Key] = value.Value;
        }

        foreach (TemplateConstraint constraint in catalog.Constraints)
        {
            if (!constraint.AppliesTo(selection) || constraint.IsSatisfiedBy(selection))
            {
                continue;
            }

            // A mensagem vai para os campos de `when`: são eles que a pessoa acabou de mexer.
            failures.AddRange(constraint.When.Select(term =>
                new ValidationFailure(term.Field, constraint.Message)));
        }

        return ValidationResult.From(failures);
    }

    private static string NotInCatalog(TemplateField field, CatalogValue value)
    {
        if (field.IsToggle)
        {
            return $"O campo {field.Label} aceita apenas verdadeiro ou falso.";
        }

        string accepted = string.Join(", ", field.Values!.Select(option => $"'{option.Value}'"));

        return $"O valor '{value}' não pertence ao catálogo do campo {field.Label}. " +
            $"Valores aceitos: {accepted}.";
    }
}
