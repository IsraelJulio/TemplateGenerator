using System.Collections.Frozen;
using TemplateGenerator.Generation.Catalog;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// O mapa entre um campo do catálogo e o diretório de fragmentos correspondente, e a seleção
/// ordenada de fragmentos de uma requisição.
/// </summary>
/// <remarks>
/// <para>
/// O layout está em docs/architecture/generation-engine.md, seção "Composição":
/// </para>
/// <code>
/// Templates/
/// ├─ common/                  o que existe em toda combinação
/// ├─ architecture/simple/     architecture/clean/
/// ├─ database/none/           database/sqlite/     database/postgresql/
/// ├─ auth/none/               auth/identity/       auth/jwt/
/// └─ swagger/enabled/         (só quando swagger = true)
/// </code>
/// <para>
/// Os <em>valores</em> vêm do catálogo, não de uma lista escrita aqui: publicar
/// <c>database/mysql</c> no catálogo passa a exigir o diretório correspondente sem tocar neste
/// arquivo. O que está escrito aqui é só a correspondência entre nome de campo e nome de
/// diretório — que não é dedutível, porque o campo se chama <c>authentication</c> e o diretório
/// se chama <c>auth</c>.
/// </para>
/// </remarks>
public static class TemplateAxes
{
    /// <summary>Fragmento presente em toda combinação.</summary>
    public const string Common = "common";

    /// <summary>Diretório do eixo booleano de Swagger quando ele está ligado.</summary>
    public const string SwaggerEnabled = "swagger/enabled";

    /// <summary>Separador de fragmento, dentro do identificador e no caminho do recurso.</summary>
    public const char Separator = '/';

    /// <summary>
    /// Campo do catálogo → diretório do eixo. Um campo ausente deste mapa (o caso de
    /// <c>dotnetVersion</c>) não tem eixo de fragmento: ele entra no pacote por token, não por
    /// seleção de arquivos.
    /// </summary>
    private static readonly FrozenDictionary<string, string> _directoryByField =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CatalogFields.Architecture] = "architecture",
            [CatalogFields.Database] = "database",
            [CatalogFields.Authentication] = "auth",
            [CatalogFields.Swagger] = "swagger",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> _all =
        BuildAll(TemplateCatalog.Current).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Todo fragmento que o catálogo vigente torna possível, ordenado. É a lista que o
    /// repositório precisa ter como diretório.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [.. _all.Order(StringComparer.Ordinal)];

    /// <summary>
    /// Os fragmentos aplicáveis a <paramref name="request"/>, na ordem da composição.
    /// </summary>
    /// <remarks>
    /// A ordem é declarada e estável, mas <strong>não</strong> é precedência: dois fragmentos que
    /// escrevam o mesmo caminho são um defeito de template, e o motor recusa a geração em vez de
    /// deixar um vencer (docs/architecture/generation-engine.md).
    /// </remarks>
    public static IReadOnlyList<string> Select(GenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> fragments =
        [
            Common,
            Fragment(CatalogFields.Architecture, request.Architecture),
            Fragment(CatalogFields.Database, request.Database),
            Fragment(CatalogFields.Authentication, request.Authentication),
        ];

        // O eixo booleano tem um diretório só: `swagger = false` é a ausência de fragmento, e não
        // um `swagger/disabled` vazio que existiria só por simetria (RF-20).
        if (request.Swagger)
        {
            fragments.Add(SwaggerEnabled);
        }

        return fragments;
    }

    /// <summary>
    /// Diz a qual fragmento pertence <paramref name="relativePath"/> — um caminho relativo a
    /// <c>Templates/</c>, com <c>/</c> como separador —, ou <c>null</c> quando ele não está sob
    /// nenhum fragmento declarado pelo catálogo.
    /// </summary>
    public static string? FragmentOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        // Fragmento é `common` (um segmento) ou `<eixo>/<valor>` (dois). Testar os dois prefixos
        // possíveis é mais direto — e mais difícil de errar — do que adivinhar pela contagem.
        foreach (int segments in (int[])[2, 1])
        {
            string? candidate = Prefix(relativePath, segments);

            if (candidate is not null && _all.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Monta o identificador do fragmento de um campo com um valor.</summary>
    private static string Fragment(string field, string value) =>
        $"{_directoryByField[field]}{Separator}{value}";

    /// <summary>
    /// Os <paramref name="segments"/> primeiros segmentos de <paramref name="path"/>, ou
    /// <c>null</c> quando o caminho não tem segmentos suficientes <em>e mais um</em> — um
    /// fragmento sem arquivo dentro dele não é fragmento.
    /// </summary>
    private static string? Prefix(string path, int segments)
    {
        int index = -1;

        for (int found = 0; found < segments; found++)
        {
            index = path.IndexOf(Separator, index + 1);

            if (index < 0)
            {
                return null;
            }
        }

        return path[..index];
    }

    private static IEnumerable<string> BuildAll(TemplateOptionsCatalog catalog)
    {
        yield return Common;

        foreach (KeyValuePair<string, TemplateField> entry in catalog.Fields)
        {
            if (!_directoryByField.TryGetValue(entry.Key, out string? directory))
            {
                continue;
            }

            if (entry.Value.IsToggle)
            {
                yield return $"{directory}{Separator}enabled";

                continue;
            }

            foreach (TemplateOptionValue option in entry.Value.Values!)
            {
                yield return $"{directory}{Separator}{option.Value}";
            }
        }
    }
}
