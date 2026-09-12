# ADR-0006 — Provedor OIDC de teste in-process

**Data:** 2026-09-12 · **Estado:** aceita

## Contexto

RF-19 exige provar que o template `jwt` aceita um token válido e **rejeita** assinatura, emissor,
audiência e validade incorretos. Isso precisa de um emissor cujas chaves e claims o teste
controle.

O caminho usual seria Keycloak ou Duende num container — indisponível por
[ADR-0005](adr-0005-sem-containers.md).

## Decisão

Um emissor OIDC **hospedado in-process** pelo próprio conjunto de testes, via
`WebApplicationFactory`:

- Expõe `/.well-known/openid-configuration` e um endpoint JWKS.
- Assina com uma chave RSA **gerada no próprio teste**, nunca versionada.
- O template sob teste recebe `Authority` apontando para esse emissor.

Os quatro casos de rejeição saem de manipulações diretas:

| Caso | Como |
|---|---|
| Assinatura inválida | assinar com uma segunda chave, ausente do JWKS |
| Emissor errado | emitir com `iss` diferente do `Authority` configurado |
| Audiência errada | emitir com `aud` diferente do configurado |
| Expirado | emitir com `exp` no passado |

## Alternativas descartadas

- **Container OIDC:** proibido pelo ambiente.
- **Mockar o handler de autenticação:** testaria o mock, não a validação real do
  `JwtBearer` — justamente o que RF-19 exige provar.
- **Provedor público na nuvem:** dependência de rede e de conta externa num teste.

## Consequências

- O teste não exercita descoberta OIDC pela internet. Aceito: o que está sob teste é a
  configuração do template, não a implementação do protocolo pela Microsoft.
- Chaves geradas por execução ⇒ tokens diferentes a cada rodada. Isso **não** afeta o
  determinismo do ZIP, que não contém token.
