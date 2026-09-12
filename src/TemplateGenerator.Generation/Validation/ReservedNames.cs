using System.Collections.Frozen;

namespace TemplateGenerator.Generation.Validation;

/// <summary>
/// Os dois conjuntos de nomes que um segmento de <c>projectName</c> não pode assumir
/// (docs/product/option-matrix.md).
/// </summary>
public static class ReservedNames
{
    /// <summary>
    /// Palavras reservadas do C#. Um namespace ou assembly chamado <c>class</c> não compila.
    /// </summary>
    /// <remarks>
    /// Só as palavras <em>reservadas</em>. As contextuais (<c>record</c>, <c>var</c>,
    /// <c>value</c>…) são identificadores legítimos e não entram aqui.
    /// </remarks>
    private static readonly FrozenSet<string> _cSharpKeywords = new[]
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Nomes de dispositivo do Windows. Um diretório com esse nome não pode ser criado, então o
    /// ZIP seria impossível de extrair na máquina de quem baixou.
    /// </summary>
    private static readonly FrozenSet<string> _windowsDeviceNames = BuildWindowsDeviceNames();

    /// <summary>Diz se <paramref name="segment"/> é palavra reservada do C#.</summary>
    /// <remarks>A comparação é sensível a caixa: <c>Class</c> é identificador válido.</remarks>
    public static bool IsCSharpKeyword(string segment) => _cSharpKeywords.Contains(segment);

    /// <summary>Diz se <paramref name="segment"/> é nome reservado do Windows.</summary>
    /// <remarks>A comparação ignora caixa: o sistema de arquivos também ignora.</remarks>
    public static bool IsWindowsDeviceName(string segment) => _windowsDeviceNames.Contains(segment);

    private static FrozenSet<string> BuildWindowsDeviceNames()
    {
        List<string> names = ["CON", "PRN", "AUX", "NUL"];

        for (int index = 1; index <= 9; index++)
        {
            names.Add($"COM{index}");
            names.Add($"LPT{index}");
        }

        return names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
