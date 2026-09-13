using System.Text;
using System.Text.Json;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// A composição do pacote: seleção por eixo, união ordenada, marcadores, manifesto — e as recusas
/// que precisam acontecer <em>antes</em> de qualquer byte ser escrito (RNF-03).
/// </summary>
public sealed class GenerationPlanTests
{
    private static readonly TemplateOptionsCatalog _catalog = TemplateCatalog.Current;

    private static GenerationRequest Request(
        string architecture = "simple",
        string database = "none",
        string authentication = "none",
        bool swagger = true,
        string projectName = "Acme.Billing.Api") =>
        new(projectName, architecture, database, authentication, swagger, "net10.0");

    private static GenerationPlan Resolve(ITemplateSource source, GenerationRequest? request = null) =>
        GenerationPlan.Resolve(_catalog, request ?? Request(), source);

    private static string TextOf(GenerationPlan plan, string path) =>
        Encoding.UTF8.GetString(plan.Files.Single(file => file.Path == path).Content);

    [Fact]
    public void Composicao_e_a_uniao_dos_fragmentos_aplicaveis()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "global.json")
            .With("architecture/simple", "src/Api/Program.cs")
            .With("database/none", "src/Api/Persistence/InMemoryItemStore.cs")
            .With("auth/none", "src/Api/Endpoints/ItemsEndpoints.cs")
            .With("swagger/enabled", "src/Api/Swagger.cs")

            // Fragmentos de outros valores do mesmo eixo não entram.
            .With("architecture/clean", "src/Domain/Item.cs")
            .With("database/sqlite", "src/Api/Persistence/AppDbContext.cs")
            .With("auth/jwt", "src/Api/Auth.cs");

        GenerationPlan plan = Resolve(source);

        string[] expected =
        [
            ".templategenerator/manifest.json",
            "global.json",
            "src/Api/Endpoints/ItemsEndpoints.cs",
            "src/Api/Persistence/InMemoryItemStore.cs",
            "src/Api/Program.cs",
            "src/Api/Swagger.cs",
        ];

        Assert.Equal(expected, plan.Files.Select(file => file.Path).ToArray());
    }

    [Fact]
    public void Swagger_desligado_nao_traz_o_fragmento_de_swagger()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("swagger/enabled", "src/Api/Swagger.cs");

        GenerationPlan plan = Resolve(source, Request(swagger: false));

        Assert.DoesNotContain("src/Api/Swagger.cs", plan.Files.Select(file => file.Path));
    }

    [Fact]
    public void Arquivos_saem_ordenados_por_caminho_ordinal()
    {
        // 'Z' (0x5A) vem antes de 'a' (0x61) em ordinal e depois em ordenação por cultura. É
        // exatamente aqui que uma ordenação dependente de cultura mudaria o SHA-256 conforme a
        // máquina (ADR-0003, item 2).
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "a.txt")
            .With("common", "Z.txt")
            .With("common", "B.txt");

        GenerationPlan plan = Resolve(source);

        string[] asWritten = [.. plan.Files.Select(file => file.Path)];
        string[] ordinal = [.. asWritten.Order(StringComparer.Ordinal)];

        Assert.Equal(ordinal, asWritten);
    }

    [Fact]
    public void Marcador_e_substituido_no_caminho_e_no_conteudo()
    {
        // Na arquitetura Simples o projeto se chama exatamente `<ProjectName>`: nada é
        // concatenado, e por isso o `.Api` dobrado não chega a existir
        // (docs/architecture/generated-projects.md, "Nomes de projeto e de pasta").
        FakeTemplateSource source = new FakeTemplateSource()
            .With(
                "common",
                "src/__ProjectName__/__ProjectName__.csproj",
                "<TargetFramework>__TargetFramework__</TargetFramework>\n<Version>__TemplateVersion__</Version>");

        GenerationPlan plan = Resolve(source);

        string path = "src/Acme.Billing.Api/Acme.Billing.Api.csproj";

        Assert.Contains(path, plan.Files.Select(file => file.Path));
        Assert.Equal(
            $"<TargetFramework>net10.0</TargetFramework>\n<Version>{_catalog.TemplateVersion}</Version>\n",
            TextOf(plan, path));
    }

    [Fact]
    public void Marcador_desconhecido_e_erro_e_a_mensagem_diz_onde()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "README.md", "Projeto __ProjectNme__.\n");

        TemplateDefectException error =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("__ProjectNme__", error.Message, StringComparison.Ordinal);
        Assert.Contains("common/README.md", error.Message, StringComparison.Ordinal);
        Assert.Contains(TemplateTokens.ProjectName, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Conflito_de_caminho_entre_fragmentos_e_erro_e_nao_precedencia()
    {
        // docs/architecture/generation-engine.md: "Conflito de caminho entre dois fragmentos é
        // erro de build dos testes, não resolução silenciosa em tempo de execução".
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "src/Api/Program.cs", "// da arquitetura\n")
            .With("auth/none", "src/Api/Program.cs", "// da autenticação\n");

        TemplatePathConflictException conflict =
            Assert.Throws<TemplatePathConflictException>(() => Resolve(source));

        Assert.Equal("src/Api/Program.cs", conflict.Path);
        Assert.Equal("architecture/simple", conflict.FirstFragment);
        Assert.Equal("auth/none", conflict.SecondFragment);
    }

    [Fact]
    public void Conflito_acontece_tambem_quando_o_marcador_faz_dois_caminhos_coincidirem()
    {
        // Os caminhos são diferentes no repositório e iguais no pacote. Comparar antes da
        // substituição deixaria passar.
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "src/__ProjectName__/a.cs")
            .With("architecture/simple", "src/Acme/a.cs");

        TemplatePathConflictException conflict = Assert.Throws<TemplatePathConflictException>(
            () => Resolve(source, Request(projectName: "Acme")));

        Assert.Equal("src/Acme/a.cs", conflict.Path);
    }

    [Fact]
    public void Fragmento_nao_pode_reivindicar_o_caminho_do_manifesto()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", GenerationManifest.Path, "{}\n");

        TemplatePathConflictException conflict =
            Assert.Throws<TemplatePathConflictException>(() => Resolve(source));

        Assert.Equal(GenerationManifest.Path, conflict.Path);
        Assert.Equal(GeneratedFile.EngineOrigin, conflict.SecondFragment);
    }

    [Theory]
    [InlineData("../fora.txt")]                 // travessia simples
    [InlineData("src/../../fora.txt")]          // travessia no meio
    [InlineData("/etc/passwd")]                 // caminho absoluto
    [InlineData("C:/Windows/System32/x.dll")]   // raiz de drive
    [InlineData("src\\Api\\Program.cs")]        // separador do Windows
    [InlineData("README.md:oculto")]            // alternate data stream
    [InlineData("src//Program.cs")]             // segmento vazio
    [InlineData("src/CON/Program.cs")]          // nome reservado do Windows
    [InlineData("src/NUL.txt")]                 // nome reservado com extensão
    [InlineData("src/Api/")]                    // diretório, não arquivo
    [InlineData("src/Program.cs ")]             // termina em espaço
    [InlineData("src/relatório.md")]            // fora do ASCII
    public void Caminho_perigoso_e_recusado_antes_de_qualquer_escrita(string path)
    {
        FakeTemplateSource source = new FakeTemplateSource().With("common", path);

        TemplateDefectException error =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains(path, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Caminho_duplicado_dentro_do_mesmo_fragmento_e_recusado()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "global.json", "{ \"a\": 1 }\n")
            .With("common", "global.json", "{ \"a\": 2 }\n");

        TemplatePathConflictException conflict =
            Assert.Throws<TemplatePathConflictException>(() => Resolve(source));

        Assert.Equal("global.json", conflict.Path);
    }

    [Fact]
    public void Configuracao_invalida_e_recusada_pelo_motor_com_o_resultado_da_validacao()
    {
        // Identity sem banco. A Api recusa antes de chegar aqui; esta é a última linha de defesa,
        // para quem chama o motor direto.
        GenerationRequestRejectedException rejected =
            Assert.Throws<GenerationRequestRejectedException>(() => Resolve(
                new FakeTemplateSource(),
                Request(authentication: "identity", database: "none")));

        Assert.False(rejected.Validation.IsValid);
        Assert.Contains(CatalogFields.Authentication, rejected.Validation.Errors.Keys);
    }

    [Fact]
    public void Manifesto_traz_templateVersion_e_as_opcoes_escolhidas_e_nada_mais()
    {
        GenerationRequest request = Request(database: "sqlite", authentication: "identity", swagger: false);

        GenerationPlan plan = Resolve(new FakeTemplateSource(), request);

        using JsonDocument manifest = JsonDocument.Parse(TextOf(plan, GenerationManifest.Path));

        string[] topLevel = [.. manifest.RootElement.EnumerateObject().Select(property => property.Name)];
        string[] expectedTopLevel = ["templateVersion", "options"];

        Assert.Equal(expectedTopLevel, topLevel);

        Assert.Equal(_catalog.TemplateVersion, manifest.RootElement.GetProperty("templateVersion").GetString());

        JsonElement options = manifest.RootElement.GetProperty("options");

        string[] expectedOptions =
        [
            CatalogFields.ProjectName,
            CatalogFields.Architecture,
            CatalogFields.Database,
            CatalogFields.Authentication,
            CatalogFields.Swagger,
            CatalogFields.DotnetVersion,
        ];

        string[] actualOptions = [.. options.EnumerateObject().Select(property => property.Name)];

        Assert.Equal(expectedOptions, actualOptions);

        Assert.Equal(request.ProjectName, options.GetProperty(CatalogFields.ProjectName).GetString());
        Assert.Equal("sqlite", options.GetProperty(CatalogFields.Database).GetString());
        Assert.Equal("identity", options.GetProperty(CatalogFields.Authentication).GetString());
        Assert.False(options.GetProperty(CatalogFields.Swagger).GetBoolean());
    }

    [Fact]
    public void Conteudo_sai_em_UTF8_sem_BOM_com_LF_e_terminando_em_nova_linha()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "README.md", "\uFEFFprimeira\r\nsegunda\rterceira");

        GenerationPlan plan = Resolve(source);

        byte[] content = plan.Files.Single(file => file.Path == "README.md").Content;

        Assert.Equal("primeira\nsegunda\nterceira\n", Encoding.UTF8.GetString(content));
        Assert.DoesNotContain((byte)'\r', content);
        Assert.NotEqual(0xEF, content[0]);
    }

    [Fact]
    public void Todo_arquivo_do_plano_diz_de_qual_fragmento_veio()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "global.json")
            .With("architecture/simple", "src/Program.cs");

        GenerationPlan plan = Resolve(source);

        Assert.Equal("common", plan.Files.Single(f => f.Path == "global.json").Fragment);
        Assert.Equal("architecture/simple", plan.Files.Single(f => f.Path == "src/Program.cs").Fragment);
        Assert.Equal(
            GeneratedFile.EngineOrigin,
            plan.Files.Single(f => f.Path == GenerationManifest.Path).Fragment);
    }
}
