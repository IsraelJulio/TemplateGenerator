using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Duas guardas sobre o <strong>arquivo gerado</strong>, e as duas nasceram de defeito real:
/// nenhum arquivo é só preâmbulo (a <em>verificação de casca</em>, ADR-0012) e todo arquivo de
/// extensão XML é XML válido (docs/architecture/generation-engine.md, "Nenhum comentário de
/// template cita um marcador pelo nome").
/// </summary>
/// <remarks>
/// <para>
/// <strong>Por que a casca.</strong> ADR-0012 registra que "presença é satisfeita por uma casca, e
/// uma casca compila". Em <c>simple</c> + banco, <em>todo</em> arquivo obrigatório está presente —
/// <c>.sln</c>, <c>.csproj</c>, <c>README.md</c>, <c>Program.cs</c>, o projeto de testes e o
/// próprio <c>Persistence/ItemStore.cs</c>. O que falta está <strong>dentro</strong> do arquivo:
/// um <c>.cs</c> com <c>using</c>, <c>namespace</c> e mais nada. Ele compila limpo, o projeto de
/// testes gerado passa, e a falha só aparece no <c>dotnet run</c> — o terceiro comando do README,
/// depois de os dois primeiros ficarem verdes. É o único sintoma que aparece <em>sem restore, sem
/// build e sem executar</em>, então as camadas 2 e 3 chegam tarde ou não chegam.
/// </para>
/// <para>
/// <strong>Por que o XML.</strong> O motor substitui marcador em qualquer posição do texto — ele
/// não sabe o que é comentário, porque ADR-0011, item 9, é justamente a decisão de o motor não
/// interpretar nada. Um <c>&lt;!-- … __ApiPackageReferences__ … --&gt;</c> no <c>.csproj</c> do
/// <c>Domain</c> virou o <c>&lt;ItemGroup&gt;</c> inteiro do Swagger <strong>dentro do
/// comentário</strong>, e o projeto gerado passou a falhar com
/// <c>MSB4025: An XML comment cannot contain '--'</c> — <strong>só</strong> com
/// <c>swagger = true</c>. Esta guarda não depende de reconhecer a causa: ela pega a família
/// inteira, e é barata porque a camada 1 já abre esses arquivos como XML.
/// </para>
/// <para>
/// <strong>O limite da verificação de casca, declarado.</strong> Ela reconhece preâmbulo em
/// <c>.cs</c> — <c>using</c>, <c>namespace</c>, comentário, diretiva e chave solta — e vazio em
/// qualquer extensão. Um <c>.json</c> que resolvesse para <c>{}</c>, ou um <c>.md</c> que ficasse
/// só com títulos, não são acusados aqui: um objeto JSON vazio é sintaticamente legítimo e um
/// documento sem corpo também. Quem vigia a família XML é a segunda guarda; o resto é limite
/// conhecido, e está escrito aqui porque herdar uma permissão que a pessoa <em>acha</em> vigiada é
/// pior que herdar uma que ela sabe que precisa conferir (ADR-0008, ADR-0010).
/// </para>
/// </remarks>
public sealed partial class GeneratedFileIntegrityMatrixTests
{
    /// <summary>
    /// As extensões que o pacote entrega como XML. <c>.sln</c> fica fora: o formato de solução
    /// não é XML.
    /// </summary>
    private static readonly string[] _xmlExtensions =
    [
        ".csproj",
        ".props",
        ".targets",
        ".config",
        ".resx",
        ".xml",
    ];

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhum_arquivo_gerado_e_so_preambulo(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // ADR-0012, item 5, linha 2 — o critério de aceite 9 de T04. Falha quando um marcador
        // resolveu vazio num lugar onde vazio não é estado legítimo.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in package.Paths)
        {
            string content = package.Read(path);

            Assert.True(
                content.Trim().Length > 0,
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' saiu vazio. Um arquivo " +
                "sem conteúdo é um hospedeiro cujo único marcador não recebeu contribuição " +
                "(docs/architecture/generation-engine.md, 'A regra do hospedeiro mínimo').");

            if (!path.EndsWith(".cs", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                HasSubstance(content),
                $"{GeneratedPackage.Describe(package.Request)}: '{path}' é só preâmbulo — " +
                "'using', 'namespace', comentário e espaço em branco, sem uma única declaração " +
                "nem instrução. É a verificação de casca de ADR-0012: o arquivo COMPILA e o " +
                "defeito só aparece no 'dotnet run', longe da causa. Um marcador cujo valor " +
                "vazio produz arquivo inválido é um marcador que não devia estar sozinho no " +
                $"arquivo — o hospedeiro pertence ao fragmento que o preenche.{Environment.NewLine}" +
                $"Conteúdo inteiro:{Environment.NewLine}{content}");
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Todo_arquivo_gerado_de_extensao_XML_e_XML_valido(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        foreach (string path in XmlFiles(package))
        {
            string content = package.Read(path);

            try
            {
                XDocument.Parse(content);
            }
            catch (XmlException failure)
            {
                Assert.Fail(
                    $"{GeneratedPackage.Describe(package.Request)}: '{path}' não é XML válido — " +
                    $"{failure.Message}{Environment.NewLine}{Environment.NewLine}" +
                    "A causa mais provável é um comentário de template que cita um marcador pelo " +
                    "nome: o motor substitui marcador em qualquer posição do texto, inclusive " +
                    "dentro de '<!-- -->', e a contribuição injetada ali dentro quebra o " +
                    "comentário (MSB4025). Descreva o marcador em vez de citá-lo, ou quebre a " +
                    "forma dele (docs/architecture/generation-engine.md, 'Nenhum comentário de " +
                    $"template cita um marcador pelo nome').{Environment.NewLine}" +
                    $"Conteúdo:{Environment.NewLine}{content}");
            }
        }
    }

    [Fact]
    public async Task A_varredura_encontrou_arquivo_de_cada_familia_para_inspecionar()
    {
        // Assert de sanidade, obrigatório: as duas guardas acima são "para todo arquivo", e um
        // pacote sem `.cs` ou sem `.csproj` as faria passar sem abrir nada. O caso não é teórico —
        // até T04 a Clean gerava cinco arquivos e nenhum deles era código.
        int sources = 0;
        int xml = 0;

        foreach (GenerationRequest request in Combinations.Available)
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(
                request,
                TestContext.Current.CancellationToken);

            sources += package.Paths.Count(path =>
                path.EndsWith(".cs", StringComparison.Ordinal));

            xml += XmlFiles(package).Count;
        }

        Assert.True(
            sources > 0,
            "Nenhum arquivo '.cs' em nenhuma combinação disponível. Enquanto isso for verdade, a " +
            "verificação de casca passa sem olhar nada.");

        Assert.True(
            xml > 0,
            "Nenhum arquivo de extensão XML em nenhuma combinação disponível. A guarda de XML " +
            "válido passaria por vacuidade — e é ela que pega a família de defeitos do marcador " +
            "citado dentro de comentário.");
    }

    [Fact]
    public void O_reconhecedor_de_casca_distingue_preambulo_de_arquivo_de_verdade()
    {
        // A decisão que `HasSubstance` toma, executável — pelo mesmo motivo que o reconhecedor de
        // invólucro de `EmptyWrapperMatrixTests` tem o seu: um reconhecedor em que ninguém mexeu é
        // uma hipótese, e a próxima pessoa que acrescentar um formato precisa de onde reatacar.

        // Casca: é exatamente o `ItemStore.cs` que ADR-0012 cita, inteiro.
        Assert.False(HasSubstance("using Acme.Models;\n\nnamespace Acme.Persistence;\n"));
        Assert.False(HasSubstance("namespace Acme.Persistence;\n"));
        Assert.False(HasSubstance("// só um comentário\n"));
        Assert.False(HasSubstance("/* bloco\n   inteiro */\n"));
        Assert.False(HasSubstance("#nullable enable\nusing System;\n"));
        Assert.False(HasSubstance("global using Xunit;\n"));
        Assert.False(HasSubstance("namespace Acme\n{\n}\n"));
        Assert.False(HasSubstance("   \n\t\n"));

        // Substância: declaração de tipo, instrução de topo, atributo de assembly.
        Assert.True(HasSubstance("namespace Acme;\n\npublic sealed class Item;\n"));
        Assert.True(HasSubstance("var builder = WebApplication.CreateBuilder(args);\n"));
        Assert.True(HasSubstance("using Acme;\n\npublic record Item(int Id);\n"));
        Assert.True(HasSubstance("namespace Acme\n{\n    public class Item;\n}\n"));

        // E o caso que mais importa acertar: `using` com CHAVE é instrução, não diretiva.
        Assert.True(HasSubstance("using (var scope = Build())\n{\n    scope.Run();\n}\n"));
    }

    /// <summary>
    /// Diz se um arquivo <c>.cs</c> tem alguma coisa além de preâmbulo: qualquer linha que não
    /// seja <c>using</c>, <c>namespace</c> de arquivo, comentário, diretiva ou chave solta.
    /// </summary>
    /// <remarks>
    /// Reconhecedor de <strong>linha</strong>, não um parser de C#. Escrever um analisador de
    /// sintaxe aqui seria trazer para o verificador o que ADR-0011, item 9, recusa no motor — e o
    /// defeito que ele precisa pegar não tem sutileza: o arquivo fica com duas ou três linhas.
    /// </remarks>
    private static bool HasSubstance(string content)
    {
        string withoutBlockComments = BlockComment().Replace(content, string.Empty);

        foreach (string raw in withoutBlockComments.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n'))
        {
            string line = raw.Trim();

            if (line.Length == 0
                || line.StartsWith("//", StringComparison.Ordinal)
                || line.StartsWith('#')
                || line is "{" or "}")
            {
                continue;
            }

            // `using X;` e `global using X;` são diretivas; `using (…)` é instrução, e essa
            // diferença é a única sutileza real do reconhecedor.
            if (UsingDirective().IsMatch(line) || NamespaceDeclaration().IsMatch(line))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> XmlFiles(GeneratedPackage package) =>
    [
        .. package.Paths
            .Where(path => _xmlExtensions.Any(extension =>
                path.EndsWith(extension, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal),
    ];

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"^(global\s+)?using\s+[^();]+;$", RegexOptions.CultureInvariant)]
    private static partial Regex UsingDirective();

    [GeneratedRegex(@"^namespace\s+[A-Za-z_][\w.]*\s*[;{]?$", RegexOptions.CultureInvariant)]
    private static partial Regex NamespaceDeclaration();
}
