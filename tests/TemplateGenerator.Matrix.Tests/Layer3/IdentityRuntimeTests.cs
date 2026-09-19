using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Camada 3 do eixo <c>auth/identity</c> (T06): extrai o pacote, executa os comandos do README
/// <strong>na ordem escrita</strong> — incluindo os <strong>dois</strong>
/// <c>dotnet ef database update</c> que os dois <c>DbContext</c> exigem —, sobe a aplicação e
/// exercita cadastro, login e renovação de verdade, contra um banco de verdade.
/// </summary>
/// <remarks>
/// <para>
/// <strong>O que esta camada acrescenta e nenhuma outra prova.</strong> As camadas 1 e 2 leem o
/// pacote e o compilam: elas afirmam que <c>MapIdentityApi</c> está no <c>Program.cs</c> e que o
/// projeto compila. Só aqui os endpoints nativos são <em>chamados</em>. A diferença é concreta —
/// um pacote em que a migração de identidade não fosse aplicada passa nas camadas 1 e 2 e falha
/// aqui, em <c>POST /auth/register</c>, porque as tabelas <c>AspNetUsers</c> não existem.
/// </para>
/// <para>
/// <strong>RF-14 e RF-15, os dois lados.</strong> <c>GET /health</c> responde <c>200</c>
/// <em>sem</em> token mesmo com autenticação ligada; todo verbo do CRUD de <c>Item</c> responde
/// <c>401</c> sem token; e o token do login — de um usuário recém-cadastrado, sem papel nenhum e
/// sem claim nenhuma atribuída — abre <strong>todo</strong> o CRUD. É essa terceira afirmação que
/// prova "independentemente de claims": a autorização é binária, e não há política que o usuário
/// nu deixe de satisfazer.
/// </para>
/// <para>
/// <strong>PostgreSQL não é pulado.</strong> A linha <c>clean + postgresql + identity</c> da tabela
/// da camada 3 roda contra um database descartável no serviço nativo
/// (<see cref="DisposablePostgres"/>); sem serviço ou sem credencial, ela falha com mensagem
/// explícita dizendo o que definir, nunca <c>Skip</c> (ADR-0005). Sem ela, as quatro combinações de
/// PostgreSQL com Identity que T06 acendeu teriam só compilação — e compilar não é executar.
/// </para>
/// </remarks>
[Trait("Camada", "3")]
public sealed class IdentityRuntimeTests
{
    private const string Email = "pessoa@exemplo.com";
    private const string Password = "Senha!123";

    /// <summary>
    /// A linha <c>simple + sqlite + identity</c> da tabela da camada 3, com Swagger
    /// <strong>ligado</strong>.
    /// </summary>
    [Fact]
    public async Task Simple_com_SQLite_e_Identity_cadastra_loga_renova_e_protege_o_CRUD()
    {
        await IdentityPontaAPonta(
            new GenerationRequest("Camada3.Identity", "simple", "sqlite", "identity", true, "net10.0"),
            swagger: true);
    }

    /// <summary>
    /// A mesma prova sobre Clean, com Swagger <strong>desligado</strong> — o par alternado que a
    /// estratégia exige para RF-20 em runtime.
    /// </summary>
    [Fact]
    public async Task Clean_com_SQLite_e_Identity_cadastra_loga_renova_e_protege_o_CRUD()
    {
        await IdentityPontaAPonta(
            new GenerationRequest("Camada3.Identity", "clean", "sqlite", "identity", false, "net10.0"),
            swagger: false);
    }

    /// <summary>
    /// A linha <c>clean + postgresql + identity</c> da tabela da camada 3, com Swagger
    /// <strong>ligado</strong> — a única das três que exercita os dois <c>DbContext</c> contra
    /// Npgsql, num database descartável.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a combinação que mais depende de a camada 3 existir. O mesmo database recebe o esquema de
    /// <c>AppDbContext</c> e o de <c>AppIdentityDbContext</c>, cada um por um <c>dotnet ef database
    /// update --context</c> próprio: se os dois comandos não forem executados, ou se o
    /// <c>--context</c> se perder pelo caminho, nada disso aparece antes de
    /// <c>POST /auth/register</c> responder erro aqui.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Clean_com_PostgreSQL_e_Identity_cadastra_loga_renova_e_protege_o_CRUD()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Database descartável no serviço nativo, derrubado no fim com os DOIS esquemas dentro.
        // Falha explícita — nunca skip — se o serviço ou a credencial não estiverem disponíveis.
        await using DisposablePostgres postgres = await DisposablePostgres.CreateAsync(cancellationToken);

        await IdentityPontaAPonta(
            new GenerationRequest("Camada3.Identity", "clean", "postgresql", "identity", true, "net10.0"),
            swagger: true,
            connectionString: postgres.ConnectionString);
    }

    private static async Task IdentityPontaAPonta(
        GenerationRequest request,
        bool swagger,
        string? connectionString = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using DatabaseRuntime runtime = await DatabaseRuntime.ExtractAsync(
            request,
            cancellationToken);

        if (connectionString is not null)
        {
            // A cadeia de conexão entra pelo ambiente, como o README ensina
            // (`ConnectionStrings__Default`), e vale para os dois `dotnet ef database update` e para
            // a aplicação. Nenhum arquivo do projeto gerado é editado — e a senha não encosta em
            // arquivo nenhum deste repositório.
            runtime.Environment["ConnectionStrings__Default"] = connectionString;
        }

        // Os comandos do README, na ordem escrita. Com Identity são DOIS `dotnet ef database
        // update` — um por `DbContext` —, e ambos com `--context`.
        await runtime.RunReadmeMigrationStepsAsync(cancellationToken);
        await runtime.BuildAsync(cancellationToken);
        await runtime.StartAsync(cancellationToken);

        using HttpClient anonimo = runtime.Client();

        // --- Verificação 4: /health é público mesmo com autenticação ligada (RF-14). ---
        using (HttpResponseMessage health = await anonimo.GetAsync("health", cancellationToken))
        {
            string body = await health.Content.ReadAsStringAsync(cancellationToken);

            Log($"GET /health (sem Authorization) -> {(int)health.StatusCode} {body}");

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Contains("\"status\":\"ok\"", body, StringComparison.Ordinal);
        }

        // --- Verificação 3: TODO verbo do CRUD responde 401 sem token (RF-14). ---
        foreach ((HttpMethod method, string route) in CrudSemToken())
        {
            using HttpRequestMessage message = new(method, route);

            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                message.Content = JsonContent.Create(new { title = "Não deveria entrar" });
            }

            using HttpResponseMessage denied = await anonimo.SendAsync(message, cancellationToken);

            Log($"{method} /{route} (sem Authorization) -> {(int)denied.StatusCode}");

            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }

        // --- Verificação 2: cadastro, login e renovação, ponta a ponta. ---
        using (HttpResponseMessage register = await anonimo.PostAsJsonAsync(
            "auth/register",
            new { email = Email, password = Password },
            cancellationToken))
        {
            Log($"POST /auth/register -> {(int)register.StatusCode} " +
                $"'{await register.Content.ReadAsStringAsync(cancellationToken)}'");

            Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        }

        string accessToken;
        string refreshToken;

        using (HttpResponseMessage login = await anonimo.PostAsJsonAsync(
            "auth/login",
            new { email = Email, password = Password },
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using JsonDocument body = JsonDocument.Parse(
                await login.Content.ReadAsStringAsync(cancellationToken));

            Assert.Equal("Bearer", body.RootElement.GetProperty("tokenType").GetString());

            accessToken = body.RootElement.GetProperty("accessToken").GetString()!;
            refreshToken = body.RootElement.GetProperty("refreshToken").GetString()!;

            Assert.False(string.IsNullOrWhiteSpace(accessToken));
            Assert.False(string.IsNullOrWhiteSpace(refreshToken));

            Log($"POST /auth/login -> {(int)login.StatusCode} tokenType=Bearer " +
                $"accessToken={Prefix(accessToken)} expiresIn=" +
                $"{body.RootElement.GetProperty("expiresIn").GetInt32()} " +
                $"refreshToken={Prefix(refreshToken)}");
        }

        string renovado;

        using (HttpResponseMessage refresh = await anonimo.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken },
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

            using JsonDocument body = JsonDocument.Parse(
                await refresh.Content.ReadAsStringAsync(cancellationToken));

            renovado = body.RootElement.GetProperty("accessToken").GetString()!;

            Assert.False(string.IsNullOrWhiteSpace(renovado));

            // A renovação precisa render um par NOVO — devolver o mesmo token seria renovação de
            // mentira.
            Assert.NotEqual(accessToken, renovado);
            Assert.NotEqual(
                refreshToken,
                body.RootElement.GetProperty("refreshToken").GetString());

            Log($"POST /auth/refresh -> {(int)refresh.StatusCode} " +
                $"accessToken={Prefix(renovado)} (diferente do anterior)");
        }

        // --- RF-15: o token renovado, de um usuário SEM papel e SEM claim, abre todo o CRUD. ---
        using HttpClient autenticado = runtime.Client();

        autenticado.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            renovado);

        using (HttpResponseMessage created = await autenticado.PostAsJsonAsync(
            "items",
            new { title = "Item autenticado" },
            cancellationToken))
        {
            Log($"POST /items (Bearer) -> {(int)created.StatusCode} " +
                $"Location={created.Headers.Location}");

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("/items/1", created.Headers.Location?.ToString());
        }

        using (HttpResponseMessage list = await autenticado.GetAsync("items", cancellationToken))
        {
            string body = await list.Content.ReadAsStringAsync(cancellationToken);

            Log($"GET /items (Bearer) -> {(int)list.StatusCode} {body}");

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            using JsonDocument json = JsonDocument.Parse(body);

            Assert.Equal(1, json.RootElement.GetArrayLength());
            Assert.Equal("Item autenticado", json.RootElement[0].GetProperty("title").GetString());
        }

        using (HttpResponseMessage one = await autenticado.GetAsync("items/1", cancellationToken))
        {
            Log($"GET /items/1 (Bearer) -> {(int)one.StatusCode}");

            Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        }

        using (HttpResponseMessage replaced = await autenticado.PutAsJsonAsync(
            "items/1",
            new { title = "Item renomeado" },
            cancellationToken))
        {
            Log($"PUT /items/1 (Bearer) -> {(int)replaced.StatusCode}");

            Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        }

        // --- RF-20 em runtime: Swagger presente ou ausente conforme a seleção. ---
        using (HttpResponseMessage openapi = await anonimo.GetAsync(
            "openapi/v1.json",
            cancellationToken))
        {
            Log($"GET /openapi/v1.json (swagger={swagger}) -> {(int)openapi.StatusCode}");

            Assert.Equal(
                swagger ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                openapi.StatusCode);
        }

        // --- Reinício: o usuário cadastrado e o item continuam no banco. ---
        await runtime.StopAsync();
        await runtime.StartAsync(cancellationToken);

        using HttpClient depois = runtime.Client();

        using (HttpResponseMessage semToken = await depois.GetAsync("items", cancellationToken))
        {
            Log($"GET /items (sem Authorization, após reinício) -> {(int)semToken.StatusCode}");

            Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
        }

        string apos;

        using (HttpResponseMessage login = await depois.PostAsJsonAsync(
            "auth/login",
            new { email = Email, password = Password },
            cancellationToken))
        {
            // O cadastro sobreviveu ao reinício: a mesma senha loga num processo novo.
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using JsonDocument body = JsonDocument.Parse(
                await login.Content.ReadAsStringAsync(cancellationToken));

            apos = body.RootElement.GetProperty("accessToken").GetString()!;

            Log($"POST /auth/login (após reinício) -> {(int)login.StatusCode} " +
                $"accessToken={Prefix(apos)}");
        }

        depois.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apos);

        using (HttpResponseMessage list = await depois.GetAsync("items", cancellationToken))
        {
            string body = await list.Content.ReadAsStringAsync(cancellationToken);

            Log($"GET /items (Bearer, após reinício) -> {(int)list.StatusCode} {body}");

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            using JsonDocument json = JsonDocument.Parse(body);

            Assert.Equal(1, json.RootElement.GetArrayLength());
            Assert.Equal("Item renomeado", json.RootElement[0].GetProperty("title").GetString());
        }
    }

    private static IEnumerable<(HttpMethod Method, string Route)> CrudSemToken() =>
    [
        (HttpMethod.Get, "items"),
        (HttpMethod.Get, "items/1"),
        (HttpMethod.Post, "items"),
        (HttpMethod.Put, "items/1"),
        (HttpMethod.Delete, "items/1"),
    ];

    private static string Prefix(string token) =>
        token.Length <= 12 ? token : token[..12] + "… (" + token.Length + " caracteres)";

    private static void Log(string line) =>
        TestContext.Current.TestOutputHelper?.WriteLine(line);
}
