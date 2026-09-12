using Xunit;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// Pré-condição estrutural das quatro camadas da matriz: os eixos de fragmento existem no
/// repositório, com os nomes que docs/architecture/generation-engine.md declara.
/// </summary>
/// <remarks>
/// Os fragmentos em si chegam de T03 em diante. O que este teste protege agora é o contrato de
/// diretórios — renomear um eixo sem atualizar a documentação quebra aqui, não em silêncio
/// durante a composição.
/// </remarks>
public sealed class TemplateAxesTests
{
    public static TheoryData<string> Axes =>
    [
        "common",
        "architecture/simple",
        "architecture/clean",
        "database/none",
        "database/sqlite",
        "database/postgresql",
        "auth/none",
        "auth/identity",
        "auth/jwt",
        "swagger/enabled",
    ];

    [Theory]
    [MemberData(nameof(Axes))]
    public void Eixo_de_template_existe(string axis)
    {
        string directory = Path.Combine(
            RepositoryLayout.TemplatesDirectory,
            axis.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(
            Directory.Exists(directory),
            $"O eixo de template '{axis}' não existe em '{directory}'. " +
            "Ver docs/architecture/generation-engine.md, seção \"Composição\".");
    }
}
