using System.Reflection;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// Localiza arquivos do repositório a partir do assembly de teste, sem depender do diretório de
/// trabalho de quem executou <c>dotnet test</c>.
/// </summary>
internal static class RepositoryLayout
{
    /// <summary>Raiz do repositório.</summary>
    public static string Root { get; } = ResolveRoot();

    /// <summary>Raiz dos fragmentos de template (docs/architecture/generation-engine.md).</summary>
    public static string TemplatesDirectory { get; } =
        Path.Combine(Root, "src", "TemplateGenerator.Generation", "Templates");

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
