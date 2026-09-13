using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Validation;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// O pacote inteiro resolvido na memória: quais arquivos, com quais bytes, em qual ordem — antes
/// de um único byte ser escrito no destino.
/// </summary>
/// <remarks>
/// <para>
/// O plano existe para separar <em>decidir</em> de <em>escrever</em>. RNF-03 exige que toda
/// verificação aconteça antes da primeira escrita, e é só assim que uma falha ainda pode virar
/// resposta HTTP: depois do primeiro byte, o status já foi enviado e o cliente recebe um ZIP
/// truncado com <c>200</c> em cima.
/// </para>
/// <para>
/// O custo é carregar o conteúdo do pacote na memória da requisição. É um conjunto de arquivos de
/// código-fonte, na casa de dezenas de KB; e o que ele compra — nenhum arquivo temporário, nenhum
/// diretório compartilhado entre requisições (RNF-04) — não tem substituto barato.
/// </para>
/// </remarks>
public sealed class GenerationPlan
{
    private GenerationPlan(GenerationRequest request, IReadOnlyList<GeneratedFile> files)
    {
        Request = request;
        Files = files;
    }

    /// <summary>A configuração que originou o plano.</summary>
    public GenerationRequest Request { get; }

    /// <summary>
    /// Os arquivos, ordenados por caminho com comparação <strong>ordinal</strong> — a ordem em
    /// que serão gravados (ADR-0003, item 2).
    /// </summary>
    public IReadOnlyList<GeneratedFile> Files { get; }

    /// <summary>
    /// Resolve o plano de <paramref name="request"/>.
    /// </summary>
    /// <param name="catalog">Catálogo vigente.</param>
    /// <param name="request">Configuração pedida.</param>
    /// <param name="source">Origem dos fragmentos.</param>
    /// <param name="availability">
    /// Quais valores têm template. Em produção é <see cref="TemplateAvailability.Current"/>, que
    /// <see cref="GenerationEngine"/> injeta; o teste de mecanismo passa
    /// <see cref="TemplateAvailability.Unrestricted"/>, porque o assunto dele é a composição.
    /// </param>
    /// <exception cref="GenerationRequestRejectedException">
    /// Quando a configuração não passa na validação.
    /// </exception>
    /// <exception cref="GenerationNotAvailableException">
    /// Quando a configuração é válida mas seleciona um valor sem template (ADR-0012).
    /// </exception>
    /// <exception cref="TemplateDefectException">
    /// Quando o conjunto de templates está errado: caminho inválido, marcador inexistente ou
    /// colisão de arquivo entre fragmentos.
    /// </exception>
    public static GenerationPlan Resolve(
        TemplateOptionsCatalog catalog,
        GenerationRequest request,
        ITemplateSource source,
        TemplateAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(availability);

        // Passo 1 de docs/architecture/generation-engine.md. A validação não é reimplementada
        // aqui: é a mesma que a Api chama para responder 400.
        ValidationResult validation = GenerationRequestValidator.Validate(catalog, request);

        if (!validation.IsValid)
        {
            throw new GenerationRequestRejectedException(validation);
        }

        // O passo entre a validação e a composição (ADR-0012, item 4). A ordem é a decisão, não um
        // detalhe: DEPOIS da validação inteira, porque um valor fora do catálogo também não tem
        // fragmento e responder "o template não existe" a um valor inexistente inverteria a culpa;
        // ANTES de qualquer composição, porque nada pode ter sido escrito.
        IReadOnlyList<string> unavailable = availability.UnavailableFields(request);

        if (unavailable.Count > 0)
        {
            throw new GenerationNotAvailableException(
                unavailable,
                TemplateAvailability.UnavailableReason);
        }

        IReadOnlyDictionary<string, string> tokens =
            TemplateTokens.For(request, catalog.TemplateVersion);

        // Os marcadores saem em duas etapas (ADR-0011). Primeiro as contribuições: elas varrem o
        // repositório inteiro para saber quais marcadores existem e concatenam, na ordem de
        // seleção, o texto dos fragmentos escolhidos. Só então os arquivos do pacote são compostos,
        // já com os dois conjuntos de marcadores resolvidos.
        TemplateContributions contributions =
            TemplateContributions.Resolve(source, request, tokens);

        Dictionary<string, GeneratedFile> byPath = new(StringComparer.Ordinal);

        // Passo 2 e 3: seleção por eixo e união ordenada.
        foreach (string fragment in TemplateAxes.Select(request))
        {
            foreach (TemplateFile file in source.Read(fragment))
            {
                string origin = $"{fragment}/{file.Path}";

                // Arquivo de contribuição não vira entrada do ZIP: ele já foi lido acima, e o
                // conteúdo dele mora dentro do arquivo de outro fragmento.
                if (TemplateContributions.MentionsReservedDirectory(file.Path))
                {
                    continue;
                }

                string path = TemplateTokens.ApplyToPath(file.Path, tokens, contributions, origin);

                if (!ArchivePath.IsValid(path, out string? error))
                {
                    throw new TemplateDefectException(
                        $"O template '{origin}' produz um caminho inválido. {error}");
                }

                // A normalização vem ANTES da substituição: a regra da linha do item 7 precisa
                // enxergar `LF` e uma última linha terminada, e um template salvo com CRLF por um
                // editor do Windows não pode mudar o resultado.
                string content = TemplateTokens.ApplyToContent(
                    TextContent.Normalize(file.Content),
                    tokens,
                    contributions,
                    origin);

                Add(byPath, new GeneratedFile(path, TextContent.ToBytes(content), fragment));
            }
        }

        // O manifesto entra por último para que uma colisão seja relatada como o que é: um
        // fragmento reivindicando um caminho que pertence ao motor.
        Add(byPath, new GeneratedFile(
            GenerationManifest.Path,
            TextContent.ToBytes(GenerationManifest.Render(request, catalog.TemplateVersion)),
            GeneratedFile.EngineOrigin));

        return new GenerationPlan(
            request,
            [.. byPath.Values.OrderBy(file => file.Path, StringComparer.Ordinal)]);
    }

    private static void Add(Dictionary<string, GeneratedFile> byPath, GeneratedFile file)
    {
        if (byPath.TryGetValue(file.Path, out GeneratedFile? existing))
        {
            throw new TemplatePathConflictException(file.Path, existing.Fragment, file.Fragment);
        }

        byPath[file.Path] = file;
    }
}
