using System.Text.Json.Serialization;

namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Um valor selecionável de um campo, com rótulo em português.
/// </summary>
/// <param name="Value">Chave estável, em inglês — é o que trafega no corpo do POST.</param>
/// <param name="Label">Rótulo exibido na tela, em português.</param>
public sealed record TemplateOptionValue(string Value, string Label)
{
    /// <summary>Texto auxiliar opcional, em português. Omitido do JSON quando ausente.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
