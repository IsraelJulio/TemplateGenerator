using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// A recusa de uma combinação <strong>sem template</strong>: <c>501</c> em
/// <c>application/problem+json</c>, com o campo em <c>errors</c> e <strong>nenhum cabeçalho de
/// download</strong> (ADR-0012, itens 3 e 4).
/// </summary>
/// <remarks>
/// <para>
/// A disponibilidade destes testes é <strong>montada aqui</strong>, sobre um repositório de
/// fragmentos controlado, e não lida de <c>Templates/</c>. O motivo é que o estado do repositório é
/// transitório por natureza: cada fragmento escrito acende um valor, até que em T08 não haja mais
/// nenhum indisponível. Um teste amarrado ao estado de hoje afirmaria "a Clean responde 501" e
/// falharia no dia em que a Clean passasse a funcionar — que é sucesso, não regressão. O que
/// <em>não</em> é transitório é a forma da resposta, e é ela que está verificada aqui.
/// </para>
/// <para>
/// Que a combinação indisponível <em>de produção</em> responda <c>501</c> é afirmação da camada 1
/// da matriz, sobre a matriz inteira (ADR-0012, item 5, linha 4).
/// </para>
/// </remarks>
public sealed class GenerationNotImplementedEndpointTests
{
    private static readonly Uri _route = new("/api/templates", UriKind.Relative);
    private static readonly Uri _catalogRoute = new("/api/template-options", UriKind.Relative);

    /// <summary>
    /// Um repositório em que só os fragmentos nomeados existem, cada um com um arquivo.
    /// </summary>
    private sealed class StubTemplateSource(params string[] fragments) : ITemplateSource
    {
        public IReadOnlyList<TemplateFile> Read(string fragment) =>
            fragments.Contains(fragment, StringComparer.Ordinal)
                ? [new TemplateFile(fragment, "arquivo.txt", "conteúdo\n")]
                : [];
    }

    /// <summary>
    /// A API com um repositório pela metade: a arquitetura Simples e o Swagger têm template;
    /// nenhum banco e nenhuma autenticação têm.
    /// </summary>
    private sealed class PartialTemplatesFactory : GeneratorApiFactory
    {
        protected override void Configure(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Registrado depois do `Program.cs`, e por isso vence. É a MESMA derivação de
            // produção, sobre outra origem de fragmentos — não um dublê da regra.
            services.AddSingleton(new TemplateAvailability(
                TemplateCatalog.Current,
                new StubTemplateSource(
                    TemplateAxes.Common,
                    "architecture/simple",
                    TemplateAxes.SwaggerEnabled)));
        }
    }

    /// <summary>Válida, dentro do catálogo, satisfaz as restrições — e sem template.</summary>
    private static Dictionary<string, object?> Configuration() =>
        new(StringComparer.Ordinal)
        {
            [CatalogFields.ProjectName] = "Acme.Billing.Api",
            [CatalogFields.Architecture] = "simple",
            [CatalogFields.Database] = "none",
            [CatalogFields.Authentication] = "none",
            [CatalogFields.Swagger] = true,
            [CatalogFields.DotnetVersion] = "net10.0",
        };

    [Fact]
    public async Task Combinacao_sem_template_responde_501_em_problem_json()
    {
        using PartialTemplatesFactory factory = new();

        using HttpResponseMessage response = await PostAsync(factory, Configuration());

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument problem = await ReadJsonAsync(response);

        // O MESMO URI que existiu até T02. O significado — "a configuração passou pela validação,
        // quem está incompleto é o servidor" — é o mesmo, com escopo menor; um URI novo faria um
        // cliente antigo tratar como desconhecido um caso que ele já sabia tratar.
        Assert.Equal(
            "https://templategenerator.local/problems/generation-not-implemented",
            problem.RootElement.GetProperty("type").GetString());

        Assert.Equal(
            "Esta combinação ainda não gera projeto",
            problem.RootElement.GetProperty("title").GetString());

        Assert.Equal(501, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task O_501_diz_qual_campo_causou_a_recusa_em_errors_na_ordem_do_catalogo()
    {
        using PartialTemplatesFactory factory = new();

        using HttpResponseMessage response = await PostAsync(factory, Configuration());
        using JsonDocument problem = await ReadJsonAsync(response);

        string[] fields =
        [
            .. problem.RootElement.GetProperty("errors").EnumerateObject().Select(entry => entry.Name),
        ];

        // Uma entrada por campo indisponível, `database` antes de `authentication` porque é a
        // ordem dos campos no catálogo. `architecture` e `swagger` têm template e não aparecem.
        Assert.Equal<string>([CatalogFields.Database, CatalogFields.Authentication], fields);

        Assert.All(
            fields,
            field => Assert.Equal<string>(
                [TemplateAvailability.UnavailableReason],
                MessagesFor(problem, field)));
    }

    [Fact]
    public async Task O_501_nao_traz_detail()
    {
        // Com `errors` preenchido, um `detail` genérico só repetiria em prosa o que já está
        // endereçado ao campo — e o frontend mostra `detail` quando NÃO há erro de campo.
        using PartialTemplatesFactory factory = new();

        using HttpResponseMessage response = await PostAsync(factory, Configuration());
        using JsonDocument problem = await ReadJsonAsync(response);

        Assert.False(problem.RootElement.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task A_recusa_nao_traz_nenhum_cabecalho_de_download()
    {
        // É o ponto do item 4: a recusa entra ANTES de `GeneratedArchiveResult`, que escreve
        // `application/zip` e `Content-Disposition: attachment` como primeira coisa que faz.
        // Conferir o status não bastaria — é o cabeçalho que manda o navegador salvar o arquivo, e
        // uma recusa tardia sairia com ele em cima.
        using PartialTemplatesFactory factory = new();

        using HttpResponseMessage response = await PostAsync(factory, Configuration());

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);

        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.DoesNotContain(
            "Content-Disposition",
            response.Headers.Concat(response.Content.Headers).Select(header => header.Key));

        Assert.NotEqual("application/zip", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_frase_do_501_e_a_mesma_que_o_catalogo_publica()
    {
        // A pessoa lê as mesmas palavras tendo aprendido pela tela ou pela recusa: as duas saem da
        // mesma derivação, não de um literal escrito em dois lugares.
        using PartialTemplatesFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await PostAsync(factory, Configuration());
        using JsonDocument problem = await ReadJsonAsync(response);

        using JsonDocument catalog = JsonDocument.Parse(await client.GetStringAsync(
            _catalogRoute,
            TestContext.Current.CancellationToken));

        string[] reasons =
        [
            .. catalog.RootElement
                .GetProperty("unavailable")
                .EnumerateArray()
                .Where(option => option.GetProperty("field").GetString() == CatalogFields.Database)
                .Select(option => option.GetProperty("reason").GetString()!),
        ];

        Assert.NotEmpty(reasons);
        Assert.All(
            reasons,
            reason => Assert.Equal<string>([reason], MessagesFor(problem, CatalogFields.Database)));
    }

    [Fact]
    public async Task A_combinacao_com_template_continua_respondendo_200_com_o_pacote()
    {
        // A contraprova: sem ela, uma implementação que respondesse 501 a tudo passaria em todos
        // os testes acima. O repositório de produção gera `simple` + `none` + `none`.
        using GeneratorApiFactory factory = new();

        using HttpResponseMessage response = await PostAsync(factory, Configuration());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task Valor_fora_do_catalogo_responde_400_e_nao_501()
    {
        // A ordem, e esta é a razão que sozinha decide: antes da validação a derivação MENTE.
        // `architecture: "banana"` não tem fragmento, logo seria chamado de "o template desta
        // opção ainda não foi escrito" — para um valor que não existe no catálogo. A resposta
        // verdadeira é que o valor não pertence ao catálogo. Inverter a ordem transforma um erro
        // claro de quem chamou num defeito inventado do servidor.
        using GeneratorApiFactory factory = new();

        Dictionary<string, object?> configuration = Configuration();
        configuration[CatalogFields.Architecture] = "banana";

        using HttpResponseMessage response = await PostAsync(factory, configuration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument problem = await ReadJsonAsync(response);

        Assert.Contains(
            MessagesFor(problem, CatalogFields.Architecture),
            message => message.Contains("não pertence ao catálogo", StringComparison.Ordinal));

        Assert.DoesNotContain(
            MessagesFor(problem, CatalogFields.Architecture),
            message => message.Contains(
                TemplateAvailability.UnavailableReason,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restricao_nao_satisfeita_responde_400_e_nao_501_mesmo_sem_template()
    {
        // O caso que o PO levantou: `clean` + `identity` + `database: none`. Nem a Clean nem o
        // Identity têm template hoje, e mesmo assim a resposta é 400 — porque a restrição é o
        // único problema que a pessoa consegue consertar, e antecipar o 501 esconderia dela
        // justamente esse. Só depois de escolher um banco é que ela vê o 501 em `architecture`.
        using GeneratorApiFactory factory = new();

        Dictionary<string, object?> configuration = Configuration();
        configuration[CatalogFields.Architecture] = "clean";
        configuration[CatalogFields.Authentication] = "identity";
        configuration[CatalogFields.Database] = "none";

        using HttpResponseMessage response = await PostAsync(factory, configuration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument problem = await ReadJsonAsync(response);

        Assert.Contains(
            "O Identity nativo precisa de um banco para persistir os usuários.",
            MessagesFor(problem, CatalogFields.Authentication));

        Assert.DoesNotContain(
            MessagesFor(problem, CatalogFields.Architecture),
            message => message.Contains(
                TemplateAvailability.UnavailableReason,
                StringComparison.Ordinal));
    }

    private static async Task<HttpResponseMessage> PostAsync(
        GeneratorApiFactory factory,
        Dictionary<string, object?> configuration)
    {
        using HttpClient client = factory.CreateClient();

        return await client.PostAsJsonAsync(
            _route,
            configuration,
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private static string[] MessagesFor(JsonDocument problem, string field)
    {
        if (!problem.RootElement.TryGetProperty("errors", out JsonElement errors)
            || !errors.TryGetProperty(field, out JsonElement messages))
        {
            return [];
        }

        return [.. messages.EnumerateArray().Select(message => message.GetString()!)];
    }
}
