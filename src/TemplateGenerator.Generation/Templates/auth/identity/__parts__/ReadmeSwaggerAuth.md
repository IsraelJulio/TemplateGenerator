### Swagger e autenticação

O documento e a interface são **públicos** em `Development`, como `GET /health`: abrir
`/swagger` não pede token. As rotas que exigem token — o CRUD de `/items` e a gestão da conta em
`/auth/manage/...` — aparecem com um cadeado, e a interface tem o botão *Authorize*, porque o
documento OpenAPI desta combinação declara o esquema de segurança `bearer`. Para usá-lo:

1. Pelo *Try it out* de `POST /auth/register`, cadastre uma conta — ou use a que você cadastrou na
   seção **Usar a autenticação**.
2. Pelo *Try it out* de `POST /auth/login`, entre com ela e copie o valor de `accessToken` da
   resposta. É o bearer próprio do Identity, opaco: não é um JWT, e não há nada a decodificar nele.
3. Clique em *Authorize*, cole **só o token** — sem a palavra `Bearer`, que a interface acrescenta
   sozinha — e confirme.

Daí em diante, o *Try it out* das rotas com cadeado manda o cabeçalho
`Authorization: Bearer <token>` e responde como os comandos de **Testar o CRUD de `Item`**:
`GET /items` responde `200`. Sem passar por *Authorize*, ou depois de o token vencer — ele dura uma
hora —, as mesmas rotas respondem `401`: entre de novo, ou troque o `refreshToken` por um par novo
em `POST /auth/refresh`, e autorize outra vez. `POST /auth/register`, `POST /auth/login` e
`POST /auth/refresh` não têm cadeado e funcionam sem token.
