namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Um arquivo de template, como ele existe no repositório — antes de qualquer substituição de
/// token.
/// </summary>
/// <param name="Fragment">
/// O fragmento de onde o arquivo veio (<c>common</c>, <c>architecture/simple</c>, …). Serve para
/// que a mensagem de um conflito diga <em>quais</em> fragmentos colidiram, não só o caminho.
/// </param>
/// <param name="Path">
/// Caminho do arquivo relativo à raiz do fragmento, sempre com <c>/</c> como separador. É este
/// caminho — depois da substituição de tokens — que vira a entrada do ZIP.
/// </param>
/// <param name="Content">
/// Conteúdo em texto. Fragmentos são arquivos de texto UTF-8: conteúdo binário não é suportado,
/// porque toda entrada passa pela normalização exigida pelo ADR-0003 (sem BOM, <c>LF</c>,
/// terminando em nova linha), que corromperia bytes arbitrários.
/// </param>
public sealed record TemplateFile(string Fragment, string Path, string Content);
