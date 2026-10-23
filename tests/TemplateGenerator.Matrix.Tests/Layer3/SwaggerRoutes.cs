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
///   o documento declara o esquema <em>bearer</em> se e somente se há autenticação, e o requisito
///   vai só nas operações protegidas (T08b) — ver <c>SecurityDeclaration</c>;
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
    /// As chaves de um <em>path item</em> que são operação. As outras (<c>parameters</c>,
    /// <c>summary</c>…) descrevem o caminho, e não pedem nem dispensam token.
    /// </summary>
    private static readonly string[] _httpMethods =
        ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    /// <summary>
    /// Confere as rotas de documentação de uma aplicação em execução contra
    /// <paramref name="expected"/>: <see langword="true"/> = documento e interface servidos;
    /// <see langword="false"/> = nenhuma rota de documentação.
    /// </summary>
    /// <param name="client">Cliente <strong>sem</strong> token: a documentação é pública.</param>
    /// <param name="expected">Se a combinação marcou Swagger.</param>
    /// <param name="authentication">
    /// O valor de <c>authentication</c> da combinação. Com Swagger, ele decide o que o documento
    /// declara de segurança (T08b): esquema <em>bearer</em> se e somente se for diferente de
    /// <c>none</c>, e o requisito só nas operações protegidas.
    /// </param>
    /// <param name="cancellationToken">Cancelamento do teste.</param>
    /// <param name="log">Onde registrar cada chamada, para a evidência.</param>
    public static async Task AssertAsync(
        HttpClient client,
        bool expected,
        string authentication,
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

            // T08b: o README de `swagger + auth` afirma que a interface tem o botão *Authorize*
            // "porque o documento OpenAPI desta combinação declara o esquema de segurança
            // `bearer`", e que as rotas com cadeado são as protegidas. Esta é a guarda das duas
            // frases — e, sem autenticação, a guarda de que nada de segurança vaza para o documento.
            SecurityDeclaration(json.RootElement, authentication, log);
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

    /// <summary>
    /// O que o documento declara de segurança, contra a autenticação da combinação (T08b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Sem autenticação:</strong> nem <c>components.securitySchemes</c>, nem
    /// <c>security</c> na raiz, nem <c>security</c> em operação nenhuma.
    /// </para>
    /// <para>
    /// <strong>Com autenticação:</strong> um esquema <c>http</c>/<c>bearer</c> — com
    /// <c>bearerFormat: JWT</c> no JWT e <strong>sem</strong> ele no Identity, cujo token é opaco —,
    /// nenhum requisito global na raiz, e o requisito em cada operação <strong>se e somente se</strong>
    /// ela é protegida: todas as de <c>/items</c> e, com Identity, todas as de
    /// <c>/auth/manage/...</c>. Qualquer outra — <c>/health</c>, <c>/auth/register</c>,
    /// <c>/auth/login</c>, <c>/auth/refresh</c> e as de e-mail do Identity — fica sem cadeado. A
    /// igualdade nos dois sentidos é o que pega os dois defeitos prováveis: o requisito aplicado em
    /// bloco a todas as operações, e o filtro de metadado que não enxerga o grupo de
    /// <c>MapIdentityApi</c>.
    /// </para>
    /// </remarks>
    private static void SecurityDeclaration(
        JsonElement document,
        string authentication,
        Action<string>? log)
    {
        bool authenticated = !string.Equals(authentication, "none", StringComparison.Ordinal);
        bool identity = string.Equals(authentication, "identity", StringComparison.Ordinal);

        Assert.False(
            document.TryGetProperty("security", out _),
            "O documento OpenAPI declara 'security' na raiz, o que poria cadeado em toda operação " +
            "— inclusive em '/health', que é pública em qualquer combinação (RF-12).");

        JsonElement schemes = default;
        bool declaresSchemes =
            document.TryGetProperty("components", out JsonElement components) &&
            components.TryGetProperty("securitySchemes", out schemes);

        log?.Invoke(
            $"/openapi/v1.json (authentication={authentication}) components.securitySchemes: " +
            (declaresSchemes ? schemes.GetRawText().ReplaceLineEndings(" ") : "(ausente)"));

        Assert.True(
            declaresSchemes == authenticated,
            $"Com authentication = {authentication}, o documento OpenAPI " +
            $"{(declaresSchemes ? "declara" : "não declara")} 'components.securitySchemes'. O " +
            "esquema bearer — e com ele o botão Authorize — existe se e somente se há autenticação " +
            "(T08b, critérios 1 e 3).");

        HashSet<string> schemeNames = new(StringComparer.Ordinal);

        if (declaresSchemes)
        {
            foreach (JsonProperty scheme in schemes.EnumerateObject())
            {
                schemeNames.Add(scheme.Name);

                Assert.Equal("http", scheme.Value.GetProperty("type").GetString());
                Assert.Equal(
                    "bearer",
                    scheme.Value.GetProperty("scheme").GetString(),
                    StringComparer.OrdinalIgnoreCase);

                bool hasFormat = scheme.Value.TryGetProperty("bearerFormat", out JsonElement format);

                if (identity)
                {
                    Assert.False(
                        hasFormat,
                        $"O esquema '{scheme.Name}' do Identity declara bearerFormat " +
                        $"'{(hasFormat ? format.GetString() : null)}'. O token do Identity é bearer " +
                        "próprio e opaco, não JWT (generated-projects.md, 'identity').");
                }
                else
                {
                    Assert.True(
                        hasFormat && format.GetString() == "JWT",
                        $"O esquema '{scheme.Name}' de authentication = {authentication} não " +
                        "declara bearerFormat 'JWT'.");
                }
            }

            Assert.Single(schemeNames);
        }

        int protectedOperations = 0;
        int manageOperations = 0;

        foreach (JsonProperty path in document.GetProperty("paths").EnumerateObject())
        {
            bool items =
                path.Name.Equals("/items", StringComparison.Ordinal) ||
                path.Name.StartsWith("/items/", StringComparison.Ordinal);
            bool manage = identity && path.Name.StartsWith("/auth/manage/", StringComparison.Ordinal);
            bool shouldRequire = authenticated && (items || manage);

            foreach (JsonProperty operation in path.Value.EnumerateObject()
                .Where(entry => _httpMethods.Contains(entry.Name, StringComparer.Ordinal)))
            {
                string[] required = RequiredSchemes(operation.Value);
                bool requires = required.Length > 0;

                log?.Invoke(
                    $"  {operation.Name.ToUpperInvariant()} {path.Name} security: " +
                    (requires ? string.Join(", ", required) : "(nenhum)"));

                Assert.True(
                    requires == shouldRequire,
                    $"Com authentication = {authentication}, a operação " +
                    $"{operation.Name.ToUpperInvariant()} {path.Name} " +
                    $"{(requires ? "exige" : "não exige")} segurança no documento. O requisito vai " +
                    "só nas operações protegidas: o CRUD de /items e, com Identity, " +
                    "/auth/manage/... (T08b, critério 2).");

                Assert.All(required, name => Assert.Contains(name, schemeNames));

                protectedOperations += requires ? 1 : 0;
                manageOperations += requires && manage ? 1 : 0;
            }
        }

        // Sanidade: os cinco verbos do CRUD, e ao menos uma rota de gestão da conta no Identity.
        // Sem isto, um documento sem `/items` faria o laço acima passar por vacuidade.
        Assert.True(
            !authenticated || protectedOperations >= 5,
            $"Só {protectedOperations} operação(ões) com requisito de segurança; o CRUD tem cinco.");
        Assert.True(
            !identity || manageOperations >= 1,
            "Nenhuma operação de /auth/manage/... com requisito de segurança. O MapIdentityApi " +
            "exige autorização nelas, e o documento precisa dizer isso.");
    }

    /// <summary>
    /// Os nomes de esquema que uma operação exige em <c>security</c>, ou nenhum.
    /// </summary>
    private static string[] RequiredSchemes(JsonElement operation) =>
        operation.ValueKind == JsonValueKind.Object &&
        operation.TryGetProperty("security", out JsonElement security) &&
        security.ValueKind == JsonValueKind.Array
            ?
            [
                .. security.EnumerateArray()
                    .SelectMany(requirement => requirement.EnumerateObject())
                    .Select(scheme => scheme.Name),
            ]
            : [];
}
