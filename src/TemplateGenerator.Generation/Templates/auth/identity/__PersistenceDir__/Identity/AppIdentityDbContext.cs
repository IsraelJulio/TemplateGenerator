using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace __PersistenceNamespace__.Identity;

/// <summary>
/// A sessão com o banco das tabelas de identidade — usuários, logins externos, <em>tokens</em> e
/// papéis —, criadas por <see cref="IdentityDbContext{TUser}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>São dois <c>DbContext</c> no mesmo banco</strong>, de propósito: este e o
/// <c>AppDbContext</c>, que guarda os dados da aplicação. Cada um tem o próprio conjunto de
/// migrações, e por isso todo comando do <c>dotnet ef</c> neste projeto leva
/// <c>--context</c> dizendo de qual dos dois se trata — o <c>README.md</c> traz os dois comandos,
/// na ordem.
/// </para>
/// <para>
/// A separação é o que mantém as tabelas do Identity fora das migrações da sua aplicação: uma
/// entidade nova sua não reescreve o esquema de identidade, e uma atualização do ASP.NET Core
/// Identity não toca nas suas tabelas.
/// </para>
/// <para>
/// As tabelas de <strong>papel</strong> (<c>AspNetRoles</c>, <c>AspNetUserRoles</c> e
/// <c>AspNetRoleClaims</c>) são criadas porque fazem parte do modelo padrão do Identity, mas
/// nenhuma autorização deste projeto olha para elas: a exigência é só a de estar autenticado.
/// </para>
/// </remarks>
/// <param name="options">As opções que a composição da aplicação já resolveu.</param>
public sealed class AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
    : IdentityDbContext<AppUser>(options);
