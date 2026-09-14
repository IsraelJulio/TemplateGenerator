# ADR-0013 — Uma branch e um pull request por tarefa do backlog

**Data:** 2026-09-14 · **Estado:** aceita

## Contexto

Até T04 o projeto trabalhou direto em `main`, e `AGENTS.md` §8 autorizava explicitamente:
*"Trabalho direto em main é aceitável neste projeto de uma pessoa"*.

O custo apareceu quando o histórico cresceu. Os 13 commits de T01 a T04 formam uma linha única;
responder *"o que exatamente T04 mudou?"* exige ler mensagem por mensagem e confiar que o prefixo
do ID está correto em todas — uma convenção que nada verifica. O parecer do `reviewer` só existe
dentro do relatório, e o relatório não é alcançável a partir do histórico: quem chega pelo
`git log` não sabe que ele existe.

**O pedido é rastreabilidade, não controle de acesso.** Não há segunda pessoa para aprovar nada, e
introduzir espera humana no fluxo seria um custo sem contrapartida — a pessoa que aprovaria é a
mesma que pediu o trabalho.

Dois fatos do ambiente moldaram a decisão:

- `main` estava **13 commits à frente de `origin/main`**: nada havia sido empurrado desde T00, e o
  remoto `github.com/IsraelJulio/TemplateGenerator` estava parado.
- O GitHub **recusa que o autor aprove o próprio pull request** —
  `gh pr review --approve` responde HTTP 422 `Can not approve your own pull request`. Com conta
  única, não existe approval nativo possível.

## Decisão

**Toda tarefa do backlog nasce em uma branch própria e entra em `main` por um pull request
julgado por um portão explícito.** Criamos o papel
[`git-flow`](../roles/git-flow.md), acionado **só pelo PO**, em dois momentos da seção 4 de
`AGENTS.md`: abre a branch no passo 4, abre e julga o PR no passo 7.

Cinco escolhas concretas:

1. **A unidade é a tarefa, não o bloco de papel.** Um PR ↔ uma tarefa ↔ um relatório ↔ um conjunto
   de `acceptanceCriteria`. Branch por papel multiplicaria PRs e quebraria essa correspondência de
   um para um, que é o que torna o PR legível.
2. **Merge commit, nunca squash.** O squash apagaria os commits individuais que §8 exige ("um
   commit por entrega coerente; não acumule a tarefa inteira em um commit só"). Com merge commit,
   `git log --first-parent main` lê **uma linha por tarefa** e o detalhe continua acessível —
   ganha-se a visão de topo sem perder a de baixo.
3. **O veredito é um comentário estruturado; o merge é a aprovação.** Como o approval nativo é
   impossível, PR mesclado = aprovado, PR aberto com comentário de mudanças = reprovado. O estado
   do PR é o registro, e ele não depende de nenhum campo que o GitHub nos negue.
4. **O portão tem sete itens verificáveis e recusa por omissão.** Item não verificado conta como
   não cumprido. Os itens estão em [`../roles/git-flow.md`](../roles/git-flow.md#o-portão) e são
   todos respondíveis por saída de comando ou citação do relatório — nenhum depende de juízo sobre
   o desenho da solução.
5. **`git-flow` não substitui o `reviewer`.** O `reviewer` pergunta *"isto está certo, seguro e
   licenciado?"* lendo o código, no passo 6. O `git-flow` pergunta *"isto deixou rastro
   verificável?"* lendo o relatório, no passo 7. Um roda depois do outro e o parecer do primeiro é
   entrada do segundo.

## Alternativas descartadas

- **Manter tudo em `main`.** É o estado que gerou o problema. O histórico continua legível só para
  quem o escreveu.
- **Squash merge.** Contradiz diretamente a convenção de commits de §8. Um PR por tarefa com um
  commit só teria o mesmo poder de busca do que já existe hoje.
- **Branch por bloco de papel** (`feat/T05-template-engineer`, `feat/T05-qa`). Rastro mais fino,
  mas os `acceptanceCriteria` e o relatório são da tarefa inteira: cada PR chegaria ao portão sem
  ter como cumprir os itens 1 a 4.
- **Proteção de branch exigindo aprovação em `main`.** Trancaria o repositório de vez: a única
  conta existente não pode aprovar os próprios PRs, e nenhum merge passaria. É o caminho que
  parece mais rigoroso e na prática obriga a desligar a proteção ou a usar `--admin` em todo
  merge, o que a esvazia.
- **Segunda conta ou token de terceiro só para aprovar.** Fabricaria um segundo revisor que não
  existe. O parecer seria do mesmo agente com outro crachá — teatro de processo, e pior que
  assumir a auto-revisão às claras.
- **"PR" local em `docs/pull-requests/<ID>.md`, sem GitHub.** Funcionaria offline, mas o rastro só
  se lê dentro do repositório. O pedido era achar a feature depois, e o PR no GitHub é navegável,
  pesquisável e sobrevive à remoção da branch.

## Consequências

- **`gh` vira dependência do processo, não do produto.** Não entra em `.csproj`, `package.json`,
  `Directory.Packages.props` nem em ZIP gerado algum. É ferramenta de desenvolvimento, como `git`
  e `dotnet-ef`, e está registrada como tal em `AGENTS.md` §7. Nada em RNF-06, ADR-0005 ou na
  auditoria de licenças é afetado.
- **Um único passo humano, uma vez por máquina:** `gh auth login`. Depois dele, nenhum PR pede
  ação humana.
- **Falha de `gh` ou do remoto é bloqueio, não atalho.** O papel é proibido de "commitar em main
  desta vez". O trabalho fica na branch até o caminho abrir — é a única forma de a decisão não se
  erodir na primeira sexta-feira difícil.
- **A auto-revisão é assumida, não disfarçada.** Quem lê o PR meses depois vê no comentário que o
  aprovador foi o agente, com quais evidências, e que o GitHub não emitiu approval. Isso é mais
  honesto do que um approval verde vindo de uma conta de fachada.
- **Tarefas fechadas antes desta ADR (T00 a T04) não têm PR.** A ausência do campo `pullRequest`
  no backlog significa "anterior a ADR-0013"; não vamos reescrever o histórico para fabricar PRs
  que não existiram.
- **`main` precisou ser empurrado uma vez** para servir de base aos PRs: sem isso, o primeiro PR
  traria os 13 commits de T01 a T04 junto com a tarefa nova.
