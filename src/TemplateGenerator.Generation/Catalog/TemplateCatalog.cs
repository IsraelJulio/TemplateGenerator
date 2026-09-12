namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// O catálogo vigente da plataforma — a matriz de docs/product/option-matrix.md escrita uma vez
/// só, do lado do servidor.
/// </summary>
/// <remarks>
/// <para>
/// É imutável e compartilhado: não há estado por requisição aqui. O gerador continua
/// <em>stateless</em> (docs/architecture/http-contract.md).
/// </para>
/// <para>
/// Acrescentar um valor ou uma restrição é editar este arquivo e mais nada — nem a Api nem o
/// frontend enumeram valores (RF-02).
/// </para>
/// </remarks>
public static class TemplateCatalog
{
    /// <summary>
    /// Versão do conjunto de templates. Aparece no catálogo e no manifesto do ZIP; é ela que
    /// identifica a geração, já que o contrato não versiona por URL.
    /// </summary>
    public const string CurrentVersion = "1.0.0";

    /// <summary>Identificador da restrição de Identity, citado pelos testes e pela tela.</summary>
    public const string IdentityRequiresDatabaseId = "identity-requires-database";

    /// <summary>O catálogo vigente.</summary>
    public static TemplateOptionsCatalog Current { get; } = Build();

    private static TemplateOptionsCatalog Build()
    {
        // Dicionário ordenado: a ordem das chaves é a ordem em que a tela desenha os campos.
        OrderedDictionary<string, TemplateField> fields = new(StringComparer.Ordinal)
        {
            [CatalogFields.Architecture] = new TemplateField
            {
                Label = "Arquitetura",
                Default = CatalogValue.OfText("simple"),
                Type = CatalogFieldTypes.Choice,
                Values =
                [
                    new TemplateOptionValue("simple", "Simples")
                    {
                        Description = "Um projeto Web API.",
                    },
                    new TemplateOptionValue("clean", "Clean Architecture")
                    {
                        Description = "Api, Application, Domain e Infrastructure.",
                    },
                ],
            },
            [CatalogFields.Database] = new TemplateField
            {
                Label = "Banco",
                Default = CatalogValue.OfText("none"),
                Type = CatalogFieldTypes.Choice,
                Values =
                [
                    new TemplateOptionValue("none", "Nenhum")
                    {
                        Description = "Sem persistência.",
                    },
                    new TemplateOptionValue("sqlite", "SQLite")
                    {
                        Description = "Arquivo local, sem serviço externo.",
                    },
                    new TemplateOptionValue("postgresql", "PostgreSQL")
                    {
                        Description = "Servidor PostgreSQL via Npgsql.",
                    },
                ],
            },
            [CatalogFields.Authentication] = new TemplateField
            {
                Label = "Autenticação",
                Default = CatalogValue.OfText("none"),
                Type = CatalogFieldTypes.Choice,
                Values =
                [
                    new TemplateOptionValue("none", "Nenhuma")
                    {
                        Description = "Endpoints abertos.",
                    },
                    new TemplateOptionValue("identity", "Identity")
                    {
                        Description = "ASP.NET Core Identity, com usuários persistidos.",
                    },
                    new TemplateOptionValue("jwt", "JWT")
                    {
                        Description = "Validação de token, sem persistência de usuário.",
                    },
                ],
            },
            [CatalogFields.Swagger] = new TemplateField
            {
                Label = "Swagger",
                Default = CatalogValue.OfFlag(true),
                Type = CatalogFieldTypes.Boolean,
                Description = "Interface de exploração da API no projeto gerado.",
            },
            [CatalogFields.DotnetVersion] = new TemplateField
            {
                Label = "Versão .NET",
                Default = CatalogValue.OfText("net10.0"),
                Type = CatalogFieldTypes.Choice,

                // Um valor só no MVP, mas modelado como lista para permitir expansão sem quebrar
                // o contrato (docs/product/option-matrix.md).
                Values = [new TemplateOptionValue("net10.0", ".NET 10")],
            },
        };

        return new TemplateOptionsCatalog
        {
            TemplateVersion = CurrentVersion,
            Fields = fields,
            Constraints =
            [
                new TemplateConstraint
                {
                    Id = IdentityRequiresDatabaseId,
                    When = [ConstraintTerm.OfText(CatalogFields.Authentication, "identity")],
                    Requires = [ConstraintTerm.OfText(CatalogFields.Database, "sqlite", "postgresql")],
                    Message = "O Identity nativo precisa de um banco para persistir os usuários.",
                },
            ],
        };
    }
}
