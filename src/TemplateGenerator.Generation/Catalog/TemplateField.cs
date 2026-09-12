using System.Text.Json.Serialization;

namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Um campo configurável do catálogo: rótulo, padrão e, quando é uma escolha, os valores aceitos.
/// </summary>
/// <remarks>
/// Campo de tipo <see cref="CatalogFieldTypes.Choice"/> traz <see cref="Values"/>; campo de tipo
/// <see cref="CatalogFieldTypes.Boolean"/> é um interruptor e não traz valor nenhum. O
/// <see cref="Type"/> é declarado sempre, inclusive na escolha: o contrato proíbe deduzir o tipo
/// pela ausência de <c>values</c> (docs/architecture/http-contract.md).
/// </remarks>
public sealed class TemplateField
{
    /// <summary>Rótulo em português, como aparece na tela.</summary>
    public required string Label { get; init; }

    /// <summary>Valor inicial do campo.</summary>
    [JsonPropertyName("default")]
    public required CatalogValue Default { get; init; }

    /// <summary>Tipo do campo. Obrigatório em todo campo — ver <see cref="CatalogFieldTypes"/>.</summary>
    public required string Type { get; init; }

    /// <summary>Valores aceitos, em ordem de exibição. Ausente no campo booleano.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TemplateOptionValue>? Values { get; init; }

    /// <summary>Texto auxiliar opcional, em português.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>Verdadeiro quando o campo é um interruptor, e não uma lista de valores.</summary>
    [JsonIgnore]
    public bool IsToggle => Values is null;

    /// <summary>Diz se <paramref name="value"/> é aceito por este campo.</summary>
    /// <remarks>
    /// Interruptor aceita qualquer booleano e nenhum texto; escolha aceita apenas um texto que
    /// esteja em <see cref="Values"/>. A comparação é ordinal — chave de catálogo não tem cultura.
    /// </remarks>
    public bool Accepts(CatalogValue value)
    {
        if (IsToggle)
        {
            return !value.IsText;
        }

        return value.IsText
            && Values!.Any(option => string.Equals(option.Value, value.Text, StringComparison.Ordinal));
    }
}
