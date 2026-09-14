using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Os marcadores que um fragmento pode usar e a substituição deles.
/// </summary>
/// <remarks>
/// <para>
/// A sintaxe é <c>__Nome__</c>: dois sublinhados, um identificador começando por letra, dois
/// sublinhados. Vale tanto no conteúdo quanto no <strong>caminho</strong> do arquivo — é assim
/// que <c>src/__ProjectName__/__ProjectName__.csproj</c> vira o diretório do projeto.
/// </para>
/// <para>
/// Marcador explícito, nunca interpolação de string em C#
/// (docs/architecture/generation-engine.md): o template precisa ser um arquivo legível e
/// revisável por diff, e um <c>$"..."</c> em C# devolveria o template para dentro do código.
/// </para>
/// <para>
/// <strong>Marcador desconhecido é erro</strong>, não texto que passa adiante. Um
/// <c>__ProjectNme__</c> digitado errado sobreviveria em silêncio até o <c>.csproj</c> gerado, e
/// o defeito apareceria como falha de compilação no projeto de outra pessoa, longe da causa.
/// </para>
/// <para>
/// Há dois conjuntos disjuntos de marcadores: os de <strong>valor</strong>, declarados aqui, que
/// vêm da requisição; e os de <strong>contribuição</strong>, declarados pelos arquivos de
/// <c>__parts__/</c> (<see cref="TemplateContributions"/>, ADR-0011). A diferença de tratamento
/// está na regra da linha: no arquivo hospedeiro, contribuição sozinha na linha consome a linha
/// inteira, e valor não.
/// </para>
/// <para>
/// Dentro de uma contribuição a diferença some para quem escreve template: um marcador de
/// contribuição de eixo estritamente anterior já está fechado e se comporta como marcador de valor
/// (ADR-0015, decisão 1). A regra da linha não vale ali — é <see cref="ApplyValues"/>, e não
/// <see cref="ApplyToContent"/>, quem resolve aquele texto.
/// </para>
/// </remarks>
public static partial class TemplateTokens
{
    /// <summary>Delimitador de marcador, dos dois lados do nome.</summary>
    public const string Delimiter = "__";

    /// <summary>Nome do projeto, como a pessoa pediu. Ex.: <c>Acme.Billing</c>.</summary>
    public const string ProjectName = "__ProjectName__";

    /// <summary>TFM do projeto gerado. Ex.: <c>net10.0</c>.</summary>
    public const string TargetFramework = "__TargetFramework__";

    /// <summary>Versão do conjunto de templates. Ex.: <c>1.0.0</c>.</summary>
    public const string TemplateVersion = "__TemplateVersion__";

    /// <summary>
    /// Os valores dos marcadores de valor para uma requisição.
    /// </summary>
    /// <param name="request">Configuração já validada.</param>
    /// <param name="templateVersion">Versão do catálogo vigente.</param>
    public static FrozenDictionary<string, string> For(GenerationRequest request, string templateVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateVersion);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProjectName] = request.ProjectName,
            [TargetFramework] = request.DotnetVersion,
            [TemplateVersion] = templateVersion,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Diz se <paramref name="name"/> tem a forma de um nome de marcador.</summary>
    public static bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) && NamePattern().IsMatch(name);

    /// <summary>
    /// Substitui, dentro de uma <strong>contribuição</strong>, os marcadores de valor e os
    /// marcadores de contribuição que ela pode ler para trás — recusando qualquer outro.
    /// </summary>
    /// <param name="text">Conteúdo de um arquivo de contribuição.</param>
    /// <param name="values">Marcadores de valor.</param>
    /// <param name="scope">
    /// O que este eixo pode ler: os marcadores já fechados, com o valor acumulado, e o que é
    /// preciso para recusar os demais nomeando os dois eixos (ADR-0015, decisão 1).
    /// </param>
    /// <param name="origin">Onde o texto foi lido, para a mensagem de erro.</param>
    /// <remarks>
    /// Uma passada só, como <see cref="ApplyToContent"/>: o que é inserido não volta a ser
    /// examinado. Aqui isso é mais do que economia — é o que faz a leitura para trás parar sem
    /// nenhuma regra de recursão. A ordem total dos eixos já garantiu que o valor inserido está
    /// fechado.
    /// </remarks>
    public static string ApplyValues(
        string text,
        IReadOnlyDictionary<string, string> values,
        TemplateContributions.Scope scope,
        string origin)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(scope);

        return MarkerPattern().Replace(text, match =>
        {
            if (values.TryGetValue(match.Value, out string? replacement))
            {
                return replacement;
            }

            // Marcador de contribuição de eixo estritamente anterior: já fechado, e portanto
            // indistinguível de um marcador de valor para quem está aqui.
            if (scope.TryRead(match.Value, out string? contributed))
            {
                return contributed;
            }

            // Conhecido, mas não para trás: o próprio eixo ou um posterior.
            if (scope.Declares(match.Value))
            {
                throw scope.NotBackwards(match.Value, origin);
            }

            throw Unknown(match.Value, origin, values, scope.Markers);
        });
    }

    /// <summary>
    /// Substitui marcadores em um <strong>caminho</strong> de arquivo: sempre literal, nunca a
    /// regra da linha — um caminho não tem linhas (ADR-0011, item 8).
    /// </summary>
    public static string ApplyToPath(
        string path,
        IReadOnlyDictionary<string, string> values,
        TemplateContributions contributions,
        string origin)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(contributions);

        return MarkerPattern().Replace(path, match => Resolve(match.Value, values, contributions, origin));
    }

    /// <summary>
    /// Substitui marcadores no <strong>conteúdo</strong> de um arquivo de template.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uma passada só sobre o texto original. Nada do que é <em>inserido</em> volta a ser
    /// examinado, e isso não é economia: um <c>projectName</c> legítimo como
    /// <c>Acme.__Interno__.Api</c> viraria "marcador desconhecido" numa segunda passada.
    /// </para>
    /// <para>
    /// A regra da linha (ADR-0011, item 7) vale só para marcador de contribuição. Um marcador de
    /// valor sozinho na linha é substituído como qualquer outro, e a linha continua existindo —
    /// consumir a quebra ali grudaria duas linhas por um motivo que o template não pediu.
    /// </para>
    /// </remarks>
    public static string ApplyToContent(
        string content,
        IReadOnlyDictionary<string, string> values,
        TemplateContributions contributions,
        string origin)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(contributions);

        return ContentPattern().Replace(content, match =>
        {
            Group alone = match.Groups["alone"];

            if (!alone.Success)
            {
                return Resolve(match.Value, values, contributions, origin);
            }

            string marker = $"{Delimiter}{alone.Value}{Delimiter}";
            string indent = match.Groups["indent"].Value;

            if (!contributions.Declares(marker))
            {
                // Marcador de valor (ou erro) sozinho na linha: a linha é preservada inteira,
                // inclusive o espaço à direita, e só o marcador é trocado.
                return indent
                    + Resolve(marker, values, contributions, origin)
                    + match.Groups["trailing"].Value
                    + "\n";
            }

            if (indent.Length > 0)
            {
                throw new TemplateDefectException(
                    $"O template '{origin}' tem o marcador de contribuição '{marker}' sozinho " +
                    $"numa linha indentada ({indent.Length} caractere(s) de recuo). Ou ele começa " +
                    "na coluna 0, e aí cada contribuição traz a própria indentação, ou ele fica no " +
                    "meio de uma linha. Indentar o marcador produziria recuo quebrado em silêncio " +
                    "(ADR-0011, item 7).");
            }

            // Coluna 0 e sozinho: a linha inteira sai, com a quebra. Zero contribuições ⇒ a linha
            // some, sem ItemGroup vazio e sem linha em branco órfã.
            return contributions.Block(marker);
        });
    }

    private static string Resolve(
        string marker,
        IReadOnlyDictionary<string, string> values,
        TemplateContributions contributions,
        string origin)
    {
        if (values.TryGetValue(marker, out string? replacement))
        {
            return replacement;
        }

        if (contributions.Declares(marker))
        {
            return contributions.Inline(marker);
        }

        throw Unknown(marker, origin, values, contributions.Markers);
    }

    private static TemplateDefectException Unknown(
        string marker,
        string origin,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyCollection<string> contributionMarkers)
    {
        string known = string.Join(", ", values.Keys.Order(StringComparer.Ordinal));

        string contributed = contributionMarkers.Count == 0
            ? "nenhum fragmento declara contribuição"
            : string.Join(", ", contributionMarkers.Order(StringComparer.Ordinal));

        return new TemplateDefectException(
            $"O template '{origin}' usa o marcador '{marker}', que não existe. " +
            $"Marcadores de valor: {known}. " +
            $"Marcadores de contribuição declarados em '{TemplateContributions.Directory}/': " +
            $"{contributed}. Ver docs/architecture/generation-engine.md.");
    }

    /// <summary>
    /// O padrão de um marcador. Deliberadamente restrito a ASCII e sem sublinhado interno, para
    /// que <c>__A____B__</c> não case como um marcador só.
    /// </summary>
    [GeneratedRegex("__[A-Za-z][A-Za-z0-9]*__", RegexOptions.CultureInvariant)]
    private static partial Regex MarkerPattern();

    /// <summary>O nome de um marcador, sem os delimitadores.</summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>
    /// O padrão do conteúdo: primeiro a forma "sozinho na linha", depois a forma solta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A alternância é o que permite decidir as duas em uma passada só. A primeira alternativa
    /// casa a linha inteira — recuo, marcador, espaço à direita e a quebra — e é ela que o item 7
    /// governa; a segunda casa o marcador onde quer que ele esteja.
    /// </para>
    /// <para>
    /// <c>[^\S\n]</c> é "espaço em branco que não é quebra de linha": sem isso, <c>\s*</c>
    /// engoliria linhas em branco inteiras antes do marcador. O conteúdo chega normalizado em
    /// <c>LF</c> e terminando em nova linha (<see cref="TextContent"/>), então a primeira
    /// alternativa pode exigir <c>\n</c> sem perder o marcador da última linha.
    /// </para>
    /// </remarks>
    [GeneratedRegex(
        @"^(?<indent>[^\S\n]*)__(?<alone>[A-Za-z][A-Za-z0-9]*)__(?<trailing>[^\S\n]*)\n|__[A-Za-z][A-Za-z0-9]*__",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ContentPattern();
}
