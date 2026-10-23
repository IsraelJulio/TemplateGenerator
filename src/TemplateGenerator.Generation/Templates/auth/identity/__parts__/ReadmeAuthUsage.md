
## Usar a autenticação

### Cadastrar, entrar e renovar

Estes comandos falam com a aplicação **já no ar**, pelo passo **Executar**, acima: use outro
terminal. Em Linux, macOS ou Git Bash:

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
