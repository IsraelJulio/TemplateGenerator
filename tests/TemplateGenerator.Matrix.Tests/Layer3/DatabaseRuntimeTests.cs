using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Camada 3 dos fragmentos de banco de T05: extrai o pacote, executa os comandos do README
/// <strong>na ordem escrita</strong> — <c>dotnet restore</c>, <c>dotnet tool restore</c>,
/// <c>dotnet ef database update</c> — antes de subir a aplicação, e prova que os dados
/// <strong>sobrevivem ao reinício</strong> (docs/quality/test-strategy.md; ADR-0015, "O que
/// defende esta decisão, executável").
/// </summary>
/// <remarks>
/// <para>
/// As duas combinações são as linhas da tabela da camada 3 que T05 alcança, sem a autenticação que
/// é de T06: <c>simple/sqlite</c> (onde a tabela previa <c>simple/sqlite/identity</c>) e
/// <c>clean/postgresql</c> (onde previa <c>clean/postgresql/identity</c>). A parte de banco é o que
/// muda em T05.
/// </para>
/// <para>
/// <strong>O que esta camada acrescenta e nenhuma outra prova.</strong> As camadas 1 e 2 leem o
/// pacote e o compilam; só aqui a migração é <em>aplicada</em> pelo comando exato do README, o
/// esquema é criado num banco de verdade, e o dado escrito continua lá depois de a aplicação ser
/// morta e ressubida — a diferença entre "o README diz que persiste" e "persiste". É também o único
/// teste que exercita a decisão 2 de ADR-0015 em execução: como o projeto não migra na subida, se o
/// passo de migração não fosse executado a primeira chamada a <c>/items</c> falharia.
/// </para>
/// <para>
/// <strong>PostgreSQL não é pulado.</strong> O database é descartável no serviço nativo
/// (<see cref="DisposablePostgres"/>); sem serviço ou sem credencial, o teste falha com mensagem
/// explícita dizendo o que definir, nunca <c>Skip</c> (ADR-0005, critério 6).
/// </para>
/// </remarks>
[Trait("Camada", "3")]
public sealed class DatabaseRuntimeTests
{
    private const string ProjectName = "Camada3.Banco";

    [Fact]
    public async Task Simple_com_SQLite_migra_pelo_README_e_os_dados_sobrevivem_ao_reinicio()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using DatabaseRuntime runtime = await DatabaseRuntime.ExtractAsync(
            new GenerationRequest(ProjectName, "simple", "sqlite", "none", false, "net10.0"),
            cancellationToken);

        // Os comandos do banco do README, na ordem escrita, incluindo `dotnet tool restore` e
        // `dotnet ef database update` — antes de a aplicação subir.
        await runtime.RunReadmeMigrationStepsAsync(cancellationToken);
        await runtime.BuildAsync(cancellationToken);

        await SaudeCrudEReinicio(runtime, cancellationToken);
    }

    [Fact]
    public async Task Clean_com_PostgreSQL_migra_num_database_descartavel_e_persiste_no_reinicio()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Database descartável no serviço nativo. Falha explícita — nunca skip — se o serviço ou a
        // credencial não estiverem disponíveis (ADR-0005, critério 6 de T05).
        await using DisposablePostgres postgres = await DisposablePostgres.CreateAsync(cancellationToken);

        await using DatabaseRuntime runtime = await DatabaseRuntime.ExtractAsync(
            new GenerationRequest(
                ProjectName,
                "clean",
                "postgresql",
                "none",
                false,
                "net10.0"),
            cancellationToken);

        // A senha entra pelo ambiente, exatamente como o README ensina (ConnectionStrings__Default),
        // e vale para o `dotnet ef database update` e para a aplicação — nenhum arquivo do projeto
        // é tocado.
        runtime.Environment["ConnectionStrings__Default"] = postgres.ConnectionString;

        await runtime.RunReadmeMigrationStepsAsync(cancellationToken);
        await runtime.BuildAsync(cancellationToken);

        await SaudeCrudEReinicio(runtime, cancellationToken);
    }

    /// <summary>
    /// O roteiro comum aos dois bancos: <c>/health</c> público, cria um item, confirma que ele
    /// aparece, reinicia a aplicação e confirma que ele <strong>continua lá</strong>.
    /// </summary>
    private static async Task SaudeCrudEReinicio(
        DatabaseRuntime runtime,
        CancellationToken cancellationToken)
    {
        await runtime.StartAsync(cancellationToken);

        const string title = "Sobrevive ao reinício?";

        using (HttpClient client = runtime.Client())
        {
            using (HttpResponseMessage health = await client.GetAsync("health", cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            }

            using (HttpResponseMessage created = await client.PostAsJsonAsync(
                "items",
                new { title },
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                Assert.Equal("/items/1", created.Headers.Location?.ToString());
            }

            await AssertUnicoItem(client, title, cancellationToken);
        }

        // O reinício: processo novo, mesmo banco. É onde "está escrito que persiste" vira "persiste".
        await runtime.StopAsync();
        await runtime.StartAsync(cancellationToken);

        using (HttpClient after = runtime.Client())
        {
            await AssertUnicoItem(after, title, cancellationToken);
        }
    }

    private static async Task AssertUnicoItem(
        HttpClient client,
        string title,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage list = await client.GetAsync("items", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using JsonDocument body = JsonDocument.Parse(
            await list.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(1, body.RootElement.GetArrayLength());
        Assert.Equal(title, body.RootElement[0].GetProperty("title").GetString());
    }
}
