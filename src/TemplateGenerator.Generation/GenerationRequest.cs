using TemplateGenerator.Generation.Catalog;

namespace TemplateGenerator.Generation;

/// <summary>
/// Configuração pedida por quem gera um projeto. É a entrada do motor e o corpo de
/// <c>POST /api/templates</c> (docs/architecture/http-contract.md).
/// </summary>
/// <remarks>
/// Os valores chegam como texto de propósito: o catálogo é dado, não enum de código, e a
/// validação (T03) é quem decide se pertencem ao catálogo. Ver
/// docs/architecture/generation-engine.md, seção "Validação".
/// </remarks>
public sealed record GenerationRequest(
    string ProjectName,
    string Architecture,
    string Database,
    string Authentication,
    bool Swagger,
    string DotnetVersion)
{
    /// <summary>
    /// O valor que esta requisição traz para o campo <paramref name="field"/> do catálogo, ou
    /// <c>null</c> quando o valor não foi informado.
    /// </summary>
    /// <remarks>
    /// Mora aqui, e não em quem pergunta, porque são dois que perguntam a mesma coisa: a validação
    /// (<c>GenerationRequestValidator</c>) e a derivação de disponibilidade
    /// (<c>TemplateAvailability</c>). Duas cópias desta correspondência divergiriam no dia em que
    /// um campo novo entrasse no catálogo, e a segunda cópia continuaria compilando.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Quando o catálogo declara um campo que esta requisição não carrega. É defeito de
    /// programação, não entrada inválida, e precisa aparecer alto.
    /// </exception>
    public CatalogValue? ValueOf(string field) => field switch
    {
        CatalogFields.Architecture => AsText(Architecture),
        CatalogFields.Database => AsText(Database),
        CatalogFields.Authentication => AsText(Authentication),
        CatalogFields.Swagger => CatalogValue.OfFlag(Swagger),
        CatalogFields.DotnetVersion => AsText(DotnetVersion),
        _ => throw new InvalidOperationException(
            $"O catálogo declara o campo '{field}', mas GenerationRequest não carrega valor " +
            "para ele. Acrescente a propriedade correspondente antes de publicar o campo."),
    };

    private static CatalogValue? AsText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : CatalogValue.OfText(value);
}
