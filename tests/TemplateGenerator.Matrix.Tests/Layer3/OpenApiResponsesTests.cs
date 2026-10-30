using System.Net;
using System.Text.Json;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// T10: o documento OpenAPI servido pelo projeto gerado declara as respostas <strong>reais</strong>
/// de cada rota de <c>/items</c> e, com autenticação, o <c>401</c> — nas rotas de <c>/items</c> e,
/// com Identity, em <c>/auth/manage/...</c>.
/// </summary>
/// <remarks>
/// Sem metadado nos endpoints o gerador nativo só declara <c>200</c>, e a interface do Swagger
/// mostra <c>201</c> e <c>401</c> como "Undocumented" (T08b). O teste lê o documento em runtime e
/// compara o conjunto de códigos de cada operação com o que a rota de fato devolve.
/// </remarks>
[Trait("Camada", "3")]
public sealed class OpenApiResponsesTests
{
    [Fact]
    public async Task Sem_autenticacao_o_documento_declara_as_respostas_reais_e_nenhum_401()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using GeneratedProject project = await GeneratedProject.BuildAsync(
            new GenerationRequest("Camada3.OpenApi", "simple", "none", "none", true, "net10.0"),
            cancellationToken);

        await project.StartAsync(cancellationToken);

        using HttpClient client = project.Client();

        AssertResponses(await ReadPathsAsync(client, cancellationToken), authenticated: false, identity: false);
    }

    [Fact]
    public async Task Com_jwt_o_documento_declara_as_respostas_reais_e_o_401_em_items()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using GeneratedProject project = await GeneratedProject.BuildAsync(
            new GenerationRequest("Camada3.OpenApi", "clean", "none", "jwt", true, "net10.0"),
            cancellationToken);

        await project.StartAsync(cancellationToken);

        using HttpClient client = project.Client();

        AssertResponses(await ReadPathsAsync(client, cancellationToken), authenticated: true, identity: false);
    }

    [Fact]
    public async Task Com_identity_o_documento_declara_o_401_em_items_e_em_auth_manage()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using DatabaseRuntime runtime = await DatabaseRuntime.ExtractAsync(
            new GenerationRequest("Camada3.OpenApi", "simple", "sqlite", "identity", true, "net10.0"),
            cancellationToken);

        await runtime.RunReadmeMigrationStepsAsync(cancellationToken);
        await runtime.BuildAsync(cancellationToken);
        await runtime.StartAsync(cancellationToken);

        using HttpClient client = runtime.Client();

        AssertResponses(await ReadPathsAsync(client, cancellationToken), authenticated: true, identity: true);
    }

    private static async Task<JsonElement> ReadPathsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync("openapi/v1.json", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        return json.RootElement.GetProperty("paths").Clone();
    }

    private static void AssertResponses(JsonElement paths, bool authenticated, bool identity)
    {
        (string Path, string Method, string[] Codes)[] items =
        [
            ("/items", "get", ["200"]),
            ("/items/{id}", "get", ["200", "404"]),
            ("/items", "post", ["201", "400"]),
            ("/items/{id}", "put", ["204", "400", "404"]),
            ("/items/{id}", "delete", ["204", "404"]),
        ];

        foreach ((string path, string method, string[] codes) in items)
        {
            string[] expected = authenticated ? [.. codes, "401"] : codes;

            Assert.Equal(
                expected.Order(StringComparer.Ordinal),
                CodesOf(paths, path, method).Order(StringComparer.Ordinal));
        }

        int manage = 0;

        foreach (JsonProperty path in paths.EnumerateObject()
            .Where(entry => entry.Name.StartsWith("/auth/manage/", StringComparison.Ordinal)))
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject()
                .Where(entry => entry.Value.ValueKind == JsonValueKind.Object &&
                    entry.Value.TryGetProperty("responses", out _)))
            {
                manage++;

                Assert.True(
                    identity == CodesOf(paths, path.Name, operation.Name).Contains("401"),
                    $"{operation.Name.ToUpperInvariant()} {path.Name}: o 401 deve estar declarado " +
                    "se e somente se a combinação é identity.");
            }
        }

        Assert.True(!identity || manage >= 1, "Nenhuma operação de /auth/manage/... no documento.");
    }

    private static string[] CodesOf(JsonElement paths, string path, string method) =>
    [
        .. paths.GetProperty(path).GetProperty(method).GetProperty("responses")
            .EnumerateObject().Select(response => response.Name),
    ];
}
