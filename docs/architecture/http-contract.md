# Contrato HTTP da API geradora

Dois endpoints. O backend é a **fonte de verdade** do catálogo; o frontend nunca codifica
valores nem a regra de compatibilidade.

## `GET /api/template-options`

Devolve opções, rótulos em português, padrões e restrições.

```jsonc
{
  "templateVersion": "1.0.0",
  "fields": {
    "architecture": {
      "label": "Arquitetura",
      "default": "simple",
      "values": [
        { "value": "simple", "label": "Simples", "description": "Um projeto Web API." },
        { "value": "clean",  "label": "Clean Architecture", "description": "Api, Application, Domain e Infrastructure." }
      ]
    },
    "database":       { "label": "Banco",        "default": "none",     "values": [ /* none, sqlite, postgresql */ ] },
    "authentication": { "label": "Autenticação", "default": "none",     "values": [ /* none, identity, jwt */ ] },
    "swagger":        { "label": "Swagger",      "default": true,       "type": "boolean" },
    "dotnetVersion":  { "label": "Versão .NET",  "default": "net10.0",  "values": [ /* net10.0 */ ] }
  },
  "constraints": [
    {
      "id": "identity-requires-database",
      "when":    { "authentication": "identity" },
      "requires": { "database": ["sqlite", "postgresql"] },
      "message": "O Identity nativo precisa de um banco para persistir os usuários."
    }
  ]
}
```

As restrições são **dados**, não código. Acrescentar uma restrição não deve exigir mudança no
frontend.

## `POST /api/templates`

**Requisição** (`application/json`):

```json
{
  "projectName": "Acme.Billing.Api",
  "architecture": "simple",
  "database": "sqlite",
  "authentication": "identity",
  "swagger": true,
  "dotnetVersion": "net10.0"
}
```

**Resposta 200** — `application/zip`, com
`Content-Disposition: attachment; filename="<projectName>.zip"`.

**Resposta 400** — `application/problem+json` ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)):

```json
{
  "type": "https://templategenerator.local/problems/invalid-configuration",
  "title": "Configuração inválida",
  "status": 400,
  "errors": {
    "authentication": ["O Identity nativo precisa de um banco para persistir os usuários."]
  }
}
```

**Resposta 429** — quando o limite de requisições ou de gerações simultâneas é atingido, também
em `ProblemDetails`, com `Retry-After`.

## Regras

- Toda validação do frontend é **conveniência**; o backend revalida tudo e é quem decide.
- Mensagens de erro em português, endereçadas ao campo que as causou, para que a tela consiga
  posicioná-las *inline*.
- O contrato não versiona por URL no MVP. `templateVersion` no catálogo e no manifesto do ZIP é
  o que identifica a geração.
- Sem autenticação, sem cookie, sem sessão. O gerador é *stateless*.
