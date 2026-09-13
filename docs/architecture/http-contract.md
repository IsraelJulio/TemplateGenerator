# Contrato HTTP da API geradora

Três endpoints. O backend é a **fonte de verdade** do catálogo; o frontend nunca codifica
valores nem a regra de compatibilidade.

## `GET /api/health`

Saúde da **plataforma geradora**. Público, sem autenticação, `200` com corpo mínimo em JSON.

Existe por um motivo concreto: sem ele, o frontend não distingue "API fora do ar" de "rota
ausente" — só consegue detectar problema falhando o próprio `GET /api/template-options`, e foi
exatamente isso que confundiu a verificação do papel `frontend` em T01.

> Não confundir com o `/health` dos **projetos gerados** (RF-12), que é outro endpoint, em outra
> aplicação, entregue em T03.

## `GET /api/template-options`

Devolve opções, rótulos em português, padrões e restrições.

```jsonc
{
  "templateVersion": "1.0.0",
  "fields": {
    "architecture": {
      "label": "Arquitetura",
      "type": "choice",
      "default": "simple",
      "values": [
        { "value": "simple", "label": "Simples", "description": "Um projeto Web API." },
        { "value": "clean",  "label": "Clean Architecture", "description": "Api, Application, Domain e Infrastructure." }
      ]
    },
    "database":       { "label": "Banco",        "type": "choice",  "default": "none",     "values": [ /* none, sqlite, postgresql */ ] },
    "authentication": { "label": "Autenticação", "type": "choice",  "default": "none",     "values": [ /* none, identity, jwt */ ] },
    "swagger":        { "label": "Swagger",      "type": "boolean", "default": true },
    "dotnetVersion":  { "label": "Versão .NET",  "type": "choice",  "default": "net10.0",  "values": [ /* net10.0 */ ] }
  },
  "constraints": [
    {
      "id": "identity-requires-database",
      "when":     { "authentication": ["identity"] },
      "requires": { "database": ["sqlite", "postgresql"] },
      "message": "O Identity nativo precisa de um banco para persistir os usuários."
    }
  ]
}
```

**`type` só admite dois valores:** `"choice"` (campo com lista em `values`) e `"boolean"`
(interruptor, sem `values`). Ambos são obrigatórios de emitir — ver regra 3 adiante.

As restrições são **dados**, não código. Acrescentar uma restrição não deve exigir mudança no
frontend.

O catálogo **não** descreve a árvore de pastas do ZIP, e por isso a "estrutura prevista" da tela é
hoje projeção do cliente — ver [ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md).

O catálogo também **ainda não** diz quais valores têm template. Que ele passe a dizer, como dado e
derivado dos fragmentos, está decidido em
[ADR-0012](../decisions/adr-0012-combinacao-sem-template.md) e cabe a T04. Nada neste documento
descreve esse campo como existente enquanto o código não o emitir.

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
em `ProblemDetails`, com `Retry-After`. Detalhe na seção "Limites de geração" adiante.

## Regras de serialização

Estes pontos foram levantados pelo papel `frontend` em T01, onde o contrato estava ambíguo o
bastante para frontend e backend divergirem sem que nenhum dos dois estivesse errado. Ficam
cravados:

1. **A ordem de `fields` no JSON é a ordem da tela.** O backend serializa numa ordem estável e
   declarada — nunca a partir de um dicionário sem ordem garantida. Se a ordem oscilar entre
   chamadas, a tela se reorganiza sozinha.
2. **`projectName` não pertence a `fields`.** Não é uma escolha de catálogo: é entrada livre com
   regra própria, definida em [`../product/option-matrix.md`](../product/option-matrix.md).
   O frontend duplica essa regra **deliberadamente**, para dar resposta imediata; o backend
   continua soberano e revalida. Se um dia o rótulo dele precisar vir do servidor, isso exige
   mudança neste contrato.
3. **`type` é obrigatório em todo campo**, inclusive nos de escolha, e admite exatamente dois
   valores: **`"choice"`** e **`"boolean"`**. Não deduza o tipo pela ausência de `values`.
4. **`when` e `requires` usam sempre array**, mesmo com um único valor. Os clientes toleram valor
   único ao **ler**, por robustez — desserializar é lidar com o que chega. Mas o backend **não
   emite** essa forma.
5. **A mensagem de erro de uma restrição é endereçada ao(s) campo(s) de `when`**, não aos de
   `requires`. Para `identity-requires-database`, a chave em `errors` é `authentication` —
   o campo que a pessoa acabou de mexer. Sem esta regra, a mensagem local e a do servidor
   aparecem em campos diferentes na mesma tela.
6. **A resposta de erro do `POST` precisa mesmo vir com `Content-Type: application/problem+json`
   e corpo JSON.** O cliente pede o corpo como binário para receber o ZIP; é o content type que
   permite distinguir um erro de um arquivo.
7. **Os seis campos do `POST` são todos obrigatórios.** Campo ausente é `400`, não "assume o
   padrão do catálogo". O caso que força a regra é `swagger`: um booleano ausente do JSON chega
   como `false`, indistinguível de uma escolha explícita — e o padrão é `true`. Assumir o padrão
   exigiria confiar num valor que ninguém escolheu; assumir `false` inverteria o padrão em
   silêncio.
8. **Os exemplos deste documento são o mínimo, não a lista fechada de chaves.** A RFC 9457
   permite extensões, e as respostas reais trazem `traceId`; o `429` traz `retryAfterSeconds`.
   Nenhum cliente deve assumir "exatamente estas chaves".

## Limites de geração

Os dois limites de RNF-04 valem **só para `POST /api/templates`** e são lidos da seção
`Generation:Limits` da configuração a cada requisição — não capturados na inicialização, o que
permite a um teste fixar um limite sem depender da ordem em que o host monta a configuração.

| Chave | Default | O que protege |
|---|---|---|
| `Generation:Limits:MaxConcurrentGenerations` | `4` | a máquina: quantas gerações cabem ao mesmo tempo, no processo inteiro |
| `Generation:Limits:RequestsPerWindow` | `30` | contra uma origem sozinha ocupar a fila inteira |
| `Generation:Limits:WindowSeconds` | `60` | tamanho da janela fixa do limite por origem |
| `Generation:Limits:RetryAfterSeconds` | `5` | `Retry-After` quando o limitador não sabe dizer quanto falta |

São dois limites diferentes de propósito, encadeados; um sem o outro deixa um dos dois buracos
aberto. **Os defaults estão no código, e não só no `appsettings.json`:** uma instalação que apague a
seção fica com o limite default, nunca sem limite.

Pontos que o contrato crava:

- **`GET /api/health` fica de fora.** Ele existe para o cliente distinguir "API fora do ar" de "rota
  ausente", e um health limitado responderia `429` exatamente quando alguém precisa saber se o
  servidor está vivo.
- **Sem fila.** O excedente é recusado na hora. Enfileirar faria o cliente esperar sem saber por
  quê, e um download que demora é indistinguível de um servidor travado.
- **O status é `429`, não `503`** — que é o default do middleware. `429` informa "tente de novo";
  `503` afirma indisponibilidade, que não é o caso.
- **A origem é o endereço da conexão, não `X-Forwarded-For`.** Um limite baseado em cabeçalho que o
  próprio cliente escreve não é limite. Atrás de proxy reverso, a forma certa é configurar
  `ForwardedHeaders` com a lista de proxies confiáveis — decisão de implantação, que não está tomada
  aqui e não deve ser fingida por default.
- **`Retry-After` em segundos**, sempre ≥ 1, com o mesmo número repetido na extensão
  `retryAfterSeconds` do `ProblemDetails`. A janela fixa sabe dizer quanto falta; o limite de
  concorrência não — ele depende de outra requisição terminar, e aí vale `RetryAfterSeconds`.

## Histórico: o `501` que existiu até T03

**Não é mais emitido.** Desde T03 uma configuração válida responde `200 application/zip`, e
`ProblemTypes.GenerationNotImplemented` foi removido do código. Um cliente **não** deve tratar
`501` como estado esperado deste contrato.

O registro fica porque o próprio texto anterior previa a troca, e porque o raciocínio continua
valendo caso a situação se repita em outro endpoint. Enquanto o motor não existia (T01 e T02), uma
configuração válida respondia `501 Not Implemented` em `ProblemDetails`, com
`type: .../problems/generation-not-implemented`. A alternativa — devolver `200 application/zip` com
um pacote vazio — foi descartada: um `200` com `Content-Disposition` é uma **afirmação de que a
geração aconteceu**. O cliente salvaria o arquivo e o defeito só apareceria ao abrir o pacote, longe
da causa. O `501` dizia a verdade exata do estado: a configuração passou pela validação, o motor não
existia.

**O `501` volta em T04, com outro escopo.** [ADR-0012](../decisions/adr-0012-combinacao-sem-template.md)
decidiu que uma combinação **sem template** não pode responder `200` com um pacote incompleto, e que
a recusa é `501` pelo mesmo raciocínio acima: a configuração é válida, quem está incompleto é o
servidor. **Isso ainda não está implementado** — hoje as 16 combinações de `clean` respondem `200`
com um ZIP de cinco arquivos, e isso é um defeito registrado, não o contrato.

**Consequência para T09:** o tratamento de `501` no frontend **não** é código morto — ele vive em
`src/web/src/app/core/catalog/generation-failure.ts` (a marca `notImplemented`), nos testes que a
exercitam e no `configurator.html`, que escolhe entre `notice--pending` e `notice--error` a partir
dela. Entre T03 e a implementação de ADR-0012 ele fica sem caso real, alcançável só por dublê de
rede; depois dela volta a ter um, mais estreito. O que T09 precisa ajustar é a **mensagem**: ela diz
hoje que o motor de geração ainda não existe, e o que passará a ser verdade é "esta combinação ainda
não gera projeto". A marca também merece um nome que descreva a causa nova.

## Regras

- Toda validação do frontend é **conveniência**; o backend revalida tudo e é quem decide.
- Mensagens de erro em português, endereçadas ao campo que as causou, para que a tela consiga
  posicioná-las *inline*.
- O contrato não versiona por URL no MVP. `templateVersion` no catálogo e no manifesto do ZIP é
  o que identifica a geração.
- Sem autenticação, sem cookie, sem sessão. O gerador é *stateless*.
