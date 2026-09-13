using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using TemplateGenerator.Generation.Validation;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// As combinações válidas da matriz, derivadas do catálogo — nunca escritas à mão —, e o corte
/// entre <strong>disponíveis</strong> e <strong>indisponíveis</strong> de ADR-0012.
/// </summary>
/// <remarks>
/// <para>
/// docs/product/option-matrix.md: 2 × 3 × 3 × 2 = 36 brutas, menos as 4 em que
/// <c>authentication = identity</c> encontra <c>database = none</c>, dão <strong>32</strong>.
/// O número aparece na definição de pronto e na estratégia de testes; aqui ele é
/// <em>calculado</em>, e um teste confere que continua 32.
/// </para>
/// <para>
/// Quem decide o que é válido é <see cref="GenerationRequestValidator"/>, o mesmo que a Api usa
/// para responder 400. Uma segunda implementação da regra de compatibilidade aqui seria uma
/// segunda oportunidade de errá-la.
/// </para>
/// <para>
/// <strong>E quem decide o que está disponível é <see cref="TemplateAvailability.Current"/></strong>,
/// o mesmo que a Api usa para responder 501 e que o motor usa para recusar. Este arquivo
/// <strong>não tem lista de combinações implementadas</strong>, e não pode ter: uma lista à mão é
/// a segunda fonte de verdade que ADR-0012 existe inteira para não criar, e ela derivaria na
/// primeira tarefa seguinte — no dia em que T05 escrever <c>database/sqlite</c>, o valor acende,
/// as combinações entram aqui sozinhas, e todo teste que depende do recorte passa a cobrá-las.
/// </para>
/// </remarks>
public static class Combinations
{
    /// <summary>Nome de projeto usado pelas combinações da matriz.</summary>
    public const string ProjectName = "Matriz.Exemplo.Api";

    /// <summary>O valor de <c>architecture</c> da arquitetura Simples.</summary>
    public const string SimpleArchitecture = "simple";

    /// <summary>
    /// O valor de <c>architecture</c> da Clean, cujo fragmento entrou em T04.
    /// </summary>
    /// <remarks>
    /// Citar o valor aqui é diferente de listar o que está implementado: o recorte de
    /// <see cref="Clean"/> nasce da interseção com <see cref="Available"/>, então ele some sozinho
    /// se a Clean apagar e cresce sozinho se ela acender. O que está proibido é a lista de
    /// disponibilidade, não o nome do eixo — o teste do diagrama de dependências precisa dizer de
    /// qual arquitetura ele fala, porque a Simples não tem diagrama a verificar.
    /// </remarks>
    public const string CleanArchitecture = "clean";

    /// <summary>Todas as combinações válidas, em ordem estável.</summary>
    public static IReadOnlyList<GenerationRequest> Valid { get; } = [.. Build()];

    /// <summary>
    /// As combinações que <strong>geram projeto</strong>: todo valor que elas selecionam tem
    /// fragmento com conteúdo (ADR-0012, R3).
    /// </summary>
    /// <remarks>
    /// É o recorte das afirmações que olham <em>dentro</em> do pacote — conteúdo obrigatório,
    /// verificação de casca, XML válido, diagrama de dependências. Nenhuma delas faz sentido para
    /// uma combinação que não produz pacote nenhum, e escrever a lista à mão faria o recorte
    /// envelhecer em silêncio.
    /// </remarks>
    public static IReadOnlyList<GenerationRequest> Available { get; } =
    [
        .. Valid.Where(request =>
            TemplateAvailability.Current.UnavailableFields(request).Count == 0),
    ];

    /// <summary>
    /// As combinações que o motor <strong>recusa</strong> e a Api responde com <c>501</c>: pelo
    /// menos um valor selecionado não tem fragmento.
    /// </summary>
    public static IReadOnlyList<GenerationRequest> Unavailable { get; } =
    [
        .. Valid.Where(request =>
            TemplateAvailability.Current.UnavailableFields(request).Count > 0),
    ];

    /// <summary>
    /// As combinações disponíveis da <strong>Clean</strong>, para o teste do diagrama de
    /// dependências (critério 4 de T04).
    /// </summary>
    public static IReadOnlyList<GenerationRequest> Clean { get; } =
    [
        .. Available.Where(request =>
            string.Equals(request.Architecture, CleanArchitecture, StringComparison.Ordinal)),
    ];

    private static IEnumerable<GenerationRequest> Build()
    {
        TemplateOptionsCatalog catalog = TemplateCatalog.Current;

        foreach (string architecture in ValuesOf(catalog, CatalogFields.Architecture))
        {
            foreach (string database in ValuesOf(catalog, CatalogFields.Database))
            {
                foreach (string authentication in ValuesOf(catalog, CatalogFields.Authentication))
                {
                    foreach (bool swagger in (bool[])[true, false])
                    {
                        GenerationRequest request = new(
                            ProjectName,
                            architecture,
                            database,
                            authentication,
                            swagger,
                            ValuesOf(catalog, CatalogFields.DotnetVersion).First());

                        if (GenerationRequestValidator.Validate(catalog, request).IsValid)
                        {
                            yield return request;
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<string> ValuesOf(TemplateOptionsCatalog catalog, string field) =>
        catalog.Fields[field].Values!.Select(option => option.Value);
}
