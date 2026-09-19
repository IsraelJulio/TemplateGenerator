
**Comece pelo token.** Nesta combinação todo o CRUD exige `Authorization`, e por isso cada comando
desta seção já traz o cabeçalho, lendo a variável `TOKEN` — sem ela, toda linha da tabela de
respostas mais abaixo vira `401`. Com a aplicação já em execução (passo **Executar**, acima), entre
com a conta que você cadastrou na seção **Autenticação** e guarde o token. Em Bash:

```bash
TOKEN=$(curl -s -X POST http://localhost:5100/auth/login -H 'Content-Type: application/json' -d '{"email":"pessoa@exemplo.com","password":"Senha!123"}' | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')
```

No PowerShell — com o corpo pelo pipe e a codificação do pipe fixada, pelos motivos explicados
adiante, na parte de `curl.exe`:

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$TOKEN = ('{"email":"pessoa@exemplo.com","password":"Senha!123"}' | curl.exe -s -X POST http://localhost:5100/auth/login -H "Content-Type: application/json" --data-binary '@-' | ConvertFrom-Json).accessToken
```

Feito isso, os comandos abaixo rodam como estão. Para ver o `401` de propósito, repita qualquer um
deles sem o cabeçalho `Authorization`; `GET /health` é a exceção — continua público e não pede token
em caso nenhum.

**O token dura uma hora** (`expiresIn: 3600`). Se você voltar a este README depois disso, os comandos
abaixo respondem `401` mesmo estando certos: refaça o passo do token acima, ou troque o `refreshToken`
guardado por um par novo com `POST /auth/refresh`, na seção **Autenticação**.
