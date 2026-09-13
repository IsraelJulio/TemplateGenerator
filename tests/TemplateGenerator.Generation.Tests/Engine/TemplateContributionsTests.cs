using System.Text;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// Os marcadores de contribuição de ADR-0011, item a item da especificação.
/// </summary>
/// <remarks>
/// <para>
/// O caso concreto que o mecanismo existe para resolver está em
/// <see cref="O_csproj_recebe_as_PackageReference_dos_outros_eixos"/>: o <c>.csproj</c> pertence ao
/// fragmento de arquitetura e os eixos <c>database</c>, <c>auth</c> e <c>swagger</c> entregam suas
/// <c>PackageReference</c> por marcador, de modo que o arquivo gerado saia <strong>literal</strong>
/// — que é o que permite à camada 1, estática, afirmar RF-20 e RNF-06 lendo um XML.
/// </para>
/// <para>
/// Tudo aqui roda sobre fixture, nunca sobre o template de produção: o que está sendo testado é o
/// mecanismo.
/// </para>
/// </remarks>
public sealed class TemplateContributionsTests
{
    private const string PartsDirectory = TemplateContributions.Directory;

    private static readonly TemplateOptionsCatalog _catalog = TemplateCatalog.Current;

    private static GenerationRequest Request(
        string architecture = "simple",
        string database = "none",
        string authentication = "none",
        bool swagger = true,
        string projectName = "Acme.Billing") =>
        new(projectName, architecture, database, authentication, swagger, "net10.0");

    private static GenerationPlan Resolve(ITemplateSource source, GenerationRequest? request = null) =>
        GenerationPlan.Resolve(_catalog, request ?? Request(), source);

    /// <summary>O caminho de um arquivo de contribuição na raiz do fragmento.</summary>
    private static string Part(string file) => $"{PartsDirectory}/{file}";

    private static string TextOf(GenerationPlan plan, string path) =>
        Encoding.UTF8.GetString(plan.Files.Single(file => file.Path == path).Content);

    // ---------------------------------------------------------------- o caso de uso inteiro

    [Fact]
    public void O_csproj_recebe_as_PackageReference_dos_outros_eixos()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With(
                "architecture/simple",
                "src/__ProjectName__/__ProjectName__.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>__TargetFramework__</TargetFramework>
                  </PropertyGroup>
                __ApiPackageReferences__
                </Project>
                """)
            // Item 7b: o invólucro é da contribuição. Cada eixo traz o próprio `<ItemGroup>`, e é
            // por isso que o `.csproj` gerado sai agrupado por eixo (item 5).
            .With("database/sqlite", Part("ApiPackageReferences.xml"),
                "  <ItemGroup>\n"
                + "    <PackageReference Include=\"Microsoft.EntityFrameworkCore.Sqlite\" Version=\"10.0.12\" />\n"
                + "  </ItemGroup>")
            .With("swagger/enabled", Part("ApiPackageReferences.xml"),
                "  <ItemGroup>\n"
                + "    <PackageReference Include=\"Swashbuckle.AspNetCore\" Version=\"10.2.3\" />\n"
                + "  </ItemGroup>");

        GenerationPlan plan = Resolve(source, Request(database: "sqlite", swagger: true));

        Assert.Equal(
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="Swashbuckle.AspNetCore" Version="10.2.3" />
              </ItemGroup>
            </Project>

            """.ReplaceLineEndings("\n"),
            TextOf(plan, "src/Acme.Billing/Acme.Billing.csproj"));
    }

    [Fact]
    public void Com_swagger_desligado_o_csproj_sai_sem_a_dependencia_e_sem_grupo_vazio()
    {
        FakeTemplateSource source = Csproj()
            .With("swagger/enabled", Part("ApiPackageReferences.xml"),
                "  <ItemGroup>\n"
                + "    <PackageReference Include=\"Swashbuckle.AspNetCore\" Version=\"10.2.3\" />\n"
                + "  </ItemGroup>");

        GenerationPlan plan = Resolve(source, Request(swagger: false));

        // RF-20 e item 7b: nem a dependência, nem linha órfã, nem um `<ItemGroup>` vazio
        // sobrevivendo ao marcador apagado. O invólucro saiu junto porque ele é da contribuição.
        Assert.Equal(
            """
            <Project>
            </Project>

            """.ReplaceLineEndings("\n"),
            TextOf(plan, "src/Acme.Billing/Acme.Billing.csproj"));

        Assert.DoesNotContain(
            "ItemGroup",
            TextOf(plan, "src/Acme.Billing/Acme.Billing.csproj"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Um <c>.csproj</c> de teste na forma que o item 7b exige: o marcador na coluna 0, sem
    /// invólucro em volta dele no arquivo hospedeiro.
    /// </summary>
    private static FakeTemplateSource Csproj() => new FakeTemplateSource()
        .With(
            "architecture/simple",
            "src/__ProjectName__/__ProjectName__.csproj",
            """
            <Project>
            __ApiPackageReferences__
            </Project>
            """);

    // ---------------------------------------------------------------- item 1: caminho

    [Fact]
    public void Item1_arquivo_de_contribuicao_nao_vira_entrada_do_ZIP()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "README.md", "Projeto __ProjectName__.\n")
            .With("common", Part("ApiPackageReferences.xml"), "<x />");

        GenerationPlan plan = Resolve(source);

        Assert.DoesNotContain(
            plan.Files.Select(file => file.Path),
            path => path.Contains(PartsDirectory, StringComparison.Ordinal));

        string[] expected = [".templategenerator/manifest.json", "README.md"];

        Assert.Equal(expected, plan.Files.Select(file => file.Path).ToArray());
    }

    [Theory]
    [InlineData("docs/__parts__/x.xml")]
    [InlineData("__parts__/sub/x.xml")]
    [InlineData("src/api/__parts__/x.xml")]
    [InlineData("__parts__")]
    public void Item1_parts_fora_da_raiz_do_fragmento_e_defeito(string path)
    {
        FakeTemplateSource source = new FakeTemplateSource().With("common", path, "<x />");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains(path, defect.Message, StringComparison.Ordinal);
        Assert.Contains("dois segmentos", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Item1_a_extensao_e_ignorada()
    {
        // `.xml` num fragmento e `.txt` em outro alimentam o mesmo marcador: a extensão existe
        // para o editor colorir a sintaxe.
        FakeTemplateSource source = Csproj()
            .With("database/none", Part("ApiPackageReferences.xml"), "  <a />")
            .With("auth/none", Part("ApiPackageReferences.txt"), "  <b />");

        GenerationPlan plan = Resolve(source);

        Assert.Contains("  <a />\n  <b />\n", TextOf(plan, "src/Acme.Billing/Acme.Billing.csproj"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- item 2: nome

    [Theory]
    [InlineData("1Api.xml")]        // não começa por letra
    [InlineData("api-refs.xml")]    // hífen
    [InlineData("Api_Refs.xml")]    // sublinhado interno
    [InlineData("Api Refs.xml")]    // espaço
    [InlineData(".xml")]            // nome vazio
    public void Item2_nome_de_marcador_invalido_e_defeito(string file)
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", Part(file), "<x />");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("[A-Za-z][A-Za-z0-9]*", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Item2_dois_arquivos_para_o_mesmo_marcador_no_mesmo_fragmento_e_defeito()
    {
        // A ordem entre os dois seria indefinida, e uma ordem indefinida vira hash instável.
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", Part("ApiPackageReferences.xml"), "<a />")
            .With("common", Part("ApiPackageReferences.txt"), "<b />");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("__ApiPackageReferences__", defect.Message, StringComparison.Ordinal);
        Assert.Contains("duas vezes", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Item2_o_mesmo_marcador_em_fragmentos_diferentes_e_o_ponto_do_mecanismo()
    {
        FakeTemplateSource source = Csproj()
            .With("database/none", Part("ApiPackageReferences.xml"), "  <a />")
            .With("swagger/enabled", Part("ApiPackageReferences.xml"), "  <b />");

        GenerationPlan plan = Resolve(source);

        Assert.Contains("  <a />\n  <b />\n", TextOf(plan, "src/Acme.Billing/Acme.Billing.csproj"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- item 3: marcador conhecido

    [Fact]
    public void Item3_marcador_declarado_so_em_fragmento_nao_selecionado_vira_string_vazia()
    {
        // O marcador é conhecido porque `auth/jwt` o declara, mesmo que esta combinação use
        // `auth/none`. Sem a varredura do repositório inteiro, isto seria "marcador desconhecido".
        FakeTemplateSource source = Csproj()
            .With("auth/jwt", Part("ApiPackageReferences.xml"), "  <jwt />");

        GenerationPlan plan = Resolve(source, Request(authentication: "none"));

        Assert.Equal(
            """
            <Project>
            </Project>

            """.ReplaceLineEndings("\n"),
            TextOf(plan, "src/Acme.Billing/Acme.Billing.csproj"));
    }

    [Fact]
    public void Item3_marcador_que_ninguem_declara_continua_sendo_erro()
    {
        // A proteção contra nome digitado errado é exatamente o que não pode se perder ao
        // introduzir "sem contribuição ⇒ string vazia".
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "x.csproj", "__ApiPackgeReferences__\n")
            .With("swagger/enabled", Part("ApiPackageReferences.xml"), "  <s />");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("__ApiPackgeReferences__", defect.Message, StringComparison.Ordinal);
        Assert.Contains("não existe", defect.Message, StringComparison.Ordinal);

        // E a mensagem mostra o marcador certo ao lado do errado.
        Assert.Contains("__ApiPackageReferences__", defect.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- item 4: disjunção

    [Theory]
    [InlineData("ProjectName.xml")]
    [InlineData("TargetFramework.xml")]
    [InlineData("TemplateVersion.xml")]
    public void Item4_contribuicao_com_nome_de_marcador_de_valor_e_defeito(string file)
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", Part(file), "x");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("marcador de valor", defect.Message, StringComparison.Ordinal);
        Assert.Contains("disjuntos", defect.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- item 5: ordem

    [Fact]
    public void Item5_a_ordem_de_concatenacao_e_a_ordem_de_selecao_dos_fragmentos()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "lista.txt", "__Itens__\n")
            .With("swagger/enabled", Part("Itens.txt"), "swagger")
            .With("auth/identity", Part("Itens.txt"), "auth")
            .With("database/sqlite", Part("Itens.txt"), "database")
            .With("architecture/simple", Part("Itens.txt"), "architecture")
            .With("common", Part("Itens.txt"), "common");

        GenerationPlan plan = Resolve(
            source,
            Request(database: "sqlite", authentication: "identity", swagger: true));

        // Não é ordem alfabética e não é a ordem em que os arquivos foram declarados: é
        // common → architecture → database → auth → swagger, como TemplateAxes.Select.
        Assert.Equal(
            "common\narchitecture\ndatabase\nauth\nswagger\n",
            TextOf(plan, "lista.txt"));
    }

    // ---------------------------------------------------------------- item 6: aninhamento

    [Fact]
    public void Item6_marcador_de_valor_dentro_da_contribuicao_e_substituido()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "__ProjectReferences__\n")
            .With("database/sqlite", Part("ProjectReferences.xml"),
                "  <ProjectReference Include=\"../__ProjectName__.Data/__ProjectName__.Data.csproj\" />");

        GenerationPlan plan = Resolve(source, Request(database: "sqlite"));

        Assert.Equal(
            "  <ProjectReference Include=\"../Acme.Billing.Data/Acme.Billing.Data.csproj\" />\n",
            TextOf(plan, "x.csproj"));
    }

    [Fact]
    public void Item6_marcador_de_contribuicao_dentro_da_contribuicao_e_erro()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "__Externo__\n")
            .With("common", Part("Externo.xml"), "antes __Interno__ depois")
            .With("common", Part("Interno.xml"), "nunca chega aqui");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("__Interno__", defect.Message, StringComparison.Ordinal);
        Assert.Contains("aninhamento", defect.Message, StringComparison.Ordinal);
        Assert.Contains("recursão", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Item6_marcador_desconhecido_dentro_da_contribuicao_tambem_e_erro()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "__Externo__\n")
            .With("common", Part("Externo.xml"), "__ProjectNme__");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("__ProjectNme__", defect.Message, StringComparison.Ordinal);
        Assert.Contains($"common/{PartsDirectory}/Externo.xml", defect.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- item 7: regra da linha

    [Fact]
    public void Item7_marcador_sozinho_na_coluna_zero_consome_a_linha_inteira()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "antes\n__Itens__\ndepois\n")
            .With("database/sqlite", Part("Itens.xml"), "    <a />")
            .With("swagger/enabled", Part("Itens.xml"), "    <b />\n    <c />\n");

        GenerationPlan plan = Resolve(source, Request(database: "sqlite", swagger: true));

        // Cada contribuição trouxe a própria indentação; o motor não reindentou nada, não somou
        // linha em branco e não deixou a quebra do marcador para trás.
        Assert.Equal(
            "antes\n    <a />\n    <b />\n    <c />\ndepois\n",
            TextOf(plan, "x.csproj"));
    }

    [Fact]
    public void Item7_sem_contribuicao_a_linha_do_marcador_some_inteira()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "antes\n__Itens__\ndepois\n")
            .With("auth/jwt", Part("Itens.xml"), "    <jwt />");

        GenerationPlan plan = Resolve(source, Request(authentication: "none"));

        Assert.Equal("antes\ndepois\n", TextOf(plan, "x.csproj"));
    }

    [Fact]
    public void Item7_espaco_a_direita_do_marcador_nao_o_tira_da_coluna_zero()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "antes\n__Itens__   \ndepois\n")
            .With("common", Part("Itens.xml"), "  <a />");

        GenerationPlan plan = Resolve(source);

        Assert.Equal("antes\n  <a />\ndepois\n", TextOf(plan, "x.csproj"));
    }

    [Fact]
    public void Item7_marcador_de_contribuicao_sozinho_e_indentado_e_defeito()
    {
        // Indentar o marcador somaria o recuo da linha ao recuo que cada contribuição já traz, e
        // o arquivo sairia com indentação quebrada sem ninguém perceber.
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "<ItemGroup>\n    __Itens__\n</ItemGroup>\n")
            .With("common", Part("Itens.xml"), "  <a />");

        TemplateDefectException defect =
            Assert.Throws<TemplateDefectException>(() => Resolve(source));

        Assert.Contains("__Itens__", defect.Message, StringComparison.Ordinal);
        Assert.Contains("coluna 0", defect.Message, StringComparison.Ordinal);
        Assert.Contains("indentada", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Item7_marcador_de_contribuicao_indentado_e_defeito_mesmo_sem_contribuicao_alguma()
    {
        // O defeito é do template, e não da combinação: ele não pode aparecer só quando alguém
        // contribui.
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.csproj", "<ItemGroup>\n  __Itens__\n</ItemGroup>\n")
            .With("auth/jwt", Part("Itens.xml"), "  <jwt />");

        Assert.Throws<TemplateDefectException>(() => Resolve(source, Request(authentication: "none")));
    }

    [Fact]
    public void Item7_a_regra_da_linha_nao_vale_para_marcador_de_valor()
    {
        // Um marcador de valor sozinho na linha, indentado ou não, continua sendo substituição
        // literal: consumir a quebra grudaria duas linhas sem que o template tivesse pedido.
        FakeTemplateSource source = new FakeTemplateSource()
            .With("common", "x.txt", "antes\n__ProjectName__\n  __ProjectName__\ndepois\n");

        GenerationPlan plan = Resolve(source);

        Assert.Equal("antes\nAcme.Billing\n  Acme.Billing\ndepois\n", TextOf(plan, "x.txt"));
    }

    // ---------------------------------------------------------------- item 8: outras posições

    [Fact]
    public void Item8_no_meio_da_linha_a_substituicao_e_literal_com_quebra_entre_contribuicoes()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.txt", "início __Itens__ fim\n")
            .With("database/sqlite", Part("Itens.txt"), "um")
            .With("swagger/enabled", Part("Itens.txt"), "dois");

        GenerationPlan plan = Resolve(source, Request(database: "sqlite", swagger: true));

        Assert.Equal("início um\ndois fim\n", TextOf(plan, "x.txt"));
    }

    [Fact]
    public void Item8_marcador_de_contribuicao_vale_no_caminho_do_arquivo()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "src/__ApiProjectDir__/Program.cs", "// código\n")
            .With("architecture/simple", Part("ApiProjectDir.txt"), "__ProjectName__");

        GenerationPlan plan = Resolve(source);

        Assert.Contains("src/Acme.Billing/Program.cs", plan.Files.Select(file => file.Path));
    }

    // ---------------------------------------------------------------- item 9: texto inerte

    [Fact]
    public void Item9_contribuicoes_iguais_nao_sao_desduplicadas_nem_reordenadas()
    {
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.txt", "__Itens__\n")
            .With("common", Part("Itens.txt"), "zzz")
            .With("architecture/simple", Part("Itens.txt"), "aaa")
            .With("database/sqlite", Part("Itens.txt"), "zzz");

        GenerationPlan plan = Resolve(source, Request(database: "sqlite"));

        // Se o motor ordenasse por conteúdo sairia 'aaa' primeiro; se desduplicasse sairia um
        // 'zzz' só. Ele não faz nem uma coisa nem outra: o texto é inerte.
        Assert.Equal("zzz\naaa\nzzz\n", TextOf(plan, "x.txt"));
    }

    [Fact]
    public void Item9_o_conteudo_da_contribuicao_nao_e_interpretado()
    {
        // Qualquer coisa que pareça condicional, laço ou expressão é texto e sai como texto.
        const string inerte = "{{#if swagger}} <!-- $(Foo) --> {% for x in y %}";

        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.txt", "__Itens__\n")
            .With("common", Part("Itens.txt"), inerte);

        GenerationPlan plan = Resolve(source);

        Assert.Equal(inerte + "\n", TextOf(plan, "x.txt"));
    }

    // ---------------------------------------------------------------- determinismo

    [Fact]
    public void A_contribuicao_entra_no_conteudo_e_portanto_no_hash()
    {
        FakeTemplateSource source = Csproj()
            .With("swagger/enabled", Part("ApiPackageReferences.xml"), "    <swashbuckle />");

        string comSwagger = TextOf(Resolve(source, Request(swagger: true)), "src/Acme.Billing/Acme.Billing.csproj");
        string semSwagger = TextOf(Resolve(source, Request(swagger: false)), "src/Acme.Billing/Acme.Billing.csproj");

        Assert.NotEqual(comSwagger, semSwagger);
        Assert.Equal(
            comSwagger,
            TextOf(Resolve(source, Request(swagger: true)), "src/Acme.Billing/Acme.Billing.csproj"));
    }

    [Fact]
    public void Contribuicao_vazia_nao_vira_linha_em_branco()
    {
        // Um arquivo de contribuição sem conteúdo contribui nada — nem uma linha em branco. A
        // alternativa seria justamente o que o item 7 evita no caso de zero contribuições.
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.txt", "antes\n__Itens__\ndepois\n")
            .With("common", Part("Itens.txt"), "")
            .With("database/none", Part("Itens.txt"), "\n");

        GenerationPlan plan = Resolve(source);

        Assert.Equal("antes\ndepois\n", TextOf(plan, "x.txt"));
    }

    [Fact]
    public void Espaco_em_branco_na_contribuicao_e_conteudo()
    {
        // A regra é uma só e é a mais simples que existe: as quebras de linha do fim saem, porque
        // quem insere é que decide se o texto termina em nova linha; o resto é conteúdo. Aparar
        // espaço seria o motor decidindo o que é significativo dentro da contribuição, e isso é
        // interpretação (item 9).
        FakeTemplateSource source = new FakeTemplateSource()
            .With("architecture/simple", "x.txt", "antes\n__Itens__\ndepois\n")
            .With("common", Part("Itens.txt"), "   \n");

        GenerationPlan plan = Resolve(source);

        Assert.Equal("antes\n   \ndepois\n", TextOf(plan, "x.txt"));
    }
}
