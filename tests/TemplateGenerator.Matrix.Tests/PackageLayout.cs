using System.Xml.Linq;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// Lê a forma de um pacote gerado <strong>por papel</strong>, e não por nome de arquivo: quais
/// projetos ele traz, qual deles é o Web API, e quais referências de projeto cada um declara.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Por que por papel.</strong> A regra de nomes mora em
/// docs/architecture/generated-projects.md ("Nomes de projeto e de pasta") e é
/// <c>&lt;projectName&gt; + "." + sufixo da camada</c>, concatenada sempre. Reimplementá-la aqui
/// criaria uma segunda cópia da mesma regra dentro do verificador dela — o teste passaria a
/// conferir a cópia, e as duas divergiriam no primeiro nome de borda. Quem fixa a regra de nomes
/// por teste é <see cref="Layer1.ZipStructureContractTests"/>, comparando a árvore real contra o
/// contrato versionado, com <c>Acme.Billing</c> e <c>Acme.Billing.Api</c> nas duas arquiteturas.
/// </para>
/// <para>
/// O papel de um projeto de produção é o <strong>último segmento</strong> do nome do
/// <c>.csproj</c>, que é o sufixo de camada — <c>Acme.Billing.Api.Api.csproj</c> tem papel
/// <c>Api</c>, qualquer que seja o <c>projectName</c>. Nada aqui concatena, compara ou remove
/// nome de projeto.
/// </para>
/// <para>
/// <strong>O projeto Web API é identificado pelo <c>Program.cs</c></strong>, não pelo sufixo. É a
/// única definição que vale nas duas arquiteturas ao mesmo tempo: na Simples o projeto de código
/// se chama exatamente <c>&lt;projectName&gt;</c> — que pode terminar em <c>.Api</c> por acidente
/// do nome que a pessoa digitou, e pode não terminar —, e na Clean ele é o <c>.Api</c> entre
/// quatro irmãos. Quem tem ponto de entrada é um só, nas duas.
/// </para>
/// </remarks>
internal static class PackageLayout
{
    /// <summary>Papel do projeto de composição e transporte (Clean) ou do projeto único (Simples).</summary>
    public const string ApiRole = "Api";

    /// <summary>Papel dos casos de uso.</summary>
    public const string ApplicationRole = "Application";

    /// <summary>Papel das entidades, regras e portas.</summary>
    public const string DomainRole = "Domain";

    /// <summary>Papel dos adaptadores.</summary>
    public const string InfrastructureRole = "Infrastructure";

    /// <summary>Papel do projeto de testes — que fica <strong>fora</strong> do diagrama.</summary>
    public const string TestsRole = "Tests";

    /// <summary>Prefixo das pastas de projeto de produção dentro do ZIP.</summary>
    public const string SourceDirectory = "src/";

    /// <summary>Prefixo da pasta do projeto de testes dentro do ZIP.</summary>
    public const string TestsDirectory = "tests/";

    /// <summary>Todo <c>.csproj</c> do pacote, em ordem ordinal.</summary>
    public static IReadOnlyList<string> ProjectFiles(GeneratedPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        return
        [
            .. package.Paths
                .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>Os <c>.csproj</c> sob <c>src/</c> — os projetos de produção.</summary>
    public static IReadOnlyList<string> SourceProjects(GeneratedPackage package) =>
    [
        .. ProjectFiles(package)
            .Where(path => path.StartsWith(SourceDirectory, StringComparison.Ordinal)),
    ];

    /// <summary>Os <c>.csproj</c> sob <c>tests/</c>.</summary>
    public static IReadOnlyList<string> TestProjects(GeneratedPackage package) =>
    [
        .. ProjectFiles(package)
            .Where(path => path.StartsWith(TestsDirectory, StringComparison.Ordinal)),
    ];

    /// <summary>
    /// O papel de um <c>.csproj</c>: o último segmento do nome do arquivo, que é o sufixo de
    /// camada. Ex.: <c>src/Acme.Billing.Api.Api/Acme.Billing.Api.Api.csproj</c> ⇒ <c>Api</c>.
    /// </summary>
    public static string RoleOf(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        string name = Path.GetFileNameWithoutExtension(projectPath);
        int dot = name.LastIndexOf('.');

        return dot < 0 ? name : name[(dot + 1)..];
    }

    /// <summary>
    /// O diretório do projeto Web API dentro do pacote, com <c>/</c> no fim — o único projeto com
    /// <c>Program.cs</c> —, ou <c>null</c> quando o pacote não tem nenhum.
    /// </summary>
    public static string? WebProjectDirectory(GeneratedPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        string[] entryPoints =
        [
            .. package.Paths
                .Where(path => path.EndsWith("/Program.cs", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];

        return entryPoints.Length == 1
            ? entryPoints[0][..(entryPoints[0].LastIndexOf('/') + 1)]
            : null;
    }

    /// <summary>
    /// As <c>ProjectReference</c> declaradas em <paramref name="projectPath"/>, resolvidas para o
    /// caminho do <c>.csproj</c> alvo dentro do pacote.
    /// </summary>
    /// <remarks>
    /// Lido como XML, e não por expressão regular: um caminho de projeto citado dentro de um
    /// comentário não é uma aresta declarada, e é exatamente esse tipo de menção que uma auditoria
    /// por <c>grep</c> confundiria com a dependência. Os <c>.csproj</c> gerados escrevem o caminho
    /// com <c>\</c>, como o SDK faz; aqui ele é normalizado para a forma do ZIP.
    /// </remarks>
    public static IReadOnlyList<string> ProjectReferences(
        GeneratedPackage package,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(projectPath);

        string directory = projectPath[..(projectPath.LastIndexOf('/') + 1)];

        return
        [
            .. XDocument.Parse(package.Read(projectPath)).Root!
                .Descendants()
                .Where(element =>
                    element.Name.LocalName.Equals("ProjectReference", StringComparison.Ordinal))
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Select(include => Resolve(directory, include))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Junta o caminho relativo declarado no <c>Include</c> ao diretório do projeto que o declara
    /// e devolve o caminho na forma do ZIP: separadores <c>/</c>, sem <c>..</c>.
    /// </summary>
    private static string Resolve(string directory, string include)
    {
        List<string> segments = [.. directory.Split('/', StringSplitOptions.RemoveEmptyEntries)];

        foreach (string segment in include.Replace('\\', '/').Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Equals("..", StringComparison.Ordinal))
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            if (!segment.Equals(".", StringComparison.Ordinal))
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }
}
