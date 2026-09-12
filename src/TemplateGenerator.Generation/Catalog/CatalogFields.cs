namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// As chaves dos campos do catálogo, escritas uma única vez.
/// </summary>
/// <remarks>
/// Elas aparecem em três lugares que precisam concordar: o JSON do catálogo, o corpo de
/// <c>POST /api/templates</c> e as propriedades de <see cref="GenerationRequest"/>. A constante
/// existe para que uma divergência vire erro de compilação, não erro de contrato em produção.
/// </remarks>
public static class CatalogFields
{
    /// <summary>Nome do projeto. Não é campo de escolha: é texto livre validado.</summary>
    public const string ProjectName = "projectName";

    /// <summary>Arquitetura do projeto gerado.</summary>
    public const string Architecture = "architecture";

    /// <summary>Banco de dados.</summary>
    public const string Database = "database";

    /// <summary>Autenticação.</summary>
    public const string Authentication = "authentication";

    /// <summary>Swagger ligado ou desligado.</summary>
    public const string Swagger = "swagger";

    /// <summary>Versão do .NET (TFM).</summary>
    public const string DotnetVersion = "dotnetVersion";
}
