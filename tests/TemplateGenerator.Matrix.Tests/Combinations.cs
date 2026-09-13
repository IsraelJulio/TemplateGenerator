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

    /// <summary>
    /// As combinações da arquitetura <strong>Simples</strong>, que é a única com fragmento escrito
    /// até T04.
    /// </summary>
    /// <remarks>
    /// Existe para que um teste cujo assunto é a Simples receba <em>só</em> as combinações dela, em
    /// vez de receber as 32 e sair pela porta dos fundos com um <c>return</c> antecipado. A
    /// diferença não é de estilo: um teste escopado por <c>return</c> aparece <strong>verde</strong>
    /// para as 16 combinações de Clean que ele não olhou, e um resultado verde que não afirma nada
    /// é a forma mais barata de perder uma verificação sem ninguém notar — a lição de ADR-0008 e
    /// ADR-0010. Recortando os dados, o nome do teste só aparece para o que ele de fato examinou.
    /// </remarks>
    public static IReadOnlyList<GenerationRequest> Simple { get; } =
    [
        .. Valid.Where(request =>
            string.Equals(request.Architecture, SimpleArchitecture, StringComparison.Ordinal)),
    ];

    /// <summary>O valor de <c>architecture</c> cujo fragmento existe em T03.</summary>
    public const string SimpleArchitecture = "simple";

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
