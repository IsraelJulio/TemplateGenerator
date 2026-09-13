using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;

namespace TemplateGenerator.Api.Endpoints;

/// <summary>
/// O corpo de <c>GET /api/template-options</c>: o catálogo inteiro <strong>mais</strong> o membro
/// de topo <c>unavailable</c>, irmão de <c>constraints</c> (ADR-0012).
/// </summary>
/// <remarks>
/// <para>
/// A composição acontece aqui, e não dentro do catálogo, porque as duas coisas têm tempos
/// diferentes: o catálogo é a matriz do produto, escrita à mão e estável; a disponibilidade é
/// derivada do repositório de fragmentos e muda sozinha conforme alguém escreve template. Juntá-las
/// num tipo só faria a fonte de verdade das opções depender de onde os arquivos estão.
/// </para>
/// <para>
/// <strong>O que isto não toca:</strong> a ordem de <c>fields</c> continua sendo a ordem da tela
/// (regra 1 de serialização) e <c>type</c> continua admitindo exatamente <c>"choice"</c> e
/// <c>"boolean"</c> (regra 3). É acréscimo, e a regra 8 já autoriza extensão: um cliente que ignore
/// o membro novo se comporta exatamente como antes.
/// </para>
/// <para>
/// A ordem das propriedades aqui é a ordem das chaves no JSON, e <c>unavailable</c> vem por último,
/// depois de <c>constraints</c>. Um teste afirma que o conjunto de chaves de topo é o do catálogo
/// mais <c>unavailable</c> — é a trava contra alguém acrescentar um membro ao catálogo e esta
/// resposta engoli-lo em silêncio.
/// </para>
/// </remarks>
public sealed class TemplateOptionsResponse
{
    /// <summary>Versão do conjunto de templates (RF-22).</summary>
    public required string TemplateVersion { get; init; }

    /// <summary>Campos configuráveis, na ordem de exibição. Inalterado.</summary>
    public required IReadOnlyDictionary<string, TemplateField> Fields { get; init; }

    /// <summary>Regras de compatibilidade, como dado. Inalterado.</summary>
    public required IReadOnlyList<TemplateConstraint> Constraints { get; init; }

    /// <summary>
    /// Os pares <c>(campo, valor)</c> que não geram projeto, na ordem do catálogo. Vazio significa
    /// "está tudo implementado".
    /// </summary>
    public required IReadOnlyList<UnavailableOption> Unavailable { get; init; }

    /// <summary>Compõe a resposta a partir do catálogo e da disponibilidade derivada.</summary>
    public static TemplateOptionsResponse From(
        TemplateOptionsCatalog catalog,
        TemplateAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(availability);

        return new TemplateOptionsResponse
        {
            TemplateVersion = catalog.TemplateVersion,
            Fields = catalog.Fields,
            Constraints = catalog.Constraints,
            Unavailable = availability.Unavailable,
        };
    }
}
