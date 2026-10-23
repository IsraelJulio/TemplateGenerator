# T08 — verificação 3: comandos do README da camada 3, do zero (execução manual, papel `qa`)

> Copiado para `docs/reports/T08/` pelo PO no fechamento, a pedido do reviewer (evidência fora do
> repositório some com a sessão). Logs da primeira rodada em `logs/`, do rerun em `rerun-logs/`,
> scripts auxiliares em `scripts/`. Os projetos extraídos e os ZIPs **não** foram versionados: são
> reproduzíveis byte a byte (SHA-256 abaixo; o reviewer os regerou a partir do commit e conferiu).
> Tokens bearer da aplicação local efêmera foram trocados por `<token redigido>`; a senha do
> PostgreSQL nunca foi gravada (`Password=***`).

Data: 2026-10-23. Executor: papel `qa`. Shell: Git Bash (bloco PowerShell em `powershell.exe` 5.1.26100.9444).

Raiz de trabalho (fora do repositório):
`C:\Users\2273129\AppData\Local\Temp\claude\c--Users-2273129-Documents-Projects-TemplateGenerator\318d2cf1-dde3-4bfe-96a2-838beef3a510\scratchpad\t08-manual\`

- `zips\cN.zip`: os ZIPs baixados; `cN\`: cada ZIP extraído numa pasta nova e vazia.
- `logs\cN-<passo>.log`: um log por comando. A primeira linha é o comando literal (`$ ...`), a última é `[exit=N]`.
- `scripts\h.sh`: funções auxiliares (`run`, `startapp`, `stopapp`, `snap`); `scripts\c2-readme-blocks.ps1`: os blocos PowerShell da combinação 2.

## Ambiente

| Item | Evidência |
|---|---|
| SDK | `export PATH="/c/Users/2273129/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_ROOT=...` e depois `dotnet --version` → `10.0.302`, exit 0 (log `cN-01-version.log` de cada combinação) |
| PostgreSQL | `psql -c "select version()"` → `PostgreSQL 18.4 on x86_64-windows`, exit 0 |
| Senha | só pelo ambiente (`ConnectionStrings__Default`, `PGPASSWORD`). Aqui e nos logs ela aparece como `Password=***`. `grep -rl 'Password=<senha real>'` sobre `logs\` e `cN\` → nenhum arquivo, exit 1 (repetido sobre este arquivo depois de escrito: 0 ocorrência) |
| Bancos descartáveis | `t08_manual_3` (combinação 3) e `t08_manual_6` (combinação 6), apagados com `DROP DATABASE ... WITH (FORCE)` → `DROP DATABASE`, exit 0. Depois, `select count(*) from pg_database where datname like 't08_manual%'` → `0` |
| Porta 5100 | uma combinação por vez. `stopapp` encerra só o processo que escuta na 5100 (o `.exe` do projeto gerado, filho do `dotnet run` que eu subi) e confere `port 5100 free` antes de seguir |
| Repositório | não escrevi nada no repositório. `git status --short` no fim mostra também `docs/architecture/generated-projects.md` e `docs/environment.md` modificados, que não constavam no instantâneo inicial. Não fui eu: são de outro papel trabalhando em paralelo |

## Obtenção dos ZIPs (como uma pessoa faria)

1. `dotnet run --project src/TemplateGenerator.Api` (raiz do repositório, HEAD `ca19287` mais a árvore de trabalho da branch `feat/T08-...`) → `Now listening on: http://localhost:5080` (log `logs\platform.log`).
2. `curl http://localhost:5080/api/template-options` → `HTTP 200`. Campos: `projectName`, `architecture`, `database`, `authentication`, `swagger`, `dotnetVersion`.
3. `curl -X POST http://localhost:5080/api/templates -H 'Content-Type: application/json' -d '<json>'`, com um JSON por combinação. Todos responderam `HTTP 200`, exit 0, `Content-Disposition: attachment; filename="<Nome>.zip"`.
4. Plataforma parada: `taskkill //PID 33048 //F` → `SUCCESS`. Depois, `netstat` sem nada escutando na 5080.
5. Nenhum arquivo de `src/TemplateGenerator.Generation/Templates` ficou mais novo que os ZIPs (`find -newer` → vazio). Os ZIPs refletem os templates atuais da árvore de trabalho.

| # | JSON (`dotnetVersion: net10.0` em todos) | SHA-256 do ZIP |
|---|---|---|
| 1 | `Manual.Um`, simple, none, none, swagger=true | `2ea3f983f4ddad159256a95e7b6d9b2dd2479fb68e3344d6ccd0a93e84239ddc` |
| 2 | `Manual.Dois`, simple, sqlite, identity, swagger=false | `e264e4abeb2a8720914f6b4ef7267abab612a4f2cb9039385dc76543723baf2c` |
| 3 | `Manual.Tres`, clean, postgresql, identity, swagger=true | `c9a900e262ae542329b732f2bdaddb399f54594904effe67f5030ab890d93ae6` |
| 4 | `Manual.Quatro`, clean, sqlite, jwt, swagger=false | `0f022fede06293a394279e142ddedbd52a5ca56ddce368a2c8103086738290c1` |
| 5 | `Manual.Cinco`, simple, none, jwt, swagger=true | `5b2852592051f8dd16b330bb9368a0e506b76415c52ed3521c8891faf9654728` |
| 6 | `Manual.Seis`, clean, postgresql, none, swagger=false | `e5679e7d1171aeb224376e9b0e4b1cb98979b8c5eecab17074418872d357df01` |

## Método

- Executei os blocos ```` ```bash ```` de cada README na ordem em que estão escritos, copiando o texto. Substituí apenas os marcadores que o próprio README manda trocar:
  - `SUA_SENHA`: a senha, só no ambiente;
  - `COLE_O_REFRESH_TOKEN_AQUI` / `COLE_O_ACCESS_TOKEN_AQUI`: os valores devolvidos pelo login;
  - `Database=<Nome>`: trocado pelo banco descartável, ajuste que o README declara esperado.
- Onde a saída tem token, o log usa um `| grep` para mostrar só status e cabeçalho.
- **RF-21 (nenhum comando modifica código-fonte):** antes do primeiro comando e depois do último, registrei o SHA-256 de todo arquivo da pasta extraída, sem `bin/`, `obj/` e `*.db` (o banco SQLite é criado pela migração e está no `.gitignore`). Depois comparei os dois com `diff` (`logs\cN-src.before` contra `logs\cN-src.after`).
- `Ctrl+C` foi substituído por encerrar o processo (`taskkill /T /F` no PID que escuta na 5100). Isso não muda o que se confere na persistência: cada escrita já tinha recebido resposta `201`/`204` antes da parada.

---

## Combinação 1 — simple + none + none, swagger=true — **OK**

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` | 0 | `10.0.302` | c1-01 |
| `dotnet restore` | 0 | `Restored ...Manual.Um.csproj`, `Restored ...Manual.Um.Tests.csproj` | c1-02 |
| `dotnet run --project src/Manual.Um` (em segundo plano) | — | `Now listening on: http://localhost:5100`, `Hosting environment: Development`; `/health` 200 em 2 s | c1-03 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c1-04 |
| `curl -i -X POST http://localhost:5100/items -H 'Content-Type: application/json' -d '{"title":"Primeiro item"}'` | 0 | `HTTP/1.1 201 Created`, `Location: /items/1`, `{"id":1,"title":"Primeiro item"}` | c1-05 |
| `curl http://localhost:5100/items` | 0 | `[{"id":1,"title":"Primeiro item"}]` | c1-06 |
| `curl http://localhost:5100/items/1` | 0 | `{"id":1,"title":"Primeiro item"}` | c1-07 |
| `curl -i -X PUT http://localhost:5100/items/1 -H 'Content-Type: application/json' -d '{"title":"Item renomeado"}'` | 0 | `HTTP/1.1 204 No Content` | c1-08 |
| `curl -i -X DELETE http://localhost:5100/items/1` | 0 | `HTTP/1.1 204 No Content` | c1-09 |
| conferência: GET /items/1 após DELETE | 0 | `HTTP 404` | c1-10 |
| Swagger: `GET /openapi/v1.json` | 0 | `HTTP 200`; `openapi 3.1.1`; paths `['/health', '/items', '/items/{id}']`; sem `securitySchemes` | c1-11, `logs\c1-openapi-dev.json` |
| Swagger: `GET /swagger/index.html` | 0 | `HTTP 200 text/html;charset=utf-8` | c1-12 |
| Swagger: `GET /swagger` (link do README) | 0 | `HTTP 301 -> http://localhost:5100/swagger/index.html` | c1-13 |
| Swagger: `GET /swagger/v1/swagger.json` | 0 | `HTTP 404` (o Swashbuckle não gera documento) | c1-14 |
| Swagger: configuração da UI (`/swagger/index.js`) | 0 | `"url":"/openapi/v1.json"` (a UI consome o documento nativo) | c1-19 |
| Persistência: POST "Antes do reinicio" → parar → `dotnet run --project src/Manual.Um` → `GET /items` | 0 | antes: `HTTP 201` id 2; depois do reinício: `[]` `HTTP 200`. **none perdeu os dados**, como o README promete | c1-16, c1-17, c1-18 |
| Production: `ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5100 dotnet run --project src/Manual.Um --no-launch-profile` | — | `Hosting environment: Production` | c1-20 |
| Production: rotas | 0 | `/health -> 200`, `/openapi/v1.json -> 404`, `/swagger -> 404`, `/swagger/index.html -> 404`, `/swagger/v1/swagger.json -> 404` | c1-21 |
| `dotnet test` | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c1-22 |
| RF-21: `diff src.before src.after` | 0 | `fontes identicos ao ZIP (23 arquivos)` | c1-23 |

## Combinação 2 — simple + sqlite + identity, swagger=false — **OK** (ver observação O1)

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` | 0 | `10.0.302` | c2-01 |
| `dotnet restore` | 0 | `Restored ...Manual.Dois.csproj`, `...Manual.Dois.Tests.csproj` | c2-02 |
| `dotnet tool restore` | 0 | `Tool 'dotnet-ef' (version '10.0.12') was restored.` `Restore was successful.` | c2-03 |
| `dotnet ef database update --project src/Manual.Dois --startup-project src/Manual.Dois --context AppDbContext` | 0 | `Done.` | c2-04 |
| `dotnet ef database update --project src/Manual.Dois --startup-project src/Manual.Dois --context AppIdentityDbContext` | 0 | `Done.` | c2-05 |
| conferência: tabelas em `src/Manual.Dois/app.db` | 0 | `AspNetRoleClaims, AspNetRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserRoles, AspNetUserTokens, AspNetUsers, Items, __EFMigrationsHistory, ...` | c2-07 |
| `curl -i -X POST .../auth/register ...` **na ordem escrita, antes do bloco Executar** | 7 | `curl: (7) Failed to connect to localhost:5100` (ver O1) | c2-06 |
| `dotnet run --project src/Manual.Dois` | — | `Now listening on: http://localhost:5100`, `Development` | c2-08 |
| `curl -i -X POST http://localhost:5100/auth/register -H 'Content-Type: application/json' -d '{"email":"pessoa@exemplo.com","password":"Senha!123"}'` | 0 | `HTTP/1.1 200 OK`, `Content-Length: 0` | c2-09 |
| `curl -i -X POST http://localhost:5100/auth/login -H 'Content-Type: application/json' -d '{"email":"pessoa@exemplo.com","password":"Senha!123"}'` | 0 | `HTTP/1.1 200 OK`, `"tokenType":"Bearer"`, `"expiresIn":3600`, accessToken e refreshToken com 731 caracteres cada | c2-10 |
| `curl -i -X POST http://localhost:5100/auth/refresh -H 'Content-Type: application/json' -d '{"refreshToken":"<refreshToken do login>"}'` | 0 | `HTTP/1.1 200 OK`, `"tokenType":"Bearer"`, `"expiresIn":3600` | c2-11 |
| `curl -i http://localhost:5100/items` | 0 | `HTTP/1.1 401 Unauthorized`, `WWW-Authenticate: Bearer` | c2-12 |
| `curl -i http://localhost:5100/items -H 'Authorization: Bearer <accessToken>'` | 0 | `HTTP/1.1 200 OK`, `[]` | c2-13 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c2-14 |
| `TOKEN=$(curl -s -X POST http://localhost:5100/auth/login ... \| sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')` | 0 | `TOKEN length=731 prefix=CfDJ8` | c2-15 |
| `curl -i -X POST http://localhost:5100/items -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" -d '{"title":"Primeiro item"}'` | 0 | `HTTP/1.1 201 Created`, `Location: /items/1`, `{"id":1,"title":"Primeiro item"}` | c2-16 |
| `curl http://localhost:5100/items -H "Authorization: Bearer $TOKEN"` | 0 | `[{"id":1,"title":"Primeiro item"}]` | c2-17 |
| `curl http://localhost:5100/items/1 -H "Authorization: Bearer $TOKEN"` | 0 | `{"id":1,"title":"Primeiro item"}` | c2-18 |
| `curl -i -X PUT http://localhost:5100/items/1 ... -d '{"title":"Item renomeado"}'` | 0 | `HTTP/1.1 204 No Content`; GET depois → `{"id":1,"title":"Item renomeado"}` | c2-19, c2-19b |
| `curl -i -X DELETE http://localhost:5100/items/1 -H "Authorization: Bearer $TOKEN"` | 0 | `HTTP/1.1 204 No Content`; GET depois → `HTTP 404` | c2-20, c2-21 |
| **PowerShell 5.1**: os três blocos ```` ```powershell ```` do README (autenticação, TOKEN, CRUD), como estão, numa mesma sessão `powershell.exe` | 0 | register `HTTP/1.1 400` `DuplicateUserName` (esperado: a conta já tinha sido criada pelo bloco bash); login `HTTP/1.1 200 OK`; `TOKEN length=731`; POST `HTTP/1.1 201 Created` `Location: /items/2` `{"id":2,"title":"Primeiro item"}`; GET `[{"id":2,"title":"Primeiro item"}]`. **O título com espaço chegou inteiro** | c2-22, `scripts\c2-readme-blocks.ps1` |
| Swagger desligado (Development) | 0 | `/openapi/v1.json -> 404`, `/swagger -> 404`, `/swagger/index.html -> 404`, `/swagger/v1/swagger.json -> 404` | c2-23 |
| Swagger desligado: `.csproj` e `Program.cs` | 1 (grep sem ocorrência) | `0` ocorrência de Swashbuckle ou Microsoft.AspNetCore.OpenApi no `.csproj`; `0` de swagger ou openapi no `Program.cs` | c2-24 |
| Persistência: parar → `dotnet run --project src/Manual.Dois` → login → `GET /items` | 0 | `[{"id":2,"title":"Primeiro item"}]` `HTTP 200`. **sqlite manteve os dados**, e o usuário também (o login funcionou depois do reinício) | c2-25, c2-26 |
| `dotnet test` | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c2-27 |
| RF-21: `diff src.before src.after` | 0 | `fontes identicos ao ZIP (35 arquivos)` | c2-28 |

## Combinação 3 — clean + postgresql + identity, swagger=true — **OK** (ver observação O1)

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` | 0 | `10.0.302` | c3-01 |
| `dotnet restore` | 0 | 5 projetos `Restored` (Api, Application, Domain, Infrastructure, Tests) | c3-02 |
| `export ConnectionStrings__Default="Host=localhost;Port=5432;Database=t08_manual_3;Username=postgres;Password=***"` | 0 | — | c3-03 |
| `dotnet tool restore` | 0 | `dotnet-ef 10.0.12`, `Restore was successful.` | c3-04 |
| `dotnet ef database update --project src/Manual.Tres.Infrastructure --startup-project src/Manual.Tres.Api --context AppDbContext` | 0 | criou o banco; `Disposed connection to database 't08_manual_3' on server 'tcp://localhost:5432'`, `Done.` | c3-05 |
| `dotnet ef database update --project src/Manual.Tres.Infrastructure --startup-project src/Manual.Tres.Api --context AppIdentityDbContext` | 0 | `Done.` | c3-06 |
| conferência: tabelas (psql) | 0 | `AspNetRoleClaims,AspNetRoles,AspNetUserClaims,AspNetUserLogins,AspNetUserRoles,AspNetUserTokens,AspNetUsers,Items,__EFMigrationsHistory` | c3-07 |
| `dotnet run --project src/Manual.Tres.Api` | — | `Now listening on: http://localhost:5100`, `Development` | c3-08 |
| `curl -i -X POST .../auth/register ...` | 0 | `HTTP/1.1 200 OK`, `Content-Length: 0` | c3-09 |
| `curl -i -X POST .../auth/login ...` | 0 | `HTTP/1.1 200 OK`, `"tokenType":"Bearer"`, `"expiresIn":3600` | c3-10 |
| `curl -i -X POST .../auth/refresh ... -d '{"refreshToken":"<refreshToken do login>"}'` | 0 | `HTTP/1.1 200 OK`, `"tokenType":"Bearer"`, `"expiresIn":3600`. A primeira tentativa respondeu 401 por erro meu de aspas (mandei o texto literal `$RT`, 22 bytes). Log preservado em `c3-11-refresh-ERRO-DO-QA-aspas.log`; não é defeito | c3-11 |
| `curl -i http://localhost:5100/items` | 0 | `HTTP/1.1 401 Unauthorized`, `WWW-Authenticate: Bearer` | c3-12 |
| `curl -i http://localhost:5100/items -H 'Authorization: Bearer <accessToken>'` | 0 | `HTTP/1.1 200 OK`, `[]` | c3-13 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c3-14 |
| `TOKEN=$(curl -s -X POST .../auth/login ... \| sed ...)` | 0 | `TOKEN length=731` | c3-15 |
| CRUD com `$TOKEN` (5 linhas do README) | 0 | POST `201 Created` `Location: /items/1`; GET lista `[{"id":1,"title":"Primeiro item"}]`; GET 1 `{"id":1,...}`; PUT `204`, depois GET `"Item renomeado"`; DELETE `204`, depois GET `404` | c3-16 … c3-21 |
| Swagger e autenticação: `GET /openapi/v1.json` **sem token** | 0 | `HTTP 200`; `openapi 3.1.1`; paths incluem `/auth/login`, `/auth/register`, `/auth/refresh`, `/health`, `/items`, `/items/{id}` (e as outras rotas do `MapIdentityApi`); `components.securitySchemes presente: False`; `security` no topo: False; `security` em operação: False | c3-23, `logs\c3-openapi-dev.json` |
| Swagger: `GET /swagger/index.html` sem token | 0 | `HTTP 200 text/html;charset=utf-8` | c3-24 |
| Swagger: `GET /swagger` | 0 | `HTTP 301 -> /swagger/index.html` | c3-25 |
| Swagger: `GET /swagger/v1/swagger.json` | 0 | `HTTP 404` | c3-26 |
| Swagger: configuração da UI | 0 | `"url":"/openapi/v1.json"` | c3-27 |
| `/items` sem token, com Swagger ligado | 0 | `HTTP 401` | c3-28 |
| Persistência: POST "Antes do reinicio" → parar → subir no mesmo terminal (com a variável) → login → `GET /items` | 0 | antes: `HTTP 201` id 2; depois: `[{"id":2,"title":"Antes do reinicio"}]` `HTTP 200`. **postgresql manteve os dados** e o usuário | c3-22, c3-29, c3-30 |
| (registro de erro meu) reinício num terminal **sem** `ConnectionStrings__Default` | — | `/health` 200, mas o login não devolve token: `Npgsql.NpgsqlException: No password has been provided but the backend requires one`. É o que o README avisa ("a variável vale para o terminal em que você a definiu"); não é defeito | c3-29-…-SEM-VARIAVEL-ERRO-DO-QA.log, c3-30-…-SEM-VARIAVEL-ERRO-DO-QA.log |
| Production: `ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5100 dotnet run --project src/Manual.Tres.Api --no-launch-profile` | — | `Hosting environment: Production` | c3-31 |
| Production: rotas | 0 | `/health -> 200`, `/openapi/v1.json -> 404`, `/swagger -> 404`, `/swagger/index.html -> 404`, `/swagger/v1/swagger.json -> 404`, `/items -> 401` | c3-32 |
| `dotnet test` | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c3-33 |
| RF-21: `diff src.before src.after` | 0 | `fontes identicos ao ZIP (38 arquivos)` | c3-34 |
| `DROP DATABASE t08_manual_3 WITH (FORCE);` | 0 | `DROP DATABASE` | c3-35 |

## Combinação 4 — clean + sqlite + jwt, swagger=false — **OK no que não depende de provedor** (ver O1)

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` | 0 | `10.0.302` | c4-01 |
| `dotnet restore` | 0 | `Restored ...` | c4-02 |
| `dotnet tool restore` | 0 | `Restore was successful.` | c4-03 |
| `dotnet ef database update --project src/Manual.Quatro.Infrastructure --startup-project src/Manual.Quatro.Api --context AppDbContext` | 0 | `Done.`; tabelas `['Items', '__EFMigrationsHistory', ...]`, sem nenhuma tabela de usuário | c4-04, c4-05 |
| `export Jwt__Authority="https://seu-provedor.exemplo/realms/sua-organizacao"` / `export Jwt__Audience="sua-api"` | 0 | — | c4-06 |
| `curl -s "$Jwt__Authority/.well-known/openid-configuration"` | **6** | **falha esperada: host de exemplo inexistente** (curl 6 = não resolveu o host) | c4-07 |
| `curl -i http://localhost:5100/health` **na ordem escrita, antes do bloco Executar** | 7 | `Failed to connect to localhost:5100` (ver O1) | c4-08 |
| `dotnet run --project src/Manual.Quatro.Api` (com as duas variáveis) | — | `Now listening on: http://localhost:5100`, `Development` | c4-09 |
| `curl -i http://localhost:5100/health` | 0 | `HTTP/1.1 200 OK`, `{"status":"ok"}` | c4-10 |
| `curl -i http://localhost:5100/items` | 0 | `HTTP/1.1 401 Unauthorized`, `WWW-Authenticate: Bearer` | c4-11 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c4-12 |
| `TOKEN=$(curl -s -X POST "$Jwt__Authority/protocol/openid-connect/token" ... -d 'grant_type=client_credentials&client_id=SEU_CLIENT_ID&client_secret=SEU_SEGREDO&audience=sua-api' \| sed ...)` | 0 (atribuição); curl isolado → **6** | **falha esperada: host de exemplo inexistente**; `TOKEN length=0` | c4-13 |
| CRUD com `$TOKEN` vazio (5 linhas do README) | 0 | as 5 respondem `HTTP/1.1 401 Unauthorized` | c4-14 … c4-18 |
| sonda: `TOKEN=COLE_O_TOKEN_AQUI` (marcador sem substituir, porque não há provedor) e depois `curl -i .../items -H "Authorization: Bearer $TOKEN"` | 0 | `HTTP/1.1 401`, `WWW-Authenticate: Bearer error="invalid_token"` (IDX14100: JWT malformado) | c4-19, c4-20 |
| sonda fora do README: JWT bem formado, assinatura falsa, `iss` igual ao Authority de exemplo | 0 | `HTTP/1.1 401`, `error="invalid_token", error_description="The issuer '...' is invalid"`. Falha **fechada** (401, não 500) com o Authority inalcançável | c4-21, c4-22 |
| reinício: `dotnet run --project src/Manual.Quatro.Api` | — | `/health` 200 | c4-23 |
| Swagger desligado | 0 | `/health -> 200`, `/items -> 401`, `/openapi/v1.json -> 404`, `/swagger -> 404`, `/swagger/index.html -> 404`, `/swagger/v1/swagger.json -> 404` | c4-24 |
| Swagger desligado: `.csproj` e `Program.cs` | 1 (grep sem ocorrência) | os 4 `.csproj` com `:0`; `Program.cs` com `0` | c4-25 |
| `dotnet test` | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c4-26 |
| RF-21: `diff src.before src.after` | 0 | `fontes identicos ao ZIP (32 arquivos)` | c4-27 |

**O caminho com token válido** — CRUD 201/200/204 com JWT, e as quatro rejeições (assinatura, emissor, audiência, validade) — **não foi exercitado nesta execução manual.** Não há provedor externo nesta máquina, e não inventei um. Esse caminho é coberto pela camada 3 automatizada, com o emissor OIDC in-process (ADR-0006).

## Combinação 5 — simple + none + jwt, swagger=true — **OK no que não depende de provedor** (ver O1)

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` | 0 | `10.0.302` | c5-01 |
| `dotnet restore` | 0 | 2 projetos `Restored` | c5-02 |
| `export Jwt__Authority="https://seu-provedor.exemplo/realms/sua-organizacao"` / `export Jwt__Audience="sua-api"` | 0 | — | c5-03 |
| `curl -s "$Jwt__Authority/.well-known/openid-configuration"` | **6** | **falha esperada: host de exemplo inexistente** | c5-04 |
| `curl -i http://localhost:5100/health` **na ordem escrita, antes do bloco Executar** | 7 | `Failed to connect` (ver O1) | c5-05 |
| `dotnet run --project src/Manual.Cinco` | — | `Now listening on: http://localhost:5100`, `Development` | c5-06 |
| `curl -i http://localhost:5100/health` | 0 | `HTTP/1.1 200 OK`, `{"status":"ok"}` | c5-07 |
| `curl -i http://localhost:5100/items` | 0 | `HTTP/1.1 401 Unauthorized`, `WWW-Authenticate: Bearer` | c5-08 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c5-09 |
| `TOKEN=$(curl -s -X POST "$Jwt__Authority/protocol/openid-connect/token" ... )` | curl isolado → **6** | **falha esperada: host de exemplo inexistente**; `TOKEN length=0` | c5-10 |
| CRUD com `$TOKEN` vazio (5 linhas do README) | 0 | as 5 respondem `HTTP/1.1 401 Unauthorized` | c5-11 … c5-15 |
| Swagger e autenticação: `GET /openapi/v1.json` sem token | 0 | `HTTP 200`; `openapi 3.1.1`; paths `['/health', '/items', '/items/{id}']`; `components.securitySchemes presente: False`; `security` no topo e em operação: False | c5-16, `logs\c5-openapi-dev.json` |
| `GET /swagger/index.html` sem token | 0 | `HTTP 200 text/html;charset=utf-8` | c5-17 |
| `GET /swagger` | 0 | `HTTP 301 -> /swagger/index.html` | c5-18 |
| `GET /swagger/v1/swagger.json` | 0 | `HTTP 404` | c5-19 |
| configuração da UI | 0 | `"url":"/openapi/v1.json"` | c5-20 |
| "mesmo com `Jwt:Authority` vazio" (README): subir sem as variáveis (`env \| grep -c '^Jwt__'` → `0`) | 0 | `/health -> 200`, `/items -> 401`, `/openapi/v1.json -> 200`, `/swagger/index.html -> 200` | c5-21, c5-22 |
| Production: `ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5100 dotnet run --project src/Manual.Cinco --no-launch-profile` | — | `Hosting environment: Production` | c5-23 |
| Production: rotas | 0 | `/health -> 200`, `/openapi/v1.json -> 404`, `/swagger -> 404`, `/swagger/index.html -> 404`, `/swagger/v1/swagger.json -> 404`, `/items -> 401` | c5-24 |
| `dotnet test` | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c5-25 |
| RF-21: `diff src.before src.after` | 0 | `fontes identicos ao ZIP (23 arquivos)` | c5-26 |

O caminho com token válido também não foi exercitado aqui. Fica com a camada 3 automatizada (emissor OIDC in-process, ADR-0006).

## Combinação 6 — clean + postgresql + none, swagger=false — **OK**

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` | 0 | `10.0.302` | c6-01 |
| `dotnet restore` | 0 | `Restored ...` | c6-02 |
| `export ConnectionStrings__Default="Host=localhost;Port=5432;Database=t08_manual_6;Username=postgres;Password=***"` | 0 | — | c6-03 |
| `dotnet tool restore` | 0 | `Restore was successful.` | c6-04 |
| `dotnet ef database update --project src/Manual.Seis.Infrastructure --startup-project src/Manual.Seis.Api --context AppDbContext` | 0 | `database 't08_manual_6' on server 'tcp://localhost:5432'`, `Done.`; tabelas `Items,__EFMigrationsHistory` | c6-05, c6-06 |
| `dotnet run --project src/Manual.Seis.Api` | — | `Now listening on: http://localhost:5100`, `Development` | c6-07 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c6-08 |
| CRUD (5 linhas do README) | 0 | POST `HTTP/1.1 201 Created` `Location: /items/1` `{"id":1,"title":"Primeiro item"}`; GET lista `[{"id":1,...}]`; GET 1 `{"id":1,...}`; PUT `204`, depois GET `"Item renomeado"`; DELETE `204`, depois GET `404` | c6-09 … c6-14 |
| Swagger desligado | 0 | `/openapi/v1.json -> 404`, `/swagger -> 404`, `/swagger/index.html -> 404`, `/swagger/v1/swagger.json -> 404`; os 4 `.csproj` com `:0`; `Program.cs` com `0` | c6-16, c6-17 |
| Persistência: POST "Antes do reinicio" → parar → `dotnet run --project src/Manual.Seis.Api` → `GET /items` | 0 | antes: `HTTP 201` id 2; depois: `[{"id":2,"title":"Antes do reinicio"}]` `HTTP 200`. **postgresql manteve os dados** | c6-15, c6-18, c6-19 |
| `dotnet test` | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c6-20 |
| RF-21: `diff src.before src.after` | 0 | `fontes identicos ao ZIP (32 arquivos)` | c6-21 |
| `DROP DATABASE t08_manual_6 WITH (FORCE);` | 0 | `DROP DATABASE` | c6-22 |

---

## Defeitos

Nenhum comando de README falhou quando executado como o texto manda. Nenhum comportamento divergiu do que o README promete. Nenhum arquivo-fonte foi alterado por comando algum (RF-21: `diff` vazio nas 6 combinações).

## Observações para o PO julgar

**O1 — ordem textual dos blocos contra a regra "a camada 3 executa os comandos na ordem em que estão escritos".**

Nas combinações com autenticação (2, 3, 4, 5), a seção `## Autenticação` traz blocos ```` ```bash ```` que falam com a aplicação no ar, e eles aparecem **antes** do bloco `## Executar`:
- identity: `curl ... /auth/register`, `/auth/login`, `/auth/refresh`, `curl -i .../items`;
- jwt: `curl -i .../health`, `curl -i .../items`.

Executados estritamente na ordem do texto, falham com `curl: (7) Failed to connect to localhost:5100` (logs `c2-06`, `c4-08`, `c5-05`). O próprio README avisa logo antes de cada um desses blocos: "Estes comandos falam com a aplicação **já no ar**: suba-a primeiro, com o passo **Executar** logo adiante, e use outro terminal". Uma pessoa que lê o texto não erra.

Mesmo assim, `docs/architecture/generated-projects.md` diz: "A camada 3 executa os comandos na ordem em que estão escritos [...] Ordem errada no README é falha de teste". Literalmente, isso vale contra esse arranjo. Não classifiquei O1 como defeito, porque o README manda explicitamente subir a aplicação antes. Cabe ao PO (ou a quem é dono do README gerado) decidir entre duas saídas: aceitar o arranjo e precisar a regra, ou mover/renumerar os blocos.

**O2 — PowerShell com `register` repetido.** Na combinação 2, o bloco PowerShell de autenticação, rodado depois do bloco bash, recebe `400 DuplicateUserName` no `register`, porque a conta já existe. É consequência de executar as duas variantes de shell no mesmo banco, não do README. O `login` e o CRUD que vêm depois passaram.

## Não executado, e por quê

1. **jwt (combinações 4 e 5), caminho com token válido**: CRUD autenticado com JWT (201/200/204) e as quatro rejeições específicas (assinatura, emissor, audiência, validade). Não há provedor OIDC nesta máquina. Os dois comandos do README que dependem do provedor foram executados e falharam como esperado (exit 6, host de exemplo inexistente). Esse caminho é coberto pela camada 3 automatizada (emissor OIDC in-process, ADR-0006), não por esta execução.
2. **jwt, persistência pela API** (combinação 4 com sqlite e combinação 5 com none): criar um item exige token válido, então não houve dado para conferir depois do reinício. A persistência dos mesmos armazenamentos foi conferida sem auth: none na combinação 1, sqlite na combinação 2.
3. **Blocos PowerShell das combinações 1, 3, 4, 5 e 6**: a tarefa pediu PowerShell em uma combinação com corpo JSON (a 2), e só nela foi executado. Na 2 rodaram os três blocos (autenticação, TOKEN, CRUD).
4. **`dotnet ef migrations add`**: o README só afirma que "também funciona"; não manda executar. Além disso, executar criaria arquivo-fonte novo, o que é o contrário de RF-21.
5. **`Ctrl+C` literal**: a aplicação foi parada por `taskkill /T /F` no processo que escutava na 5100, porque o processo rodava em segundo plano, sem terminal interativo. As escritas conferidas na persistência já tinham recebido resposta antes da parada.
6. **Abrir a interface Swagger num navegador** (o *Try it out*): conferi por HTTP que `index.html` responde 200, que a configuração aponta para `/openapi/v1.json`, que `/items` responde 401 sem token e que o documento não declara `securitySchemes` (logo, não há botão *Authorize*). Não houve captura de tela da UI.

---

# Rerun pós-O1 (combinações 2, 3, 4 e 5)

Motivo: o PO tratou O1 como defeito. Na correção, os `curl` que falam com a aplicação saíram de `## Autenticação` e foram para a seção nova `## Usar a autenticação`, que fica entre `## Verificar a saúde` e `## Testar o CRUD de Item`.

Mantive as regras e o ambiente da primeira rodada: SDK 10.0.302 por usuário, senha só no ambiente, nada escrito no repositório. Mudaram três coisas:
- pastas novas em `t08-manual\rerun\cN\` e logs em `t08-manual\rerun-logs\cN-<passo>.log`;
- banco novo `t08_rerun_3`;
- funções auxiliares em `scripts\h2.sh` (sobre `h.sh`).

## ZIPs novos

`dotnet run --project src/TemplateGenerator.Api` → `Now listening on: http://localhost:5080`; `GET /api/template-options` → 200. Cada `POST /api/templates` → `HTTP 200`, exit 0. No fim, plataforma parada (`taskkill` → `SUCCESS`, 5080 livre). Nenhum template ficou mais novo que os ZIPs (`find -newer` → vazio).

| # | SHA-256 novo | SHA-256 da 1ª rodada | Difere? |
|---|---|---|---|
| 2 | `868231b6c06695d2c4b0e7e77b9dea99a93fa9d3e9d64a0417dc5c4d292b6e3f` | `e264e4ab…3baf2c` | sim |
| 3 | `e7db82409a8e0385d14b0d7d42bceba99e8827a4dd5290659177dce81f129ed1` | `c9a900e2…d93ae6` | sim |
| 4 | `9d8876ec478607b7a23c8d9d027cdb7949f1d7514230f912db5d84081441660c` | `0f022fed…8290c1` | sim |
| 5 | `e1adddd6b9d9cd06f8e905f478fb61d6f25904769d699b9db180b233ec0588de` | `5b285259…654728` | sim |

## Conferência por leitura (item 3)

Script `scripts\readme_check.py`, rodado sobre cada `README.md` (`python -I`). Saída completa em `rerun-logs\readme-structure.log`. Ele:
- lista os blocos `bash` em ordem, com a seção de cada um;
- marca qualquer bloco com `localhost:5100` antes do bloco `dotnet run`;
- confere se cada `seção **X**` ou `passo **X**` aponta para um título que existe.

| # | Seções `##` em ordem | `localhost:5100` antes de Executar | Referências cruzadas |
|---|---|---|---|
| 2 | Pré-requisitos, Estrutura, Instalação, Banco de dados, Autenticação, Executar, Verificar a saúde, **Usar a autenticação**, Testar o CRUD de Item, Testes automatizados, Próximos passos | **nenhuma** | **Executar** OK; **Usar a autenticação** OK; **Testar o CRUD** OK (prefixo do título "Testar o CRUD de Item", forma abreviada já usada na 1ª rodada) |
| 3 | as mesmas, mais Swagger antes de Próximos passos | **nenhuma** | as mesmas da 2 |
| 4 | as mesmas da 2 | **nenhuma** | **Autenticação**, **Executar**, **Testar o CRUD de Item**: todas OK |
| 5 | as mesmas da 3 | **nenhuma** | as mesmas da 4 |

A prosa também lê bem de cima a baixo:
- Em `## Autenticação`, a identity tem a subseção "Cadastrar, entrar e renovar vem depois de subir a aplicação", que remete a "**Usar a autenticação**, logo depois de **Executar**". No jwt, o fim de "### 2." remete ao mesmo lugar.
- Em `## Usar a autenticação`, os blocos dizem "pelo passo **Executar**, acima".
- O bloco de token da identity diz "a conta que você cadastrou na seção **Usar a autenticação**", o link novo.
- Os "acima" e "adiante" apontam para o lado certo.
- Em `## Autenticação` ficaram só passos com a aplicação parada: a migração de identidade (identity), e o export de `Jwt__*` e o `curl` de discovery (jwt).

## Execução estritamente na ordem do texto

Subi a aplicação em segundo plano exatamente no bloco `dotnet run` de `## Executar`, nada antes e nada depois. O `dotnet test` também rodou no ponto do texto, ou seja, **com a aplicação ainda no ar**, já que o CRUD acontece "em outro terminal". Só depois parei para o reinício.

`grep -l 'curl: (7)' rerun-logs\*.log` → nenhum arquivo, exit 1. **Nenhum `Failed to connect` em comando algum.**

### Combinação 2 (simple + sqlite + identity, swagger=false) — OK

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` | 0 | `10.0.302` | c2-01 |
| `dotnet restore` | 0 | `Restored ...` | c2-02 |
| `dotnet tool restore` | 0 | `Restore was successful.` | c2-03 |
| `dotnet ef database update --project src/Manual.Dois --startup-project src/Manual.Dois --context AppDbContext` | 0 | `Done.` | c2-04 |
| `dotnet ef database update --project src/Manual.Dois --startup-project src/Manual.Dois --context AppIdentityDbContext` | 0 | `Done.` | c2-05 |
| `dotnet run --project src/Manual.Dois` (segundo plano) | — | `Now listening on: http://localhost:5100`, `Development` | c2-06 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c2-07 |
| `curl -i -X POST http://localhost:5100/auth/register -H 'Content-Type: application/json' -d '{"email":"pessoa@exemplo.com","password":"Senha!123"}'` | 0 | `HTTP/1.1 200 OK` | c2-08 |
| `curl -i -X POST http://localhost:5100/auth/login -H 'Content-Type: application/json' -d '{"email":"pessoa@exemplo.com","password":"Senha!123"}'` | 0 | `HTTP/1.1 200 OK`, `"tokenType":"Bearer"`, `"expiresIn":3600` | c2-09 |
| `curl -i -X POST http://localhost:5100/auth/refresh ... -d '{"refreshToken":"<refreshToken do login>"}'` | 0 | `HTTP/1.1 200 OK`, `Bearer`, `3600` | c2-10 |
| `curl -i http://localhost:5100/items` | 0 | `HTTP/1.1 401 Unauthorized` | c2-11 |
| `curl -i http://localhost:5100/items -H 'Authorization: Bearer <accessToken do login>'` | 0 | `HTTP/1.1 200 OK`, `[]` | c2-12 |
| `TOKEN=$(curl -s -X POST http://localhost:5100/auth/login ... \| sed ...)` | 0 | `TOKEN length=731` | c2-13 |
| CRUD com `$TOKEN` (5 linhas) | 0 | POST `201 Created`, `Location: /items/1`, `{"id":1,"title":"Primeiro item"}`; GET `[{"id":1,"title":"Primeiro item"}]`; GET 1 `{"id":1,...}`; PUT `204 No Content`; DELETE `204 No Content` | c2-14 … c2-18 |
| PowerShell 5.1: bloco do TOKEN e bloco do CRUD, **extraídos do próprio README por sed/awk**, sem redigitar (`scripts\rerun-c2-crud.ps1`) | 0 | `TOKEN length=731`; `HTTP/1.1 201 Created`, `Location: /items/2`, `{"id":2,"title":"Primeiro item"}`; GET `[{"id":2,"title":"Primeiro item"}]`. O título com espaço chegou inteiro | c2-19 |
| `dotnet test` (aplicação no ar) | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c2-20 |
| reinício: parar → `dotnet run --project src/Manual.Dois` → login → `GET /items` | 0 | `[{"id":2,"title":"Primeiro item"}]` `HTTP 200`. sqlite manteve os dados | c2-21, c2-22 |
| RF-21: `diff src.before src.after` | 0 | `fontes identicos ao ZIP (35 arquivos)` | c2-23 |

### Combinação 3 (clean + postgresql + identity, swagger=true) — OK

| Passo (comando literal) | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` / `dotnet restore` | 0 / 0 | `10.0.302` / `Restored ...` | c3-01, c3-02 |
| `export ConnectionStrings__Default="Host=localhost;Port=5432;Database=t08_rerun_3;Username=postgres;Password=***"` | 0 | — | c3-03 |
| `dotnet tool restore` | 0 | `Restore was successful.` | c3-04 |
| `dotnet ef database update --project src/Manual.Tres.Infrastructure --startup-project src/Manual.Tres.Api --context AppDbContext` | 0 | `Done.` (criou `t08_rerun_3`, que não existia: `count=0` antes) | c3-05 |
| `dotnet ef database update ... --context AppIdentityDbContext` | 0 | `Done.` | c3-06 |
| `dotnet run --project src/Manual.Tres.Api` | — | `Now listening on: http://localhost:5100`, `Development` | c3-07 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c3-08 |
| register / login / refresh | 0 / 0 / 0 | `200 OK` / `200 OK`, `Bearer`, `3600` / `200 OK` | c3-09, c3-10, c3-11 |
| `curl -i http://localhost:5100/items` / com o accessToken | 0 / 0 | `401 Unauthorized` / `200 OK`, `[]` | c3-12, c3-13 |
| `TOKEN=$(curl ... /auth/login ... \| sed ...)` | 0 | `TOKEN length=731` | c3-14 |
| CRUD com `$TOKEN` (5 linhas) | 0 | POST `201 Created`, `Location: /items/1`; GET `[{"id":1,"title":"Primeiro item"}]`; GET 1 `{"id":1,"title":"Primeiro item"}`; PUT `204`; DELETE `204` | c3-15 … c3-19 |
| item para o reinício (POST "Antes do reinicio") | 0 | `{"id":2,...}` `HTTP 201` | c3-20 |
| "### Swagger e autenticação": `/openapi/v1.json`, `/swagger/index.html` e `/items`, todos sem token | 0 | `200`, `200`, `401` | c3-21 |
| `dotnet test` (aplicação no ar) | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c3-22 |
| reinício (mesmo terminal, com a variável) → login → `GET /items` | 0 | `[{"id":2,"title":"Antes do reinicio"}]` `HTTP 200`. postgresql manteve os dados | c3-23, c3-24 |
| RF-21: `diff` | 0 | `fontes identicos ao ZIP (38 arquivos)` | c3-25 |
| `DROP DATABASE t08_rerun_3 WITH (FORCE);` | 0 | `DROP DATABASE` | c3-26 |

### Combinação 4 (clean + sqlite + jwt, swagger=false) — OK no que não depende de provedor

| Passo (comando literal), na ordem do README | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` / `dotnet restore` / `dotnet tool restore` | 0 / 0 / 0 | `10.0.302` / — / `Restore was successful.` | c4-01 … c4-03 |
| `dotnet ef database update --project src/Manual.Quatro.Infrastructure --startup-project src/Manual.Quatro.Api --context AppDbContext` | 0 | `Done.` | c4-04 |
| `export Jwt__Authority="https://seu-provedor.exemplo/realms/sua-organizacao"` / `export Jwt__Audience="sua-api"` | 0 | — | c4-05 |
| `curl -s "$Jwt__Authority/.well-known/openid-configuration"` | **6** | **falha esperada: host de exemplo inexistente** (fala com o provedor, não com a aplicação) | c4-06 |
| `dotnet run --project src/Manual.Quatro.Api` | — | `Now listening on: http://localhost:5100`, `Development` | c4-07 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c4-08 |
| `## Usar a autenticação`: `curl -i http://localhost:5100/health` / `curl -i http://localhost:5100/items` | 0 / 0 | `HTTP/1.1 200 OK` / `HTTP/1.1 401 Unauthorized` | c4-09, c4-10 |
| `TOKEN=$(curl -s -X POST "$Jwt__Authority/protocol/openid-connect/token" ... \| sed ...)` | curl → **6** | **falha esperada: host de exemplo inexistente**; `TOKEN length=0` | c4-11 |
| `TOKEN=COLE_O_TOKEN_AQUI` | — | não substituído: não há provedor. O CRUD seguiu com o TOKEN vazio | c4-12 |
| CRUD com `$TOKEN` (5 linhas) | 0 | POST `401`, GET `"status":401`, GET 1 `"status":401`, PUT `401`, DELETE `401` | c4-13 … c4-17 |
| `dotnet test` (aplicação no ar) | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c4-19 |
| RF-21: `diff` | 0 | `fontes identicos ao ZIP (32 arquivos)` | c4-20 |

### Combinação 5 (simple + none + jwt, swagger=true) — OK no que não depende de provedor

| Passo (comando literal), na ordem do README | Exit | Saída que comprova | Log |
|---|---|---|---|
| `dotnet --version` / `dotnet restore` | 0 / 0 | `10.0.302` | c5-01, c5-02 |
| `export Jwt__Authority=...` / `export Jwt__Audience=...` (valores de exemplo) | 0 | — | c5-05 |
| `curl -s "$Jwt__Authority/.well-known/openid-configuration"` | **6** | **falha esperada: host de exemplo inexistente** | c5-06 |
| `dotnet run --project src/Manual.Cinco` | — | `Now listening on: http://localhost:5100`, `Development` | c5-07 |
| `curl http://localhost:5100/health` | 0 | `{"status":"ok"}` | c5-08 |
| `curl -i .../health` / `curl -i .../items` | 0 / 0 | `200 OK` / `401 Unauthorized` | c5-09, c5-10 |
| pedido de token | curl → **6** | **falha esperada**; `TOKEN length=0` | c5-11 |
| CRUD com `$TOKEN` (5 linhas) | 0 | as 5 com `401` | c5-13 … c5-17 |
| "### Swagger e autenticação": `/openapi/v1.json`, `/swagger/index.html` e `/items`, todos sem token | 0 | `200`, `200`, `401` | c5-18 |
| `dotnet test` (aplicação no ar) | 0 | `Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13` | c5-19 |
| RF-21: `diff` | 0 | `fontes identicos ao ZIP (23 arquivos)` | c5-20 |

O caminho jwt com token válido continua fora da execução manual. Ele é coberto pela camada 3 automatizada (emissor OIDC in-process, ADR-0006).

## Defeitos no rerun

Nenhum. O1 está resolvido:
- nenhum bloco antes de Executar chama `localhost:5100`;
- nenhum `curl: (7)` em comando algum;
- todos os status HTTP saíram como o README promete.

## Estado final do rerun

- `grep -rl` da senha real sobre `rerun\` → nenhum arquivo, exit 1.
- `select count(*) from pg_database where datname like 't08_%'` → `0`.
- As portas 5100 e 5080 estão livres.
- Não escrevi nada no repositório.

## Não executado no rerun, e por quê

1. jwt (4 e 5) com token válido, e as quatro rejeições: não há provedor (ver acima).
2. jwt, reinício e persistência pela API: sem token não há como criar item. A tarefa pediu para a 4 e a 5 "o mesmo de antes", e a persistência de sqlite e none já está conferida.
3. Production e o 404 do Swagger fora de Development nas combinações 3 e 5: a tarefa pediu para repetir só a checagem de "### Swagger e autenticação". O resultado da 1ª rodada continua valendo para essas rotas, já que a mudança tocou apenas o README.
4. Blocos PowerShell além do TOKEN e do CRUD da combinação 2: não pedidos.
5. `Ctrl+C` literal: a parada foi por `taskkill /T /F` no processo da 5100, como na 1ª rodada.
