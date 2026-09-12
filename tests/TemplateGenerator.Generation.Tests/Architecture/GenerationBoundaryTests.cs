using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Architecture;

/// <summary>
/// Teste arquitetural da restrição central da plataforma (docs/architecture/platform.md):
/// <c>TemplateGenerator.Generation</c> não referencia ASP.NET Core nem provider de banco.
/// </summary>
/// <remarks>
/// <para>São três olhares, de propósito, porque um só não basta:</para>
/// <list type="number">
///   <item>
///     <description>
///     O <c>.csproj</c> não usa o SDK Web e não declara <c>FrameworkReference</c> —
///     <c>Microsoft.NET.Sdk.Web</c> injeta o ASP.NET Core inteiro sem nenhum
///     <c>PackageReference</c> visível.
///     </description>
///   </item>
///   <item>
///     <description>
///     O grafo de restore (<c>obj/project.assets.json</c>) não contém pacote nem
///     <c>FrameworkReference</c> proibido, em nenhum nível de transitividade. Isto pega a
///     dependência declarada mas ainda não usada em código — que compila sem aviso e passaria
///     despercebida por inspeção de IL.
///     </description>
///   </item>
///   <item>
///     <description>
///     O assembly compilado não referencia assembly proibido. Isto pega o uso real, inclusive
///     o que venha por caminho que não seja <c>PackageReference</c>.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class GenerationBoundaryTests
{
    /// <summary>Uma dependência declarada no grafo de restore e onde ela foi encontrada.</summary>
    private sealed record DependencyReference(string Section, string Name);

    [Fact]
    public void Projeto_de_geracao_nao_usa_o_SDK_Web()
    {
        string csprojPath = Path.Combine(
            RepositoryLayout.GenerationProjectDirectory,
            "TemplateGenerator.Generation.csproj");

        Assert.True(File.Exists(csprojPath), $"Projeto não encontrado em '{csprojPath}'.");

        // XML, não busca de texto: os comentários do próprio .csproj citam o que é proibido, e
        // uma busca por substring acusaria a documentação da regra como violação da regra.
        XElement project = XDocument.Load(csprojPath).Root!;

        string sdk = project.Attribute("Sdk")?.Value ?? string.Empty;

        Assert.True(
            sdk.Equals("Microsoft.NET.Sdk", StringComparison.Ordinal),
            $"O SDK de TemplateGenerator.Generation é '{sdk}' e precisa ser 'Microsoft.NET.Sdk'. " +
            "'Microsoft.NET.Sdk.Web' injeta a FrameworkReference do ASP.NET Core sem nenhum " +
            "PackageReference visível. A biblioteca de geração é pura — entra configuração, sai " +
            "fluxo de bytes (docs/architecture/platform.md).");

        string[] frameworkReferences =
        [
            .. project
                .Descendants()
                .Where(element =>
                    element.Name.LocalName.Equals("FrameworkReference", StringComparison.Ordinal))
                .Select(element => element.Attribute("Include")?.Value ?? "(sem Include)"),
        ];

        Assert.True(
            frameworkReferences.Length == 0,
            "TemplateGenerator.Generation não pode declarar FrameworkReference alguma; é uma " +
            "biblioteca pura (docs/architecture/platform.md). Declaradas: " +
            string.Join(", ", frameworkReferences));
    }

    [Fact]
    public void Grafo_de_restore_da_geracao_nao_contem_AspNetCore_nem_provider_de_banco()
    {
        string assetsPath = RepositoryLayout.GenerationAssetsFile;

        Assert.True(
            File.Exists(assetsPath),
            $"'{assetsPath}' não existe. O teste arquitetural depende do grafo de restore do " +
            "projeto de geração; rode 'dotnet restore' antes.");

        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));

        DependencyReference[] dependencies = [.. ReadDependencies(assets.RootElement)];

        // Sanidade: se o NuGet mudar o formato e a leitura vier vazia, o teste passaria sem ter
        // olhado nada. Um teste arquitetural que não enxerga nada é pior que nenhum.
        Assert.True(
            dependencies.Length > 0,
            $"Nenhuma dependência foi lida de '{assetsPath}'. O formato do grafo de restore " +
            "mudou e este teste precisa ser reescrito — ele não está mais protegendo a fronteira.");

        string[] violations =
        [
            .. dependencies
                .Select(dependency => new
                {
                    dependency.Section,
                    dependency.Name,
                    Prefix = ForbiddenDependencies.MatchPrefix(dependency.Name),
                })
                .Where(match => match.Prefix is not null)
                .Select(match => $"'{match.Name}' (proibido por '{match.Prefix}') em '{match.Section}'")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            violations.Length == 0,
            "TemplateGenerator.Generation é uma biblioteca pura e não pode depender de ASP.NET " +
            "Core nem de provider de banco (docs/architecture/platform.md). " +
            $"Encontrado no grafo de restore ({dependencies.Length} dependências inspecionadas):" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Assembly_da_geracao_nao_referencia_AspNetCore_nem_provider_de_banco()
    {
        Assembly generation = typeof(IGenerationEngine).Assembly;

        string[] violations =
        [
            .. generation
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => new { Name = name!, Prefix = ForbiddenDependencies.MatchPrefix(name!) })
                .Where(match => match.Prefix is not null)
                .Select(match => $"'{match.Name}' (proibido por '{match.Prefix}')")
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            violations.Length == 0,
            $"O assembly '{generation.GetName().Name}' referencia dependência proibida:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Lê do grafo de restore apenas as seções que declaram dependência de fato.
    /// </summary>
    /// <remarks>
    /// A leitura é dirigida, não uma varredura do JSON inteiro. Seções como
    /// <c>centralPackageVersions</c> (o catálogo de <c>Directory.Packages.props</c>),
    /// <c>packagesToPrune</c> (consequência de uma <c>FrameworkReference</c>) e a lista de
    /// arquivos de cada <c>.nupkg</c> citam nomes proibidos sem que exista dependência alguma —
    /// incluí-las produziria acusação falsa e afogaria o diagnóstico real.
    /// </remarks>
    private static IEnumerable<DependencyReference> ReadDependencies(JsonElement root)
    {
        // Grafo resolvido, com toda a transitividade.
        if (root.TryGetProperty("libraries", out JsonElement libraries))
        {
            foreach (JsonProperty library in libraries.EnumerateObject())
            {
                yield return new DependencyReference("libraries", PackageName(library.Name));
            }
        }

        if (root.TryGetProperty("targets", out JsonElement targets))
        {
            foreach (JsonProperty framework in targets.EnumerateObject())
            {
                foreach (JsonProperty library in framework.Value.EnumerateObject())
                {
                    string section = $"targets.{framework.Name}";

                    yield return new DependencyReference(section, PackageName(library.Name));

                    if (library.Value.TryGetProperty("frameworkReferences", out JsonElement inherited)
                        && inherited.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in inherited.EnumerateArray())
                        {
                            yield return new DependencyReference(
                                $"{section}.{library.Name}.frameworkReferences",
                                item.GetString() ?? string.Empty);
                        }
                    }
                }
            }
        }

        // Dependências diretas, como escritas no .csproj.
        if (root.TryGetProperty("projectFileDependencyGroups", out JsonElement groups))
        {
            foreach (JsonProperty group in groups.EnumerateObject())
            {
                foreach (JsonElement item in group.Value.EnumerateArray())
                {
                    yield return new DependencyReference(
                        $"projectFileDependencyGroups.{group.Name}",
                        FirstToken(item.GetString()));
                }
            }
        }

        if (!root.TryGetProperty("project", out JsonElement project)
            || !project.TryGetProperty("frameworks", out JsonElement frameworks))
        {
            yield break;
        }

        foreach (JsonProperty framework in frameworks.EnumerateObject())
        {
            foreach (string key in (string[])["dependencies", "frameworkReferences", "downloadDependencies"])
            {
                if (!framework.Value.TryGetProperty(key, out JsonElement declared))
                {
                    continue;
                }

                string section = $"project.frameworks.{framework.Name}.{key}";

                if (declared.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty entry in declared.EnumerateObject())
                    {
                        yield return new DependencyReference(section, entry.Name);
                    }
                }
                else if (declared.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in declared.EnumerateArray())
                    {
                        string name = entry.ValueKind == JsonValueKind.Object
                            && entry.TryGetProperty("name", out JsonElement named)
                                ? named.GetString() ?? string.Empty
                                : entry.GetString() ?? string.Empty;

                        yield return new DependencyReference(section, name);
                    }
                }
            }
        }
    }

    /// <summary>Extrai <c>Npgsql</c> de uma chave no formato <c>Npgsql/10.0.3</c>.</summary>
    private static string PackageName(string key)
    {
        int separator = key.IndexOf('/', StringComparison.Ordinal);

        return separator < 0 ? key : key[..separator];
    }

    /// <summary>Extrai <c>Npgsql</c> de uma entrada no formato <c>Npgsql &gt;= 10.0.3</c>.</summary>
    private static string FirstToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int separator = value.IndexOf(' ', StringComparison.Ordinal);

        return separator < 0 ? value : value[..separator];
    }
}
