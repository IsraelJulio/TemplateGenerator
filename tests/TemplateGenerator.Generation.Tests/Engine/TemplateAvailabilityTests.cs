using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// A derivação de disponibilidade (ADR-0012, "Forma de implementação", item 1): quais valores do
/// catálogo têm template, lido dos fragmentos e de mais nada.
/// </summary>
/// <remarks>
/// <para>
/// Quase tudo aqui roda sobre um repositório montado no próprio teste, e não sobre
/// <c>Templates/</c>. É deliberado: o estado do repositório de produção é <strong>transitório</strong>
/// — no dia em que alguém escrever <c>architecture/clean</c>, a Clean acende, e um teste amarrado
/// ao estado de hoje falharia por causa do sucesso de outra pessoa. O que não é transitório, e por
/// isso é afirmado sobre a produção, é a <strong>regra R2</strong>: <c>swagger: false</c> está
/// disponível porque não implica fragmento nenhum, hoje e sempre.
/// </para>
/// </remarks>
public sealed class TemplateAvailabilityTests
{
    private static readonly TemplateOptionsCatalog _catalog = TemplateCatalog.Current;

    private static TemplateAvailability Over(ITemplateSource source) => new(_catalog, source);

    /// <summary>
    /// Um repositório em que todo fragmento declarado pelo catálogo tem um arquivo — menos os
    /// nomeados em <paramref name="missing"/>, que ficam vazios.
    /// </summary>
    private static FakeTemplateSource Complete(params string[] missing)
    {
        FakeTemplateSource source = new();

        foreach (string fragment in TemplateAxes.All)
        {
            if (!missing.Contains(fragment, StringComparer.Ordinal))
            {
                source.With(fragment, "arquivo.txt");
            }
        }

        return source;
    }

    [Fact]
    public void R1a_um_arquivo_que_e_entrada_de_ZIP_torna_o_valor_disponivel()
    {
        TemplateAvailability availability = Over(
            new FakeTemplateSource().With("architecture/clean", "src/Domain/Item.cs"));

        Assert.True(availability.IsAvailable(CatalogFields.Architecture, CatalogValue.OfText("clean")));
    }

    [Fact]
    public void R1b_um_fragmento_que_so_contribui_texto_tambem_torna_o_valor_disponivel()
    {
        // Não é enfeite: `database/none`, `auth/none` e `swagger/enabled` SÓ têm `__parts__/`.
        // Sem esta alínea a derivação declararia indisponível tudo que hoje funciona.
        TemplateAvailability availability = Over(new FakeTemplateSource()
            .With("database/sqlite", "__parts__/ItemStoreImplementation.cs", "public sealed class X;\n"));

        Assert.True(availability.IsAvailable(CatalogFields.Database, CatalogValue.OfText("sqlite")));
    }

    [Fact]
    public void Fragmento_sem_arquivo_nenhum_torna_o_valor_indisponivel()
    {
        // É o caso do diretório que só tem `.gitkeep` (R1.2): o `.csproj` já o exclui do
        // `EmbeddedResource`, então o fragmento chega aqui com ZERO arquivos. Nenhuma segunda
        // exclusão é escrita em C#.
        TemplateAvailability availability = Over(new FakeTemplateSource());

        Assert.False(availability.IsAvailable(CatalogFields.Architecture, CatalogValue.OfText("clean")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\n\n\n")]
    [InlineData("\r\n")]
    public void R1b_contribuicao_vazia_nao_torna_o_valor_disponivel(string content)
    {
        TemplateAvailability availability = Over(new FakeTemplateSource()
            .With("auth/jwt", "__parts__/ProgramServices.cs", content));

        Assert.False(availability.IsAvailable(CatalogFields.Authentication, CatalogValue.OfText("jwt")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\n\n\n")]
    public void R11_o_vazio_da_derivacao_e_o_vazio_do_motor(string content)
    {
        // A derivação e a composição perguntam à MESMA função. Duas noções de vazio seriam duas
        // respostas para "este fragmento contribui?", e a divergência apareceria como uma opção
        // marcada disponível que gera arquivo com buraco dentro.
        Assert.False(TemplateContributions.Contributes(content));

        Assert.False(Over(new FakeTemplateSource().With("auth/jwt", "__parts__/X.cs", content))
            .IsAvailable(CatalogFields.Authentication, CatalogValue.OfText("jwt")));
    }

    [Fact]
    public void R11_contribuicao_com_conteudo_conta_para_os_dois()
    {
        Assert.True(TemplateContributions.Contributes("services.AddAuthentication();\n"));

        Assert.True(Over(new FakeTemplateSource()
                .With("auth/jwt", "__parts__/X.cs", "services.AddAuthentication();\n"))
            .IsAvailable(CatalogFields.Authentication, CatalogValue.OfText("jwt")));
    }

    [Fact]
    public void R2_swagger_desligado_esta_disponivel_no_repositorio_vazio()
    {
        // O erro mais fácil desta tarefa, e o mais caro: `swagger = false` é a AUSÊNCIA do
        // fragmento (RF-20), não um `swagger/disabled` que ninguém escreveu. Lê-lo como template
        // faltando recusaria METADE da matriz — inclusive as duas combinações que hoje funcionam
        // de ponta a ponta.
        TemplateAvailability availability = Over(new FakeTemplateSource());

        Assert.True(availability.IsAvailable(CatalogFields.Swagger, CatalogValue.OfFlag(false)));

        // E a contraprova, no mesmo repositório: a posição LIGADA depende de fragmento e está
        // indisponível. Sem esta linha o teste acima passaria com uma derivação que dissesse
        // "disponível" para tudo.
        Assert.False(availability.IsAvailable(CatalogFields.Swagger, CatalogValue.OfFlag(true)));
    }

    [Fact]
    public void R2_swagger_desligado_esta_disponivel_no_repositorio_de_producao()
    {
        // A mesma afirmação contra os templates de verdade, que é onde ela protege a matriz.
        Assert.True(
            TemplateAvailability.Current.IsAvailable(
                CatalogFields.Swagger,
                CatalogValue.OfFlag(false)));

        Assert.DoesNotContain(
            TemplateAvailability.Current.Unavailable,
            option => option.Field == CatalogFields.Swagger && !option.Value.Flag);
    }

    [Fact]
    public void R2_a_combinacao_que_funciona_ponta_a_ponta_continua_disponivel_com_swagger_desligado()
    {
        // O efeito concreto de R2 sobre a matriz: `simple` + `none` + `none` + `swagger: false` é
        // uma das duas combinações verificadas ponta a ponta em T03. Se ela deixar de estar
        // disponível, a derivação quebrou a metade da matriz que R2 existe para proteger.
        GenerationRequest request =
            new("Acme.Billing.Api", "simple", "none", "none", false, "net10.0");

        Assert.Empty(TemplateAvailability.Current.UnavailableFields(request));
    }

    [Fact]
    public void R2_campo_sem_eixo_de_fragmento_esta_sempre_disponivel()
    {
        // `dotnetVersion` entra no pacote por marcador de valor, não por seleção de arquivos:
        // não há diretório `dotnetVersion/net10.0` e nunca houve.
        Assert.Null(TemplateAxes.FragmentFor(CatalogFields.DotnetVersion, CatalogValue.OfText("net10.0")));

        Assert.True(Over(new FakeTemplateSource())
            .IsAvailable(CatalogFields.DotnetVersion, CatalogValue.OfText("net10.0")));
    }

    [Fact]
    public void R3_a_combinacao_esta_disponivel_quando_todos_os_valores_que_ela_seleciona_estao()
    {
        TemplateAvailability availability = Over(Complete());

        GenerationRequest request =
            new("Acme.Billing.Api", "clean", "postgresql", "jwt", true, "net10.0");

        Assert.Empty(availability.UnavailableFields(request));
        Assert.Empty(availability.Unavailable);
    }

    [Fact]
    public void R3_um_unico_valor_indisponivel_derruba_a_combinacao_e_o_campo_e_nomeado()
    {
        TemplateAvailability availability = Over(Complete("database/postgresql"));

        GenerationRequest request =
            new("Acme.Billing.Api", "clean", "postgresql", "jwt", true, "net10.0");

        Assert.Equal<string>([CatalogFields.Database], availability.UnavailableFields(request));
    }

    [Fact]
    public void R3_dois_campos_indisponiveis_saem_na_ordem_do_catalogo()
    {
        TemplateAvailability availability = Over(Complete("auth/jwt", "architecture/clean"));

        GenerationRequest request =
            new("Acme.Billing.Api", "clean", "postgresql", "jwt", true, "net10.0");

        // `architecture` antes de `authentication`: a ordem dos campos no catálogo, não a ordem em
        // que a derivação os encontrou.
        Assert.Equal<string>(
            [CatalogFields.Architecture, CatalogFields.Authentication],
            availability.UnavailableFields(request));
    }

    [Fact]
    public void R3_common_nao_entra_na_conta_da_combinacao()
    {
        // `common` não é valor de campo: ele não aparece em `unavailable` nem derruba combinação.
        // Um `common` vazio é outro problema — e é assunto do assert de sanidade da camada 1.
        TemplateAvailability availability = Over(Complete());

        Assert.DoesNotContain(
            availability.Unavailable,
            option => option.Field == TemplateAxes.Common);
    }

    [Fact]
    public void O_valor_indisponivel_sai_no_tipo_do_campo()
    {
        // Texto no campo de escolha, booleano no interruptor: é o que permite ao frontend casar
        // `field`/`value` contra o que o próprio catálogo lhe entregou.
        TemplateAvailability availability = Over(new FakeTemplateSource());

        UnavailableOption architecture = Assert.Single(
            availability.Unavailable,
            option => option.Field == CatalogFields.Architecture && option.Value.Text == "simple");

        Assert.True(architecture.Value.IsText);

        UnavailableOption swagger = Assert.Single(
            availability.Unavailable,
            option => option.Field == CatalogFields.Swagger);

        Assert.False(swagger.Value.IsText);
        Assert.True(swagger.Value.Flag);
    }

    [Fact]
    public void A_razao_e_uma_frase_so_igual_para_todas()
    {
        TemplateAvailability availability = Over(new FakeTemplateSource());

        Assert.NotEmpty(availability.Unavailable);

        Assert.All(
            availability.Unavailable,
            option => Assert.Equal(TemplateAvailability.UnavailableReason, option.Reason));
    }

    [Fact]
    public void A_lista_sai_na_ordem_do_catalogo()
    {
        TemplateAvailability availability = Over(new FakeTemplateSource());

        // Ordem dos campos no catálogo e, dentro de cada campo, ordem dos valores no catálogo.
        (string Field, string? Text)[] expected =
        [
            (CatalogFields.Architecture, "simple"),
            (CatalogFields.Architecture, "clean"),
            (CatalogFields.Database, "none"),
            (CatalogFields.Database, "sqlite"),
            (CatalogFields.Database, "postgresql"),
            (CatalogFields.Authentication, "none"),
            (CatalogFields.Authentication, "identity"),
            (CatalogFields.Authentication, "jwt"),
            (CatalogFields.Swagger, null),
        ];

        (string Field, string? Text)[] actual =
        [
            .. availability.Unavailable.Select(option => (option.Field, option.Value.Text)),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Escrever_o_fragmento_acende_o_valor_sozinho()
    {
        // A propriedade que torna esta derivação diferente de um comentário: nenhuma edição de
        // catálogo, nenhuma linha de C#, nenhuma chance de alguém esquecer.
        CatalogValue clean = CatalogValue.OfText("clean");

        Assert.False(Over(new FakeTemplateSource()).IsAvailable(CatalogFields.Architecture, clean));

        Assert.True(
            Over(new FakeTemplateSource().With("architecture/clean", "src/Domain/Item.cs"))
                .IsAvailable(CatalogFields.Architecture, clean));
    }

    [Fact]
    public void Nada_aqui_conhece_o_nome_de_um_valor_o_repositorio_e_quem_decide()
    {
        // R4, como propriedade verificável: a derivação não tem lista do que está implementado.
        // Um repositório completo não devolve NENHUM indisponível — o que é impossível para uma
        // implementação que carregasse constantes de valor.
        Assert.Empty(Over(Complete()).Unavailable);

        // E o repositório vazio devolve todos, menos os dois casos de R2.
        int values = _catalog.Fields.Values.Sum(field => field.IsToggle ? 2 : field.Values!.Count);

        // Tudo indisponível, exceto: a posição desligada do interruptor e os valores de
        // `dotnetVersion`, que não têm eixo.
        int withoutFragment = 1 + _catalog.Fields[CatalogFields.DotnetVersion].Values!.Count;

        Assert.Equal(values - withoutFragment, Over(new FakeTemplateSource()).Unavailable.Count);
    }

    [Fact]
    public void Um_valor_fora_do_catalogo_nao_e_chamado_de_indisponivel()
    {
        // A ordem protege isto no endpoint, mas a derivação também não mente sozinha: quem não
        // pertence ao catálogo é assunto da validação, que responde 400 e roda antes.
        Assert.True(
            Over(new FakeTemplateSource())
                .IsAvailable(CatalogFields.Architecture, CatalogValue.OfText("banana")));
    }

    [Fact]
    public void Nenhum_gitkeep_chega_a_derivacao_no_repositorio_de_producao()
    {
        // R1.2: a exclusão mora no `.csproj`, e é por isso que não há segunda exclusão em C#.
        Assert.DoesNotContain(
            EmbeddedTemplateSource.Default.ResourcePaths,
            path => path.EndsWith(".gitkeep", StringComparison.Ordinal));
    }
}
