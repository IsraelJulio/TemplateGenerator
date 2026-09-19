using Microsoft.AspNetCore.Identity;

namespace __PersistenceNamespace__.Identity;

/// <summary>
/// O usuário da aplicação.
/// </summary>
/// <remarks>
/// <para>
/// Herda de <see cref="IdentityUser"/> e não acrescenta nada: o identificador é uma
/// <see cref="string"/> (um GUID em texto, gerado pelo próprio Identity), e o e-mail e a senha já
/// vêm dele. Acrescente aqui as propriedades que a sua aplicação precisar — e lembre que toda
/// propriedade nova é uma coluna, logo uma migração nova.
/// </para>
/// <para>
/// Não há papel nem <em>claim</em> de autorização em uso: quem apresenta um token válido acessa o
/// CRUD inteiro, e os dados são compartilhados entre os usuários autenticados.
/// </para>
/// </remarks>
public sealed class AppUser : IdentityUser;
