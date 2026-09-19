using System.Text.RegularExpressions;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 do README como <strong>script</strong>: os comandos que ele manda copiar precisam
/// sobreviver ao shell em que ele manda executá-los (RF-21).
/// </summary>
/// <remarks>
/// <para>
/// <strong>O defeito que deu origem a esta classe</strong>, achado em T06 e anterior a ela — a linha
/// vem de T03. O bloco PowerShell do README mandava:
/// </para>
/// <code>
/// curl.exe -i -X POST … -H "Content-Type: application/json" -d '{\"title\":\"Primeiro item\"}'
/// </code>
/// <para>
/// O Windows PowerShell 5.1 entrega argumentos a um programa nativo <em>sem reconstruir as aspas</em>:
/// o valor sai verbatim na linha de comando e o espaço de <c>Primeiro item</c> passa a ser um
/// separador. Medido com um programa que imprime o próprio <c>argv</c>, o <c>curl.exe</c> recebia
/// <c>['-d', '{"title":"Primeiro', 'item"}']</c> — dois argumentos, JSON truncado — e o servidor
/// respondia <c>400</c>. Trocar o valor por um sem espaço fazia passar, o que é exatamente o que
/// tornava o defeito fácil de não ver.
/// </para>
/// <para>
/// <strong>O que esta classe afirma, e o que ela não afirma.</strong> Ela <em>não</em> simula o
/// PowerShell: a camada 1 é estática e não executa shell nenhum. O que ela faz é proibir a
/// <strong>forma</strong> que foi medida quebrando — corpo de requisição viajando como
/// <em>argumento</em> de um comando nativo dentro de um bloco <c>powershell</c>. A forma aceita é o
/// corpo chegar por fora da linha de comando: <c>--data-binary '@-'</c> lendo do <em>pipe</em>, ou
/// <c>'@arquivo'</c>. Uma verificação de verdade sobre citação de shell exigiria executar o shell, e
/// isso é camada 3.
/// </para>
/// <para>
/// O bloco <strong>Bash</strong> fica de fora de propósito: ali o aspeamento é do próprio shell, as
/// aspas simples preservam o conteúdo literalmente, e o caminho foi verificado ponta a ponta. Uma
/// regra que valesse para os dois blocos proibiria em Bash uma forma que em Bash está correta.
/// </para>
/// <para>
/// <strong>Assert de sanidade, obrigatório</strong> (ADR-0008, ADR-0011): se o README deixasse de
/// ter bloco <c>powershell</c>, ou se o recorte deles quebrasse, a afirmação abaixo passaria sem
/// olhar nada — que é precisamente como o defeito original atravessou 678 testes verdes.
/// </para>
/// </remarks>
public sealed partial class ReadmeShellMatrixTests
{
    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhum_comando_nativo_do_bloco_PowerShell_passa_corpo_pela_linha_de_comando(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            new GenerationRequest(
                Combinations.ProjectName,
                architecture,
                database,
                authentication,
                swagger,
                "net10.0"),
            TestContext.Current.CancellationToken);

        string[] blocks = [.. PowerShellBlocks(package.Read("README.md"))];

        Assert.True(
            blocks.Length > 0,
            $"{GeneratedPackage.Describe(package.Request)}: o README não tem nenhum bloco " +
            "'powershell'. Se ele deixou de existir, esta verificação passa por vacuidade; se ele " +
            "mudou de rótulo, ela parou de olhar o que deveria.");

        string[] native =
        [
            .. blocks
                .SelectMany(block => block.Split('\n'))
                .Select(line => line.Trim())
                .Where(line => line.Contains("curl.exe", StringComparison.Ordinal)),
        ];

        Assert.True(
            native.Length > 0,
            $"{GeneratedPackage.Describe(package.Request)}: nenhum bloco 'powershell' do README " +
            "chama 'curl.exe'. O laço abaixo passaria sem olhar nada.");

        int bodies = 0;

        foreach (string line in native)
        {
            foreach (Match body in DataArgument().Matches(line))
            {
                bodies++;

                string value = body.Groups["value"].Value;

                Assert.True(
                    value.StartsWith("'@", StringComparison.Ordinal),
                    $"{GeneratedPackage.Describe(package.Request)}: no bloco PowerShell do README, " +
                    $"`{line}` passa o corpo como ARGUMENTO ({body.Groups["flag"].Value} {value}). " +
                    "O Windows PowerShell entrega argumentos a um programa nativo sem reconstruir " +
                    "as aspas, e um corpo com espaço chega partido em dois — o servidor responde " +
                    "400. Mande o corpo por fora da linha de comando: '@-' lendo do pipe, ou " +
                    "'@arquivo' (RF-21).");
            }
        }

        // A contraprova: se nenhuma linha carregasse corpo nenhum, o laço acima seria verdade por
        // vacuidade e esta classe viraria enfeite. Toda combinação tem ao menos o POST de `/items`.
        Assert.True(
            bodies > 0,
            $"{GeneratedPackage.Describe(package.Request)}: nenhum 'curl.exe' do bloco PowerShell " +
            "passa corpo de requisição. A afirmação desta classe passaria por vacuidade.");
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Todo_bloco_PowerShell_que_manda_corpo_pelo_pipe_fixa_a_codificacao_antes(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // O segundo defeito do mesmo idioma, e o mais perigoso dos dois. Tirar o corpo da linha de
        // comando resolveu a quebra por espaço, mas pôs o corpo no `stdin` — e o Windows PowerShell
        // 5.1 escreve no `stdin` de um programa nativo em **US-ASCII** por padrão
        // (`$OutputEncoding.EncodingName`). Medido com os bytes que chegam do outro lado:
        //
        //   sem a linha:  {"title":"Primeiro ?tem"}     <- 0x3F no lugar de 'í'
        //   com a linha:  {"title":"Primeiro \303\255tem"}  <- UTF-8 correto
        //
        // A diferença entre este defeito e o do espaço é que este **não grita**: o corpo continua
        // sendo JSON válido, o servidor responde 201 e só o dado fica corrompido. Num projeto em
        // português, o primeiro valor que alguém digita ao trocar o exemplo já cai nele.
        //
        // Por isso a regra é posicional: a linha precisa vir ANTES do primeiro pipe do bloco. Uma
        // verificação de "existe em algum lugar do README" deixaria passar a ordem errada, que é
        // justamente o modo de falhar.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            new GenerationRequest(
                Combinations.ProjectName,
                architecture,
                database,
                authentication,
                swagger,
                "net10.0"),
            TestContext.Current.CancellationToken);

        int piping = 0;

        foreach (string block in PowerShellBlocks(package.Read("README.md")))
        {
            string[] lines = [.. block.Split('\n').Select(line => line.Trim())];

            int firstPipe = Array.FindIndex(
                lines,
                line => line.Contains("| curl.exe", StringComparison.Ordinal));

            if (firstPipe < 0)
            {
                continue;
            }

            piping++;

            int encoding = Array.FindIndex(
                lines,
                line => line.StartsWith("$OutputEncoding", StringComparison.Ordinal) &&
                    line.Contains("UTF8Encoding", StringComparison.Ordinal));

            Assert.True(
                encoding >= 0 && encoding < firstPipe,
                $"{GeneratedPackage.Describe(package.Request)}: um bloco PowerShell do README manda " +
                $"corpo pelo pipe em `{lines[firstPipe]}` " +
                $"{(encoding < 0 ? "sem fixar" : "e só fixa depois")} a codificação do pipe. O " +
                "Windows PowerShell escreve no stdin de um programa nativo em US-ASCII por padrão: " +
                "um acento vira '?', o servidor responde 201 e o dado grava corrompido, sem erro " +
                "nenhum. Ponha '$OutputEncoding = [System.Text.UTF8Encoding]::new($false)' como " +
                "primeira linha do bloco.");
        }

        Assert.True(
            piping > 0,
            $"{GeneratedPackage.Describe(package.Request)}: nenhum bloco PowerShell do README manda " +
            "corpo pelo pipe. Esta afirmação passaria por vacuidade — e se o idioma mudou, é aqui " +
            "que a regra precisa mudar junto.");
    }

    /// <summary>O conteúdo de cada cerca <c>```powershell</c> do README.</summary>
    private static IEnumerable<string> PowerShellBlocks(string readme)
    {
        foreach (Match block in PowerShellFence().Matches(readme))
        {
            yield return block.Groups["body"].Value;
        }
    }

    /// <summary>
    /// Um argumento de corpo de requisição do <c>curl</c> e o valor dele, até o próximo espaço.
    /// </summary>
    /// <remarks>
    /// O recorte "até o próximo espaço" é grosseiro de propósito: o valor aceito —
    /// <c>'@-'</c> ou <c>'@arquivo'</c> — nunca tem espaço, e um valor que tenha espaço é
    /// exatamente o caso que esta classe existe para recusar. Um casador mais esperto, que
    /// entendesse aspas, entenderia justamente a citação que o PowerShell não entende.
    /// </remarks>
    [GeneratedRegex(
        @"(?<flag>--data-binary|--data-raw|--data|-d)\s+(?<value>\S+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex DataArgument();

    [GeneratedRegex(
        "^```powershell\\n(?<body>.*?)^```",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex PowerShellFence();
}
