using System.Reflection;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Localiza arquivos do repositório a partir do assembly de teste, sem depender do diretório de
/// trabalho de quem executou <c>dotnet test</c>.
/// </summary>
/// <remarks>
/// A raiz vem do metadado <c>RepositoryRoot</c> gravado por <c>Directory.Build.props</c> — o mesmo
/// mecanismo dos projetos <c>Generation.Tests</c> e <c>Matrix.Tests</c>. Cada assembly de teste tem
/// a sua cópia porque o tipo é <c>internal</c> e lê o metadado do próprio assembly.
/// </remarks>
internal static class RepositoryLayout
{
    /// <summary>Raiz do repositório.</summary>
    public static string Root { get; } = ResolveRoot();

    /// <summary>Caminho absoluto de um arquivo dado relativo à raiz, com <c>/</c> como separador.</summary>
    public static string Resolve(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

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
