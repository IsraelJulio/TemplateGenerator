using System.Text.Json;
using TemplateGenerator.Generation.Catalog;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// O manifesto de RF-22: <c>.templategenerator/manifest.json</c>, dentro do pacote.
/// </summary>
/// <remarks>
/// <para>
/// Carrega <c>templateVersion</c> e as opções escolhidas — <strong>e nada mais</strong>. A
/// tentação de acrescentar "gerado em" ou "gerado por" é forte e está descartada: data e máquina
/// são exatamente o que quebra o SHA-256 estável do ADR-0003, e o próprio manifesto viraria a
/// razão de duas gerações iguais diferirem.
/// </para>
/// <para>
/// É escrito pelo motor, não por um fragmento. Um fragmento que reivindique este caminho é
/// tratado como conflito, como qualquer outra colisão.
/// </para>
/// </remarks>
public static class GenerationManifest
{
    /// <summary>Caminho do manifesto dentro do pacote.</summary>
    public const string Path = ".templategenerator/manifest.json";

    /// <summary>
    /// Serializa o manifesto de <paramref name="request"/>.
    /// </summary>
    /// <param name="request">Configuração já validada.</param>
    /// <param name="templateVersion">Versão do catálogo vigente.</param>
    public static string Render(GenerationRequest request, string templateVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateVersion);

        using MemoryStream buffer = new();

        // A ordem das chaves é escrita à mão, e não deduzida de um dicionário: a ordem de
        // serialização é conteúdo do arquivo e, portanto, entra no hash (ADR-0003, item 5).
        // `NewLine` explícito porque o default é o da máquina — CRLF no Windows.
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteString("templateVersion", templateVersion);

            writer.WriteStartObject("options");
            writer.WriteString(CatalogFields.ProjectName, request.ProjectName);
            writer.WriteString(CatalogFields.Architecture, request.Architecture);
            writer.WriteString(CatalogFields.Database, request.Database);
            writer.WriteString(CatalogFields.Authentication, request.Authentication);
            writer.WriteBoolean(CatalogFields.Swagger, request.Swagger);
            writer.WriteString(CatalogFields.DotnetVersion, request.DotnetVersion);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return TextContent.Normalize(TextContent.Utf8WithoutBom.GetString(buffer.ToArray()));
    }
}
