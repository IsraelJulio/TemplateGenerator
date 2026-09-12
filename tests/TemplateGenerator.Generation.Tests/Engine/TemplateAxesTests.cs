using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// A correspondência entre catálogo e diretório de fragmentos
/// (docs/architecture/generation-engine.md, seção "Composição").
/// </summary>
public sealed class TemplateAxesTests
{
    private static GenerationRequest Request(
        string architecture = "simple",
        string database = "none",
        string authentication = "none",
        bool swagger = true) =>
        new("Acme.Api", architecture, database, authentication, swagger, "net10.0");

    [Fact]
    public void Os_eixos_do_catalogo_sao_os_dez_diretorios_documentados()
    {
        // A lista vem do catálogo, não está escrita no motor. Este teste é o que amarra as duas
        // coisas: publicar `database/mysql` no catálogo passa a exigir o diretório, e apagar um
        // valor do catálogo deixa de exigir — as duas mudanças aparecem aqui.
        string[] expected =
        [
            "architecture/clean",
            "architecture/simple",
            "auth/identity",
            "auth/jwt",
            "auth/none",
            "common",
            "database/none",
            "database/postgresql",
            "database/sqlite",
            "swagger/enabled",
        ];

        string[] actual = [.. TemplateAxes.All];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void A_selecao_segue_a_ordem_declarada_da_composicao()
    {
        string[] expected =
        [
            "common",
            "architecture/clean",
            "database/sqlite",
            "auth/identity",
            "swagger/enabled",
        ];

        string[] actual = [.. TemplateAxes.Select(Request("clean", "sqlite", "identity"))];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Swagger_desligado_nao_seleciona_fragmento_de_swagger()
    {
        Assert.DoesNotContain(
            TemplateAxes.SwaggerEnabled,
            TemplateAxes.Select(Request(swagger: false)));
    }

    [Fact]
    public void O_campo_authentication_mora_no_diretorio_auth()
    {
        // A única correspondência que não é dedutível do nome do campo. Se ela mudar em silêncio,
        // todo fragmento de autenticação deixa de ser encontrado e o pacote sai sem erro nenhum.
        Assert.Contains("auth/jwt", TemplateAxes.Select(Request(authentication: "jwt")));
    }

    [Theory]
    [InlineData("common/global.json", "common")]
    [InlineData("architecture/simple/src/Program.cs", "architecture/simple")]
    [InlineData("auth/identity/src/Identity.cs", "auth/identity")]
    [InlineData("swagger/enabled/src/Swagger.cs", "swagger/enabled")]
    public void Um_caminho_de_template_e_atribuido_ao_fragmento_certo(string path, string fragment) =>
        Assert.Equal(fragment, TemplateAxes.FragmentOf(path));

    [Theory]
    [InlineData("comon/global.json")]           // eixo com nome errado
    [InlineData("database/mysql/AppDb.cs")]     // valor que o catálogo não publica
    [InlineData("authentication/jwt/Auth.cs")]  // diretório 'authentication' não existe
    [InlineData("global.json")]                 // solto, fora de qualquer fragmento
    [InlineData("common")]                      // um fragmento sem arquivo dentro não é arquivo
    public void Caminho_fora_de_um_eixo_declarado_nao_pertence_a_fragmento_nenhum(string path) =>
        Assert.Null(TemplateAxes.FragmentOf(path));
}
