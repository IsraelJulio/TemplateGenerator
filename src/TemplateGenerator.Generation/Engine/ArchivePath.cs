using System.Diagnostics.CodeAnalysis;
using TemplateGenerator.Generation.Validation;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// A regra do caminho de uma entrada do ZIP (RNF-03).
/// </summary>
/// <remarks>
/// <para>
/// O ZIP tem uma <em>raiz virtual</em>: tudo que ele carrega precisa cair dentro do diretório em
/// que a pessoa extrair o pacote. Um extrator ingênuo obedece ao caminho gravado, e é por isso
/// que <c>../../.ssh/authorized_keys</c> dentro de um ZIP é um ataque conhecido e não uma
/// curiosidade. O motor recusa antes de escrever, e não conta com o extrator para se defender.
/// </para>
/// <para>
/// A checagem é <strong>léxica</strong>, sem tocar o sistema de arquivos: nada aqui chama
/// <c>Path.GetFullPath</c> nem abre diretório. Um caminho de ZIP não é um caminho de máquina, e
/// resolver contra o disco local traria a cultura, o drive corrente e o comprimento máximo do
/// Windows para dentro de uma decisão que precisa ser a mesma em qualquer máquina (RNF-02).
/// </para>
/// <para>
/// O separador é sempre <c>/</c>: é o que a especificação do formato ZIP (APPNOTE, 4.4.17.1)
/// manda gravar, e uma entrada com <c>\</c> é interpretada como nome de arquivo em Linux e como
/// diretório no Windows — o mesmo pacote extrai diferente em cada lugar.
/// </para>
/// </remarks>
public static class ArchivePath
{
    /// <summary>Separador de segmentos dentro do pacote.</summary>
    public const char Separator = '/';

    /// <summary>
    /// Caracteres que o Windows recusa em nome de arquivo. <c>:</c> está na lista e cobre, de
    /// uma vez, o caminho com letra de drive (<c>C:/x</c>) e o <em>alternate data stream</em>
    /// (<c>arquivo.txt:oculto</c>) — que grava conteúdo invisível numa listagem comum.
    /// </summary>
    private static readonly char[] _forbiddenCharacters = ['<', '>', ':', '"', '|', '?', '*', '\\'];

    /// <summary>
    /// Verifica <paramref name="candidate"/> como caminho de entrada do pacote.
    /// </summary>
    /// <param name="candidate">Caminho já com os tokens substituídos.</param>
    /// <param name="error">
    /// Quando o caminho é recusado, a razão — em português e dizendo o que está errado.
    /// </param>
    /// <returns><c>true</c> quando o caminho é aceito.</returns>
    public static bool IsValid(string candidate, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            error = "Caminho vazio.";

            return false;
        }

        if (candidate[0] == Separator)
        {
            error = $"Caminho absoluto: '{candidate}'. Todo caminho é relativo à raiz do pacote.";

            return false;
        }

        if (candidate[^1] == Separator)
        {
            error =
                $"Caminho de diretório: '{candidate}'. O pacote carrega apenas arquivos; " +
                "os diretórios nascem do caminho deles.";

            return false;
        }

        int forbidden = candidate.IndexOfAny(_forbiddenCharacters);

        if (forbidden >= 0)
        {
            error =
                $"O caminho '{candidate}' contém o caractere '{candidate[forbidden]}', que não é " +
                "aceito em caminho de pacote (letra de drive, alternate data stream e separador " +
                "do Windows entram nesta regra).";

            return false;
        }

        foreach (char character in candidate)
        {
            if (char.IsControl(character))
            {
                error = $"O caminho '{candidate}' contém caractere de controle.";

                return false;
            }

            // Fora de ASCII exigiria ligar a flag UTF-8 nas entradas do ZIP, e é a mesma troca já
            // recusada para o nome do projeto (docs/product/option-matrix.md, "Por que ASCII").
            if (character > '\u007E')
            {
                error =
                    $"O caminho '{candidate}' tem caractere fora do ASCII imprimível " +
                    $"('{character}'). Acento e símbolo ficam no conteúdo, não no nome do arquivo.";

                return false;
            }
        }

        foreach (string segment in candidate.Split(Separator))
        {
            if (segment.Length == 0)
            {
                error = $"O caminho '{candidate}' tem segmento vazio ('//').";

                return false;
            }

            // Nenhum segmento de travessia. Com '..' e '.' fora, o caminho já é a própria forma
            // normalizada e a profundidade nunca decresce: "normalizado, continua dentro da raiz"
            // deixa de ser uma conta e vira uma propriedade do formato aceito.
            //
            // A verificação é por SEGMENTO, não por substring: 'Acme..Api.cs' contém '..' e é um
            // nome de arquivo perfeitamente legítimo; 'a/../b' não é.
            if (segment is "." or "..")
            {
                error =
                    $"O caminho '{candidate}' tem o segmento de travessia '{segment}'. " +
                    "Todo caminho de template é escrito na forma final, a partir da raiz do pacote.";

                return false;
            }

            if (segment[^1] is ' ' or '.')
            {
                error =
                    $"O segmento '{segment}' de '{candidate}' termina em espaço ou ponto. " +
                    "O Windows apaga esse final ao extrair e dois arquivos viram um.";

                return false;
            }

            if (ReservedNames.IsWindowsDeviceName(BeforeExtension(segment)))
            {
                error =
                    $"O segmento '{segment}' de '{candidate}' é um nome reservado do Windows " +
                    "e não pode ser extraído lá.";

                return false;
            }
        }

        error = null;

        return true;
    }

    /// <summary>O nome do segmento antes da primeira extensão: <c>CON.txt</c> devolve <c>CON</c>.</summary>
    /// <remarks>
    /// No Windows o nome reservado continua reservado com extensão: <c>NUL.txt</c> não é um
    /// arquivo comum.
    /// </remarks>
    private static string BeforeExtension(string segment)
    {
        int dot = segment.IndexOf('.', StringComparison.Ordinal);

        return dot < 0 ? segment : segment[..dot];
    }
}
