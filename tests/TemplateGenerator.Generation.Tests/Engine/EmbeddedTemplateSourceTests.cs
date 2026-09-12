using System.Xml.Linq;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// O contrato de carga dos fragmentos: onde eles moram, como são declarados no <c>.csproj</c> e
/// o que acontece com um arquivo fora dos eixos.
/// </summary>
/// <remarks>
/// Este é o teste que o papel <c>template-engineer</c> quebra primeiro se errar um nome de
/// diretório. Sem ele, um fragmento em <c>Templates/autenticacao/jwt/</c> simplesmente nunca
/// entraria em pacote nenhum — e nada falharia.
/// </remarks>
public sealed class EmbeddedTemplateSourceTests
{
    [Fact]
    public void Todo_arquivo_embutido_pertence_a_um_eixo_declarado()
    {
        string[] orphans =
        [
            .. EmbeddedTemplateSource.Default.ResourcePaths
                .Where(path => TemplateAxes.FragmentOf(path) is null),
        ];

        Assert.True(
            orphans.Length == 0,
            "Estes arquivos estão sob Templates/ mas não sob um eixo que o catálogo declara, " +
            "e por isso nunca entrariam em pacote algum. Eixos válidos: " +
            string.Join(", ", TemplateAxes.All) + Environment.NewLine +
            string.Join(Environment.NewLine, orphans));
    }

    [Fact]
    public void O_prefixo_do_recurso_concorda_com_o_declarado_no_csproj()
    {
        // O nome lógico do recurso é fixado por metadado no .csproj. Se alguém mexer lá e não
        // aqui, a origem passa a não achar template nenhum — e o pacote sai só com o manifesto,
        // sem erro. Este teste transforma esse silêncio em falha.
        string csprojPath = Path.Combine(
            RepositoryLayout.GenerationProjectDirectory,
            "TemplateGenerator.Generation.csproj");

        string[] logicalNames =
        [
            .. XDocument.Load(csprojPath).Root!
                .Descendants()
                .Where(element =>
                    element.Name.LocalName.Equals("EmbeddedResource", StringComparison.Ordinal))
                .Select(element => element.Attribute("LogicalName")?.Value)
                .Where(value => !string.IsNullOrEmpty(value))
                .Select(value => value!),
        ];

        string declared = Assert.Single(logicalNames);

        Assert.StartsWith(EmbeddedTemplateSource.ResourcePrefix, declared, StringComparison.Ordinal);
    }

    [Fact]
    public void Um_fragmento_sem_arquivo_devolve_lista_vazia_e_nao_explode()
    {
        // Enquanto os templates de produção não existem, é isto que acontece com todo eixo. O
        // conteúdo obrigatório de cada combinação é cobrado na camada 1 da matriz, que é onde a
        // ausência precisa doer.
        Assert.NotNull(EmbeddedTemplateSource.Default.Read(TemplateAxes.Common));
    }

    [Fact]
    public void Ler_um_fragmento_devolve_os_arquivos_em_ordem_ordinal()
    {
        foreach (string fragment in TemplateAxes.All)
        {
            IReadOnlyList<TemplateFile> files = EmbeddedTemplateSource.Default.Read(fragment);

            string[] asRead = [.. files.Select(file => file.Path)];

            Assert.Equal([.. asRead.Order(StringComparer.Ordinal)], asRead);
        }
    }
}
