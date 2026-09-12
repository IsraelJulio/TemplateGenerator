using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// Pré-condição estrutural das quatro camadas da matriz: os eixos de fragmento existem no
/// repositório, com os nomes que docs/architecture/generation-engine.md declara.
/// </summary>
/// <remarks>
/// A lista de eixos vem de <see cref="TemplateAxes.All"/>, que o motor deriva do catálogo — não
/// de uma cópia escrita aqui. Publicar um valor novo no catálogo passa a exigir o diretório
/// correspondente automaticamente; renomear um eixo sem criar o diretório quebra aqui, e não em
/// silêncio durante a composição.
/// </remarks>
public sealed class TemplateAxesTests
{
    public static TheoryData<string> Axes
    {
        get
        {
            TheoryData<string> data = [];

            foreach (string axis in TemplateAxes.All)
            {
                data.Add(axis);
            }

            return data;
        }
    }

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
