
## Autenticação

Esta combinação usa **ASP.NET Core Identity nativo**, com os usuários guardados no mesmo banco da
aplicação. Cadastro, login e renovação são endpoints do próprio framework, mapeados por
`MapIdentityApi` em `Program.cs`: não há código de autenticação escrito neste projeto, e não há
biblioteca de terceiros envolvida.

### Os tokens são bearer próprios do Identity, e não JWT de terceiros

O que `POST /auth/login` devolve é um **bearer token próprio do ASP.NET Core Identity** — um valor
opaco, emitido e validado pela própria aplicação. **Não é um JWT de terceiros.** Não há provedor de
identidade externo, não há emissor nem audiência a apontar e, principalmente, **não existe segredo
de assinatura para configurar**: nem em `appsettings.json`, nem em variável de ambiente, nem em
`dotnet user-secrets`. Se você procurar uma chave de assinatura neste projeto, não vai encontrar,
porque ela não existe. Para validar token emitido por um provedor que já existe na sua empresa,
gere o projeto com a opção JWT em vez desta.

### Criar as tabelas de identidade

As tabelas do Identity ficam no mesmo banco, num `DbContext` separado — `AppIdentityDbContext` —,
com migração própria em `Persistence/Identity/Migrations/`, ao lado dele. Como o projeto passa a ter
dois `DbContext`, cada comando do `dotnet ef` diz em qual deles está trabalhando. Depois de aplicar
a migração da aplicação, aplique a de identidade:

```bash
dotnet ef database update --project __PersistenceProjectDir__ --startup-project __ApiProjectDir__ --context AppIdentityDbContext
```

Isso cria as tabelas `AspNetUsers`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`,
`AspNetRoles`, `AspNetUserRoles` e `AspNetRoleClaims`. Reaplicar o que já está aplicado não faz
nada, então repetir o comando é seguro.

### Cadastrar, entrar e renovar vem depois de subir a aplicação

São chamadas HTTP, e precisam da aplicação no ar: estão na seção **Usar a autenticação**, logo
depois de **Executar**. Aqui, antes de subir, só há o que se faz com a aplicação parada — a
migração de identidade, acima.

### Envio de e-mail exige configuração adicional e está fora do fluxo garantido do MVP

O Identity também expõe `/auth/confirmEmail`, `/auth/resendConfirmationEmail`,
`/auth/forgotPassword` e `/auth/resetPassword`. Eles existem, mas **enviar a mensagem exige
configurar um serviço de e-mail, e isso está fora do fluxo garantido deste projeto**: como nenhum
`IEmailSender` foi registrado, o endpoint responde com sucesso e **nenhum e-mail sai**. Para usá-los
de verdade, registre uma implementação de `IEmailSender<AppUser>` na composição da aplicação e
aponte-a para o seu provedor.

O que este projeto garante é o caminho que não depende de e-mail: **cadastro, login e renovação**. A
conta nasce utilizável e `POST /auth/login` funciona sem nenhuma confirmação.
