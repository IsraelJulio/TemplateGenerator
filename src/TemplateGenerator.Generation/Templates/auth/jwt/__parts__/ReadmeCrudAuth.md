
**Comece pelo token.** Nesta combinação todo o CRUD exige `Authorization`, e por isso cada comando
desta seção já traz o cabeçalho, lendo a variável `TOKEN` — sem ela, toda linha da tabela de
respostas mais abaixo vira `401`. O token vem do **seu** provedor, o mesmo apontado por
`Jwt:Authority` na seção **Autenticação**: esta aplicação não emite nenhum.

Se o seu provedor aceita o fluxo de credenciais de cliente, dá para pedir o token da própria linha
de comando. Troque o endereço pelo `token_endpoint` do documento de descoberta e os valores pelos do
seu *client*. Em Bash:

```bash
TOKEN=$(curl -s -X POST "$Jwt__Authority/protocol/openid-connect/token" -H 'Content-Type: application/x-www-form-urlencoded' -d 'grant_type=client_credentials&client_id=SEU_CLIENT_ID&client_secret=SEU_SEGREDO&audience=sua-api' | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')
```

No PowerShell — com o corpo pelo pipe e a codificação do pipe fixada, pelos motivos explicados
adiante, na parte de `curl.exe`:

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$TOKEN = ('grant_type=client_credentials&client_id=SEU_CLIENT_ID&client_secret=SEU_SEGREDO&audience=sua-api' | curl.exe -s -X POST "$env:Jwt__Authority/protocol/openid-connect/token" -H "Content-Type: application/x-www-form-urlencoded" --data-binary '@-' | ConvertFrom-Json).access_token
```

Se o fluxo do seu provedor for outro — código de autorização, por exemplo —, obtenha o token por lá
e cole-o na variável. Em Bash:

```bash
TOKEN=COLE_O_TOKEN_AQUI
```

No PowerShell:

```powershell
$TOKEN = "COLE_O_TOKEN_AQUI"
```

**O segredo do *client* não fica em arquivo nenhum deste projeto**, e não deve ser versionado: ele
aparece acima apenas como valor a substituir na linha de comando.

Feito isso, os comandos abaixo rodam como estão. Para ver o `401` de propósito, repita qualquer um
deles sem o cabeçalho `Authorization`; `GET /health` é a exceção — continua público e não pede token
em caso nenhum.

**Um token vencido responde `401`**, e sem tolerância de relógio. Se você voltar a este README
depois de o token expirar, os comandos abaixo respondem `401` mesmo estando certos: peça outro
token ao seu provedor, repetindo o passo acima.
