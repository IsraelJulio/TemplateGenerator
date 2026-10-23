using System.Net;
using System.Text.Json;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// RF-20 na parte observável, num lugar só: as rotas de documentação respondem ou não conforme a
/// seleção — e, quando respondem, cada uma vem do pacote que ADR-0001 diz que a serve.
/// </summary>
/// <remarks>
/// <para>
/// Até T07 cada roteiro da camada 3 conferia o Swagger do seu jeito: o de <c>simple/none/none</c>
/// olhava documento e interface, os de <c>identity</c> e <c>jwt</c> só o documento, e os de banco
/// nada. Uma interface que deixasse de subir passaria por três dos quatro. Este tipo existe para que
/// "Swagger presente ou ausente conforme a seleção" (verify-mvp, passo 9) signifique a mesma coisa
/// em toda combinação da camada.
/// </para>
/// <para>
/// <strong>O que "presente" afirma</strong>, além de <c>200</c>:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <c>/openapi/v1.json</c> é um documento OpenAPI de verdade, que descreve <c>/health</c> e
///   <c>/items</c> — o gerador nativo enxergou os endpoints, e não devolveu um esqueleto vazio;
///   </description></item>
///   <item><description>
///   <c>/swagger/index.html</c> é HTML — a interface do Swashbuckle subiu;
///   </description></item>
///   <item><description>
///   <c>/swagger/v1/swagger.json</c> <strong>não</strong> responde. É a rota do gerador de documento
///   do próprio Swashbuckle; respondendo, o pacote estaria fazendo mais do que "apenas a UI", que é
///   a metade de ADR-0001 que nenhuma leitura de <c>.csproj</c> consegue ver.
///   </description></item>
/// </list>
/// <para>
/// <strong>O que "ausente" afirma:</strong> as três rotas de documentação respondem <c>404</c> —
/// documento, interface e a raiz <c>/swagger</c>. Conferir só uma deixaria a outra passar, porque
/// ADR-0001 traz dois pacotes.
/// </para>
/// </remarks>
internal static class SwaggerRoutes
{
    private static readonly string[] _documentationRoutes =
        ["openapi/v1.json", "swagger", "swagger/index.html"];

    /// <summary>
    /// Confere as rotas de documentação de uma aplicação em execução contra
    /// <paramref name="expected"/>: <see langword="true"/> = documento e interface servidos;
    /// <see langword="false"/> = nenhuma rota de documentação.
    /// </summary>
    public static async Task AssertAsync(
        HttpClient client,
        bool expected,
        CancellationToken cancellationToken,
        Action<string>? log = null)
    {
        if (!expected)
        {
            foreach (string route in _documentationRoutes)
            {
                using HttpResponseMessage response = await client.GetAsync(route, cancellationToken);

                log?.Invoke($"GET /{route} (swagger=false) -> {(int)response.StatusCode}");

                Assert.True(
                    response.StatusCode == HttpStatusCode.NotFound,
                    $"Com swagger desmarcado, '/{route}' respondeu {(int)response.StatusCode} em " +
                    "vez de 404 (RF-20, ADR-0001).");
            }

            return;
        }

        using (HttpResponseMessage document = await client.GetAsync("openapi/v1.json", cancellationToken))
        {
            log?.Invoke($"GET /openapi/v1.json (swagger=true) -> {(int)document.StatusCode}");

            Assert.Equal(HttpStatusCode.OK, document.StatusCode);

            using JsonDocument json = JsonDocument.Parse(
                await document.Content.ReadAsStringAsync(cancellationToken));

            Assert.True(
                json.RootElement.TryGetProperty("openapi", out JsonElement version) &&
                    version.GetString() is { Length: > 0 },
                "'/openapi/v1.json' respondeu 200, mas não é um documento OpenAPI: falta 'openapi'.");

            JsonElement paths = json.RootElement.GetProperty("paths");

            foreach (string path in (string[])["/health", "/items", "/items/{id}"])
            {
                Assert.True(
                    paths.TryGetProperty(path, out _),
                    $"O documento OpenAPI não descreve '{path}'. O gerador nativo não enxergou os " +
                    "endpoints da aplicação (ADR-0001).");
            }

            // O README de `swagger + auth` afirma que a interface não tem botão *Authorize*
            // "porque o documento OpenAPI desta combinação não declara esquema de segurança".
            // Esta é a guarda dessa frase: no dia em que alguém declarar o esquema bearer, o
            // teste cai e manda reescrever a subseção "Swagger e autenticação" junto.
            bool declaresSecurity =
                json.RootElement.TryGetProperty("security", out _) ||
                (json.RootElement.TryGetProperty("components", out JsonElement components) &&
                    components.TryGetProperty("securitySchemes", out _));

            Assert.False(
                declaresSecurity,
                "O documento OpenAPI passou a declarar esquema de segurança. O README de " +
                "'swagger + auth' diz o contrário (subseção 'Swagger e autenticação'): atualize-o.");
        }

        using (HttpResponseMessage ui = await client.GetAsync("swagger/index.html", cancellationToken))
        {
            log?.Invoke($"GET /swagger/index.html (swagger=true) -> {(int)ui.StatusCode}");

            Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
            Assert.Equal("text/html", ui.Content.Headers.ContentType?.MediaType);
        }

        using (HttpResponseMessage swashbuckleDocument = await client.GetAsync(
            "swagger/v1/swagger.json",
            cancellationToken))
        {
            log?.Invoke(
                $"GET /swagger/v1/swagger.json (swagger=true) -> {(int)swashbuckleDocument.StatusCode}");

            Assert.True(
                swashbuckleDocument.StatusCode == HttpStatusCode.NotFound,
                "'/swagger/v1/swagger.json' respondeu " +
                $"{(int)swashbuckleDocument.StatusCode}: o Swashbuckle está gerando documento, e " +
                "ADR-0001 o admite apenas como interface.");
        }
    }
}
