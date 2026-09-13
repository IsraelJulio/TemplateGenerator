using System.Text.RegularExpressions;
using System.Xml.Linq;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// A guarda da <strong>regra 7b</strong> de ADR-0011: quem escreve template não envolve o marcador
/// de contribuição em <c>&lt;ItemGroup&gt;</c>, <c>{ }</c> ou qualquer outro par de abre-fecha,
/// porque o motor apaga a linha do marcador e o invólucro vazio sobreviveria — entregue a um
/// humano dentro do pacote.
/// </summary>
/// <remarks>
/// <para>
/// A regra nasceu em T03 a partir de um <c>&lt;ItemGroup&gt;&lt;/ItemGroup&gt;</c> real, e nasceu
/// <strong>sem verificador</strong>: o motor não tem como detectá-la, porque um invólucro é só
/// texto e ADR-0011, item 9, proíbe o motor de interpretar texto. Uma regra que vive de revisão
/// humana de diff é uma regra que o primeiro fragmento distraído de T04 quebra em silêncio — o
/// mesmo padrão que ADR-0010 registrou três vezes e ADR-0008 uma.
/// </para>
/// <para><strong>São três guardas, e cada uma diz até onde alcança:</strong></para>
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
///   <strong>template</strong>, que é onde a regra 7b de fato mora, e a que responde ao ponto
///   levantado pelo papel <c>template-engineer</c>: sem ela, um invólucro novo em
///   <c>Program.cs</c> ou em <c>appsettings.json</c> não derrubaria nada até alguém abrir o ZIP.
///   O alcance dela é <strong>exatamente</strong> o que <see cref="Opens"/> e <see cref="Closes"/>
///   reconhecem — ver a lista de limites abaixo, que não é curta.
///   </description></item>
/// </list>
/// <para>
/// <strong>O QUE A GUARDA 3 NÃO ALCANÇA.</strong> Ela reconhece um par abre-fecha em uma forma só:
/// linha que termina em <c>{</c>, <c>[</c> ou <c>(</c>, ou é tag XML de abertura; seguida de linha
/// que começa em <c>}</c>, <c>]</c>, <c>)</c> ou <c>&lt;/</c>. Isso cobre XML (o <c>.csproj</c>) e
/// as chaves de C# e JSON. <strong>Não cobre:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>
///   <strong>Os invólucros próprios do Markdown</strong> — a cerca <c>```</c>, a citação
///   <c>&gt;</c>, o item de lista, a tabela. <c>Opens("```bash")</c> é <c>false</c> e
///   <c>Closes("```")</c> é <c>false</c>, e os dois estão afirmados assim em
///   <see cref="O_reconhecedor_sabe_distinguir_involucro_de_linha_inocente"/>. Um marcador posto
///   <em>dentro</em> de uma cerca não seria acusado, e sem contribuição sobraria uma cerca vazia no
///   <c>README.md</c> — o defeito literal da regra 7b, no formato que hoje mais hospeda marcador.
///   <strong>Hoje não há violação:</strong> em <c>architecture/simple/README.md</c> os dois
///   marcadores estão <em>entre</em> cercas, nunca dentro (as 16 linhas de <c>```</c> formam 8
///   pares balanceados). O que falta é a guarda, não o conserto.
///   </description></item>
///   <item><description>
///   <strong>Invólucro separado do marcador por um comentário.</strong> A guarda olha a linha
///   não-vazia imediatamente anterior e a seguinte. Com
///   <c>&lt;ItemGroup&gt;</c> / <c>&lt;!-- … --&gt;</c> / marcador / <c>&lt;/ItemGroup&gt;</c>, o
///   "anterior" é o comentário, <c>Opens</c> devolve <c>false</c> e o grupo vazio passa.
///   </description></item>
///   <item><description>
///   <strong>Invólucro de mais de uma linha</strong> — uma tag XML de abertura quebrada em várias
///   linhas de atributos, por exemplo.
///   </description></item>
/// </list>
/// <para>
/// Nada disso é acidente: a guarda 3 é um reconhecedor de <em>linha</em>, não um parser. Escrever
/// um analisador de blocos por formato seria trazer para o verificador a linguagem de template que
/// ADR-0011, item 9, recusa no motor. Mas <strong>o limite precisa estar escrito</strong>: ADR-0008
/// e ADR-0010 registram que herdar uma permissão que a pessoa <em>acha</em> vigiada é pior que
/// herdar uma que ela sabe que precisa conferir. As guardas 1 e 2 continuam olhando o pacote e
/// pegam o sintoma de saída onde ele deixa rastro — mas uma cerca Markdown vazia não deixa linha em
/// branco nem elemento XML vazio, então ela escapa das três.
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

    public static TheoryData<string, string, string, bool> ValidCombinations =>
        GenerationMatrixTests.ValidCombinations;

    [Theory]
    [MemberData(nameof(ValidCombinations))]
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
    [MemberData(nameof(ValidCombinations))]
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
        // A regra 7b onde ela mora: no template. Varre todo arquivo hospedeiro, qualquer que seja a
        // extensão — mas só acusa os invólucros que `Opens`/`Closes` reconhecem, que são os de XML
        // e os de chave/colchete/parêntese. Ver a lista de limites no `<remarks>` da classe: os
        // invólucros próprios do Markdown ficam de fora, e é justamente `.md` que mais hospeda
        // marcador hoje.
        //
        // O ganho real está em `appsettings.json`, no dia em que T04 escrever o primeiro fragmento
        // com banco: um `{}` vazio não deixa linha em branco nem elemento XML, então as duas
        // guardas de saída ficariam cegas e só esta acusaria.
        TemplateInventory inventory = TemplateInventory.Read();

        List<string> violations = [];

        foreach (TemplateHost host in inventory.Hosts)
        {
            string[] lines = host.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

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
    public void O_reconhecedor_sabe_distinguir_involucro_de_linha_inocente()
    {
        // A decisão que `Opens` e `Closes` tomam, executável — pelo mesmo motivo que
        // `project-structure.spec.ts` executa o reconhecedor de vazamento dele: um reconhecedor em
        // que ninguém mexeu é uma hipótese, e a próxima pessoa que acrescentar um formato precisa
        // de onde reatacar.

        // Abre: as três chaves de bloco e a tag XML de abertura.
        Assert.True(Opens("  <ItemGroup>"));
        Assert.True(Opens("  <ItemGroup Label=\"Swagger\">"));
        Assert.True(Opens("  \"Logging\": {"));
        Assert.True(Opens("{"));
        Assert.True(Opens("  builder.Services.Configure(options => {"));
        Assert.True(Opens("  \"itens\": ["));

        // Não abre: tag que se fecha sozinha, tag de fechamento, comentário, instrução terminada.
        Assert.False(Opens("  <PackageReference Include=\"X\" Version=\"1.0.0\" />"));
        Assert.False(Opens("  </PropertyGroup>"));
        Assert.False(Opens("  -->"));
        Assert.False(Opens("<!-- comentário -->"));
        Assert.False(Opens("<?xml version=\"1.0\"?>"));
        Assert.False(Opens("app.UseStatusCodePages();"));
        Assert.False(Opens("namespace Acme.Billing.Persistence;"));
        Assert.False(Opens("```bash"));
        Assert.False(Opens(string.Empty));

        // Fecha: as três chaves e a tag XML de fechamento.
        Assert.True(Closes("  </ItemGroup>"));
        Assert.True(Closes("}"));
        Assert.True(Closes("  ]"));
        Assert.True(Closes("  );"));

        // Não fecha: qualquer outra coisa.
        Assert.False(Closes("## Executar"));
        Assert.False(Closes("dotnet restore"));
        Assert.False(Closes("  <ItemGroup>"));
        Assert.False(Closes(string.Empty));

        // E o par inteiro, que é o que a guarda pergunta: a forma exata do defeito de T03.
        Assert.True(Opens("  <ItemGroup>") && Closes("  </ItemGroup>"));
        Assert.False(Opens("  -->") && Closes("</Project>"));
    }

    [Fact]
    public async Task A_varredura_encontrou_o_que_inspecionar()
    {
        // Assert de sanidade, obrigatório — ADR-0011 já exige um para a classe de
        // PackageReferenceMatrixTests, e esta classe precisa do seu pelo mesmo motivo, só que mais
        // forte: "nenhum invólucro vazio" é verdade por vacuidade num pacote sem `.csproj`, e HOJE
        // 16 das 32 combinações são assim, porque o fragmento de Clean é de T04.
        //
        // Então não basta contar arquivos. É preciso mostrar o par que prova que a regra 7b está
        // sendo exercida nos dois estados: uma combinação em que o grupo APARECE porque alguém
        // contribuiu, e outra em que ele SOME porque ninguém contribuiu.
        int projects = 0;
        int containers = 0;
        int withItemGroup = 0;
        int withoutAnyItemGroup = 0;

        foreach (GenerationRequest request in Combinations.Valid)
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

        // E a guarda do lado do template, pelo mesmo raciocínio.
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
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    private static IReadOnlyList<string> ProjectFiles(GeneratedPackage package) =>
    [
        .. package.Paths
            .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal),
    ];

    private static string? PreviousNonBlank(string[] lines, int index)
    {
        for (int cursor = index - 1; cursor >= 0; cursor--)
        {
            if (lines[cursor].Trim().Length > 0)
            {
                return lines[cursor];
            }
        }

        return null;
    }

    private static string? NextNonBlank(string[] lines, int index)
    {
        for (int cursor = index + 1; cursor < lines.Length; cursor++)
        {
            if (lines[cursor].Trim().Length > 0)
            {
                return lines[cursor];
            }
        }

        return null;
    }

    /// <summary>
    /// Diz se a linha <strong>abre</strong> um bloco: termina em <c>{</c>, <c>[</c> ou <c>(</c>, ou
    /// é uma tag XML de abertura.
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

        return XmlOpenTag().IsMatch(text);
    }

    /// <summary>
    /// Diz se a linha <strong>fecha</strong> um bloco: começa com <c>}</c>, <c>]</c> ou <c>)</c>,
    /// ou é uma tag XML de fechamento.
    /// </summary>
    private static bool Closes(string line)
    {
        string text = line.Trim();

        return text.Length > 0
            && (text[0] is '}' or ']' or ')' || text.StartsWith("</", StringComparison.Ordinal));
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
}
