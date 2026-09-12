using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TemplateGenerator.Generation.Catalog;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Contrato de <c>POST /api/templates</c> (docs/architecture/http-contract.md): validação
/// soberana no servidor e erro em <c>ProblemDetails</c> endereçado ao campo culpado.
/// </summary>
/// <remarks>
/// <para>
/// Nenhum destes testes passa pelo frontend, de propósito: o que o frontend valida é
/// conveniência, e a única garantia real é a que está verificada aqui.
/// </para>
/// <para>
/// Enquanto o motor de geração não existir (T03), a configuração válida responde
/// <c>501</c> — ver <c>GeneratorEndpoints.CreateTemplate</c>.
/// </para>
/// </remarks>
public sealed class TemplateCreationEndpointTests : IClassFixture<GeneratorApiFactory>
{
    private static readonly Uri _route = new("/api/templates", UriKind.Relative);

    private readonly GeneratorApiFactory _factory;

    public TemplateCreationEndpointTests(GeneratorApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Uma configuração válida, usada como base dos casos negativos.</summary>
    private static Dictionary<string, object?> ValidConfiguration() =>
        new(StringComparer.Ordinal)
        {
            [CatalogFields.ProjectName] = "Acme.Billing.Api",
            [CatalogFields.Architecture] = "simple",
            [CatalogFields.Database] = "sqlite",
            [CatalogFields.Authentication] = "identity",
            [CatalogFields.Swagger] = true,
            [CatalogFields.DotnetVersion] = "net10.0",
        };

    [Fact]
    public async Task Configuracao_valida_nao_e_recusada_pela_validacao()
    {
        using HttpResponseMessage response = await PostAsync(ValidConfiguration());

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Configuracao_valida_responde_501_enquanto_o_motor_nao_existe()
    {
        // Honestidade explícita: a Api não finge gerar. Quando T03 entregar o motor, este teste
        // muda para 200 com application/zip — e a mudança fica visível no diff.
        using HttpResponseMessage response = await PostAsync(ValidConfiguration());

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument problem = await ReadJsonAsync(response);

        Assert.Equal(
            "https://templategenerator.local/problems/generation-not-implemented",
            problem.RootElement.GetProperty("type").GetString());

        Assert.False(problem.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Identity_sem_banco_responde_400_apontando_o_campo_authentication()
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.Database] = "none";

        using HttpResponseMessage response = await PostAsync(configuration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument problem = await ReadJsonAsync(response);

        Assert.Equal(
            "https://templategenerator.local/problems/invalid-configuration",
            problem.RootElement.GetProperty("type").GetString());
        Assert.Equal("Configuração inválida", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());

        string[] messages = MessagesFor(problem, CatalogFields.Authentication);

        Assert.Contains(
            "O Identity nativo precisa de um banco para persistir os usuários.",
            messages);
    }

    [Theory]
    [InlineData("")]                    // vazio
    [InlineData("..")]                  // travessia de diretório
    [InlineData("../etc/passwd")]       // travessia com barra
    [InlineData("CON")]                 // nome reservado do Windows
    [InlineData("class")]               // palavra reservada do C#
    [InlineData("Acme Billing!")]       // caractere inválido em identificador
    [InlineData("1Acme")]               // identificador não pode começar com dígito
    [InlineData("Acme..Api")]           // trecho vazio entre pontos
    [InlineData("Cotação.Api")]         // letra fora do ASCII
    public async Task Nome_de_projeto_invalido_responde_400_apontando_projectName(string projectName)
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.ProjectName] = projectName;

        using HttpResponseMessage response = await PostAsync(configuration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument problem = await ReadJsonAsync(response);

        string[] messages = MessagesFor(problem, CatalogFields.ProjectName);

        Assert.NotEmpty(messages);
        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
    }

    [Fact]
    public async Task Nome_com_acento_e_recusado_com_mensagem_que_diz_o_que_fazer()
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.ProjectName] = "Cotação.Api";

        using HttpResponseMessage response = await PostAsync(configuration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument problem = await ReadJsonAsync(response);

        // Recusar sem ensinar deixa a pessoa tentando variações às cegas.
        Assert.Contains(
            MessagesFor(problem, CatalogFields.ProjectName),
            message => message.Contains("letras sem acento", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(" Acme.Api ")]
    [InlineData("\tAcme.Api\n")]
    public async Task Espaco_nas_pontas_do_nome_e_aparado_e_nao_recusado(string projectName)
    {
        // A tela apara antes de enviar; o servidor apara também, para não recusar de quem chama
        // a API direto o que aceita de quem usa a tela.
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.ProjectName] = projectName;

        using HttpResponseMessage response = await PostAsync(configuration);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nome_de_projeto_acima_de_cem_caracteres_e_recusado()
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.ProjectName] = new string('A', 101);

        using HttpResponseMessage response = await PostAsync(configuration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument problem = await ReadJsonAsync(response);

        Assert.NotEmpty(MessagesFor(problem, CatalogFields.ProjectName));
    }

    [Theory]
    [InlineData(CatalogFields.Architecture, "hexagonal")]
    [InlineData(CatalogFields.Database, "oracle")]
    [InlineData(CatalogFields.Authentication, "oauth")]
    [InlineData(CatalogFields.DotnetVersion, "net9.0")]
    public async Task Valor_fora_do_catalogo_responde_400_apontando_o_proprio_campo(
        string field,
        string value)
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[field] = value;

        using HttpResponseMessage response = await PostAsync(configuration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument problem = await ReadJsonAsync(response);

        Assert.Contains(
            MessagesFor(problem, field),
            message => message.Contains($"'{value}'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Corpo_sem_nenhum_campo_cobra_todos_os_campos()
    {
        using HttpResponseMessage response = await PostRawAsync("{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument problem = await ReadJsonAsync(response);

        string[] expected =
        [
            CatalogFields.ProjectName,
            CatalogFields.Architecture,
            CatalogFields.Database,
            CatalogFields.Authentication,
            CatalogFields.Swagger,
            CatalogFields.DotnetVersion,
        ];

        // Inclusive `swagger`: um booleano ausente chega como `false` e, sem este cuidado, a
        // omissão viraria em silêncio o oposto do padrão do catálogo.
        Assert.All(expected, field => Assert.NotEmpty(MessagesFor(problem, field)));
    }

    [Fact]
    public async Task Json_malformado_responde_400_e_nao_500()
    {
        // Corpo quebrado é entrada hostil, não defeito do servidor. Um 500 aqui apagaria a
        // diferença entre "mandei errado" e "o servidor caiu".
        using HttpResponseMessage response = await PostRawAsync("{\"projectName\":");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Corpo_com_UTF8_invalido_responde_400_e_nao_500()
    {
        // 0xE7 sozinho é 'ç' em Latin-1 e byte inválido em UTF-8 — exatamente o que um cliente
        // mal configurado manda ao digitar um nome com acento.
        byte[] corrupted =
        [
            .. Encoding.UTF8.GetBytes("{\"projectName\":\"Cota"),
            0xE7,
            .. Encoding.UTF8.GetBytes("ao.Api\"}"),
        ];

        using HttpClient client = _factory.CreateClient();
        using ByteArrayContent content = new(corrupted);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await client.PostAsync(
            _route,
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Toda_mensagem_de_erro_esta_enderecada_a_um_campo_do_catalogo()
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.ProjectName] = "..";
        configuration[CatalogFields.Database] = "none";

        using HttpResponseMessage response = await PostAsync(configuration);

        using JsonDocument problem = await ReadJsonAsync(response);

        string[] known =
        [
            CatalogFields.ProjectName,
            .. TemplateCatalog.Current.Fields.Keys,
        ];

        Assert.All(
            problem.RootElement.GetProperty("errors").EnumerateObject(),
            entry => Assert.Contains(entry.Name, known));
    }

    private async Task<HttpResponseMessage> PostAsync(Dictionary<string, object?> configuration)
    {
        using HttpClient client = _factory.CreateClient();

        return await client.PostAsJsonAsync(
            _route,
            configuration,
            TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostRawAsync(string json)
    {
        using HttpClient client = _factory.CreateClient();
        using StringContent content = new(json, Encoding.UTF8, "application/json");

        return await client.PostAsync(_route, content, TestContext.Current.CancellationToken);
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
