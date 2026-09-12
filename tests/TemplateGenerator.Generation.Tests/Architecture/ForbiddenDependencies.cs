namespace TemplateGenerator.Generation.Tests.Architecture;

/// <summary>
/// As duas famílias de dependência proibidas em <c>TemplateGenerator.Generation</c>, conforme
/// docs/architecture/platform.md.
/// </summary>
internal static class ForbiddenDependencies
{
    /// <summary>
    /// ASP.NET Core. A biblioteca de geração não conhece HTTP: entra configuração, sai fluxo de
    /// bytes. É isso que permite exercitar as 32 combinações sem subir a Api.
    /// </summary>
    public static readonly string[] AspNetCorePrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Hosting", // hospedagem é composição, não geração
        "Swashbuckle",
    ];

    /// <summary>
    /// Providers de banco. O gerador é stateless: não tem banco, não guarda nada
    /// (docs/architecture/platform.md).
    /// </summary>
    public static readonly string[] DatabaseProviderPrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Data.Sqlite",
        "Microsoft.Data.SqlClient",
        "System.Data.SqlClient",
        "System.Data.SQLite",
        "SQLitePCLRaw",
        "Npgsql",
        "MySql",
        "MySqlConnector",
        "Oracle.ManagedDataAccess",
        "MongoDB.Driver",
        "Dapper",
    ];

    /// <summary>
    /// <c>FrameworkReference</c> proibida: é o que o SDK <c>Microsoft.NET.Sdk.Web</c> injeta.
    /// </summary>
    public const string AspNetCoreFrameworkReference = "Microsoft.AspNetCore.App";

    /// <summary>Todas as proibições, para varredura única.</summary>
    public static IEnumerable<string> All => AspNetCorePrefixes.Concat(DatabaseProviderPrefixes);

    /// <summary>
    /// Diz se <paramref name="name"/> é um identificador proibido. A comparação é ordinal e por
    /// prefixo de segmento, para pegar tanto <c>Npgsql</c> quanto
    /// <c>Npgsql.EntityFrameworkCore.PostgreSQL</c>.
    /// </summary>
    public static string? MatchPrefix(string name)
    {
        foreach (string prefix in All)
        {
            bool matches =
                name.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                return prefix;
            }
        }

        return null;
    }
}
