namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Um par campo/valores aceitos dentro de uma restrição.
/// </summary>
/// <remarks>
/// É o que, no JSON, aparece como uma propriedade de <c>when</c> ou de <c>requires</c>:
/// <c>"database": ["sqlite", "postgresql"]</c>. Quando há um único valor aceito, o JSON traz o
/// escalar — <c>"authentication": "identity"</c> — como no exemplo do contrato.
/// </remarks>
/// <param name="Field">Chave do campo no catálogo.</param>
/// <param name="AcceptedValues">Valores que satisfazem o termo. Nunca vazio.</param>
public sealed record ConstraintTerm(string Field, IReadOnlyList<CatalogValue> AcceptedValues)
{
    /// <summary>Cria um termo a partir de valores textuais.</summary>
    public static ConstraintTerm OfText(string field, params string[] acceptedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(acceptedValues);

        if (acceptedValues.Length == 0)
        {
            throw new ArgumentException(
                "Um termo de restrição sem valor aceito nunca poderia ser satisfeito.",
                nameof(acceptedValues));
        }

        return new ConstraintTerm(field, [.. acceptedValues.Select(CatalogValue.OfText)]);
    }

    /// <summary>Diz se <paramref name="value"/> satisfaz o termo.</summary>
    public bool Matches(CatalogValue value) => AcceptedValues.Contains(value);
}
