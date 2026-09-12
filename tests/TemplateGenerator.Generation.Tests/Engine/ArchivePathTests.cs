using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// A regra do caminho de entrada do ZIP (RNF-03), examinada isoladamente.
/// </summary>
public sealed class ArchivePathTests
{
    [Theory]
    [InlineData("README.md")]
    [InlineData("src/Acme.Api/Program.cs")]
    [InlineData(".templategenerator/manifest.json")]
    [InlineData(".gitignore")]
    [InlineData("tests/Acme.Tests/Acme.Tests.csproj")]
    [InlineData("src/Acme..Api/Item.cs")]
    [InlineData("Acme.Api.sln")]
    public void Caminho_legitimo_e_aceito(string path)
    {
        Assert.True(ArchivePath.IsValid(path, out string? error), error);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("", "vazio")]
    [InlineData("   ", "vazio")]
    [InlineData("../fora.txt", "travessia")]
    [InlineData("a/../../fora.txt", "travessia")]
    [InlineData("./a.txt", "travessia")]
    [InlineData("/etc/passwd", "absoluto")]
    [InlineData("C:/Windows/x.dll", "caractere")]
    [InlineData("\\\\servidor\\share\\x", "caractere")]
    [InlineData("a.txt:fluxo", "caractere")]
    [InlineData("src\\Program.cs", "caractere")]
    [InlineData("src/a*.cs", "caractere")]
    [InlineData("src/a?.cs", "caractere")]
    [InlineData("src/Api/", "diretório")]
    [InlineData("src//Program.cs", "vazio")]
    [InlineData("CON/x.txt", "reservado")]
    [InlineData("src/lpt3.log", "reservado")]
    [InlineData("src/x.cs ", "espaço")]
    [InlineData("src/x.", "espaço")]
    [InlineData("src/relatório.md", "ASCII")]
    public void Caminho_perigoso_e_recusado_com_motivo(string path, string expectedInMessage)
    {
        Assert.False(ArchivePath.IsValid(path, out string? error));
        Assert.NotNull(error);
        Assert.Contains(expectedInMessage, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Caminho_com_caractere_de_controle_e_recusado()
    {
        Assert.False(ArchivePath.IsValid("src/Pro\u0000gram.cs", out string? error));
        Assert.Contains("controle", error, StringComparison.Ordinal);
    }
}
