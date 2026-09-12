using TemplateGenerator.Generation.Catalog;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Catalog;

/// <summary>
/// O catálogo é a fonte de verdade das opções (RF-02): estes testes comparam o que ele devolve
/// com a matriz de docs/product/option-matrix.md.
/// </summary>
public sealed class TemplateCatalogTests
{
    private static readonly TemplateOptionsCatalog _catalog = TemplateCatalog.Current;

    [Fact]
    public void Declara_a_versao_do_conjunto_de_templates()
    {
        Assert.False(string.IsNullOrWhiteSpace(_catalog.TemplateVersion));
    }

    [Fact]
    public void Publica_os_campos_configuraveis_na_ordem_da_matriz()
    {
        string[] expected =
        [
            CatalogFields.Architecture,
            CatalogFields.Database,
            CatalogFields.Authentication,
            CatalogFields.Swagger,
            CatalogFields.DotnetVersion,
        ];

        Assert.Equal(expected, _catalog.Fields.Keys);
    }

    [Fact]
    public void Nao_publica_projectName_como_campo_de_escolha()
    {
        // `projectName` é o sexto campo da matriz, mas não é uma escolha: é texto livre validado
        // por regra (docs/product/option-matrix.md) e o contrato o mantém fora de `fields`.
        Assert.False(_catalog.Fields.ContainsKey(CatalogFields.ProjectName));
    }

    [Theory]
    [InlineData(CatalogFields.Architecture, "Arquitetura", "simple")]
    [InlineData(CatalogFields.Database, "Banco", "none")]
    [InlineData(CatalogFields.Authentication, "Autenticação", "none")]
    [InlineData(CatalogFields.DotnetVersion, "Versão .NET", "net10.0")]
    public void Campo_de_escolha_traz_rotulo_em_portugues_e_padrao(
        string key,
        string label,
        string defaultValue)
    {
        TemplateField field = _catalog.Fields[key];

        Assert.Equal(label, field.Label);
        Assert.Equal(CatalogValue.OfText(defaultValue), field.Default);
        Assert.Equal(CatalogFieldTypes.Choice, field.Type);
        Assert.NotNull(field.Values);
        Assert.Contains(field.Values!, option => option.Value == defaultValue);
        Assert.All(field.Values!, option => Assert.False(string.IsNullOrWhiteSpace(option.Label)));
    }

    [Theory]
    [InlineData(CatalogFields.Architecture, "simple", "clean")]
    [InlineData(CatalogFields.Database, "none", "sqlite", "postgresql")]
    [InlineData(CatalogFields.Authentication, "none", "identity", "jwt")]
    [InlineData(CatalogFields.DotnetVersion, "net10.0")]
    public void Campo_de_escolha_traz_exatamente_os_valores_da_matriz(
        string key,
        params string[] expected)
    {
        Assert.Equal(expected, _catalog.Fields[key].Values!.Select(option => option.Value));
    }

    [Fact]
    public void Swagger_e_um_interruptor_ligado_por_padrao()
    {
        TemplateField field = _catalog.Fields[CatalogFields.Swagger];

        Assert.Equal("Swagger", field.Label);
        Assert.Equal(CatalogFieldTypes.Boolean, field.Type);
        Assert.Null(field.Values);
        Assert.True(field.IsToggle);
        Assert.Equal(CatalogValue.OfFlag(true), field.Default);
    }

    [Fact]
    public void Todo_campo_declara_o_proprio_tipo()
    {
        // O contrato proíbe deduzir o tipo pela ausência de `values`, inclusive no campo de
        // escolha (docs/architecture/http-contract.md, "Regras de serialização", item 3).
        Assert.All(
            _catalog.Fields,
            entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value.Type));

                Assert.Equal(
                    entry.Value.IsToggle ? CatalogFieldTypes.Boolean : CatalogFieldTypes.Choice,
                    entry.Value.Type);
            });
    }

    [Fact]
    public void O_padrao_de_todo_campo_e_um_valor_que_o_proprio_campo_aceita()
    {
        Assert.All(
            _catalog.Fields,
            entry => Assert.True(
                entry.Value.Accepts(entry.Value.Default),
                $"O padrão de '{entry.Key}' não está entre os valores que ele aceita."));
    }

    [Fact]
    public void A_restricao_de_Identity_esta_no_catalogo_como_dado()
    {
        TemplateConstraint constraint = Assert.Single(
            _catalog.Constraints,
            candidate => candidate.Id == TemplateCatalog.IdentityRequiresDatabaseId);

        ConstraintTerm when = Assert.Single(constraint.When);
        Assert.Equal(CatalogFields.Authentication, when.Field);
        Assert.Equal<CatalogValue>([CatalogValue.OfText("identity")], when.AcceptedValues);

        ConstraintTerm requires = Assert.Single(constraint.Requires);
        Assert.Equal(CatalogFields.Database, requires.Field);
        Assert.Equal<CatalogValue>(
            [CatalogValue.OfText("sqlite"), CatalogValue.OfText("postgresql")],
            requires.AcceptedValues);

        Assert.Equal(
            "O Identity nativo precisa de um banco para persistir os usuários.",
            constraint.Message);
    }

    [Fact]
    public void Toda_restricao_cita_campos_e_valores_que_existem_no_catalogo()
    {
        // Uma restrição sobre campo inexistente nunca dispararia, e ninguém perceberia: ela
        // simplesmente não se aplicaria a seleção nenhuma.
        foreach (TemplateConstraint constraint in _catalog.Constraints)
        {
            foreach (ConstraintTerm term in constraint.When.Concat(constraint.Requires))
            {
                TemplateField? field = _catalog.FindField(term.Field);

                Assert.True(
                    field is not null,
                    $"A restrição '{constraint.Id}' cita o campo '{term.Field}', que não existe.");

                Assert.All(
                    term.AcceptedValues,
                    value => Assert.True(
                        field!.Accepts(value),
                        $"A restrição '{constraint.Id}' cita o valor '{value}', que o campo " +
                        $"'{term.Field}' não aceita."));
            }

            Assert.False(string.IsNullOrWhiteSpace(constraint.Message));
        }
    }
}
