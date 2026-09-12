namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// De onde o motor lê os fragmentos de template.
/// </summary>
/// <remarks>
/// <para>
/// A implementação de produção é <see cref="EmbeddedTemplateSource"/>, que lê os arquivos
/// embutidos no próprio assembly. A fronteira existe para que os testes possam compor um conjunto
/// mínimo de fragmentos sem depender do template de produção — e não para permitir trocar a
/// origem em runtime: só há uma origem real.
/// </para>
/// <para>
/// A leitura é somente leitura e sem estado: a mesma instância atende requisições concorrentes
/// (RNF-04).
/// </para>
/// </remarks>
public interface ITemplateSource
{
    /// <summary>
    /// Devolve os arquivos do fragmento <paramref name="fragment"/>, ordenados por caminho com
    /// comparação ordinal. Fragmento sem arquivo algum devolve lista vazia — não é erro.
    /// </summary>
    /// <param name="fragment">
    /// Identificador do fragmento no formato de <see cref="TemplateAxes"/>: <c>common</c> ou
    /// <c>&lt;eixo&gt;/&lt;valor&gt;</c>.
    /// </param>
    IReadOnlyList<TemplateFile> Read(string fragment);
}
