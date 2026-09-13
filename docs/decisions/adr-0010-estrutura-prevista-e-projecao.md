# ADR-0010 — A "estrutura prevista" é projeção do frontend, não dado do backend

**Data:** 2026-09-12 · **Estado:** **parcialmente expirada em T03** — ver
["Estado em T03"](#estado-em-t03) · **Levantada por:** papel `po` em T02, registrada pelo
`architect` na mesma tarefa

> **Leia isto antes do resto.** A condição de expiração desta ADR foi cumprida **para a arquitetura
> Simples** em T03: a árvore da tela está amarrada a um ZIP real, por teste. Para a **Clean** ela
> continua valendo inteira. O texto original está preservado abaixo porque o histórico e a lição
> valem mais que a decisão; o que mudou está em ["Estado em T03"](#estado-em-t03), no fim.

## Contexto

[RF-04](../product/requirements.md) exige que o resumo lateral mostre "arquitetura, funcionalidades
e **estrutura prevista do projeto**". [RF-02](../product/requirements.md) exige o oposto no mesmo
lugar: "o catálogo de opções vem do backend; **o frontend não codifica valores nem regras**".

Em T02 as duas não tinham como ser cumpridas ao mesmo tempo:

- O catálogo de `GET /api/template-options` não descreve a árvore de pastas do ZIP, e
  [`../architecture/http-contract.md`](../architecture/http-contract.md) não prevê esse campo.
- O motor de geração só nasce em T03. Não existe ZIP real contra o qual comparar coisa alguma —
  configuração válida responde `501` por decisão do próprio contrato.
- O papel `backend` não fazia parte de T02.

A única fonte que descreve a árvore hoje é um documento de arquitetura,
[`../architecture/generated-projects.md`](../architecture/generated-projects.md), lido por humanos.

## Decisão

**A árvore da "estrutura prevista" é derivada no cliente, como projeção declarada, confinada a um
único módulo.**

- Todo o conhecimento vive em `PROJECT_STRUCTURE_RULES`, um mapa declarativo em
  [`../../src/web/src/app/core/summary/project-structure.ts`](../../src/web/src/app/core/summary/project-structure.ts).
  É o **único** arquivo de produção do frontend autorizado a citar valor de opção (`clean`,
  `sqlite`, `identity`, …).
- Nenhum `.html`, `.css` ou componente pode repetir um desses valores. A derivação é dado
  percorrido, não condicional espalhada no template.
- A tela **diz que é projeção**, com essas palavras: *"O motor de geração entra em outra etapa,
  então confira a árvore contra o pacote quando ele existir."*
- Isto é uma **exceção explícita e única a RF-02**, não uma reinterpretação dela. RF-02 continua
  valendo literalmente em todo o resto do frontend.

## Alternativas descartadas

**Estender o contrato para o backend fornecer a árvore.** É a saída correta a médio prazo, e a
única que mata a tensão na raiz: quem compõe o ZIP é o único que sabe a árvore de verdade.
Descartada em T02 porque **em T02 ninguém compõe ZIP nenhum**. O backend só poderia emitir a mesma
árvore adivinhada a partir do mesmo `generated-projects.md` — a diferença seria o palpite passar a
ser servido com autoridade de contrato, e contrato publicado é bem mais caro de corrigir que um
módulo de frontend. Some-se que o papel `backend` não estava na tarefa.

**Não mostrar a árvore até T03.** A alternativa honesta: nada é afirmado, nada pode divergir,
nenhuma dívida é criada. Foi levada a sério. Descartada por duas razões: RF-04 é critério de aceite
de T02, e abrir mão dele é decisão de escopo de produto, não escolha de implementação; e a coluna
de resumo era justamente o que T02 precisava desenhar — entregá-la sem um terço do conteúdo
deixaria layout, espaçamento e tipografia dessa coluna sem verificação, empurrando trabalho visual
para uma tarefa que não é de design.

**Derivar a árvore do próprio catálogo, por heurística** (inferir pastas a partir dos `values`
recebidos). Descartada: continuaria sendo regra codificada, só que disfarçada de genérica e bem
mais difícil de auditar por leitura ou por `grep`.

**Condicionais no template HTML.** Violaria RF-02 em N lugares em vez de um, e inviabilizaria a
verificação de confinamento.

## Consequências

- **Risco nomeado: a projeção pode divergir do ZIP real sem que nada quebre.** Não há teste
  possível hoje, porque não há pacote. É exatamente o padrão que mordeu T01, onde a fixture do
  catálogo divergiu do payload real em **três** pontos sem derrubar um único teste — o código
  tolera as duas formas na leitura — e a deriva só apareceu em conferência manual
  ([`../reports/T01.md`](../reports/T01.md)).
- **O confinamento, ao contrário da correção, é verificável nas duas direções e por teste
  reexecutável — dentro de um limite declarado.** `project-structure.spec.ts` varre os arquivos de
  produção do frontend (`.ts`, `.html` e `.css` sob `src/`, menos `*.spec.ts`, menos
  `src/app/testing/**`, menos o próprio módulo autorizado) e falha listando `arquivo: 'valor'` a
  cada vazamento; um teste irmão exige que os mesmos valores **estejam** no módulo autorizado.
  "Limpar" o módulo movendo as regras para outro lugar derruba os dois lados em vez de esconder a
  exceção.
- **O que o reconhecedor enxerga**, que são as duas formas em que um valor de opção aparece na
  prática: o **literal entre aspas** (`'clean'`, `"clean"`, `` `clean` ``) e o **seletor de
  atributo** (`[data-value=clean]`, com ou sem aspas). A segunda não é hipótese: o template expõe
  `[attr.data-value]="option.value"` justamente para as opções serem selecionáveis, e em CSS as
  aspas do seletor são opcionais — os sete valores são identificadores CSS válidos. Procurar a
  palavra solta, em vez dessas formas, acusaria `list-style: none` e a palavra inglesa em
  comentário; um teste que grita sem motivo é desligado pela próxima pessoa e aí não protege mais
  nada. Dois cuidados completam: guarda contra varredura vazia — a lista de arquivos precisa conter
  `configurator.ts`, `configurator.html` e `styles.css` e passar de 15 entradas —, pelo mesmo motivo
  do assert de sanidade de [ADR-0008](adr-0008-fronteira-por-grafo-de-restore.md), já que um
  verificador que silenciosamente para de verificar é pior que nenhum; e a lista de valores
  proibidos é derivada das próprias `PROJECT_STRUCTURE_RULES`, para não virar ficção quando uma
  regra mudar.
- **O que o reconhecedor não enxerga, dito com todas as letras: valor montado em tempo de
  execução.** `'ident' + 'ity'`, interpolação, tabela indexada por chave — qualquer varredura
  textual escapa disso, e sempre escapará. Ou seja: a trava reduz o vazamento **acidental** a quase
  zero e **não impede o deliberado**. Para o risco que ela existe para conter — alguém estilizar ou
  ramificar uma opção específica sem pensar — isso basta. É uma cerca, não um muro, e a ADR precisa
  dizer cerca: a próxima pessoa herdando permissão que ela **acha** vigiada é pior que herdar uma
  permissão que ela sabe que precisa conferir.
- **Atualização de T03: já são quatro falhas, e as duas novas são piores que as antigas.** O
  `reviewer` de T03 atacou o reconhecedor de novo e passou por **cinco** formas, entre elas o valor
  **colado a um prefixo ou sufixo, sem aspas próprias**: `.option--clean`, `#opt-postgresql`,
  `class="badge badge-identity"` e `[class.arch-simple]="…"`. A última é **a forma idiomática de
  estilizar por opção num template Angular** — ou seja, a cerca não cobria o caso exato que esta ADR
  diz existir para conter. Passa também o valor dentro de string maior (`'simple/sqlite/identity'`)
  e, por um caminho diferente, **todo arquivo de extensão não varrida**: o reconhecedor lê
  `.ts|.html|.css` e ignora `.json`, `.scss` e `.svg` sob `src/` — e já existe um `.json` ali,
  `zip-structure.contract.json`, que **precisa** conter valores porque é gerado do ZIP, de modo que
  ampliar a varredura exige lista de autorizados, não só mais uma extensão. A lista completa e a
  garantia reescrita sem folga estão no docstring de `leaksValue`, em
  [`../../src/web/src/app/core/summary/project-structure.spec.ts`](../../src/web/src/app/core/summary/project-structure.spec.ts).
  **O conserto é de T04; o registro honesto é de agora** — a frase que dizia "nenhuma forma legível
  de citar um valor de opção passa" foi removida, porque cinco formas legíveis passam.
- **Esta garantia já falhou duas vezes, e as duas apareceram por reataque, não por leitura.**
  Primeira: o teste original só conferia que os valores *estavam* no módulo autorizado, nunca que
  estavam *ausentes* nos demais — um `export const REVIEWER_LEAK_TEST = ['identity', 'sqlite',
  'clean', 'jwt'];` plantado em `configurator.ts` passou com a suíte inteira verde. Segunda, **já
  com esta ADR afirmando a garantia como fechada**: `[data-value=identity]` em CSS e em
  `querySelector`, sem aspas, passou nas duas. As duas correções foram provadas nos dois sentidos —
  o teste falha com o vazamento e passa sem ele. A lição vale mais que as correções, e é a mesma de
  ADR-0008: **garantia declarada fechada é hipótese até alguém tentar furá-la de novo.** Quem
  acrescentar uma forma nova de escrever valor no frontend — outro atributo, outro mecanismo de
  template — precisa reatacar o reconhecedor antes de confiar nele.
- Corrigir a árvore quando o ZIP existir custa **um arquivo e um teste**. Foi por isso que o mapa é
  declarativo.
- Esta ADR não autoriza uma segunda exceção a RF-02. A próxima precisa de ADR própria.

### Ambiguidade deixada em aberto de propósito

> **Resolvida.** Para a **Simples**, em T03: o sufixo deixou de ser acrescentado, porque ali não há
> projeto irmão para desambiguar — e isso não é uma regra de deduplicação. Para a **Clean**, em T04:
> o sufixo é concatenado **sempre**, sem comparação e sem remoção, e `Acme.Billing.Api` produz
> `src/Acme.Billing.Api.Api/` de propósito. O aviso desta ADR — "inventar deduplicação é trocar um
> palpite por outro" — foi seguido nos dois casos, e nos dois a pergunta que decidiu foi a mesma:
> *o sufixo desambigua alguma coisa aqui?* As duas decisões, com os casos de borda respondidos, em
> ["Nomes de projeto e de pasta"](../architecture/generated-projects.md#nomes-de-projeto-e-de-pasta).

Com `projectName = Acme.Billing.Api`, a árvore mostra `src/Acme.Billing.Api.Api/` — o `.Api`
dobrado. É consequência de aplicar `<Nome>.Api` às duas arquiteturas: para Clean isso vem de
`generated-projects.md`, que nomeia os quatro projetos; para Simples o documento só diz "um projeto
Web API", sem nomear a pasta.

**Nenhuma regra de deduplicação foi inventada.** Seria trocar um palpite por outro, e o segundo
ficaria escondido atrás de uma aparência melhor. Quem decide é T03/T04, ao escrever os templates, e
a decisão pertence a `generated-projects.md` — a projeção segue o documento, não o contrário.

## Quando esta decisão expira

**Esta ADR fica obsoleta assim que a projeção estiver amarrada a um ZIP real.** Basta uma das duas:

1. **Um teste que gere o ZIP da combinação e compare a árvore real com a de `projectStructure()`,
   falhando na divergência** — T03 para a arquitetura Simples, T04 para a Clean. É o caminho barato.
2. **O catálogo passar a carregar a árvore**, o que aposenta o módulo inteiro e devolve RF-02 à
   forma literal. Exige mudar `http-contract.md` e vale a pena se um segundo cliente aparecer.

Enquanto nenhuma das duas existir, a exceção continua de pé. **Se T03 e T04 fecharem sem nenhuma
delas, isto deixa de ser dívida deliberada e passa a ser defeito** — e o `reviewer` deve tratá-lo
assim, não herdar esta ADR como permissão permanente. Inscrever a amarração como critério de aceite
de T03 e T04 é do papel PO; o `architect` não edita o backlog ([ADR-0004](adr-0004-propriedade-do-backlog.md)).

## Estado em T03

**A saída 1 existe, para a arquitetura Simples.** A amarração é uma corrente de três elos, e cada um
fecha um lado que o anterior deixaria aberto:

| Arquivo | O que garante |
|---|---|
| [`../../tests/TemplateGenerator.Matrix.Tests/Layer1/ZipStructureContractTests.cs`](../../tests/TemplateGenerator.Matrix.Tests/Layer1/ZipStructureContractTests.cs) | que o contrato **é** o ZIP: reconstrói o arquivo a partir do pacote gerado a cada execução e falha se divergir |
| [`../../src/web/src/app/core/summary/zip-structure.contract.json`](../../src/web/src/app/core/summary/zip-structure.contract.json) | o contrato em si — **gerado, nunca escrito à mão** |
| [`../../src/web/src/app/core/summary/zip-structure.spec.ts`](../../src/web/src/app/core/summary/zip-structure.spec.ts) | que `projectStructure()` **é** o contrato: chama a função de verdade e compara |

Sem o primeiro elo, o contrato viraria uma segunda cópia adivinhada — exatamente a fixture de T01.
Sem o terceiro, ele seria um arquivo que ninguém lê.

**Funcionou, e o que achou não era hipótese.** A amarração acusou **37 caminhos divergentes** na
primeira execução, herdados de T02 e até então invisíveis para a suíte inteira, o `.Api` dobrado
entre eles. Nenhum deles tinha derrubado um único teste antes. **Esta é a terceira confirmação da
mesma lição** — as duas primeiras estão listadas em "Consequências", acima —, e a lição é a de
[ADR-0008](adr-0008-fronteira-por-grafo-de-restore.md): *garantia declarada fechada é hipótese até
alguém tentar furá-la de novo.* Aqui quem furou foi um teste, não uma leitura, e foi por isso que
funcionou.

### O que expirou

Para `architecture = simple` com os fragmentos que existem hoje (`database = none`,
`authentication = none`, nos dois valores de `swagger`), a árvore da tela **não é mais uma
afirmação sem verificação**. O risco nomeado em "Consequências" — "a projeção pode divergir do ZIP
real sem que nada quebre" — deixou de valer nessa faixa: agora quebra.

### O que sobrevive, e vale inteiro

- **A Clean continua projeção pura.** Não há template `architecture/clean`, logo não há pacote real
  contra o qual comparar. As 16 combinações de `clean` são o caso em que esta ADR ainda é a única
  coisa de pé.
- **As combinações de `simple` com banco ou autenticação também continuam projeção**, pelo mesmo
  motivo: os fragmentos `database/sqlite`, `database/postgresql`, `auth/identity` e `auth/jwt` ainda
  não existem. A amarração cobre o que tem template, não o eixo inteiro.
- **A exceção a RF-02 continua valendo**, e com ela o confinamento: `PROJECT_STRUCTURE_RULES` segue
  sendo o único arquivo de produção do frontend autorizado a citar valor de opção, e
  `project-structure.spec.ts` segue varrendo os demais. Amarrar a árvore não a moveu para o backend.
- **A frase da tela mudou**, porque a antiga virou falsa no instante em que a amarração passou: ela
  pedia à pessoa uma conferência que a suíte passou a fazer sozinha. O texto vigente está em
  [`../architecture/generated-projects.md`](../architecture/generated-projects.md), seção "A frase
  da tela", e é ele que vale — não o citado em "Decisão", acima, que é registro do que se dizia até
  T03.

### O que T04 precisa fazer para expirar o resto

**Revisto pelo `architect` em T04**, porque a resposta escrita em T03 — "quando `clean` entrar no
contrato, esta ADR fica obsoleta por inteiro" — **estava errada por dois lados**, e os dois importam.

**Errada para menos: `clean` sozinho não bastaria.** Mesmo com o template de Clean escrito, `simple`
e `clean` **com banco ou com autenticação** continuam sem fragmento, logo continuam projeção. A
amarração cobre combinação, não eixo. Contar `clean` como quitação repetiria a conta que esta ADR
errou três vezes.

**Errada para mais: com [ADR-0012](adr-0012-combinacao-sem-template.md) implementada, o resto expira
mesmo assim** — e não por causa do template de Clean, mas por causa da disponibilidade. A tela passa
a **desabilitar todo valor sem fragmento**, então a seleção que o resumo enxerga é sempre uma
combinação **disponível**; e se a lista de combinações amarradas for **derivada da mesma
disponibilidade**, toda combinação que a tela consegue mostrar está amarrada, por construção. O
risco que esta ADR nomeou — *"a projeção pode divergir do ZIP real sem que nada quebre"* — deixa de
existir para tudo que é alcançável.

**As três condições, e nenhuma delas é opcional:**

1. **`ZipStructureContractTests.Tied()` deriva a lista de `TemplateAvailability`**, não a enumera.
   Uma lista escrita à mão voltaria a envelhecer exatamente como envelheceria o catálogo que
   ADR-0012 se recusou a escrever à mão — e envelheceria em silêncio, porque um teste que olha menos
   combinações continua verde. **Derivada, ela cresce sozinha:** o dia em que T05 escrever
   `database/sqlite`, o valor acende, as combinações novas entram na lista, o contrato deixa de bater
   e a suíte cai até alguém regenerá-lo e ajustar a projeção. É o que faz a dívida restante se fechar
   sem ninguém precisar lembrar dela.
   **Os dois nomes de projeto que o teste já percorre continuam**, e por um motivo que T04 tornou
   maior: `Acme.Billing.Api` em `clean` é o que fixa o `.Api` dobrado
   ([`../architecture/generated-projects.md`](../architecture/generated-projects.md)), agora que ele
   é decisão e não pendência. Sem esse par, a regra de nomes da Clean voltaria a valer só do lado do
   documento.
2. **Um teste afirma que os dois conjuntos coincidem**: toda combinação disponível está amarrada e
   toda combinação amarrada está disponível. Sem ele, "derivada" é só uma intenção no código.
3. **A tela desabilita o indisponível** (ADR-0012, decisão 1). Sem isso, a pessoa alcança uma
   combinação sem template, vê uma árvore que nada confere, e a condição 1 não protege nada.

**O que sobrevive a T04, dito com todas as letras**, porque declarar quitado o que não está é o erro
que esta ADR registra ter cometido:

- **A exceção a RF-02 e o confinamento continuam inteiros.** `PROJECT_STRUCTURE_RULES` segue sendo o
  único arquivo de produção do frontend autorizado a citar valor de opção. Isso **não** é o que
  expira: o que expira é o risco de divergência silenciosa. Só a alternativa 2 — o catálogo carregar
  a árvore — encerra a exceção, e ela continua descartada, agora com um argumento a mais: com a
  amarração de pé, o custo que ela evitaria já está pago por teste, e publicar a árvore no contrato
  HTTP continua sendo caro de corrigir.
- **As regras de valores sem fragmento continuam palpite** — `sqlite`, `postgresql`, `identity`,
  `jwt` — e permanecem no módulo. A diferença é que passam a ser palpite **inalcançável**: a tela não
  os mostra, e no instante em que passarem a ser alcançáveis a condição 1 os amarra. Palpite
  inalcançável e verificado ao acender não é dívida; é trabalho ainda não feito, com a trava já
  montada.
- **Esta ADR não fica obsoleta. Ela muda de estado**, de *parcialmente expirada* para **expirada
  quanto ao risco de divergência, com a exceção a RF-02 de pé**. O cabeçalho é atualizado no mesmo
  commit que entregar as três condições — não antes, porque até lá continua valendo o de T03.

**Enquanto as três condições não estiverem no lugar, o `reviewer` deve tratar esta ADR como dívida
viva**, não como permissão — e em particular deve recusar a condição 1 implementada como lista
escrita à mão, que é a forma mais fácil de parecer entregue.
