using System.Text.RegularExpressions;
using System.Xml.Linq;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// A guarda da <strong>regra 7b</strong> de ADR-0011 — quem escreve template não envolve o
/// marcador de contribuição em <c>&lt;ItemGroup&gt;</c>, <c>{ }</c>, uma cerca Markdown ou
/// qualquer outro par de abre-fecha, porque o motor apaga a linha do marcador e o invólucro vazio
/// sobreviveria — e a guarda da regra de T04 de docs/architecture/generation-engine.md:
/// <strong>nenhum comentário de template cita um marcador pelo nome</strong>.
/// </summary>
/// <remarks>
/// <para>
/// A 7b nasceu em T03 a partir de um <c>&lt;ItemGroup&gt;&lt;/ItemGroup&gt;</c> real, e nasceu
/// <strong>sem verificador</strong>: o motor não tem como detectá-la, porque um invólucro é só
/// texto e ADR-0011, item 9, proíbe o motor de interpretar texto. Uma regra que vive de revisão
/// humana de diff é uma regra que o primeiro fragmento distraído quebra em silêncio — o mesmo
/// padrão que ADR-0010 registrou três vezes e ADR-0008 uma.
/// </para>
/// <para><strong>São quatro guardas, e cada uma diz até onde alcança:</strong></para>
/// <list type="number">
///   <item><description>
///   <see cref="Nenhum_csproj_gerado_traz_grupo_vazio"/> — no <strong>pacote</strong>, o sintoma
///   exato, lido como XML. Sem heurística: um grupo do MSBuild sem filho é lixo, sempre.
///   </description></item>
///   <item><description>
///   <see cref="Nenhum_arquivo_gerado_traz_bloco_de_linhas_em_branco"/> — no <strong>pacote</strong>,
///   a outra metade do item 7 ("nem uma linha em branco órfã"), em qualquer formato, sem parser.
///   </description></item>
///   <item><description>
///   <see cref="Nenhum_marcador_de_contribuicao_esta_envolvido_no_template"/> — no
///   <strong>template</strong>, que é onde a regra 7b de fato mora. O alcance dela é
///   <strong>exatamente</strong> o que <see cref="Opens"/> e <see cref="Closes"/> reconhecem — ver
///   os limites abaixo.
///   </description></item>
///   <item><description>
///   <see cref="Nenhum_comentario_de_template_cita_marcador_pelo_nome"/> — no
///   <strong>template</strong>, a regra de T04. É a guarda que pega o defeito <em>antes</em> de
///   ele virar pacote; a rede geral do lado do pacote é
///   <c>GeneratedFileIntegrityMatrixTests.Todo_arquivo_gerado_de_extensao_XML_e_XML_valido</c>.
///   </description></item>
/// </list>
/// <para>
/// <strong>O que a guarda 3 passou a alcançar em T04</strong>, e que era limite declarado em T03:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <strong>A cerca Markdown</strong> (<c>```</c>). <c>Opens("```bash")</c> e <c>Closes("```")</c>
///   agora respondem <c>true</c>. Era o buraco mais caro dos três, porque <c>.md</c> é justamente
///   o formato que mais hospeda marcador: um marcador posto <em>dentro</em> de uma cerca deixaria,
///   sem contribuição, uma cerca vazia no <c>README.md</c> — e uma cerca vazia não deixa linha em
///   branco nem elemento XML, então escapava também das guardas 1 e 2.
///   <strong>Não há violação hoje:</strong> nos dois <c>README.md</c> os marcadores estão
///   <em>entre</em> cercas, nunca dentro. O que faltava era a guarda, não o conserto.
///   </description></item>
///   <item><description>
///   <strong>Invólucro separado do marcador por um comentário.</strong>
///   <see cref="PreviousNonBlank"/> e <see cref="NextNonBlank"/> agora pulam também comentário —
///   XML de uma ou de várias linhas, e <c>//</c>. Com
///   <c>&lt;ItemGroup&gt;</c> / <c>&lt;!-- … --&gt;</c> / marcador / <c>&lt;/ItemGroup&gt;</c>, o
///   "anterior" deixou de ser o comentário e o grupo vazio passa a ser acusado.
///   </description></item>
/// </list>
/// <para>
/// <strong>O QUE A GUARDA 3 CONTINUA NÃO ALCANÇANDO</strong>, e é limite declarado, não descuido:
/// </para>
/// <list type="bullet">
///   <item><description>
///   os outros invólucros do Markdown — a citação <c>&gt;</c>, o item de lista, a tabela —, porque
///   nenhum deles tem forma de par abre-fecha em linha própria;
///   </description></item>
///   <item><description>
///   o invólucro de mais de uma linha, como uma tag XML de abertura quebrada em várias linhas de
///   atributos;
///   </description></item>
///   <item><description>
///   <strong>o invólucro <em>inline</em></strong> — <c>&lt;ItemGroup&gt;__Marcador__&lt;/ItemGroup&gt;</c>
///   na mesma linha. A guarda só reconhece o marcador <strong>sozinho na linha</strong>, e nessa
///   forma ele não está: ela nem chega a perguntar o que vem antes e depois. Apontado pelo
///   <c>reviewer</c> em T04, e <strong>declarado em vez de fechado</strong> — a razão está no
///   parágrafo seguinte.
///   </description></item>
/// </list>
/// <para>
/// <strong>Por que o inline fica declarado e não fechado.</strong> Fechá-lo exigiria reconhecer
/// marcador <em>dentro</em> de uma linha e decidir se o resto da linha é invólucro — isto é,
/// interpretar conteúdo de elemento, que é onde o reconhecedor de linha vira parser, e aí ele
/// passa a acusar também o item 8 de ADR-0011 (<c>__ApiProjectDir__</c> no meio de um caminho),
/// que é uso legítimo e frequente. O que decide é que <strong>essa forma não tem buraco</strong>:
/// ela produz o <c>&lt;ItemGroup&gt;&lt;/ItemGroup&gt;</c> vazio literal, e a guarda 1 o lê como
/// XML no pacote, sem heurística nenhuma. É a única das quatro formas listadas aqui que tem
/// verificador — as outras três escapam das quatro guardas, e é por isso que elas são o limite que
/// custa. <see cref="O_involucro_inline_escapa_da_guarda_de_template_e_cai_na_guarda_do_pacote"/>
/// mantém as duas metades dessa frase executáveis.
/// </para>
/// <para>
/// <strong>E ela acusa o marcador que está SOZINHO dentro do invólucro</strong>, que é a condição
/// exata do defeito: é só nesse caso que o invólucro sobrevive <em>vazio</em> quando ninguém
/// contribui. Um marcador dentro de uma cerca que também tem outras linhas não é violação da
/// regra 7b — a cerca continua com conteúdo — e não ser acusado ali é acerto, não buraco.
/// </para>
/// <para>
/// Nada disso é acidente: a guarda 3 é um reconhecedor de <em>linha</em>, não um parser. Escrever
/// um analisador de blocos por formato seria trazer para o verificador a linguagem de template que
/// ADR-0011, item 9, recusa no motor. Mas <strong>o limite precisa estar escrito</strong>:
/// ADR-0008 e ADR-0010 registram que herdar uma permissão que a pessoa <em>acha</em> vigiada é
/// pior que herdar uma que ela sabe que precisa conferir.
/// </para>
/// </remarks>
public sealed partial class EmptyWrapperMatrixTests
{
    /// <summary>
    /// Os elementos do MSBuild cujo significado inteiro são os filhos. Um deles sem filho é peso
    /// morto em qualquer projeto, gerado ou escrito à mão — não existe forma legítima de um
    /// <c>&lt;ItemGroup&gt;</c> vazio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A lista é por <strong>nome</strong>, e tinha de ser: não há diferença sintática entre
    /// <c>&lt;PackageReference Include="X" /&gt;</c>, que carrega todo o sentido nos atributos e é
    /// legítimo vazio, e <c>&lt;ItemGroup Label="X"&gt;&lt;/ItemGroup&gt;</c>, que é lixo. A
    /// diferença é semântica, então a regra é semântica.
    /// </para>
    /// <para>
    /// <c>Target</c> e <c>UsingTask</c> ficam <strong>de fora de propósito</strong>: um
    /// <c>&lt;Target Name="X" /&gt;</c> vazio é um gancho de ordenação legítimo, e um teste que
    /// acusa código legítimo é desligado pela próxima pessoa — e aí não protege mais nada.
    /// Acrescentar um invólucro novo a um template significa acrescentá-lo aqui.
    /// </para>
    /// </remarks>
    private static readonly string[] _msbuildContainers =
    [
        "Project",
        "ItemGroup",
        "PropertyGroup",
        "ItemDefinitionGroup",
        "ImportGroup",
        "Choose",
        "When",
        "Otherwise",
    ];

    /// <summary>
    /// Os marcadores de <strong>valor</strong> (docs/architecture/generation-engine.md). Entram na
    /// guarda 4 junto com os de contribuição: a regra de T04 é sobre marcador, e o motor substitui
    /// os dois tipos em qualquer posição do texto. Um <c>__ProjectName__</c> dentro de um
    /// comentário não quebra XML, mas transforma a explicação numa afirmação sobre um projeto — e a
    /// regra existe justamente para não ter de julgar caso a caso qual substituição é inofensiva.
    /// </summary>
    private static readonly string[] _valueMarkers =
    [
        TemplateTokens.ProjectName,
        TemplateTokens.TargetFramework,
        TemplateTokens.TemplateVersion,
    ];

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhum_csproj_gerado_traz_grupo_vazio(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in ProjectFiles(package))
        {
            XElement root = XDocument.Parse(package.Read(path)).Root!;

            string[] empty =
            [
                .. root
                    .DescendantsAndSelf()
                    .Where(element => _msbuildContainers.Contains(
                        element.Name.LocalName,
                        StringComparer.Ordinal))
                    .Where(element => !element.Elements().Any())
                    .Select(element => element.Name.LocalName)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ];

            Assert.True(
                empty.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' traz " +
                $"{string.Join(", ", empty.Select(name => $"<{name}></{name}>"))} sem nada " +
                "dentro. É a regra 7b de ADR-0011: o invólucro pertence à contribuição, não ao " +
                "arquivo hospedeiro — quando ninguém contribui, ele tem de sumir junto com o " +
                "marcador. Mova o par abre-fecha para dentro de cada arquivo de '__parts__/'.");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhum_arquivo_gerado_traz_bloco_de_linhas_em_branco(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // A outra metade do item 7: "nem uma linha em branco órfã". Duas linhas em branco seguidas
        // num arquivo gerado são o rastro típico de um marcador que sumiu e deixou o espaçamento
        // dele para trás. Não depende de formato e não precisa de parser — é a única afirmação
        // sobre Markdown, `.http` e C# que esta camada consegue fazer com honestidade.
        //
        // O limiar é 2 porque hoje o pacote inteiro não tem nenhuma sequência maior que 1, nos
        // dois estados de Swagger: a margem é real, não inventada.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in package.Paths)
        {
            Match match = BlankRun().Match(package.Read(path));

            Assert.True(
                !match.Success,
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' tem duas ou mais linhas " +
                "em branco seguidas. ADR-0011, item 7, promete que a linha do marcador some " +
                "inteira, sem deixar linha órfã — confira se algum '__parts__/' contribuiu vazio " +
                "ou se o template deixou espaçamento em volta do marcador.");
        }
    }

    [Fact]
    public void Nenhum_marcador_de_contribuicao_esta_envolvido_no_template()
    {
        // A regra 7b onde ela mora: no template. Varre todo arquivo hospedeiro, qualquer que seja
        // a extensão — mas só acusa os invólucros que `Opens`/`Closes` reconhecem: XML, as chaves
        // de C#/JSON e, desde T04, a cerca Markdown. Ver a lista de limites no `<remarks>`.
        TemplateInventory inventory = TemplateInventory.Read();

        List<string> violations = [];

        foreach (TemplateHost host in inventory.Hosts)
        {
            string[] lines = Lines(host.Content);

            for (int index = 0; index < lines.Length; index++)
            {
                string marker = lines[index].Trim();

                // Só o marcador SOZINHO na linha. Um marcador no meio de uma linha é a forma do
                // item 8 (`__ApiProjectDir__`), em que "envolver" não quer dizer nada.
                if (!inventory.Markers.Contains(marker, StringComparer.Ordinal))
                {
                    continue;
                }

                string? before = PreviousNonBlank(lines, index);
                string? after = NextNonBlank(lines, index);

                if (before is not null && after is not null && Opens(before) && Closes(after))
                {
                    violations.Add(
                        $"{host.Path}:{index + 1} — '{marker}' está entre '{before.Trim()}' e " +
                        $"'{after.Trim()}'");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Marcador de contribuição envolvido por um par abre-fecha do arquivo hospedeiro " +
            $"(ADR-0011, regra 7b):{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", violations) + Environment.NewLine +
            "Quando nenhum fragmento contribui, o motor apaga a linha do marcador e esse " +
            "invólucro sobra vazio no pacote. Mova o par para dentro de cada arquivo de " +
            "'__parts__/' — cada contribuição traz o próprio invólucro completo.");
    }

    [Fact]
    public void Nenhum_comentario_de_template_cita_marcador_pelo_nome()
    {
        // A regra de T04, achada ao escrever a Clean e achada EXECUTANDO: o motor substitui
        // marcador em qualquer posição do texto — ele não sabe o que é comentário, porque
        // ADR-0011, item 9, é a decisão de o motor não interpretar nada. Um
        // `<!-- … __ApiPackageReferences__ … -->` no `.csproj` do Domain virou o `<ItemGroup>`
        // inteiro do Swagger DENTRO do comentário, e o projeto gerado passou a falhar com
        // MSB4025. Só com `swagger = true`: com ele desmarcado o marcador resolvia vazio e o
        // comentário continuava um comentário.
        //
        // O ALCANCE, escrito onde o reconhecedor mora: comentário XML, aberto e fechado na mesma
        // linha ou atravessando várias. Comentário de C# (`//`, `/* */`), de Markdown e de outros
        // formatos ficam DE FORA — não porque a regra não valha para eles (ela vale para todo
        // formato), mas porque este reconhecedor não os lê. Para esses, o que existe é a rede do
        // lado do pacote, e ela só cobre a família XML.
        TemplateInventory inventory = TemplateInventory.Read();

        string[] known = [.. inventory.Markers, .. _valueMarkers];

        List<string> violations = [];

        foreach (TemplateHost host in inventory.Hosts)
        {
            violations.AddRange(MarkersInsideXmlComments(host, known));
        }

        Assert.True(
            violations.Count == 0,
            "Comentário de template citando marcador pelo nome " +
            "(docs/architecture/generation-engine.md, 'Nenhum comentário de template cita um " +
            $"marcador pelo nome'):{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", violations) + Environment.NewLine +
            "O motor substitui o marcador ali dentro, e a contribuição injetada quebra o " +
            "comentário — no XML isso é MSB4025, e no '//' de C# vira código comentado. Para " +
            "falar de um marcador, descreva-o ('a contribuição de PackageReferences') ou quebre a " +
            "forma dele ('__ ApiPackageReferences __').");
    }

    [Fact]
    public void O_reconhecedor_sabe_distinguir_involucro_de_linha_inocente()
    {
        // A decisão que `Opens` e `Closes` tomam, executável — pelo mesmo motivo que
        // `project-structure.spec.ts` executa o reconhecedor de vazamento dele: um reconhecedor em
        // que ninguém mexeu é uma hipótese, e a próxima pessoa que acrescentar um formato precisa
        // de onde reatacar.

        // Abre: as três chaves de bloco, a tag XML de abertura e a cerca Markdown.
        Assert.True(Opens("  <ItemGroup>"));
        Assert.True(Opens("  <ItemGroup Label=\"Swagger\">"));
        Assert.True(Opens("  \"Logging\": {"));
        Assert.True(Opens("{"));
        Assert.True(Opens("  builder.Services.Configure(options => {"));
        Assert.True(Opens("  \"itens\": ["));
        Assert.True(Opens("```bash"));
        Assert.True(Opens("```"));

        // Não abre: tag que se fecha sozinha, tag de fechamento, comentário, instrução terminada.
        Assert.False(Opens("  <PackageReference Include=\"X\" Version=\"1.0.0\" />"));
        Assert.False(Opens("  </PropertyGroup>"));
        Assert.False(Opens("  -->"));
        Assert.False(Opens("<!-- comentário -->"));
        Assert.False(Opens("<?xml version=\"1.0\"?>"));
        Assert.False(Opens("app.UseStatusCodePages();"));
        Assert.False(Opens("namespace Acme.Billing.Persistence;"));
        Assert.False(Opens("`código inline`"));
        Assert.False(Opens(string.Empty));

        // Fecha: as três chaves, a tag XML de fechamento e a cerca que fecha — que, ao contrário
        // da que abre, NUNCA carrega linguagem depois das crases. É o que distingue as duas pontas
        // de um par em Markdown, onde o mesmo símbolo faz os dois papéis.
        Assert.True(Closes("  </ItemGroup>"));
        Assert.True(Closes("}"));
        Assert.True(Closes("  ]"));
        Assert.True(Closes("  );"));
        Assert.True(Closes("```"));

        // Não fecha: qualquer outra coisa.
        Assert.False(Closes("## Executar"));
        Assert.False(Closes("dotnet restore"));
        Assert.False(Closes("  <ItemGroup>"));
        Assert.False(Closes("```bash"));
        Assert.False(Closes(string.Empty));

        // E o par inteiro, que é o que a guarda pergunta: a forma exata do defeito de T03, e a
        // forma que a cerca Markdown lhe dá.
        Assert.True(Opens("  <ItemGroup>") && Closes("  </ItemGroup>"));
        Assert.True(Opens("```bash") && Closes("```"));
        Assert.False(Opens("  -->") && Closes("</Project>"));
    }

    [Fact]
    public void O_reconhecedor_pula_comentario_ao_procurar_o_involucro()
    {
        // O segundo limite que T04 fechou. A guarda olha a linha não-vazia imediatamente anterior
        // e a seguinte; com `<ItemGroup>` / `<!-- … -->` / marcador / `</ItemGroup>`, o "anterior"
        // era o comentário, `Opens` devolvia false e o grupo vazio passava. Agora o comentário é
        // pulado, e o par volta a ser visível.
        string[] oneLine =
        [
            "  <ItemGroup>",
            "    <!-- a contribuição de PackageReferences entra aqui -->",
            "__ApiPackageReferences__",
            "  </ItemGroup>",
        ];

        Assert.Equal("  <ItemGroup>", PreviousNonBlank(oneLine, 2));
        Assert.Equal("  </ItemGroup>", NextNonBlank(oneLine, 2));

        string[] manyLines =
        [
            "  <ItemGroup>",
            "    <!--",
            "      um comentário de várias linhas",
            "    -->",
            "",
            "__ApiPackageReferences__",
            "    <!-- e outro depois -->",
            "  </ItemGroup>",
        ];

        Assert.Equal("  <ItemGroup>", PreviousNonBlank(manyLines, 5));
        Assert.Equal("  </ItemGroup>", NextNonBlank(manyLines, 5));

        // E a contraprova: pular comentário não pode fazer a busca atravessar código de verdade.
        string[] innocent =
        [
            "  <PropertyGroup>",
            "    <Nullable>enable</Nullable>",
            "  </PropertyGroup>",
            "__ApiPackageReferences__",
            "",
            "</Project>",
        ];

        Assert.Equal("  </PropertyGroup>", PreviousNonBlank(innocent, 3));
    }

    [Fact]
    public void O_involucro_inline_escapa_da_guarda_de_template_e_cai_na_guarda_do_pacote()
    {
        // O limite que o `reviewer` de T04 apontou, executável — as duas metades dele.
        //
        // PRIMEIRA METADE: a guarda 3 não vê `<ItemGroup>__Marcador__</ItemGroup>`, porque ela só
        // reconhece o marcador SOZINHO na linha. Afirmar isso por teste é o que impede o limite de
        // virar folclore: se alguém fechar o inline um dia, este nome cai e a lista de limites do
        // `<remarks>` é corrigida junto, em vez de continuar prometendo um buraco que não existe
        // mais.
        const string inline = "  <ItemGroup>__ApiPackageReferences__</ItemGroup>";

        Assert.DoesNotContain(inline.Trim(), (string[])["__ApiPackageReferences__"]);
        Assert.False(Opens(inline));

        // SEGUNDA METADE, e é ela que faz o limite ser aceitável em vez de dívida: a forma inline
        // produz o `<ItemGroup></ItemGroup>` literal quando ninguém contribui, e a guarda 1 lê
        // isso como XML, sem heurística. O par abaixo é exatamente o que a guarda 1 pergunta.
        XElement empty = XDocument.Parse("<Project><ItemGroup></ItemGroup></Project>").Root!;

        Assert.Contains(
            empty.DescendantsAndSelf(),
            element => _msbuildContainers.Contains(element.Name.LocalName, StringComparer.Ordinal)
                && !element.Elements().Any());

        // E a contraprova, para a guarda 1 não estar acusando qualquer coisa: um grupo COM filho
        // não é lixo.
        XElement filled = XDocument.Parse(
            "<Project><ItemGroup><PackageReference Include=\"X\" /></ItemGroup></Project>").Root!;

        Assert.DoesNotContain(
            filled.DescendantsAndSelf(),
            element => element.Name.LocalName.Equals("ItemGroup", StringComparison.Ordinal)
                && !element.Elements().Any());
    }

    [Fact]
    public void O_reconhecedor_de_comentario_XML_acha_o_marcador_dentro_e_ignora_o_de_fora()
    {
        // A decisão da guarda 4, executável. O caso real de T04 é o primeiro; os outros são as
        // bordas que separam "dentro do comentário" de "fora dele".
        TemplateHost dentroDeUmaLinha = new(
            "exemplo.csproj",
            "<!-- as dependências entram em __ApiPackageReferences__ -->\n");

        Assert.NotEmpty(MarkersInsideXmlComments(dentroDeUmaLinha, ["__ApiPackageReferences__"]));

        TemplateHost dentroDeVariasLinhas = new(
            "exemplo.csproj",
            "<!--\n  e aqui __ApiPackageReferences__ também\n-->\n");

        Assert.NotEmpty(
            MarkersInsideXmlComments(dentroDeVariasLinhas, ["__ApiPackageReferences__"]));

        TemplateHost fora = new("exemplo.csproj", "<!-- nada aqui -->\n__ApiPackageReferences__\n");

        Assert.Empty(MarkersInsideXmlComments(fora, ["__ApiPackageReferences__"]));

        // Depois do fechamento, na MESMA linha: é fora, e confundir isto acusaria template limpo.
        TemplateHost depoisDoFechamento = new(
            "exemplo.csproj",
            "<!-- nada --> __ApiPackageReferences__\n");

        Assert.Empty(MarkersInsideXmlComments(depoisDoFechamento, ["__ApiPackageReferences__"]));
    }

    [Fact]
    public async Task A_varredura_encontrou_o_que_inspecionar()
    {
        // Assert de sanidade, obrigatório — ADR-0011 já exige um para a classe de
        // PackageReferenceMatrixTests, e esta classe precisa do seu pelo mesmo motivo, só que mais
        // forte: "nenhum invólucro vazio" é verdade por vacuidade num pacote sem `.csproj`, e
        // ADR-0012 faz a maioria das combinações não produzir pacote nenhum.
        //
        // Então não basta contar arquivos. É preciso mostrar o par que prova que a regra 7b está
        // sendo exercida nos dois estados: uma combinação em que o grupo APARECE porque alguém
        // contribuiu, e outra em que ele SOME porque ninguém contribuiu.
        int projects = 0;
        int containers = 0;
        int withItemGroup = 0;
        int withoutAnyItemGroup = 0;

        foreach (GenerationRequest request in Combinations.Available)
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(
                request,
                TestContext.Current.CancellationToken);

            foreach (string path in ProjectFiles(package))
            {
                projects++;

                XElement root = XDocument.Parse(package.Read(path)).Root!;

                int groups = root
                    .Descendants()
                    .Count(element =>
                        element.Name.LocalName.Equals("ItemGroup", StringComparison.Ordinal));

                containers += root
                    .DescendantsAndSelf()
                    .Count(element => _msbuildContainers.Contains(
                        element.Name.LocalName,
                        StringComparer.Ordinal));

                if (groups > 0)
                {
                    withItemGroup++;
                }
                else
                {
                    withoutAnyItemGroup++;
                }
            }
        }

        Assert.True(
            projects > 0,
            "Nenhum '.csproj' em nenhuma combinação da matriz. Enquanto isso for verdade, " +
            "'nenhum grupo vazio' é verdade por vacuidade e esta classe não protege nada.");

        Assert.True(
            containers > 0,
            $"Os {projects} '.csproj' encontrados não têm um único elemento de invólucro para " +
            "inspecionar. Ou a lista '_msbuildContainers' ficou desalinhada do que os templates " +
            "escrevem, ou a geração parou de emitir projeto de verdade — nos dois casos, esta " +
            "classe precisa ser reescrita antes de voltar a ser confiável.");

        Assert.True(
            withItemGroup > 0,
            "Nenhum '.csproj' da matriz tem um '<ItemGroup>'. Sem esse lado, 'nenhum grupo vazio' " +
            "passaria pelo motivo errado: não porque o invólucro acompanha a contribuição, mas " +
            "porque invólucro nenhum é gerado.");

        Assert.True(
            withoutAnyItemGroup > 0,
            "Todo '.csproj' da matriz tem pelo menos um '<ItemGroup>'. Sem esse lado, a regra 7b " +
            "não está sendo exercida: é justamente a combinação em que NINGUÉM contribui que " +
            "revela se o invólucro some junto com o marcador.");

        // E as guardas do lado do template, pelo mesmo raciocínio.
        TemplateInventory inventory = TemplateInventory.Read();

        Assert.True(
            inventory.Markers.Count > 0,
            "Nenhum marcador de contribuição foi encontrado em '__parts__/'. " +
            $"'{nameof(Nenhum_marcador_de_contribuicao_esta_envolvido_no_template)}' estaria " +
            "varrendo template nenhum e passaria sozinho para sempre.");

        Assert.True(
            inventory.Hosts.Count > 0,
            "Nenhum arquivo de template hospeda marcador de contribuição, embora existam " +
            $"{inventory.Markers.Count}. A varredura de arquivos hospedeiros quebrou.");

        Assert.True(
            inventory.Hosts.Any(host => host.Content.Contains("<!--", StringComparison.Ordinal)),
            "Nenhum arquivo hospedeiro tem comentário XML. " +
            $"'{nameof(Nenhum_comentario_de_template_cita_marcador_pelo_nome)}' não teria um " +
            "único comentário para inspecionar e passaria por vacuidade.");

        Assert.True(
            inventory.Hosts.Any(host => host.Content.Contains("```", StringComparison.Ordinal)),
            "Nenhum arquivo hospedeiro tem cerca Markdown. O reconhecimento de cerca que T04 " +
            "acrescentou não teria onde ser exercido sobre template de verdade.");
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    private static IReadOnlyList<string> ProjectFiles(GeneratedPackage package) =>
        PackageLayout.ProjectFiles(package);

    private static string[] Lines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    /// <summary>
    /// Os marcadores de <paramref name="known"/> que aparecem <strong>dentro</strong> de um
    /// comentário XML de <paramref name="host"/>, com a linha em que estão.
    /// </summary>
    /// <remarks>
    /// Reconhecedor de linha com um estado só — "estou dentro de um comentário?" —, que é o que
    /// permite atravessar o comentário de várias linhas sem virar parser de XML. Ele não entende
    /// <c>CDATA</c> nem <c>&lt;!--</c> dentro de atributo; nenhum dos dois aparece em template de
    /// projeto, e o custo de errar para mais aqui é uma falha que se lê e se entende.
    /// </remarks>
    private static IReadOnlyList<string> MarkersInsideXmlComments(
        TemplateHost host,
        IReadOnlyList<string> known)
    {
        const string open = "<!--";
        const string close = "-->";

        List<string> found = [];
        string[] lines = Lines(host.Content);
        bool inside = false;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            int cursor = 0;

            while (cursor <= line.Length)
            {
                if (!inside)
                {
                    int start = line.IndexOf(open, cursor, StringComparison.Ordinal);

                    if (start < 0)
                    {
                        break;
                    }

                    inside = true;
                    cursor = start + open.Length;

                    continue;
                }

                int end = line.IndexOf(close, cursor, StringComparison.Ordinal);
                string region = end < 0 ? line[cursor..] : line[cursor..end];

                found.AddRange(known
                    .Where(marker => region.Contains(marker, StringComparison.Ordinal))
                    .Select(marker => $"{host.Path}:{index + 1} — '{marker}' em '{region.Trim()}'"));

                if (end < 0)
                {
                    break;
                }

                inside = false;
                cursor = end + close.Length;
            }
        }

        return found;
    }

    /// <summary>
    /// A linha anterior a <paramref name="index"/> que não é branca <strong>nem comentário</strong>.
    /// </summary>
    private static string? PreviousNonBlank(string[] lines, int index)
    {
        bool[] commentOnly = CommentOnlyLines(lines);

        for (int cursor = index - 1; cursor >= 0; cursor--)
        {
            if (Skippable(lines[cursor], commentOnly[cursor]))
            {
                continue;
            }

            return lines[cursor];
        }

        return null;
    }

    /// <summary>
    /// A linha seguinte a <paramref name="index"/> que não é branca <strong>nem comentário</strong>.
    /// </summary>
    private static string? NextNonBlank(string[] lines, int index)
    {
        bool[] commentOnly = CommentOnlyLines(lines);

        for (int cursor = index + 1; cursor < lines.Length; cursor++)
        {
            if (Skippable(lines[cursor], commentOnly[cursor]))
            {
                continue;
            }

            return lines[cursor];
        }

        return null;
    }

    /// <summary>
    /// Diz se a linha deve ser pulada na busca pelo invólucro: branca, comentário de <c>//</c>, ou
    /// linha cujo conteúdo inteiro está dentro de um comentário XML.
    /// </summary>
    private static bool Skippable(string line, bool commentOnly) =>
        line.Trim().Length == 0
        || commentOnly
        || line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Para cada linha, diz se <strong>todo</strong> o conteúdo dela está dentro de um comentário
    /// XML — a linha do <c>&lt;!--</c>, as do miolo e a do <c>--&gt;</c> incluídas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É uma passada única com um estado só, "estou dentro de um comentário?", exatamente como
    /// <see cref="MarkersInsideXmlComments"/>. A primeira versão desta guarda tentava responder a
    /// pergunta <em>por linha</em>, varrendo para cima ou para baixo à procura da ponta mais
    /// próxima — e errava: varrendo para trás, encontrar <c>&lt;!--</c> antes de <c>--&gt;</c>
    /// significa estar <em>dentro</em>, e a versão anterior lia isso ao contrário. Uma passada
    /// pelo arquivo inteiro não tem essa classe de engano, e a própria linha de delimitador cai no
    /// lugar certo sem caso especial.
    /// </para>
    /// <para>
    /// O que resta de fora, e é limite conhecido: <c>CDATA</c> e <c>&lt;!--</c> dentro de valor de
    /// atributo. Nenhum dos dois aparece em template de projeto.
    /// </para>
    /// </remarks>
    private static bool[] CommentOnlyLines(string[] lines)
    {
        const string open = "<!--";
        const string close = "-->";

        bool[] commentOnly = new bool[lines.Length];
        bool inside = false;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            bool hasCode = false;
            bool hasComment = inside;
            int cursor = 0;

            while (cursor < line.Length)
            {
                if (!inside)
                {
                    int start = line.IndexOf(open, cursor, StringComparison.Ordinal);

                    if (start < 0)
                    {
                        hasCode |= line[cursor..].Trim().Length > 0;

                        break;
                    }

                    hasCode |= line[cursor..start].Trim().Length > 0;
                    hasComment = true;
                    inside = true;
                    cursor = start + open.Length;

                    continue;
                }

                hasComment = true;

                int end = line.IndexOf(close, cursor, StringComparison.Ordinal);

                if (end < 0)
                {
                    break;
                }

                inside = false;
                cursor = end + close.Length;
            }

            commentOnly[index] = hasComment && !hasCode;
        }

        return commentOnly;
    }

    /// <summary>
    /// Diz se a linha <strong>abre</strong> um bloco: termina em <c>{</c>, <c>[</c> ou <c>(</c>, é
    /// uma tag XML de abertura, ou é uma cerca Markdown.
    /// </summary>
    private static bool Opens(string line)
    {
        string text = line.Trim();

        if (text.Length == 0)
        {
            return false;
        }

        if (text[^1] is '{' or '[' or '(')
        {
            return true;
        }

        if (Fence().IsMatch(text))
        {
            return true;
        }

        return XmlOpenTag().IsMatch(text);
    }

    /// <summary>
    /// Diz se a linha <strong>fecha</strong> um bloco: começa com <c>}</c>, <c>]</c> ou <c>)</c>,
    /// é uma tag XML de fechamento, ou é a cerca Markdown que fecha — só crases, sem linguagem.
    /// </summary>
    private static bool Closes(string line)
    {
        string text = line.Trim();

        if (text.Length == 0)
        {
            return false;
        }

        if (ClosingFence().IsMatch(text))
        {
            return true;
        }

        return text[0] is '}' or ']' or ')' || text.StartsWith("</", StringComparison.Ordinal);
    }

    /// <summary>Duas ou mais linhas em branco seguidas.</summary>
    [GeneratedRegex(@"\n[ \t]*\n[ \t]*\n", RegexOptions.CultureInvariant)]
    private static partial Regex BlankRun();

    /// <summary>
    /// Tag XML de abertura em linha própria: <c>&lt;Nome …&gt;</c>, sem ser fechamento
    /// (<c>&lt;/</c>), comentário ou instrução (<c>&lt;!</c>, <c>&lt;?</c>) nem tag que se fecha
    /// sozinha (<c>/&gt;</c>).
    /// </summary>
    [GeneratedRegex(@"^<[A-Za-z_][^>]*(?<!/)>$", RegexOptions.CultureInvariant)]
    private static partial Regex XmlOpenTag();

    /// <summary>Cerca Markdown: três ou mais crases, com ou sem linguagem depois.</summary>
    [GeneratedRegex(@"^`{3,}[A-Za-z0-9_+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Fence();

    /// <summary>Cerca Markdown de fechamento: só crases, nada depois.</summary>
    [GeneratedRegex(@"^`{3,}$", RegexOptions.CultureInvariant)]
    private static partial Regex ClosingFence();
}
