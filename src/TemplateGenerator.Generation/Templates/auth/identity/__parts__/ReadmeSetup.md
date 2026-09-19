
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

### Cadastrar, entrar e renovar

Estes comandos falam com a aplicação **já no ar**: suba-a primeiro, com o passo **Executar** logo
adiante, e use outro terminal. Em Linux, macOS ou Git Bash:

```bash
curl -i -X POST http://localhost:5100/auth/register -H 'Content-Type: application/json' -d '{"email":"pessoa@exemplo.com","password":"Senha!123"}'
curl -i -X POST http://localhost:5100/auth/login -H 'Content-Type: application/json' -d '{"email":"pessoa@exemplo.com","password":"Senha!123"}'
```

No PowerShell, o corpo vai pelo **pipe**, e não em `-d`, e a primeira linha fixa a codificação dele.
Os dois cuidados estão explicados na seção **Testar o CRUD**, adiante, e valem para qualquer chamada
com corpo JSON — inclusive estas, cujo exemplo não tem espaço nem acento e por isso funcionaria de
qualquer jeito. É justamente esse "funciona à toa" que faz a forma errada parecer certa:

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
'{"email":"pessoa@exemplo.com","password":"Senha!123"}' | curl.exe -i -X POST http://localhost:5100/auth/register -H "Content-Type: application/json" --data-binary '@-'
'{"email":"pessoa@exemplo.com","password":"Senha!123"}' | curl.exe -i -X POST http://localhost:5100/auth/login -H "Content-Type: application/json" --data-binary '@-'
```

O cadastro responde `200` com corpo vazio. O login responde `200` com um corpo assim:

```json
{
  "tokenType": "Bearer",
  "accessToken": "CfDJ8...",
  "expiresIn": 3600,
  "refreshToken": "CfDJ8..."
}
```

Guarde os dois valores. O `accessToken` **vale uma hora** — é o que `expiresIn` diz, em segundos —, e
depois disso toda chamada autenticada volta a responder `401`. Quando isso acontecer, troque o
`refreshToken` por um par novo, sem pedir a senha de novo:

```bash
curl -i -X POST http://localhost:5100/auth/refresh -H 'Content-Type: application/json' -d '{"refreshToken":"COLE_O_REFRESH_TOKEN_AQUI"}'
```

A senha precisa ter ao menos seis caracteres, com maiúscula, minúscula, dígito e um caractere não
alfanumérico — são as regras padrão do Identity. Uma senha fraca responde `400` com a lista do que
falta.

**O cadastro é aberto.** `POST /auth/register` não exige autenticação, e quem se cadastra passa a
ter acesso ao CRUD inteiro — os dados são compartilhados entre todos os usuários autenticados, sem
dono e sem papel. É o comportamento pedido para este ponto de partida, e **não** é o que você quer
em produção. Fechar o cadastro é decisão sua e cabe a você: exigir convite, exigir confirmação de
e-mail antes do primeiro login, restringir a rota por rede, ou trocá-la por um provedor de
identidade da sua organização. Enquanto isso não for feito, trate a aplicação como aberta a quem
alcançar a porta.

### Sem token, o CRUD responde 401

`GET /health` continua **público**. O CRUD de `Item`, não:

```bash
curl -i http://localhost:5100/items
curl -i http://localhost:5100/items -H 'Authorization: Bearer COLE_O_ACCESS_TOKEN_AQUI'
```

A primeira chamada responde `401`; a segunda, `200`. **Qualquer token válido serve**: não há papel,
não há claim exigida e não há dono de registro — quem está autenticado enxerga e altera os mesmos
itens que todo mundo.

### Envio de e-mail exige configuração adicional e está fora do fluxo garantido do MVP

O Identity também expõe `/auth/confirmEmail`, `/auth/resendConfirmationEmail`,
`/auth/forgotPassword` e `/auth/resetPassword`. Eles existem, mas **enviar a mensagem exige
configurar um serviço de e-mail, e isso está fora do fluxo garantido deste projeto**: como nenhum
`IEmailSender` foi registrado, o endpoint responde com sucesso e **nenhum e-mail sai**. Para usá-los
de verdade, registre uma implementação de `IEmailSender<AppUser>` na composição da aplicação e
aponte-a para o seu provedor.

O que este projeto garante é o caminho que não depende de e-mail: **cadastro, login e renovação**. A
conta nasce utilizável e `POST /auth/login` funciona sem nenhuma confirmação.
