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
    /// <exception cref="GenerationRequestRejectedException">
    /// Quando a configuração não passa na validação.
    /// </exception>
    /// <exception cref="TemplateDefectException">
    /// Quando o conjunto de templates está errado: caminho inválido, marcador inexistente ou
    /// colisão de arquivo entre fragmentos.
    /// </exception>
    public static GenerationPlan Resolve(
        TemplateOptionsCatalog catalog,
        GenerationRequest request,
        ITemplateSource source)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        // Passo 1 de docs/architecture/generation-engine.md. A validação não é reimplementada
        // aqui: é a mesma que a Api chama para responder 400.
        ValidationResult validation = GenerationRequestValidator.Validate(catalog, request);

        if (!validation.IsValid)
        {
            throw new GenerationRequestRejectedException(validation);
        }

        IReadOnlyDictionary<string, string> tokens =
            TemplateTokens.For(request, catalog.TemplateVersion);

        Dictionary<string, GeneratedFile> byPath = new(StringComparer.Ordinal);

        // Passo 2 e 3: seleção por eixo e união ordenada.
        foreach (string fragment in TemplateAxes.Select(request))
        {
            foreach (TemplateFile file in source.Read(fragment))
            {
                string origin = $"{fragment}/{file.Path}";
                string path = TemplateTokens.Apply(file.Path, tokens, origin);

                if (!ArchivePath.IsValid(path, out string? error))
                {
                    throw new TemplateDefectException(
                        $"O template '{origin}' produz um caminho inválido. {error}");
                }

                Add(byPath, new GeneratedFile(
                    path,
                    TextContent.ToBytes(TemplateTokens.Apply(file.Content, tokens, origin)),
                    fragment));
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
