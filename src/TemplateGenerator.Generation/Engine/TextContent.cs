using System.Text;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// A forma canônica do conteúdo de todo arquivo gerado (ADR-0003, item 4).
/// </summary>
/// <remarks>
/// <para>
/// UTF-8 <strong>sem BOM</strong>, quebras de linha <c>LF</c>, arquivo terminando em nova linha.
/// A normalização acontece no motor, e não na disciplina de quem escreve template: um arquivo
/// salvo com CRLF por um editor do Windows mudaria o SHA-256 do pacote sem mudar uma linha de
/// conteúdo, e o defeito apareceria como "o determinismo quebrou" muito longe da causa.
/// </para>
/// <para>
/// A consequência é que fragmentos são <strong>texto</strong>. Um arquivo binário passaria por
/// aqui corrompido — e nenhum item do conteúdo obrigatório de um ZIP
/// (docs/architecture/generated-projects.md) é binário.
/// </para>
/// </remarks>
public static class TextContent
{
    /// <summary>UTF-8 sem BOM, que é a única codificação de saída.</summary>
    public static readonly Encoding Utf8WithoutBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// U+FEFF, o BOM. Escrito como código, e não como literal, porque um caractere invisível no
    /// meio do código-fonte é impossível de revisar em diff.
    /// </summary>
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// Devolve <paramref name="text"/> com <c>LF</c>, sem BOM e terminando em nova linha.
    /// </summary>
    /// <remarks>
    /// Texto vazio continua vazio: um arquivo deliberadamente sem conteúdo não tem linha para
    /// terminar, e acrescentar uma inventaria conteúdo que ninguém escreveu.
    /// </remarks>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Só CRLF e CR viram LF. `ReplaceLineEndings` faria mais do que isso: ele também troca
        // U+0085, U+2028 e U+2029, que dentro de uma string literal em C# são conteúdo, não
        // quebra de linha.
        string normalized = text
            .TrimStart(ByteOrderMark)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        if (normalized.Length == 0 || normalized[^1] == '\n')
        {
            return normalized;
        }

        return normalized + "\n";
    }

    /// <summary>Converte para os bytes que vão para o pacote.</summary>
    public static byte[] ToBytes(string text) => Utf8WithoutBom.GetBytes(Normalize(text));
}
