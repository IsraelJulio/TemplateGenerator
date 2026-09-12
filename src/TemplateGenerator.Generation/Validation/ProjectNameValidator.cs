namespace TemplateGenerator.Generation.Validation;

/// <summary>
/// Valida <c>projectName</c> conforme docs/product/option-matrix.md.
/// </summary>
/// <remarks>
/// <para>
/// O nome vira namespace, nome de assembly, nome de diretório dentro do ZIP e nome do próprio
/// arquivo baixado. Cada uma dessas quatro coisas tem regra própria, e é a interseção delas que
/// está escrita aqui.
/// </para>
/// <para>
/// A mesma regra existe no frontend, para dar resposta imediata. Isto aqui não é a cópia: é a
/// fonte de verdade. O backend rejeita mesmo que o frontend tenha deixado passar.
/// </para>
/// </remarks>
public static class ProjectNameValidator
{
    /// <summary>Comprimento máximo do nome inteiro, em caracteres.</summary>
    public const int MaximumLength = 100;

    /// <summary>Separador de segmentos.</summary>
    public const char SegmentSeparator = '.';

    /// <summary>
    /// Devolve o nome como ele será considerado: sem espaços nas pontas.
    /// </summary>
    /// <remarks>
    /// Aparar é decisão de produto (docs/product/option-matrix.md, "Espaço em branco"): o
    /// frontend apara antes de enviar, e um servidor que não aparasse recusaria de quem chama a
    /// API direto exatamente o que aceita de quem usa a tela. É esta forma — e não a crua — que
    /// deve seguir para o motor de geração.
    /// </remarks>
    public static string Normalize(string? projectName) => projectName?.Trim() ?? string.Empty;

    /// <summary>
    /// Devolve as mensagens que impedem <paramref name="projectName"/> de ser aceito. Lista
    /// vazia significa nome válido.
    /// </summary>
    /// <remarks>
    /// O nome é normalizado antes de ser examinado, para que validar e gerar olhem para a mesma
    /// string.
    /// </remarks>
    public static IReadOnlyList<string> Validate(string? projectName)
    {
        string name = Normalize(projectName);

        if (name.Length == 0)
        {
            return ["Informe o nome do projeto."];
        }

        List<string> errors = [];

        if (name.Length > MaximumLength)
        {
            errors.Add(
                $"O nome do projeto deve ter no máximo {MaximumLength} caracteres; " +
                $"este tem {name.Length}.");
        }

        if (name.Any(char.IsControl))
        {
            errors.Add("O nome do projeto não pode conter caracteres de controle.");
        }

        if (name.Contains('/') || name.Contains('\\'))
        {
            errors.Add(@"O nome do projeto não pode conter '/' nem '\'.");
        }

        if (name.Contains("..", StringComparison.Ordinal))
        {
            errors.Add("O nome do projeto não pode conter '..'.");
        }

        // Estrutura quebrada: examinar segmento a segmento só acrescentaria ruído a um erro que
        // a pessoa já sabe como corrigir.
        if (errors.Count > 0)
        {
            return errors;
        }

        bool emptySegmentReported = false;

        foreach (string segment in name.Split(SegmentSeparator))
        {
            if (segment.Length == 0)
            {
                if (!emptySegmentReported)
                {
                    errors.Add(
                        "Cada trecho do nome separado por '.' precisa ser preenchido; " +
                        "'.' no início, no fim ou repetido não é aceito.");
                    emptySegmentReported = true;
                }

                continue;
            }

            if (!IsCSharpIdentifier(segment))
            {
                errors.Add(
                    $"O trecho '{segment}' não é um identificador C# válido. " +
                    "Use apenas letras sem acento, dígitos e '_', começando por letra ou '_'.");
            }
            else if (ReservedNames.IsCSharpKeyword(segment))
            {
                errors.Add($"O trecho '{segment}' é uma palavra reservada do C#.");
            }

            if (ReservedNames.IsWindowsDeviceName(segment))
            {
                errors.Add($"O trecho '{segment}' é um nome reservado do Windows.");
            }
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Diz se <paramref name="segment"/> é um identificador C# válido <em>e</em> ASCII.
    /// </summary>
    /// <remarks>
    /// O C# aceitaria <c>Cotação</c>; esta regra não. O corte é deliberado e está justificado em
    /// docs/product/option-matrix.md: o nome vira diretório dentro do ZIP e nome de arquivo no
    /// <c>Content-Disposition</c>, e Unicode nos dois exige a flag UTF-8 nas entradas do ZIP e
    /// codificação RFC 5987 no header — superfície de erro a mais, e mais um detalhe no
    /// determinismo do ADR-0003.
    /// </remarks>
    public static bool IsCSharpIdentifier(string segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        if (segment.Length == 0)
        {
            return false;
        }

        if (!char.IsAsciiLetter(segment[0]) && segment[0] != '_')
        {
            return false;
        }

        for (int index = 1; index < segment.Length; index++)
        {
            char current = segment[index];

            if (!char.IsAsciiLetterOrDigit(current) && current != '_')
            {
                return false;
            }
        }

        return true;
    }
}
