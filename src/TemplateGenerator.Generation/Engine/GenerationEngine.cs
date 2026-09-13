using TemplateGenerator.Generation.Catalog;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// O motor de geração: requisição → validação → seleção → composição → empacotamento → stream.
/// </summary>
/// <remarks>
/// <para>
/// Nenhuma dessas etapas executa processo externo. <strong>Não há <c>dotnet restore</c>,
/// <c>dotnet build</c> nem <c>dotnet new</c></strong> (RNF-01) — o motor lê recursos embutidos,
/// substitui marcadores e escreve entradas de ZIP, e mais nada. O teste
/// <c>NoExternalProcessTests</c> inspeciona os metadados do assembly para provar que nem sequer
/// existe referência a <c>System.Diagnostics.Process</c>.
/// </para>
/// <para>
/// Sem estado entre requisições: o catálogo e a origem de templates são imutáveis e
/// compartilhados, e tudo que pertence a uma geração vive no <see cref="GenerationPlan"/> daquela
/// chamada (RNF-04).
/// </para>
/// </remarks>
public sealed class GenerationEngine : IGenerationEngine
{
    private readonly TemplateOptionsCatalog _catalog;
    private readonly ITemplateSource _source;
    private readonly TemplateAvailability _availability;

    /// <summary>Cria o motor sobre o catálogo vigente e os templates embutidos.</summary>
    public GenerationEngine()
        : this(TemplateCatalog.Current, EmbeddedTemplateSource.Default, TemplateAvailability.Current)
    {
    }

    /// <summary>Cria o motor sobre um catálogo e uma origem de fragmentos.</summary>
    /// <remarks>
    /// A origem entra por construtor para que os testes componham um conjunto mínimo de
    /// fragmentos. Em produção há uma única origem — <see cref="EmbeddedTemplateSource.Default"/>.
    /// A disponibilidade é <strong>derivada dessa mesma origem</strong>: um motor montado sobre um
    /// repositório de teste recusa exatamente o que aquele repositório não tem.
    /// </remarks>
    public GenerationEngine(TemplateOptionsCatalog catalog, ITemplateSource source)
        : this(catalog, source, new TemplateAvailability(catalog, source))
    {
    }

    /// <summary>
    /// Cria o motor sobre um catálogo, uma origem e uma disponibilidade já derivada.
    /// </summary>
    /// <remarks>
    /// A disponibilidade é calculada uma vez e compartilhada (ADR-0012): ela varre o repositório
    /// inteiro, e o resultado é imutável porque os templates são recursos embutidos.
    /// </remarks>
    public GenerationEngine(
        TemplateOptionsCatalog catalog,
        ITemplateSource source,
        TemplateAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(availability);

        _catalog = catalog;
        _source = source;
        _availability = availability;
    }

    /// <summary>
    /// Resolve o pacote de <paramref name="request"/> sem escrever nada.
    /// </summary>
    /// <remarks>
    /// Exposto porque a camada 1 da matriz inspeciona o conteúdo gerado das 32 combinações e não
    /// precisa, para isso, de um ZIP — e porque separa explicitamente "decidir" de "escrever".
    /// </remarks>
    public GenerationPlan Plan(GenerationRequest request) =>
        GenerationPlan.Resolve(_catalog, request, _source, _availability);

    /// <inheritdoc />
    public async Task WriteArchiveAsync(
        GenerationRequest request,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        // A ordem importa: o plano inteiro é resolvido — e portanto toda a validação de RNF-03
        // acontece — antes de o primeiro byte tocar o destino. Se algo estiver errado, a exceção
        // sai com a resposta HTTP ainda intacta.
        GenerationPlan plan = Plan(request);

        await DeterministicZip.WriteAsync(plan, destination, cancellationToken).ConfigureAwait(false);
    }
}
