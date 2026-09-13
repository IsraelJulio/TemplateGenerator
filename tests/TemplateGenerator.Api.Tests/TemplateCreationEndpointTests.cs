using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
/// Desde T03 a configuração válida responde <c>200 application/zip</c> com o pacote gerado. Até
/// T02 ela respondia <c>501</c>, porque o motor não existia; o teste que afirmava isso continua
/// aqui, com o mesmo request e a asserção trocada.
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

    /// <summary>
    /// Uma configuração válida <strong>e disponível</strong>, usada como base dos casos negativos
    /// e de todo teste que espera um pacote.
    /// </summary>
    /// <remarks>
    /// Era <c>sqlite</c> + <c>identity</c> até T04. Continua válida — passa pela validação, pertence
    /// ao catálogo, satisfaz as restrições —, mas o template dela não existe, e desde ADR-0012 uma
    /// combinação sem template responde <c>501</c> em vez de um pacote com buraco dentro. Quem
    /// espera ZIP precisa pedir uma combinação que gera ZIP; a recusa da outra está em
    /// <see cref="GenerationNotImplementedEndpointTests"/>.
    /// </remarks>
    private static Dictionary<string, object?> ValidConfiguration() =>
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
    public async Task Configuracao_valida_nao_e_recusada_pela_validacao()
    {
        using HttpResponseMessage response = await PostAsync(ValidConfiguration());

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Configuracao_valida_responde_200_com_um_ZIP_de_verdade()
    {
        // Este teste afirmava `501 Not Implemented` até T02: o motor não existia e um 200 com
        // pacote vazio teria sido uma afirmação falsa de que a geração aconteceu. O motor entrou
        // em T03 e a troca é esta — a mesma configuração, o mesmo request, outro contrato de
        // resposta (docs/architecture/http-contract.md, "Estado transitório: 501").
        using HttpResponseMessage response = await PostAsync(ValidConfiguration());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "attachment; filename=\"Acme.Billing.Api.zip\"",
            response.Content.Headers.ContentDisposition?.ToString());

        byte[] archive = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken);

        // Não basta ter bytes: o pacote precisa abrir como ZIP.
        using ZipArchive opened = new(new MemoryStream(archive), ZipArchiveMode.Read);

        Assert.NotEmpty(opened.Entries);
    }

    [Fact]
    public async Task O_pacote_traz_o_manifesto_com_a_versao_e_as_opcoes_pedidas()
    {
        Dictionary<string, object?> configuration = ValidConfiguration();

        using HttpResponseMessage response = await PostAsync(configuration);
        using ZipArchive archive = await OpenArchiveAsync(response);

        ZipArchiveEntry entry = Assert.Single(
            archive.Entries,
            candidate => candidate.FullName == ".templategenerator/manifest.json");

        using StreamReader reader = new(entry.Open());
        using JsonDocument manifest = JsonDocument.Parse(await reader.ReadToEndAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(
            TemplateCatalog.CurrentVersion,
            manifest.RootElement.GetProperty("templateVersion").GetString());

        JsonElement options = manifest.RootElement.GetProperty("options");

        Assert.Equal("Acme.Billing.Api", options.GetProperty(CatalogFields.ProjectName).GetString());
        Assert.Equal("simple", options.GetProperty(CatalogFields.Architecture).GetString());
        Assert.Equal("none", options.GetProperty(CatalogFields.Database).GetString());
        Assert.Equal("none", options.GetProperty(CatalogFields.Authentication).GetString());
        Assert.True(options.GetProperty(CatalogFields.Swagger).GetBoolean());
        Assert.Equal("net10.0", options.GetProperty(CatalogFields.DotnetVersion).GetString());
    }

    [Fact]
    public async Task Dois_downloads_da_mesma_configuracao_tem_o_mesmo_SHA256()
    {
        // RNF-02 medido onde importa: no que sai pela rede, e não só no que o motor escreve num
        // MemoryStream de teste.
        using HttpResponseMessage first = await PostAsync(ValidConfiguration());
        using HttpResponseMessage second = await PostAsync(ValidConfiguration());

        Assert.Equal(await HashAsync(first), await HashAsync(second));
    }

    [Fact]
    public async Task O_nome_do_arquivo_baixado_e_o_nome_do_projeto()
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.ProjectName] = " Contoso.Faturamento.Api ";

        using HttpResponseMessage response = await PostAsync(configuration);

        // O nome vai aparado: é a mesma forma que a validação examinou
        // (docs/product/option-matrix.md, "Espaço em branco").
        Assert.Equal(
            "attachment; filename=\"Contoso.Faturamento.Api.zip\"",
            response.Content.Headers.ContentDisposition?.ToString());
    }

    [Fact]
    public async Task Nenhuma_entrada_do_pacote_sai_da_raiz()
    {
        using HttpResponseMessage response = await PostAsync(ValidConfiguration());
        using ZipArchive archive = await OpenArchiveAsync(response);

        Assert.All(archive.Entries, entry =>
        {
            Assert.DoesNotContain("..", entry.FullName.Split('/'));
            Assert.DoesNotContain('\\', entry.FullName);
            Assert.False(entry.FullName.StartsWith('/'));
        });

        string[] paths = [.. archive.Entries.Select(entry => entry.FullName)];

        Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Identity_sem_banco_responde_400_apontando_o_campo_authentication()
    {
        Dictionary<string, object?> configuration = ValidConfiguration();
        configuration[CatalogFields.Authentication] = "identity";
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

    private static async Task<ZipArchive> OpenArchiveAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken);

        return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
    }

    private static async Task<string> HashAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return Convert.ToHexString(SHA256.HashData(
            await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)));
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
