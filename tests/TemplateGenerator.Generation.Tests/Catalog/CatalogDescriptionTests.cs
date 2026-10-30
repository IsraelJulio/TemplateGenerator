using TemplateGenerator.Generation.Catalog;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Catalog;

/// <summary>
/// Contrato (docs/architecture/http-contract.md): todo valor de um campo "choice" traz
/// <c>description</c> não vazia.
/// </summary>
public sealed class CatalogDescriptionTests
{
    [Fact]
    public void Todo_valor_de_escolha_tem_description_nao_vazia()
    {
        var missing = new List<string>();

        foreach (var (name, field) in TemplateCatalog.Current.Fields)
        {
            if (field.Type != CatalogFieldTypes.Choice)
            {
                continue;
            }

            foreach (var value in field.Values ?? [])
            {
                if (string.IsNullOrWhiteSpace(value.Description))
                {
                    missing.Add($"{name}/{value.Value}");
                }
            }
        }

        Assert.True(missing.Count == 0, "Sem description: " + string.Join(", ", missing));
    }
}
