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
  ],
  "unavailable": [
    { "field": "database",       "value": "sqlite",     "reason": "O template desta opção ainda não foi escrito." },
    { "field": "database",       "value": "postgresql", "reason": "O template desta opção ainda não foi escrito." },
    { "field": "authentication", "value": "identity",   "reason": "O template desta opção ainda não foi escrito." },
    { "field": "authentication", "value": "jwt",        "reason": "O template desta opção ainda não foi escrito." }
  ]
}
```

**`type` só admite dois valores:** `"choice"` (campo com lista em `values`) e `"boolean"`
(interruptor, sem `values`). Ambos são obrigatórios de emitir — ver regra 3 adiante.

As restrições são **dados**, não código. Acrescentar uma restrição não deve exigir mudança no
frontend.

O catálogo **não** descreve a árvore de pastas do ZIP, e por isso a "estrutura prevista" da tela é
projeção do cliente — ver [ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md).

### `unavailable` — quais valores ainda não geram projeto

**Membro de topo, irmão de `constraints`**, entregue desde T04. Cada item é um par `(campo, valor)`
que não tem template, com a razão em português. Decisão e alternativas descartadas em
[ADR-0012](../decisions/adr-0012-combinacao-sem-template.md).

- **É dado, como as restrições já são.** A tela desabilita o que vier aqui e mostra a `reason`,
  casando `field` e `value` contra o que o próprio catálogo lhe entregou. Nenhum valor de opção é
  codificado do outro lado — **RF-02 continua literal**.
- **`value` carrega o tipo do campo:** texto no campo de escolha, booleano no interruptor. Nunca a
  string `"true"`.
- **A ordem é a do catálogo** — dos campos, e dentro de cada campo a dos valores. Mesma disciplina da
  regra 1.
- **`reason` é uma frase só, igual para todas.** Ela é derivada, não escrita por valor: um motivo
  específico por opção teria de morar em algum lugar escrito à mão, que é a segunda fonte de verdade
  que ADR-0012 existe para não criar. A especificidade vem da **posição** — a frase aparece colada ao
  rótulo da opção, que o catálogo já manda.
- **Lista vazia significa "está tudo implementado".** É o estado final do produto, e é por isso que
  o membro lista só os indisponíveis em vez de um `available` por valor.
- **Todo par pertence ao catálogo da mesma resposta.** Não existe `field`/`value` em `unavailable`
  que a tela não consiga localizar em `fields`; um par órfão desabilitaria uma opção que não existe.
- **A lista é derivada dos fragmentos do servidor, não escrita à mão** — quando o template que falta
  for escrito, a opção acende sozinha e some daqui, sem edição de catálogo.

**Duas coisas que este membro não toca**, e é deliberado: a ordem de `fields` continua sendo a ordem
da tela (regra 1) e `type` continua admitindo exatamente `"choice"` e `"boolean"` (regra 3). É
acréscimo, e a regra 8 já o autoriza — **um cliente que ignore `unavailable` se comporta exatamente
como antes dele existir.**

Dois valores nunca aparecem aqui, e as duas ausências são regra, não acaso: **a posição desligada de
um interruptor** — `swagger = false` é a *ausência* do fragmento, não um fragmento vazio — e **todo
valor de campo sem eixo de fragmento**, que é `dotnetVersion`. Onde a escolha não implica fragmento,
não há ausência que se possa confundir com template incompleto.

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

**Resposta 501** — quando a configuração é **válida** e o servidor não tem template para ela
([ADR-0012](../decisions/adr-0012-combinacao-sem-template.md)):

```json
{
  "type": "https://templategenerator.local/problems/generation-not-implemented",
  "title": "Esta combinação ainda não gera projeto",
  "status": 501,
  "errors": {
    "database": ["O template desta opção ainda não foi escrito."]
  }
}
```

- **`501` e não `400`:** a escolha da pessoa está certa — passou pela validação, pertence ao catálogo
  e satisfaz as restrições. Quem está incompleto é o servidor, e não há nada que ela possa consertar.
- **`errors` diz qual campo causou a recusa**, na mesma estrutura do `400`, para a tela posicionar a
  mensagem *inline* no campo. Uma entrada por campo indisponível, na ordem dos campos no catálogo.
  **Não há extensão paralela** carregando a mesma informação.
- **A frase é a mesma que o catálogo publica em `unavailable`.** A pessoa lê as mesmas palavras
  tendo aprendido pela tela ou pela recusa.
- **Sem `detail`.** Com `errors` preenchido, um `detail` genérico repetiria em prosa o que já está
  endereçado ao campo — e o cliente trata `detail` como o que se mostra *quando não há* erro de
  campo.
- **Nenhum cabeçalho de download:** a recusa sai sem `Content-Disposition` e sem
  `application/zip`. Ela acontece antes de qualquer byte do pacote existir.

**A ordem entre `400` e `501` é parte do contrato:** a validação inteira vem primeiro — nome do
projeto, pertinência ao catálogo e a restrição `identity-requires-database` —, e só depois a
disponibilidade. Então `clean` + `identity` + `database: none` responde **`400`** com a mensagem da
restrição, endereçada a `authentication`, e nunca `501`. O motivo de a ordem não poder ser a inversa
é concreto: um valor que **não existe** no catálogo também não tem fragmento, e responder "o template
desta opção ainda não foi escrito" a um `architecture: "banana"` trocaria um erro claro de quem
chamou por um defeito inventado do servidor.

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

## Quem alcança o `501`, e por que o cliente precisa tratá-lo

Há uma objeção óbvia a fazer, e ela já custou uma discussão: **se a tela desabilita todo valor
indisponível, como é que a tela recebe um `501`?**

Recebe, e o caso é real: **o catálogo é buscado uma vez, no carregamento da página.** Uma aba aberta
antes de uma mudança no conjunto de fragmentos segue com a disponibilidade de ontem e recebe do
servidor a de hoje. É exatamente o mesmo motivo pelo qual o tratamento de `400` existe embora a tela
valide localmente — a primeira regra da seção "Regras", adiante: *toda validação do frontend é
conveniência; o backend revalida tudo e é quem decide*.

**Consequência prática, para quem escrever teste:** o `501` **não é alcançável clicando** numa tela
com catálogo fresco. Demonstrá-lo pede um catálogo envelhecido ou um dublê de rede, e isso não é
fraqueza do teste — é a forma do caso. Quem o tratar como inalcançável e apagar o ramo quebra o
cliente na primeira implantação com aba aberta.

No frontend ele vive em `src/web/src/app/core/catalog/generation-failure.ts` (a marca
`notImplemented`) e no `configurator.html`, que escolhe entre `notice--pending` e `notice--error` a
partir dela — `notice--pending` porque não é erro de quem chamou.

### Histórico: o `501` de T01 e T02 era outro

O `501` **saiu em T03 e voltou em T04 com escopo menor**, e o registro fica porque o `type` é o
mesmo URI nos dois — um cliente antigo continua reconhecendo a classe do erro, que é o motivo de não
o termos trocado.

Enquanto o motor não existia (T01 e T02), **qualquer** configuração válida respondia `501`: a
mensagem era "o motor de geração ainda não existe". Em T03 o motor nasceu, o `501` deixou de ser
emitido e `ProblemTypes.GenerationNotImplemented` foi removido do código. Em T04 ele voltou
dizendo outra coisa — **"esta combinação ainda não gera projeto"** —, endereçada ao campo.

O raciocínio que sustentou as três fases é o mesmo, e vale registrar porque se aplica a qualquer
endpoint que um dia esteja incompleto: a alternativa — devolver `200 application/zip` com um pacote
vazio ou parcial — foi descartada, porque um `200` com `Content-Disposition` é uma **afirmação de que
a geração aconteceu**. O cliente salvaria o arquivo e o defeito só apareceria ao abrir o pacote,
longe da causa.

## Regras

- Toda validação do frontend é **conveniência**; o backend revalida tudo e é quem decide.
- Mensagens de erro em português, endereçadas ao campo que as causou, para que a tela consiga
  posicioná-las *inline*.
- O contrato não versiona por URL no MVP. `templateVersion` no catálogo e no manifesto do ZIP é
  o que identifica a geração.
- Sem autenticação, sem cookie, sem sessão. O gerador é *stateless*.
