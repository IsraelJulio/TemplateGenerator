# Playbook: git-flow

Os comandos exatos de branch, pull request e merge. A responsabilidade e os limites do papel estão
em [`../roles/git-flow.md`](../roles/git-flow.md); a decisão e seus porquês, em
[ADR-0013](../decisions/adr-0013-branch-e-pr-por-tarefa.md).

Invocado **só pelo PO**, em dois momentos: abertura (passo 4 de `AGENTS.md`) e fechamento
(passo 7). No Claude Code, `/git-flow`.

## Pré-requisitos, uma vez por máquina

```bash
gh --version      # 2.100.0, instalado por winget em %LOCALAPPDATA%\Microsoft\WinGet\Links
gh auth status    # precisa dizer "Logged in to github.com"
```

Se não estiver autenticado, **este é o único passo humano do fluxo inteiro**:

```bash
gh auth login --hostname github.com --git-protocol https --web
```

Feito uma vez, o token fica no gerenciador de credenciais e nenhum PR pede ação humana de novo.

## Nome da branch

```
<tipo>/<ID>-<slug>
```

- `<tipo>`: `feat` para tarefa nova, `fix` para tarefa de correção (o título começa com
  "Corrigir").
- `<ID>`: exatamente como no backlog, maiúsculas preservadas — `T05`, `T09b`.
- `<slug>`: título em minúsculas, sem acento, separado por hífen, até ~50 caracteres.

```
feat/T05-sqlite-postgresql-e-migracoes
feat/T06-autenticacao-identity-nativa
fix/T09b-corrida-entre-troca-de-opcao-e-leitura-da-arvore
```

### Mudança que não é tarefa do backlog

Alteração do próprio manual de operação — `AGENTS.md`, `docs/`, `.claude/`, `.codex/` — não tem ID
de tarefa. Nome: `chore/<slug>`, por exemplo `chore/adr-0013-fluxo-de-branch-e-pr`.

**Ela também passa por PR.** O portão muda só nos quatro primeiros itens, que falam de um
relatório que não existe aqui; eles são substituídos por **um**:

> **0.** A decisão está registrada como ADR em `docs/decisions/`, e o checklist de paridade de
> `docs/interop.md` passa inteiro.

Os itens **5, 6 e 7 valem sem alteração** — diff limpo, commits com prefixo coerente, merge limpo.
O prefixo do commit passa a ser o slug (`adr-0013: …`), já que não há ID.

Isto **não é uma porta dos fundos**: mudança de código de produto nunca é `chore/`. Se o diff toca
`src/` ou `tests/`, é tarefa do backlog e o portão de sete itens vale inteiro.

## Abertura — passo 4 de AGENTS.md

Acontece **junto** com a marcação de `in_progress`, antes de qualquer trabalho da tarefa.

```bash
git status --short                    # tem de estar limpo; se não estiver, pare e leia o que há
git checkout main
git pull --ff-only origin main        # se falhar, main divergiu: resolva antes, não force
git checkout -b feat/<ID>-<slug>
git branch --show-current             # confirme que saiu de main
```

A partir daqui todos os papéis da tarefa trabalham nesta branch. Os commits seguem [`../conventions.md`](../conventions.md#commits):
`<ID>: <resumo no imperativo>`, um por entrega coerente — **não** acumule a tarefa inteira em um
commit só. O PR preserva todos eles.

## Fechamento — passo 7 de AGENTS.md

Só depois de o `reviewer` ter aprovado e de o PO ter marcado `done` no backlog.

### 1. Empurrar a branch

```bash
git push -u origin feat/<ID>-<slug>
```

### 2. Abrir o PR

O corpo do PR é o resumo navegável da tarefa. Escreva-o em um arquivo temporário **fora do
repositório** (o diretório de scratch da sessão), nunca versionado:

```bash
gh pr create \
  --base main \
  --head feat/<ID>-<slug> \
  --title "<ID>: <título da tarefa, como está no backlog>" \
  --body-file <scratch>/pr-<ID>.md
```

Corpo mínimo:

```markdown
## Objetivo
<copiado de docs/backlog.json>

## O que mudou
<uma linha por entrega coerente, na ordem dos commits>

## Critérios de aceite
<a tabela do relatório: critério | evidência | ✅>

## Verificações executadas
<os comandos de `verifications`, com o exit code — a evidência detalhada fica no relatório>

## Relatório
docs/reports/<ID>.md
```

Guarde o número que o comando devolve.

### 3. Julgar — o portão

Percorra os **sete itens** de [`../roles/git-flow.md`](../roles/git-flow.md#o-portão), com o diff
na frente:

```bash
gh pr diff <n> --name-only     # item 5: nada de lixo, nada fora do escopo
gh pr view <n> --json commits --jq '.commits[].messageHeadline'   # item 6: todo commit com <ID>:
gh pr view <n> --json mergeable --jq .mergeable                   # item 7: MERGEABLE
```

### 4a. Aprovado

Lembre que `gh pr review --approve` **falha no próprio PR** (HTTP 422). O veredito vai como
comentário, e o merge é o portão real:

```bash
gh pr comment <n> --body "$(cat <<'EOF'
## Parecer git-flow: APROVADO

| # | Item do portão | Evidência |
|---|---|---|
| 1 | Saída real para cada `verifications` | relatório, seção Verificações |
| 2 | Cada critério com evidência apontável | relatório, tabela de critérios |
| 3 | Parecer do reviewer: aprovado | relatório, seção Parecer do reviewer |
| 4 | Backlog em `done`, com `finishedAt` e `report` | docs/backlog.json |
| 5 | Diff sem segredo, temporário ou arquivo fora do escopo | `gh pr diff --name-only` |
| 6 | Todo commit prefixado com o ID | `gh pr view --json commits` |
| 7 | Merge limpo em main | `mergeable: MERGEABLE` |

Aprovado por auto-revisão do agente `git-flow`, conforme ADR-0013. Não há approval nativo do
GitHub: o autor não pode aprovar o próprio PR. **O merge é o registro da aprovação.**
EOF
)"

gh pr merge <n> --merge --delete-branch
```

`--merge`, **não** `--squash`: o squash apagaria os commits individuais que [`../conventions.md`](../conventions.md#commits) exige, e
com eles a granularidade que motivou a decisão. Com merge commit, `git log --first-parent main` lê
uma linha por tarefa e `git log feat/<ID>...` continua mostrando o detalhe.

Depois do merge, volte para `main` e sincronize:

```bash
git checkout main
git pull --ff-only origin main
git log --first-parent --oneline -5
```

Informe ao PO o número e a URL do PR, para ele registrar no relatório e no backlog.

### 4b. Reprovado

```bash
gh pr comment <n> --body "$(cat <<'EOF'
## Parecer git-flow: MUDANÇAS SOLICITADAS

Itens do portão não cumpridos:

1. **<item n>** — <o que falta, concretamente>
   Destrava com: <a ação específica>

<repetir por item>

PR mantido aberto. Devolvido ao PO. Sem merge até que todos os itens passem.
EOF
)"
```

**Não faça merge.** Não conserte o que encontrou — devolva ao papel dono. Quando o defeito for
corrigido, os commits novos entram na mesma branch, o PR se atualiza sozinho, e você reexecuta o
portão inteiro do item 1 — não só o que falhou.

## Onde se erra

| Erro | Por que importa |
|---|---|
| Criar a branch depois de já ter trabalhado em `main` | os commits nascem no lugar errado e o PR vira um `cherry-pick` manual. A branch abre **junto** com o `in_progress`. |
| `--squash` no merge | apaga a granularidade de commit que `conventions.md` exige e ADR-0013 protege. |
| Aprovar porque "eu mesmo fiz e sei que está certo" | é a auto-aprovação sem portão. O veredito se apoia na saída de comando, não na memória da sessão. |
| Reexecutar só o item que falhou | os commits de correção podem ter quebrado outro item. O portão é de sete itens, sempre. |
| Cair para commit em `main` quando o `gh` falha | destrói exatamente a propriedade que a decisão comprou. Falha de `gh` é bloqueio, não atalho. |
| Deixar a branch viva depois do merge | `--delete-branch` limpa local e remoto. O PR sobrevive no GitHub e é ele o registro permanente. |

## Quando bloquear

- `gh auth status` não autenticado e não há como pedir ao humano agora.
- O remoto está inacessível.
- `main` divergiu do remoto e a reconciliação exige decisão que não é sua.

Em qualquer um: **pare, reporte ao PO e deixe a branch como está.** O trabalho não se perde — ele
fica na branch até o caminho abrir.
