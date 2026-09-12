using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Validation;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// As combinações válidas da matriz, derivadas do catálogo — nunca escritas à mão.
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
/// </remarks>
public static class Combinations
{
    /// <summary>Nome de projeto usado pelas combinações da matriz.</summary>
    public const string ProjectName = "Matriz.Exemplo.Api";

    /// <summary>Todas as combinações válidas, em ordem estável.</summary>
    public static IReadOnlyList<GenerationRequest> Valid { get; } = [.. Build()];

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
