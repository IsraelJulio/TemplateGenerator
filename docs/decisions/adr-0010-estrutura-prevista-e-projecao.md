# ADR-0010 — A "estrutura prevista" é projeção do frontend, não dado do backend

**Data:** 2026-09-12 · **Estado:** aceita · **Levantada por:** papel `po` em T02, registrada pelo
`architect` na mesma tarefa

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
