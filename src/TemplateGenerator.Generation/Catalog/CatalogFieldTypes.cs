namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Os tipos que um campo do catálogo pode declarar em <c>type</c>.
/// </summary>
/// <remarks>
/// O contrato exige <c>type</c> em TODO campo, inclusive nos de escolha
/// (docs/architecture/http-contract.md, "Regras de serialização", item 3): quem lê o catálogo
/// não deve deduzir o tipo pela ausência de <c>values</c>.
/// </remarks>
public static class CatalogFieldTypes
{
    /// <summary>Campo que oferece uma lista fechada de valores.</summary>
    public const string Choice = "choice";

    /// <summary>Campo de dois estados, desenhado como interruptor.</summary>
    public const string Boolean = "boolean";
}
