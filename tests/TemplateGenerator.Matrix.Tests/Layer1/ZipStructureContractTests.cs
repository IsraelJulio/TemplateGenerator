using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// O lado <strong>backend</strong> da amarração exigida pelo critério de aceite 12 de T03 e por
/// ADR-0010 ("Quando esta decisão expira", saída 1): a árvore de pastas que a tela mostra como
/// "estrutura prevista" tem de bater com o conteúdo real do ZIP.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Por que a amarração precisa de dois lados.</strong> Quem sabe a árvore real é o motor,
/// que é C#. Quem produz a árvore mostrada é <c>projectStructure()</c>, que é TypeScript e roda no
/// navegador. Não há runtime que execute os dois, e reimplementar <c>projectStructure()</c> em C#
/// seria conferir uma <em>cópia</em> da derivação contra o ZIP — o teste passaria mesmo com a tela
/// mostrando outra coisa, que é precisamente a falha que ADR-0010 nomeia. Então:
/// </para>
/// <list type="number">
///   <item><description>
///   <strong>aqui</strong>, a árvore real é extraída do ZIP gerado e comparada com
///   <c>src/web/src/app/core/summary/zip-structure.contract.json</c>, versionado. Este teste é o
///   que impede o arquivo de envelhecer: ele é reconstruído do pacote a cada execução;
///   </description></item>
///   <item><description>
///   <strong>no frontend</strong>, <c>zip-structure.spec.ts</c> lê o mesmo arquivo e o compara com
///   a saída da função <em>de verdade</em>, sem cópia e sem dublê.
///   </description></item>
/// </list>
/// <para>
/// É o padrão de fixture que mordeu T01 — com a diferença que ali nada reconferia a fixture contra
/// o payload real, e aqui é exatamente esse o trabalho deste arquivo.
/// </para>
/// <para>
/// <strong>Árvore de pastas contra lista de arquivos.</strong> A projeção é uma árvore de nós; o
/// ZIP é uma lista plana de arquivos. Os dois lados são reduzidos à mesma forma: o conjunto de
/// caminhos, com <c>/</c> no fim quando é diretório. Os diretórios do ZIP são <em>derivados</em>
/// dos caminhos dos arquivos, e a comparação é por <strong>igualdade de conjunto</strong>, não por
/// contenção — um lado que listasse metade da árvore passaria em qualquer teste de subconjunto.
/// </para>
/// <para>
/// <strong>O que não é comparado, dito com todas as letras:</strong> as notas ("volátil, perdido
/// no reinício") e a ordem de exibição. Nota é prosa para humano e não tem contraparte no pacote;
/// a ordem é assunto da tela, e já tem teste próprio em <c>project-structure.spec.ts</c>.
/// </para>
/// </remarks>
public sealed class ZipStructureContractTests
{
    /// <summary>
    /// Caminho do contrato, dentro do módulo que ADR-0010 confina como único dono da projeção.
    /// </summary>
    public const string ContractPath =
        "src/web/src/app/core/summary/zip-structure.contract.json";

    /// <summary>
    /// As combinações amarradas. Hoje só a Simples: é a única com fragmento escrito (T03), e
    /// ADR-0010 deixa a Clean para T04 — inclusive a decisão sobre o <c>.Api</c> dobrado, que
    /// <strong>é</strong> o comportamento documentado até lá e não uma divergência a corrigir.
    /// </summary>
    /// <remarks>
    /// Os dois nomes de projeto são deliberados. <c>Acme.Billing</c> é o caso comum;
    /// <c>Acme.Billing.Api</c> é o caso que ADR-0010 deixou em aberto e que
    /// generated-projects.md decidiu para a Simples — nada é concatenado, logo
    /// <c>src/Acme.Billing.Api/</c> e nunca <c>src/Acme.Billing.Api.Api/</c>. Sem ele, a regra de
    /// nomes ficaria amarrada só do lado do backend.
    /// </remarks>
    public static IEnumerable<(string ProjectName, GenerationRequest Request)> Tied()
    {
        foreach (string name in (string[])["Acme.Billing", "Acme.Billing.Api"])
        {
            foreach (bool swagger in (bool[])[true, false])
            {
                yield return (name, new GenerationRequest(
                    name,
                    "simple",
                    "none",
                    "none",
                    swagger,
                    "net10.0"));
            }
        }
    }

    [Fact]
    public async Task O_contrato_versionado_e_a_arvore_real_do_ZIP()
    {
        string expected = await BuildContractAsync(TestContext.Current.CancellationToken);
        string file = Path.Combine(RepositoryLayout.Root, ContractPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(
            File.Exists(file),
            $"'{ContractPath}' não existe. Ele é o contrato entre o ZIP real e a projeção da " +
            $"tela (ADR-0010). Conteúdo esperado:{Environment.NewLine}{expected}");

        string actual = Normalize(await File.ReadAllTextAsync(file, Encoding.UTF8, TestContext.Current.CancellationToken));

        Assert.True(
            string.Equals(actual, expected, StringComparison.Ordinal),
            $"'{ContractPath}' não corresponde mais ao pacote gerado.{Environment.NewLine}" +
            $"{Environment.NewLine}{Diff(expected, actual)}{Environment.NewLine}" +
            $"Se a mudança do pacote é intencional, grave este conteúdo no arquivo — e confira " +
            $"que a projeção do frontend acompanhou, porque `zip-structure.spec.ts` vai cobrar:" +
            $"{Environment.NewLine}{expected}");
    }

    [Fact]
    public async Task O_contrato_nao_pode_ficar_vazio_nem_perder_a_raiz_da_arvore()
    {
        // Assert de sanidade, pela mesma razão de ADR-0008 e de ADR-0011: um contrato vazio, ou
        // um contrato sem os arquivos que dão forma à árvore, faria a comparação acima e a do
        // frontend passarem sem olhar nada. Os nomes citados aqui são os que qualquer pacote da
        // Simples tem de ter — se um deles sumir, isto cai antes de a comparação virar vácuo.
        JsonNode contract = JsonNode.Parse(await BuildContractAsync(TestContext.Current.CancellationToken))!;
        JsonArray combinations = contract["combinacoes"]!.AsArray();

        Assert.Equal(4, combinations.Count);

        foreach (JsonNode? combination in combinations)
        {
            string[] paths = [.. combination!["caminhos"]!.AsArray().Select(node => node!.GetValue<string>())];

            Assert.True(
                paths.Length >= 20,
                $"A árvore amarrada tem só {paths.Length} caminhos. Um pacote da Simples tem " +
                "mais de vinte; um número menor é sinal de geração quebrada, e um contrato " +
                "curto faria a comparação passar por vacuidade.");

            Assert.Contains("src/", paths);
            Assert.Contains("tests/", paths);
            Assert.Contains(".templategenerator/manifest.json", paths);
            Assert.Contains("README.md", paths);
        }
    }

    [Fact]
    public async Task A_arvore_amarrada_nao_tem_diretorio_sem_arquivo_dentro()
    {
        // A diferença de forma entre os dois lados é aqui que fica honesta. A projeção pode
        // declarar um diretório SEM conteúdo (`Persistence/Migrations/`, uma regra que termina em
        // `/`); o ZIP, por construção, só tem arquivos. Se a combinação amarrada tivesse um
        // diretório assim, a comparação teria de decidir o que fazer com ele — e a decisão mais
        // provável, ignorá-lo, abriria a porta para a projeção esconder qualquer coisa dentro de
        // um diretório vazio.
        //
        // Não tem: em simple/none/none todo diretório da árvore contém arquivo. Este teste é o
        // que garante que a afirmação continua verdadeira, e falha no dia em que deixar de ser —
        // que é quando a regra de comparação precisa ser repensada, não contornada.
        JsonNode contract = JsonNode.Parse(await BuildContractAsync(TestContext.Current.CancellationToken))!;

        foreach (JsonNode? combination in contract["combinacoes"]!.AsArray())
        {
            string[] paths = [.. combination!["caminhos"]!.AsArray().Select(node => node!.GetValue<string>())];

            foreach (string directory in paths.Where(path => path.EndsWith('/')))
            {
                Assert.True(
                    paths.Any(path =>
                        !path.EndsWith('/') &&
                        path.StartsWith(directory, StringComparison.Ordinal)),
                    $"O diretório '{directory}' não tem nenhum arquivo dentro.");
            }
        }
    }

    /// <summary>
    /// Monta o contrato a partir dos ZIPs gerados de verdade. É a única fonte do arquivo
    /// versionado — nada aqui é digitado à mão.
    /// </summary>
    private static async Task<string> BuildContractAsync(CancellationToken cancellationToken)
    {
        JsonArray combinations = [];

        foreach ((string projectName, GenerationRequest request) in Tied())
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(request, cancellationToken);

            combinations.Add(new JsonObject
            {
                ["selecao"] = new JsonObject
                {
                    ["architecture"] = request.Architecture,
                    ["database"] = request.Database,
                    ["authentication"] = request.Authentication,
                    ["swagger"] = request.Swagger,
                    ["dotnetVersion"] = request.DotnetVersion,
                },
                ["projectName"] = projectName,
                ["caminhos"] = new JsonArray([.. TreePaths(package.Paths).Select(path => JsonValue.Create(path))]),
            });
        }

        JsonObject contract = new()
        {
            ["$origem"] =
                "Gerado por tests/TemplateGenerator.Matrix.Tests/Layer1/ZipStructureContractTests.cs " +
                "a partir do ZIP real. Não edite à mão: o teste o reconstrói do pacote a cada " +
                "execução e falha se divergirem. Quem o consome é zip-structure.spec.ts, que " +
                "compara esta árvore com a de projectStructure() (ADR-0010, critério 12 de T03).",
            ["combinacoes"] = combinations,
        };

        return Normalize(contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Os caminhos da árvore de um pacote: cada arquivo, mais cada diretório que o contém, com
    /// <c>/</c> no fim. Ordem ordinal, que é a mesma dos dois lados da amarração.
    /// </summary>
    private static IReadOnlyList<string> TreePaths(IEnumerable<string> files)
    {
        SortedSet<string> paths = new(StringComparer.Ordinal);

        foreach (string file in files)
        {
            paths.Add(file);

            string[] segments = file.Split('/');

            for (int depth = 1; depth < segments.Length; depth++)
            {
                paths.Add(string.Join('/', segments.Take(depth)) + "/");
            }
        }

        return [.. paths];
    }

    /// <summary>Quebra de linha e fim de arquivo iguais dos dois lados, em qualquer sistema.</summary>
    private static string Normalize(string json) =>
        json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    /// <summary>As primeiras linhas em que os dois textos diferem, para a mensagem de falha.</summary>
    private static string Diff(string expected, string actual)
    {
        string[] left = expected.Split('\n');
        string[] right = actual.Split('\n');

        List<string> lines = [];

        for (int index = 0; index < Math.Max(left.Length, right.Length) && lines.Count < 30; index++)
        {
            string one = index < left.Length ? left[index] : "(fim)";
            string other = index < right.Length ? right[index] : "(fim)";

            if (!string.Equals(one, other, StringComparison.Ordinal))
            {
                lines.Add($"  linha {index + 1}: ZIP tem `{one.Trim()}`, arquivo tem `{other.Trim()}`");
            }
        }

        return lines.Count == 0 ? "  (sem diferença de linha)" : string.Join(Environment.NewLine, lines);
    }
}
