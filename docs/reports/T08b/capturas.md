# T08b — verificação 3: interface do Swagger por cliques

Combinação `simple + sqlite + identity + swagger = true`, projeto `Manual.Authorize`, gerada por
`POST http://localhost:5080/api/templates` e preparada **só** pelos comandos do README gerado. A
interface foi usada em Chromium (Playwright, headless). Cada clique foi feito na tela, sem chamada
HTTP por fora. O script afirma o status e falha com código ≠ 0 se algo divergir: **exit code 0,
37 linhas `PASS` e nenhuma `FAIL`**. O comando, a saída completa e o exit code estão em [`ui-run.txt`](ui-run.txt).

O token aparece só na tela e só mascarado: o script troca o texto renderizado por
`CfDJ8M...[mascarado, 752 caracteres]`, e faz isso **depois** de afirmar o valor real. No campo
do diálogo, a máscara é a do Playwright, um retângulo magenta. Nenhum arquivo de texto registra
o token.

| Captura | O que mostra |
|---|---|
| [`00-login-pela-interface-200.png`](00-login-pela-interface-200.png) | *Try it out* de `POST /auth/login` **sem token**: corpo com e-mail e senha, curl sem `Authorization`, **200** com `tokenType: "Bearer"`, `accessToken` e `refreshToken` mascarados e `expiresIn: 3600`. Antes dele, `POST /auth/register` pela interface respondeu 200 (está no `ui-run.txt`). |
| [`01-swagger-ui-authorize-e-cadeados.png`](01-swagger-ui-authorize-e-cadeados.png) | `/swagger` aberto (OAS 3.1, `Manual.Authorize v1`). O botão **Authorize** fica no canto direito, com o cadeado aberto. Há cadeado em `GET`/`POST /items`, em `GET`/`PUT`/`DELETE /items/{id}`, em `POST /auth/manage/2fa` e em `GET`/`POST /auth/manage/info`. Não há cadeado em `/health`, em `register`/`login`/`refresh` nem nas outras rotas públicas do Identity. |
| [`02-get-items-sem-authorize-401.png`](02-get-items-sem-authorize-401.png) | `GET /items` com *Try it out* + *Execute* **antes** de autorizar. O curl não tem `Authorization` e a resposta é **401** `Error: Unauthorized`. O corpo é `ProblemDetails` e o cabeçalho traz `www-authenticate: Bearer`. |
| [`03a-dialogo-authorize-token-colado.png`](03a-dialogo-authorize-token-colado.png) | Diálogo *Available authorizations*: esquema **`Bearer (http, Bearer)`**, a descrição "O accessToken devolvido por POST /auth/login, sem o prefixo 'Bearer '." e o campo *Value* com o token colado sem "Bearer ", coberto pela máscara. Botões *Authorize* e *Close*. |
| [`03b-dialogo-authorize-autorizado.png`](03b-dialogo-authorize-autorizado.png) | O mesmo diálogo depois de *Authorize*: **Authorized**, `Value: ******`, botões *Logout* e *Close*. Ao fundo, o cadeado do botão Authorize e o de `GET /items` aparecem fechados. |
| [`04-get-items-com-authorize-200-curl-bearer.png`](04-get-items-com-authorize-200-curl-bearer.png) | `GET /items` depois de autorizar. O curl traz `-H 'Authorization: Bearer CfDJ8M...[mascarado, 752 caracteres]'` e a resposta é **200** com `[]`, porque o banco estava vazio. |
| [`05-post-items-com-authorize-201.png`](05-post-items-com-authorize-201.png) | Bônus: `POST /items` com `{"title":"Item criado pelo Swagger UI"}`. O curl leva o Bearer e a resposta é **201** com `{"id":1,...}` e `location: /items/1`. Um `GET /items` em seguida respondeu 200 com esse item (está no `ui-run.txt`). |
| [`06-get-auth-manage-info-com-authorize-200.png`](06-get-auth-manage-info-com-authorize-200.png) | Bônus: `GET /auth/manage/info` depois de autorizar. O curl leva o Bearer e a resposta é **200** com o e-mail da conta cadastrada pela interface e `isEmailConfirmed: false`. |

Além do que a tela mostra, o script conferiu a requisição que saiu do navegador:

- antes de *Authorize*, ela não levava `Authorization`;
- depois, levava exatamente `Bearer <accessToken do login>`;
- o prefixo não saiu duplicado (`Bearer Bearer`);
- o status que a interface mostra é o mesmo que a rede recebeu, em todos os passos.

## Observação (não bloqueia o critério)

A interface marca o **401** de `GET /items` e o **201** de `POST /items` como *Undocumented*, como
se vê nas capturas 02 e 05. O documento OpenAPI declara só `200` para as cinco rotas de `/items`.
Na prática, `POST` responde 201, `PUT` e `DELETE` respondem 204 e todas respondem 401 sem token.
A descrição da API deixa de bater com as respostas reais, mas o comportamento não diverge do
README.

## Nota sobre o ensaio

Antes desta execução, um ensaio no scratchpad expôs dois defeitos **do próprio script**. Primeiro,
a célula de status traz o texto "Undocumented" junto do código. Segundo, ao executar de novo a
mesma rota, a interface ainda mostra a resposta anterior enquanto a nova não chega. O script
corrigido clica em *Clear* antes de *Execute*, espera a resposta de rede e compara o status da
rede com o da tela. Depois do ensaio, o banco foi recriado para a execução oficial começar vazia.
