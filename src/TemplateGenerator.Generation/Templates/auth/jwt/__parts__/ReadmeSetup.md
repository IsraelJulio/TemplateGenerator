
## Autenticação

Esta combinação valida **JWT emitido por um provedor externo** — o que a sua organização já usa,
seja ele Keycloak, Entra ID, Auth0, Okta ou qualquer outro emissor OIDC. **Este projeto não hospeda
provedor de identidade e não emite token nenhum:** não há cadastro, não há login, não há tabela de
usuários e não existe chave de assinatura em lugar algum do pacote. Ele só confere o que chega no
cabeçalho `Authorization`.

É por isso que esta opção funciona com **qualquer banco de dados, inclusive sem banco nenhum**: a
autenticação não guarda nada e não tem esquema a migrar.

### 1. Apontar `Authority` e `Audience` para o seu provedor

`appsettings.json` traz as duas chaves **vazias**, de propósito — um provedor embutido não existe, e
um endereço de exemplo apontaria para lugar nenhum:

```json
"Jwt": {
  "Authority": "",
  "Audience": ""
}
```

- **`Authority`** é o endereço base do seu emissor. É dele que a aplicação busca
  `/.well-known/openid-configuration` e, por ele, as chaves públicas com que confere a assinatura —
  e é o emissor anunciado ali que precisa bater com o `iss` do token.
- **`Audience`** é o destinatário que o token precisa declarar em `aud`. Nos provedores é o
  identificador da API, do recurso ou do *client* para o qual o token foi pedido.

Preencha os dois em `__ApiProjectDir__/appsettings.json`, ou informe-os pelo ambiente, que é o
caminho que não mexe em arquivo nenhum do projeto. Em Linux, macOS ou Git Bash:

```bash
export Jwt__Authority="https://seu-provedor.exemplo/realms/sua-organizacao"
export Jwt__Audience="sua-api"
```

No PowerShell:

```powershell
$env:Jwt__Authority = "https://seu-provedor.exemplo/realms/sua-organizacao"
$env:Jwt__Audience = "sua-api"
```

Os dois sublinhados são a forma como o ASP.NET Core lê uma chave aninhada do ambiente: eles
substituem o `:` de `Jwt:Authority`. A variável vale para o terminal em que você a definiu, então
defina-a no mesmo terminal em que for executar a aplicação.

Para conferir o endereço antes de subir a aplicação, peça ao provedor o mesmo documento que ela vai
pedir:

```bash
curl -s "$Jwt__Authority/.well-known/openid-configuration"
```

A resposta traz o `issuer` — que precisa ser igual ao `iss` dos seus tokens — e o `jwks_uri`, de
onde saem as chaves públicas de assinatura.

**Enquanto `Authority` estiver vazio, nenhum token é aceito.** A aplicação sobe normalmente,
`GET /health` responde e todo o CRUD responde `401`. É deliberado: sem emissor configurado não há
como distinguir um token legítimo de um forjado.

**Fora de `Development`, o endereço precisa ser HTTPS.** Em `Development` — que é o ambiente do
`launchSettings.json` deste projeto — `http://` é aceito, porque apontar para um emissor local
durante o desenvolvimento é comum.

### 2. O que é aceito, e o que responde 401

Um token só passa se as quatro conferências abaixo passarem. Qualquer uma que falhe responde `401`,
e nenhuma delas depende de papel, de claim ou de banco de dados:

| Conferência | O que é comparado |
|---|---|
| Assinatura | a assinatura do token contra as chaves públicas do `jwks_uri` do provedor |
| Emissor | o `iss` do token contra o emissor anunciado pelo `Authority` |
| Audiência | o `aud` do token contra o `Audience` configurado |
| Validade | o `exp` do token contra o relógio, **sem tolerância** |

A tolerância de relógio está zerada na composição da aplicação, de propósito: por padrão a
biblioteca aceitaria um token vencido havia até cinco minutos. Quem precisar de folga para relógios
dessincronizados ajusta `ClockSkew` em `Program.cs`.

### 3. Sem token, o CRUD responde 401

Estes comandos falam com a aplicação **já no ar**: suba-a primeiro, com o passo **Executar** logo
adiante, e use outro terminal. `GET /health` continua **público** em qualquer combinação; o CRUD de
`Item`, não:

```bash
curl -i http://localhost:5100/health
curl -i http://localhost:5100/items
```

A primeira chamada responde `200`; a segunda, `401`. **Qualquer token válido serve** na segunda: não
há papel, não há claim exigida e não há dono de registro — quem apresenta um token que passa nas
quatro conferências enxerga e altera os mesmos itens que todo mundo.

Pegar o token no seu provedor e usá-lo é o assunto da seção **Testar o CRUD de `Item`**, adiante.
