using System.Globalization;
using System.Text.Json;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Compara dois documentos JSON por <strong>igualdade estrutural completa</strong> e devolve a
/// primeira divergência, com o caminho JSON dela.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Por que não um "contém".</strong> Em T01 a fixture do frontend divergiu do payload real
/// em três pontos — <c>type</c> ausente, <c>when</c> escalar, <c>description</c> ausente — e
/// nenhum teste caiu, porque os leitores toleram as duas formas. Uma comparação por contenção
/// repetiria a falha: um campo a mais passaria, e um campo a menos passaria se a contenção fosse
/// no outro sentido. Aqui as duas direções são cobradas.
/// </para>
/// <para>
/// <strong>O que conta como igual:</strong> o mesmo <see cref="JsonValueKind"/> em cada nó (texto,
/// número, booleano, nulo, array e objeto são todos distintos — <c>"1"</c> não é <c>1</c>, e
/// <c>"identity"</c> não é <c>["identity"]</c>); o mesmo conjunto de chaves em cada objeto; os
/// mesmos elementos, <strong>na mesma ordem</strong>, em cada array; o mesmo texto, ordinal, em
/// cada string; o mesmo literal em cada número.
/// </para>
/// <para>
/// <strong>O que não conta:</strong> a ordem das propriedades de um objeto e a formatação. A
/// ordem de <c>fields</c> é contrato (regra 1 de "Regras de serialização"), mas é a ordem das
/// <em>chaves</em> de um objeto, que o JSON não garante e o leitor do frontend não usa para
/// isso — ela já tem teste próprio em <c>TemplateOptionsEndpointTests</c>. Arrays, sim, têm ordem
/// que importa, e ela é comparada.
/// </para>
/// <para>
/// <strong>Número por literal, não por valor.</strong> <c>1</c> e <c>1.0</c> são o mesmo número,
/// mas um payload que mudasse de um para o outro mudou de serialização, e o snapshot existe
/// exatamente para notar mudança de serialização.
/// </para>
/// </remarks>
internal static class JsonStructuralComparer
{
    /// <summary>
    /// A primeira divergência entre <paramref name="expected"/> (o snapshot) e
    /// <paramref name="actual"/> (o payload), ou <see langword="null"/> se forem iguais.
    /// </summary>
    /// <remarks>
    /// "Primeira" é em profundidade, na ordem do snapshot: para cada objeto, as chaves do snapshot
    /// primeiro e só depois as que sobram no payload; para cada array, os elementos em comum
    /// primeiro e só depois a diferença de tamanho.
    /// </remarks>
    public static JsonDivergence? FirstDivergence(JsonElement expected, JsonElement actual) =>
        Compare(expected, actual, "$");

    private static JsonDivergence? Compare(JsonElement expected, JsonElement actual, string path)
    {
        if (Kind(expected) != Kind(actual))
        {
            return new JsonDivergence(
                path,
                $"o snapshot tem {Kind(expected)} e o payload tem {Kind(actual)}",
                expected.GetRawText(),
                actual.GetRawText());
        }

        return expected.ValueKind switch
        {
            JsonValueKind.Object => CompareObjects(expected, actual, path),
            JsonValueKind.Array => CompareArrays(expected, actual, path),
            JsonValueKind.String => string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal)
                ? null
                : new JsonDivergence(path, "o texto é outro", expected.GetRawText(), actual.GetRawText()),
            JsonValueKind.Number => string.Equals(expected.GetRawText(), actual.GetRawText(), StringComparison.Ordinal)
                ? null
                : new JsonDivergence(path, "o número é outro", expected.GetRawText(), actual.GetRawText()),

            // true, false e null: o Kind igual já é o valor igual.
            _ => null,
        };
    }

    private static JsonDivergence? CompareObjects(JsonElement expected, JsonElement actual, string path)
    {
        // Chave duplicada é JSON que cada leitor resolve de um jeito; aceitar seria comparar
        // contra uma interpretação, não contra o documento.
        if (FindDuplicate(expected) is { } duplicateInSnapshot)
        {
            return new JsonDivergence(
                Child(path, duplicateInSnapshot), "a chave está duplicada no snapshot", null, null);
        }

        if (FindDuplicate(actual) is { } duplicateInPayload)
        {
            return new JsonDivergence(
                Child(path, duplicateInPayload), "a chave está duplicada no payload", null, null);
        }

        foreach (JsonProperty property in expected.EnumerateObject())
        {
            string childPath = Child(path, property.Name);

            if (!actual.TryGetProperty(property.Name, out JsonElement actualValue))
            {
                return new JsonDivergence(
                    childPath,
                    "o snapshot tem esta chave e o payload não",
                    property.Value.GetRawText(),
                    null);
            }

            if (Compare(property.Value, actualValue, childPath) is { } divergence)
            {
                return divergence;
            }
        }

        foreach (JsonProperty property in actual.EnumerateObject())
        {
            if (!expected.TryGetProperty(property.Name, out _))
            {
                return new JsonDivergence(
                    Child(path, property.Name),
                    "o payload tem esta chave e o snapshot não",
                    null,
                    property.Value.GetRawText());
            }
        }

        return null;
    }

    private static JsonDivergence? CompareArrays(JsonElement expected, JsonElement actual, string path)
    {
        int expectedLength = expected.GetArrayLength();
        int actualLength = actual.GetArrayLength();
        int common = Math.Min(expectedLength, actualLength);

        for (int index = 0; index < common; index++)
        {
            if (Compare(expected[index], actual[index], Index(path, index)) is { } divergence)
            {
                return divergence;
            }
        }

        if (expectedLength > actualLength)
        {
            return new JsonDivergence(
                Index(path, common),
                $"o snapshot tem {expectedLength} elementos e o payload tem {actualLength}",
                expected[common].GetRawText(),
                null);
        }

        if (actualLength > expectedLength)
        {
            return new JsonDivergence(
                Index(path, common),
                $"o payload tem {actualLength} elementos e o snapshot tem {expectedLength}",
                null,
                actual[common].GetRawText());
        }

        return null;
    }

    private static string? FindDuplicate(JsonElement element)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                return property.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// O nome do tipo JSON para a mensagem — e a chave da comparação de tipo. <c>true</c> e
    /// <c>false</c> são nomes distintos de propósito: assim um booleano invertido cai no mesmo
    /// ramo que um tipo trocado, sem precisar de um caso só para ele.
    /// </summary>
    private static string Kind(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "objeto",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "texto",
        JsonValueKind.Number => "número",
        JsonValueKind.True => "booleano true",
        JsonValueKind.False => "booleano false",
        JsonValueKind.Null => "null",
        _ => element.ValueKind.ToString(),
    };

    private static string Child(string path, string name) =>
        IsPlainIdentifier(name)
            ? $"{path}.{name}"
            : $"{path}[{JsonSerializer.Serialize(name)}]";

    private static string Index(string path, int index) =>
        $"{path}[{index.ToString(CultureInfo.InvariantCulture)}]";

    private static bool IsPlainIdentifier(string name) =>
        name.Length > 0 &&
        (char.IsAsciiLetter(name[0]) || name[0] == '_') &&
        name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}

/// <summary>
/// Uma divergência entre snapshot e payload: onde, o quê, e o trecho de cada lado
/// (<see langword="null"/> quando aquele lado não tem nada naquele caminho).
/// </summary>
internal sealed record JsonDivergence(string Path, string Reason, string? Snapshot, string? Payload)
{
    public override string ToString() =>
        $"Primeira divergência em {Path}: {Reason}.{Environment.NewLine}" +
        $"  snapshot: {Snapshot ?? "(ausente)"}{Environment.NewLine}" +
        $"  payload:  {Payload ?? "(ausente)"}";
}
