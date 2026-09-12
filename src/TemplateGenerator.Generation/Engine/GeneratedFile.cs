namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Um arquivo pronto para virar entrada do ZIP: caminho final e bytes finais.
/// </summary>
/// <param name="Path">Caminho dentro do pacote, já validado por <see cref="ArchivePath"/>.</param>
/// <param name="Content">Conteúdo em UTF-8 sem BOM, com <c>LF</c>.</param>
/// <param name="Fragment">
/// Fragmento de origem, ou <see cref="EngineOrigin"/> para o que o próprio motor escreve. Serve à
/// mensagem de conflito e à inspeção nos testes.
/// </param>
public sealed record GeneratedFile(string Path, byte[] Content, string Fragment)
{
    /// <summary>Origem dos arquivos que o motor escreve, e não um fragmento — o manifesto.</summary>
    public const string EngineOrigin = "(motor de geração)";
}
