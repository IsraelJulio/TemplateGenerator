### Swagger e autenticação

O documento e a interface são **públicos** em `Development`, como `GET /health`: abrir
`/swagger` não pede token, mesmo com `Jwt:Authority` vazio. As rotas do CRUD de `/items` exigem
token e aparecem com um cadeado, e a interface tem o botão *Authorize*, porque o documento OpenAPI
desta combinação declara o esquema de segurança `bearer`, com formato `JWT`. Para usá-lo:

1. Peça um token ao **seu** provedor, como na seção **Testar o CRUD de `Item`**. Esta aplicação não
   emite token nenhum, e a interface não tem onde pedi-lo.
2. Clique em *Authorize*, cole **só o token** — sem a palavra `Bearer`, que a interface acrescenta
   sozinha — e confirme.

Daí em diante, o *Try it out* das rotas com cadeado manda o cabeçalho
`Authorization: Bearer <token>`: com um token que passe nas quatro conferências da seção
**Autenticação**, `GET /items` responde `200`. Sem passar por *Authorize*, com `Jwt:Authority`
vazio, ou com assinatura, emissor, audiência ou validade errados, as mesmas rotas respondem `401`.
`GET /health` não tem cadeado e funciona sem token.
