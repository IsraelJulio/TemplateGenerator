namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// O catálogo inteiro: versão, campos e restrições. É o corpo de
/// <c>GET /api/template-options</c> e a FONTE DE VERDADE das opções (RF-02).
/// </summary>
/// <remarks>
/// A ordem de <see cref="Fields"/> é a ordem de exibição — por isso a instância concreta é um
/// dicionário ordenado, e não um <c>Dictionary&lt;,&gt;</c> qualquer.
/// </remarks>
public sealed class TemplateOptionsCatalog
{
    /// <summary>Versão do conjunto de templates, repetida no manifesto do ZIP (RF-22).</summary>
    public required string TemplateVersion { get; init; }

    /// <summary>Campos configuráveis, na ordem de exibição.</summary>
    public required IReadOnlyDictionary<string, TemplateField> Fields { get; init; }

    /// <summary>Regras de compatibilidade, como dado.</summary>
    public required IReadOnlyList<TemplateConstraint> Constraints { get; init; }

    /// <summary>Devolve o campo de chave <paramref name="key"/>, ou <c>null</c>.</summary>
    public TemplateField? FindField(string key) =>
        Fields.TryGetValue(key, out TemplateField? field) ? field : null;
}
