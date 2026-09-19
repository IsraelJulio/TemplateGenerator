using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace __PersistenceNamespace__.Identity;

/// <summary>
/// O registro do armazenamento das tabelas de identidade, em um lugar só.
/// </summary>
/// <remarks>
/// <para>
/// Só o <strong>armazenamento</strong> mora aqui. Quem liga os endpoints do Identity e a
/// autorização é a composição da aplicação, no projeto de API: eles dependem do ASP.NET Core, e
/// este projeto não.
/// </para>
/// <para>
/// A migração não é aplicada aqui, pelo mesmo motivo do armazenamento dos dados da aplicação:
/// aplicá-la é um passo próprio, com <c>dotnet ef database update</c>, descrito no
/// <c>README.md</c>. A aplicação não altera o esquema do banco ao subir.
/// </para>
/// </remarks>
public static class IdentityRegistration
{
    /// <summary>
    /// Registra a sessão com o banco das tabelas de identidade.
    /// </summary>
    /// <param name="services">Os serviços da aplicação.</param>
    /// <param name="connectionString">
    /// A cadeia de conexão, normalmente lida de <c>ConnectionStrings:Default</c>. É a mesma do
    /// restante da aplicação: as tabelas de identidade ficam no mesmo banco.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Quando a cadeia de conexão não foi configurada. A falha aparece na subida, com o nome da
    /// chave que falta, e não no meio de uma requisição.
    /// </exception>
    public static IServiceCollection AddIdentityStores(
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

        services.AddDbContext<AppIdentityDbContext>(options => __EfUseProvider__);

        return services;
    }
}
