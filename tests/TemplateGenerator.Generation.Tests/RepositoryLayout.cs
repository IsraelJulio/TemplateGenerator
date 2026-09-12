using System.Reflection;

namespace TemplateGenerator.Generation.Tests;

/// <summary>
/// Localiza arquivos do repositório a partir do assembly de teste, sem depender do diretório de
/// trabalho de quem executou <c>dotnet test</c>.
/// </summary>
/// <remarks>
/// A raiz vem do metadado <c>RepositoryRoot</c> gravado por <c>Directory.Build.props</c>.
/// </remarks>
internal static class RepositoryLayout
{
    /// <summary>Raiz do repositório.</summary>
    public static string Root { get; } = ResolveRoot();

    /// <summary>Diretório do projeto <c>TemplateGenerator.Generation</c>.</summary>
    public static string GenerationProjectDirectory { get; } =
        Path.Combine(Root, "src", "TemplateGenerator.Generation");

    /// <summary>
    /// Grafo de restore do projeto <c>TemplateGenerator.Generation</c>, produzido pelo NuGet.
    /// </summary>
    public static string GenerationAssetsFile { get; } =
        Path.Combine(GenerationProjectDirectory, "obj", "project.assets.json");

    private static string ResolveRoot()
    {
        string? fromMetadata = typeof(RepositoryLayout).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Key, "RepositoryRoot", StringComparison.Ordinal))
            ?.Value;

        if (string.IsNullOrWhiteSpace(fromMetadata))
        {
            throw new InvalidOperationException(
                "O metadado de assembly 'RepositoryRoot' não foi gravado. " +
                "Verifique o ItemGroup de AssemblyMetadata em Directory.Build.props.");
        }

        string root = Path.GetFullPath(fromMetadata);

        if (!File.Exists(Path.Combine(root, "TemplateGenerator.sln")))
        {
            throw new InvalidOperationException(
                $"'{root}' não parece a raiz do repositório: TemplateGenerator.sln não está lá.");
        }

        return root;
    }
}
