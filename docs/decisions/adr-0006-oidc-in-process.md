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

## Emenda de T07 — o hospedeiro é Kestrel, não `WebApplicationFactory`

**Data:** 2026-09-25 · **Levantada pelo papel `qa` em T07, registrada pelo PO após o parecer do
`reviewer`.** A decisão acima **não muda**; muda o mecanismo que ela nomeia.

Esta ADR foi escrita em 2026-09-12, antes de a camada 3 passar a subir a aplicação gerada como
**processo separado** — que é o que a distingue das camadas 1 e 2: código compilado, executando de
verdade. O `TestServer` da `WebApplicationFactory` é um pipeline **em memória, sem socket**, e
portanto **inalcançável de outro processo**. Para o consumidor que esta decisão precisa servir, o
mecanismo nomeado é impossível.

O emissor passa a ser hospedado por **Kestrel numa porta de loopback**, ainda dentro do processo de
testes. Os quatro elementos que a decisão protege seguem inteiros, e a substituição fortalece dois
deles:

| O que a decisão protege | Como fica |
|---|---|
| hospedado **in-process** pelo conjunto de testes | preservado — "in-process" aqui sempre se opôs a *container* ([ADR-0005](adr-0005-sem-containers.md)), não a *socket* |
| expõe descoberta e JWKS | preservado e **mais forte**: por HTTP real, não por pipeline em memória |
| chave RSA gerada no próprio teste, nunca versionada | preservado, e agora afirmado **como teste**, não como intenção |
| o template recebe `Authority` apontando para o emissor | preservado |
| a recusa de "mockar o handler" | preservada e **excedida**: os tokens são montados à mão — cabeçalho, corpo e assinatura RSA sobre Base64Url — e não pela biblioteca que os valida, para que um par de defeitos simétricos não passe despercebido |

**Duas armadilhas de ambiente que isto trouxe, e que custaram uma falha intermitente em T07.**

1. **O proxy do sistema.** O `HttpClient` padrão resolve o proxy do Windows, e numa máquina
   corporativa a chamada a `127.0.0.1` pode sair pelo gateway — que aceita a conexão e não responde.
   O sintoma é cruel: falha só sob a suíte inteira, passa isolado, e sempre no prazo exato do
   cliente. Quem fala com o emissor precisa de `UseProxy = false`. Ver a observação (b) do parecer
   de T07 em [`../reports/T07.md`](../reports/T07.md): o cliente do teste foi tratado, e há outros
   dois com a mesma exposição.
2. **A reserva de porta.** Bindar a porta 0, ler o número e **soltar o socket** antes de rebindar é
   uma janela de corrida. Para um servidor deste processo ela some inteira: dê `:0` ao Kestrel e leia
   o endereço efetivo **depois** do `StartAsync`. Para a aplicação gerada ela não some — o número
   precisa ser decidido antes, para entrar em `ASPNETCORE_URLS` — e o que se faz é estreitá-la e
   **falhar alto**, nunca fingir que sumiu.

## Consequências

- O teste não exercita descoberta OIDC pela internet. Aceito: o que está sob teste é a
  configuração do template, não a implementação do protocolo pela Microsoft.
- Chaves geradas por execução ⇒ tokens diferentes a cada rodada. Isso **não** afeta o
  determinismo do ZIP, que não contém token.
- **O emissor escuta uma porta real de loopback** (emenda de T07). Isso traz as duas armadilhas de
  ambiente acima, que não existiriam num pipeline em memória — é o preço de o consumidor ser um
  processo separado, e ele é pago do lado dos testes, não do projeto entregue.
