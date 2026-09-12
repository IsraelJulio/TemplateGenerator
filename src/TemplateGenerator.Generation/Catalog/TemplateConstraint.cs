using System.Text.Json.Serialization;

namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Uma regra de compatibilidade expressa como DADO.
/// </summary>
/// <remarks>
/// <para>
/// Quando todos os termos de <see cref="When"/> casam com a seleção, todos os termos de
/// <see cref="Requires"/> precisam ser satisfeitos. Acrescentar uma restrição é acrescentar um
/// item nesta lista — não existe código especial para nenhuma regra em particular, nem aqui nem
/// no frontend (docs/architecture/http-contract.md).
/// </para>
/// <para>
/// A mensagem é endereçada aos campos de <see cref="When"/>: são eles que a pessoa acabou de
/// mexer e é ao lado deles que a tela mostra o erro.
/// </para>
/// </remarks>
public sealed class TemplateConstraint
{
    /// <summary>Identificador estável da regra, em inglês.</summary>
    public required string Id { get; init; }

    /// <summary>Condição de aplicação da regra.</summary>
    [JsonConverter(typeof(ConstraintTermsJsonConverter))]
    public required IReadOnlyList<ConstraintTerm> When { get; init; }

    /// <summary>O que precisa valer quando a regra se aplica.</summary>
    [JsonConverter(typeof(ConstraintTermsJsonConverter))]
    public required IReadOnlyList<ConstraintTerm> Requires { get; init; }

    /// <summary>Explicação em português, exibida ao lado do campo culpado.</summary>
    public required string Message { get; init; }

    /// <summary>Diz se a regra se aplica à <paramref name="selection"/> informada.</summary>
    /// <remarks>
    /// Campo ausente da seleção nunca casa: a regra simplesmente não se aplica.
    /// </remarks>
    public bool AppliesTo(IReadOnlyDictionary<string, CatalogValue> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return When.All(term =>
            selection.TryGetValue(term.Field, out CatalogValue value) && term.Matches(value));
    }

    /// <summary>Diz se a <paramref name="selection"/> satisfaz o lado <c>requires</c>.</summary>
    /// <remarks>
    /// Campo ausente da seleção é considerado satisfeito — a ausência já é reportada pela
    /// validação de campo obrigatório, e duplicar o erro só polui a tela.
    /// </remarks>
    public bool IsSatisfiedBy(IReadOnlyDictionary<string, CatalogValue> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return Requires.All(term =>
            !selection.TryGetValue(term.Field, out CatalogValue value) || term.Matches(value));
    }
}
