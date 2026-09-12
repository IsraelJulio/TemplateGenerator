using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Serializa uma lista de <see cref="ConstraintTerm"/> como um objeto JSON: uma propriedade por
/// campo, o valor <b>sempre</b> um vetor.
/// </summary>
/// <remarks>
/// <para>
/// Sempre vetor, mesmo com um único valor aceito
/// (docs/architecture/http-contract.md, "Regras de serialização", item 4). O frontend tolera o
/// escalar por robustez, mas o backend não deve emitir essa forma: uma regra que muda de formato
/// conforme o número de valores obriga todo cliente a lidar com dois casos, e o dia em que uma
/// restrição ganhar um segundo valor aceito o formato mudaria sem aviso.
/// </para>
/// <para>
/// A leitura continua aceitando as duas formas: desserializar é lidar com o que chega, não com
/// o que se deveria ter emitido.
/// </para>
/// </remarks>
public sealed class ConstraintTermsJsonConverter : JsonConverter<IReadOnlyList<ConstraintTerm>>
{
    /// <inheritdoc />
    public override IReadOnlyList<ConstraintTerm> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                $"'when' e 'requires' são objetos JSON; veio '{reader.TokenType}'.");
        }

        List<ConstraintTerm> terms = [];

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string field = reader.GetString()!;
            reader.Read();

            List<CatalogValue> accepted = [];

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    accepted.Add(ReadScalar(ref reader));
                }
            }
            else
            {
                accepted.Add(ReadScalar(ref reader));
            }

            terms.Add(new ConstraintTerm(field, accepted));
        }

        return terms;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<ConstraintTerm> value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        foreach (ConstraintTerm term in value)
        {
            writer.WritePropertyName(term.Field);

            writer.WriteStartArray();

            foreach (CatalogValue accepted in term.AcceptedValues)
            {
                WriteScalar(writer, accepted);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static CatalogValue ReadScalar(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.String => CatalogValue.OfText(reader.GetString()!),
        JsonTokenType.True => CatalogValue.OfFlag(true),
        JsonTokenType.False => CatalogValue.OfFlag(false),
        _ => throw new JsonException(
            $"Um valor de restrição é texto ou booleano; veio '{reader.TokenType}'."),
    };

    private static void WriteScalar(Utf8JsonWriter writer, CatalogValue value)
    {
        if (value.IsText)
        {
            writer.WriteStringValue(value.Text);
        }
        else
        {
            writer.WriteBooleanValue(value.Flag);
        }
    }
}
