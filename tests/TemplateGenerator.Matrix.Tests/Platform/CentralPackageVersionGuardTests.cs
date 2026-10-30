using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Platform;

/// <summary>
/// Guarda de RNF-06 para a <strong>plataforma</strong>: toda versão em <c>Directory.Packages.props</c>
/// (e em qualquer declaração de pacote fora dos templates) é exata. Os <c>.csproj</c> gerados já são
/// cobertos por <c>PackageReferenceMatrixTests.Toda_versao_declarada_e_exata</c>; isto cobre o resto.
/// </summary>
/// <remarks>
/// O verificador é provado nos dois sentidos: a teoria sobre o parser rejeita intervalo, curinga e
/// versão ausente (sem isso, um parser que aceitasse tudo deixaria a asserção sobre o arquivo real
/// passar por vacuidade), e a asserção sobre o arquivo real olha o repositório de fato.
/// </remarks>
public sealed partial class CentralPackageVersionGuardTests
{
    private static readonly string[] _versionedElements =
        ["PackageVersion", "PackageReference", "GlobalPackageReference"];

    private static readonly string[] _ignoredFolders = ["Templates", "bin", "obj", "node_modules"];

    [Theory]
    [InlineData("10.0.12")]
    [InlineData("3.2.2")]
    [InlineData("1.0.0-preview.1")]
    public void Versao_exata_e_aceita(string version) =>
        Assert.True(IsExact(version));

    [Theory]
    [InlineData("[1.0,2.0)")]
    [InlineData("(,2.0]")]
    [InlineData("[1.0.0]")]
    [InlineData("(1.0,)")]
    [InlineData("1.*")]
    [InlineData("10.0.*")]
    [InlineData("*")]
    [InlineData("1.0")]
    [InlineData("1.0.0-*")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Intervalo_curinga_e_versao_ausente_sao_recusados(string? version) =>
        Assert.False(IsExact(version));

    [Fact]
    public void O_verificador_acusa_intervalo_curinga_e_versao_ausente_num_props_ruim()
    {
        XDocument bad = XDocument.Parse(
            """
            <Project><ItemGroup>
              <PackageVersion Include="A" Version="[1.0,2.0)" />
              <PackageVersion Include="B" Version="1.*" />
              <PackageVersion Include="C" />
              <PackageVersion Include="D" Version="1.2.3" />
            </ItemGroup></Project>
            """);

        string[] violations = [.. Violations(bad, "ruim.props", requireVersion: true)];

        Assert.Equal(3, violations.Length);
        Assert.Contains(violations, v => v.Contains("'A'", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("'B'", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("'C'", StringComparison.Ordinal));
    }

    [Fact]
    public void Directory_Packages_props_tem_so_versao_exata()
    {
        string path = Path.Combine(RepositoryLayout.Root, "Directory.Packages.props");

        Assert.True(File.Exists(path), $"'{path}' não existe: o gerenciamento central sumiu.");

        XDocument document = XDocument.Load(path);

        // Sanidade: um arquivo sem nenhum PackageVersion faria a guarda passar por vacuidade.
        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "PackageVersion");

        string[] violations = [.. Violations(document, path, requireVersion: true)];

        Assert.True(
            violations.Length == 0,
            "RNF-06 exige versão exata na plataforma ('10.0.12'), nunca intervalo nem curinga:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Nenhum_csproj_ou_props_da_plataforma_declara_versao_inexata()
    {
        // Cobre `Version=`/`VersionOverride=` escritos fora do arquivo central. Os templates
        // gerados ficam de fora: têm a guarda própria na camada 1.
        string[] files =
        [
            .. new[] { "src", "tests" }
                .Select(folder => Path.Combine(RepositoryLayout.Root, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                .Where(file => file.EndsWith(".csproj", StringComparison.Ordinal) ||
                    file.EndsWith(".props", StringComparison.Ordinal) ||
                    file.EndsWith(".targets", StringComparison.Ordinal))
                .Where(file => !IsInIgnoredFolder(file))
                .Order(StringComparer.Ordinal),
        ];

        Assert.NotEmpty(files);

        string[] violations =
        [
            .. files.SelectMany(file => Violations(XDocument.Load(file), file, requireVersion: false)),
        ];

        Assert.True(
            violations.Length == 0,
            "RNF-06: versão inexata fora do Directory.Packages.props:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool IsInIgnoredFolder(string file)
    {
        string relative = Path.GetRelativePath(RepositoryLayout.Root, file);

        return relative
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .SkipLast(1)
            .Any(segment => _ignoredFolders.Contains(segment, StringComparer.Ordinal));
    }

    /// <summary>
    /// As violações de RNF-06 num documento MSBuild. Com <paramref name="requireVersion"/>, versão
    /// ausente também é violação (é o caso do arquivo central); fora dele, a ausência é legítima
    /// porque o gerenciamento central a fornece.
    /// </summary>
    private static IEnumerable<string> Violations(
        XDocument document,
        string source,
        bool requireVersion)
    {
        foreach (XElement element in document.Descendants()
            .Where(e => _versionedElements.Contains(e.Name.LocalName, StringComparer.Ordinal)))
        {
            string name = element.Attribute("Include")?.Value ??
                element.Attribute("Update")?.Value ?? "(sem nome)";

            string?[] present =
            [
                .. new[]
                {
                    element.Attribute("Version")?.Value ??
                        element.Element(element.Name.Namespace + "Version")?.Value,
                    element.Attribute("VersionOverride")?.Value ??
                        element.Element(element.Name.Namespace + "VersionOverride")?.Value,
                }.Where(v => v is not null),
            ];

            if (present.Length == 0)
            {
                if (requireVersion)
                {
                    yield return $"{source}: pacote '{name}' sem versão (ausente).";
                }

                continue;
            }

            foreach (string? version in present)
            {
                if (!IsExact(version))
                {
                    yield return $"{source}: pacote '{name}' com versão '{version}' (não é exata).";
                }
            }
        }
    }

    private static bool IsExact(string? version) =>
        !string.IsNullOrWhiteSpace(version) && ExactVersion().IsMatch(version);

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactVersion();
}
