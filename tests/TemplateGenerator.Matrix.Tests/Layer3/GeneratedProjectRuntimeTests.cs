using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Camada 3 da matriz para <c>simple/none/none</c>: compila o pacote, sobe a aplicação e
/// exercita comportamento de verdade (docs/quality/test-strategy.md, docs/playbooks/verify-mvp.md).
/// </summary>
/// <remarks>
/// <para>
/// Cada teste é um <strong>roteiro</strong> ordenado, e não uma asserção isolada, porque é isso
/// que a camada 3 é: subir, chamar, reiniciar, chamar de novo. Quebrá-lo em vários
/// <c>[Fact]</c> exigiria ou um processo por asserção — caro — ou estado compartilhado entre
/// testes cuja ordem o xUnit não garante, e o teste de volatilidade de RF-16 é justamente o que
/// apagaria o estado dos outros.
/// </para>
/// <para>
/// <strong>O que esta camada acrescenta sobre as anteriores</strong>, e que nenhuma delas
/// conseguiria afirmar:
/// </para>
/// <list type="bullet">
///   <item><description>o pacote <em>compila</em> e <em>sobe</em>;</description></item>
///   <item><description>
///   <c>GET /health</c> responde <c>200</c> e o CRUD inteiro funciona com os códigos que o README
///   promete (RF-12, RF-13);
///   </description></item>
///   <item><description>
///   os dados somem no reinício (RF-16) — a diferença entre "está escrito que é em memória" e
///   "é em memória";
///   </description></item>
///   <item><description>
///   com <c>swagger = false</c>, nem o documento nem a interface respondem. É a terceira parte de
///   RF-20, a observável, que ADR-0011 atribui nominalmente a esta camada;
///   </description></item>
///   <item><description>
///   um corpo JSON malformado responde <c>400</c>, e não <c>500</c>. <strong>É regressão</strong>:
///   com <c>app.UseExceptionHandler()</c> sem opções, o tratador engolia a
///   <c>BadHttpRequestException</c> — que já carrega o 400 — e devolvia 500. O defeito passou
///   ileso pelas camadas 1 e 2 e só apareceu na execução.
///   </description></item>
/// </list>
/// </remarks>
[Trait("Camada", "3")]
public sealed class GeneratedProjectRuntimeTests
{
    private const string ProjectName = "Camada3.Exemplo";

    [Fact]
    public async Task Com_swagger_o_projeto_compila_sobe_atende_o_CRUD_e_esquece_tudo_no_reinicio()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using GeneratedProject project = await GeneratedProject.BuildAsync(
            new GenerationRequest(ProjectName, "simple", "none", "none", true, "net10.0"),
            cancellationToken);

        await project.StartAsync(cancellationToken);

        using HttpClient client = project.Client();

        await SaudeEhPublica(client, cancellationToken);
        await CrudCompleto(client, cancellationToken);
        await CorpoInvalidoNaoViraErroDeServidor(client, cancellationToken);

        // RF-20 na parte observável: com Swagger marcado, documento e interface respondem em
        // Development.
        using (HttpResponseMessage document = await client.GetAsync("openapi/v1.json", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        }

        using (HttpResponseMessage ui = await client.GetAsync("swagger/index.html", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        }

        await OsDadosSomemNoReinicio(project, cancellationToken);
    }

    [Fact]
    public async Task Sem_swagger_o_projeto_sobe_igual_e_nenhuma_rota_de_documentacao_responde()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using GeneratedProject project = await GeneratedProject.BuildAsync(
            new GenerationRequest(ProjectName, "simple", "none", "none", false, "net10.0"),
            cancellationToken);

        await project.StartAsync(cancellationToken);

        using HttpClient client = project.Client();

        await SaudeEhPublica(client, cancellationToken);
        await CrudCompleto(client, cancellationToken);
        await CorpoInvalidoNaoViraErroDeServidor(client, cancellationToken);

        // A contraparte do teste acima e o fecho de RF-20: a opção desmarcada não deixa rota para
        // trás. As duas rotas são verificadas porque ADR-0001 traz dois pacotes — o documento é
        // nativo do .NET 10 e a interface é do Swashbuckle; conferir só uma deixaria a outra
        // passar.
        foreach (string route in (string[])["openapi/v1.json", "swagger", "swagger/index.html"])
        {
            using HttpResponseMessage response = await client.GetAsync(route, cancellationToken);

            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound,
                $"Com swagger desmarcado, '/{route}' respondeu {(int)response.StatusCode} em vez " +
                "de 404 (RF-20, ADR-0001).");
        }
    }

    [Fact]
    public async Task A_Clean_compila_sobe_e_atende_o_mesmo_CRUD_que_a_Simples()
    {
        // A Clean entrou em T04 e, até este teste, NENHUM teste automatizado compilava ou
        // executava o pacote dela: as camadas 1 e 2 leem o `.csproj`, e os critérios 2 e 4 são
        // estáticos por natureza. Faltava a única afirmação que o produto promete em
        // docs/product/vision.md — "um ZIP compilável" — e que só a execução prova.
        //
        // É UM roteiro, e não dois: o que a Clean acrescenta sobre a Simples é a divisão em
        // quatro projetos, e o que precisa ser provado é que ela COMPILA com essa divisão e que o
        // comportamento comum de generated-projects.md continua o mesmo. RF-20 e RF-16 já estão
        // exercitados nos dois roteiros da Simples, e repeti-los aqui dobraria o custo da camada
        // mais cara da suíte para reafirmar o que não muda com a arquitetura.
        //
        // Este teste também é o que prova, executando, a decisão de T04 de pôr a porta no
        // `Domain`: se `Infrastructure` não enxergasse `IItemStore`, o build falharia aqui — e
        // falharia com a saída do compilador na mensagem, não com uma asserção abstrata.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using GeneratedProject project = await GeneratedProject.BuildAsync(
            new GenerationRequest(ProjectName, "clean", "none", "none", true, "net10.0"),
            cancellationToken);

        await project.StartAsync(cancellationToken);

        using HttpClient client = project.Client();

        await SaudeEhPublica(client, cancellationToken);
        await CrudCompleto(client, cancellationToken);
        await CorpoInvalidoNaoViraErroDeServidor(client, cancellationToken);
    }

    /// <summary>RF-12: <c>GET /health</c> responde 200 e sem exigir token.</summary>
    private static async Task SaudeEhPublica(HttpClient client, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync("health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
    }

    /// <summary>RF-13: as cinco rotas, com os códigos que o README gerado promete.</summary>
    private static async Task CrudCompleto(HttpClient client, CancellationToken cancellationToken)
    {
        using (HttpResponseMessage created = await client.PostAsJsonAsync(
            "items",
            new { title = "Primeiro item" },
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("/items/1", created.Headers.Location?.ToString());
        }

        using (HttpResponseMessage list = await client.GetAsync("items", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            using JsonDocument body = JsonDocument.Parse(
                await list.Content.ReadAsStringAsync(cancellationToken));

            Assert.Equal(1, body.RootElement.GetArrayLength());
            Assert.Equal("Primeiro item", body.RootElement[0].GetProperty("title").GetString());
        }

        using (HttpResponseMessage one = await client.GetAsync("items/1", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        }

        using (HttpResponseMessage replaced = await client.PutAsJsonAsync(
            "items/1",
            new { title = "Item renomeado" },
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        }

        using (HttpResponseMessage renamed = await client.GetAsync("items/1", cancellationToken))
        {
            using JsonDocument body = JsonDocument.Parse(
                await renamed.Content.ReadAsStringAsync(cancellationToken));

            Assert.Equal("Item renomeado", body.RootElement.GetProperty("title").GetString());
        }

        using (HttpResponseMessage removed = await client.DeleteAsync("items/1", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        // O outro lado de cada rota: o que o README chama de "quando não existe".
        foreach (HttpRequestMessage request in (HttpRequestMessage[])
        [
            new(HttpMethod.Get, "items/1"),
            new(HttpMethod.Delete, "items/1"),
        ])
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (HttpResponseMessage missing = await client.PutAsJsonAsync(
            "items/999",
            new { title = "x" },
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        // `title` obrigatório, com ProblemDetails endereçado ao campo.
        using (HttpResponseMessage invalid = await client.PostAsJsonAsync(
            "items",
            new { },
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            using JsonDocument body = JsonDocument.Parse(
                await invalid.Content.ReadAsStringAsync(cancellationToken));

            Assert.True(
                body.RootElement.GetProperty("errors").TryGetProperty("title", out _),
                "O 400 de `title` ausente precisa endereçar o erro ao campo 'title'.");
        }
    }

    /// <summary>
    /// A regressão do defeito que só a execução pegou: corpo JSON malformado é erro de quem
    /// chamou, e a resposta é <c>400</c>.
    /// </summary>
    /// <remarks>
    /// Com <c>app.UseExceptionHandler()</c> sem <c>ExceptionHandlerOptions</c>, o tratador
    /// transformava a <c>BadHttpRequestException</c> em <c>500</c> — um erro do cliente
    /// contabilizado como falha do servidor, calado, e invisível para qualquer camada que não
    /// suba a aplicação. A correção é o <c>StatusCodeSelector</c> no <c>Program.cs</c> do
    /// template; este teste é o que impede que ela seja "simplificada" de volta.
    /// </remarks>
    private static async Task CorpoInvalidoNaoViraErroDeServidor(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using StringContent malformed = new("{\"title\":", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync("items", malformed, cancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Corpo JSON malformado respondeu {(int)response.StatusCode}. Tem de ser 400: o erro " +
            "é de quem chamou. Se voltou 500, o `StatusCodeSelector` do UseExceptionHandler " +
            "sumiu do Program.cs do template.");
    }

    /// <summary>RF-16: sem banco, o reinício apaga tudo.</summary>
    private static async Task OsDadosSomemNoReinicio(
        GeneratedProject project,
        CancellationToken cancellationToken)
    {
        using (HttpClient before = project.Client())
        {
            using HttpResponseMessage created = await before.PostAsJsonAsync(
                "items",
                new { title = "Sobrevive ao reinício?" },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using HttpResponseMessage list = await before.GetAsync("items", cancellationToken);

            using JsonDocument body = JsonDocument.Parse(
                await list.Content.ReadAsStringAsync(cancellationToken));

            // Sanidade: sem um item aqui, o `[]` de depois não provaria nada.
            Assert.Equal(1, body.RootElement.GetArrayLength());
        }

        await project.StopAsync();
        await project.StartAsync(cancellationToken);

        using HttpClient after = project.Client();

        using HttpResponseMessage reloaded = await after.GetAsync("items", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, reloaded.StatusCode);

        Assert.Equal(
            "[]",
            (await reloaded.Content.ReadAsStringAsync(cancellationToken)).Trim());
    }
}
