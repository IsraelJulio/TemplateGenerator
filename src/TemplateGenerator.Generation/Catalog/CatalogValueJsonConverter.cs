using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Serializa <see cref="CatalogValue"/> como escalar JSON — texto vira string, booleano vira
/// <c>true</c>/<c>false</c> —, exatamente como o exemplo de
/// docs/architecture/http-contract.md.
/// </summary>
public sealed class CatalogValueJsonConverter : JsonConverter<CatalogValue>
{
    /// <inheritdoc />
    public override CatalogValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => CatalogValue.OfText(reader.GetString()!),
            JsonTokenType.True => CatalogValue.OfFlag(true),
            JsonTokenType.False => CatalogValue.OfFlag(false),
            _ => throw new JsonException(
                $"Um valor de catálogo é texto ou booleano; veio '{reader.TokenType}'."),
        };

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        CatalogValue value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

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
