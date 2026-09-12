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
/// que <c>src/__ProjectName__.Api/__ProjectName__.Api.csproj</c> vira o diretório do projeto.
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
/// </remarks>
public static partial class TemplateTokens
{
    /// <summary>Nome do projeto, como a pessoa pediu. Ex.: <c>Acme.Billing.Api</c>.</summary>
    public const string ProjectName = "__ProjectName__";

    /// <summary>TFM do projeto gerado. Ex.: <c>net10.0</c>.</summary>
    public const string TargetFramework = "__TargetFramework__";

    /// <summary>Versão do conjunto de templates. Ex.: <c>1.0.0</c>.</summary>
    public const string TemplateVersion = "__TemplateVersion__";

    /// <summary>
    /// Os valores dos marcadores para uma requisição.
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

    /// <summary>
    /// Substitui em <paramref name="text"/> todo marcador de <paramref name="values"/>.
    /// </summary>
    /// <param name="text">Conteúdo ou caminho vindo de um fragmento.</param>
    /// <param name="values">Marcadores conhecidos, de <see cref="For"/>.</param>
    /// <param name="origin">Onde o texto foi lido, para a mensagem de erro.</param>
    /// <exception cref="TemplateDefectException">
    /// Quando o texto traz um marcador que não existe.
    /// </exception>
    public static string Apply(
        string text,
        IReadOnlyDictionary<string, string> values,
        string origin)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);

        return MarkerPattern().Replace(text, match =>
        {
            if (values.TryGetValue(match.Value, out string? replacement))
            {
                return replacement;
            }

            throw new TemplateDefectException(
                $"O template '{origin}' usa o marcador '{match.Value}', que não existe. " +
                $"Marcadores disponíveis: {string.Join(", ", values.Keys.Order(StringComparer.Ordinal))}. " +
                "Ver TemplateTokens (docs/architecture/generation-engine.md).");
        });
    }

    /// <summary>
    /// O padrão de um marcador. Deliberadamente restrito a ASCII e sem sublinhado interno, para
    /// que <c>__A____B__</c> não case como um marcador só.
    /// </summary>
    [GeneratedRegex("__[A-Za-z][A-Za-z0-9]*__", RegexOptions.CultureInvariant)]
    private static partial Regex MarkerPattern();
}
