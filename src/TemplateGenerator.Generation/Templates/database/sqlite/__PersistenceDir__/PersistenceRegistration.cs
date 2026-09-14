using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
__PersistenceHeader__

/// <summary>
/// O registro da persistência em SQLite, em um lugar só.
/// </summary>
/// <remarks>
/// A composição da aplicação chama este método e não precisa conhecer EF Core, provedor nem
/// <c>DbContext</c>: trocar de banco troca este arquivo, e nada no projeto de API.
///
/// A migração não é aplicada aqui, de propósito: aplicá-la é um passo próprio, com
/// <c>dotnet ef database update</c>, descrito no <c>README.md</c>. A aplicação não altera o
/// esquema do banco ao subir.
/// </remarks>
public static class PersistenceRegistration
{
    /// <summary>
    /// Registra a sessão com o banco e a implementação da porta de armazenamento.
    /// </summary>
    /// <param name="services">Os serviços da aplicação.</param>
    /// <param name="connectionString">
    /// A cadeia de conexão, normalmente lida de <c>ConnectionStrings:Default</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Quando a cadeia de conexão não foi configurada. A falha aparece na subida, com o nome da
    /// chave que falta, e não no meio de uma requisição.
    /// </exception>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A cadeia de conexão 'ConnectionStrings:Default' não está configurada. " +
                "Ela vem preenchida em appsettings.json; confira se o arquivo foi alterado.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IItemStore, ItemStore>();

        return services;
    }
}
