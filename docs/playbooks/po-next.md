# Playbook: po-next

Selecionar, executar, retomar e fechar uma tarefa. É o procedimento invocado por
*"PO, execute a próxima tarefa"* — e por `/po-next` no Claude Code.

O procedimento canônico é a **seção 4 de [`../../AGENTS.md`](../../AGENTS.md)**. Este documento
detalha os pontos onde se costuma errar.

## Passo 2 — inspecionar a realidade

```bash
git status
git log --oneline -10
git branch --show-current
git branch --list 'feat/*' 'fix/*'
```

Compare com `docs/backlog.json`. **O disco manda.** Cenários e o que fazer:

| Situação | Ação |
|---|---|
| Backlog diz `in_progress`, disco não tem nada | Voltar a tarefa para `pending`, registrar no relatório |
| Backlog diz `pending`, disco tem trabalho feito | Marcar `in_progress` e **retomar**, nunca recomeçar do zero |
| Backlog diz `done`, verificação não passa mais | Reabrir como `blocked` com o motivo |
| Há mudanças não commitadas de origem desconhecida | **Ler antes de tocar.** Nunca descartar trabalho existente |
| Você está numa branch `feat/<ID>-…` e o backlog diz `pending` | A branch é o disco falando: marcar `in_progress` e retomar (ADR-0013) |
| Existe branch de tarefa que o backlog diz `done` | O PR não foi mesclado. Conferir com `gh pr list`; a tarefa **não** estava fechada |
| Você está em `main` com trabalho de tarefa não commitado | A branch de abertura não foi criada. Criar agora e mover o trabalho — não commitar em `main` |

## Passo 3 — selecionar

```
se existe tarefa in_progress → retomar ELA
senão → primeira pending, em ordem, com todas as dependsOn em done
senão → parar e explicar o que bloqueia
```

Nunca duas `in_progress`. Nunca pular a ordem porque outra tarefa parece mais fácil.

## Passo 4 — registrar o início e abrir a branch

O `in_progress`, o relatório e a branch nascem **no mesmo ato**. Acione o `git-flow` com
"abertura da tarefa `<ID>`" e confirme antes de seguir:

```bash
git branch --show-current    # tem de ser feat/<ID>-… ou fix/<ID>-…, nunca main
```

O erro que custa caro é abrir a branch depois: os commits nascem em `main` e o PR vira
`cherry-pick` manual. Se você já trabalhou em `main` sem perceber, pare e corrija a posição antes
de commitar qualquer coisa.

## Passo 5 — executar por papéis

**Claude Code:** delegar a cada agente, passando papel, objetivo, área de escrita e leitura
obrigatória.

**Codex:** assumir os papéis em sequência, na mesma ordem. Antes de cada bloco, anunciar no
relatório: *"assumindo papel `backend`, conforme docs/roles/backend.md"*. Isso dá o mesmo rastro
que a delegação daria.

O resultado esperado é idêntico nos dois casos.

## Passo 6 — verificar

Rode **todas** as `verifications` da tarefa. Cole a saída — completa, não resumida, não
parafraseada. Se um comando falhou, o relatório mostra a falha; não rode de novo até passar sem
explicar o que mudou.

Depois, e só depois, execute o papel `reviewer`.

## Passo 7 — fechar

Antes de marcar `done`, percorra `acceptanceCriteria` item a item e escreva, para cada um, **qual
evidência do relatório o comprova**. Um critério sem evidência apontável impede o fechamento.

Atualize `state`, `finishedAt` e `report`. Commit **na branch da tarefa**.

Só então acione o `git-flow` com "fechamento da tarefa `<ID>`". Ele abre o PR, percorre o portão
de sete itens e mescla — ou reprova.

**Reprovação não é formalidade a contornar.** Se ele devolver MUDANÇAS SOLICITADAS, a tarefa não
está fechada: volte ao passo que falhou, corrija com o papel dono do defeito, e peça o fechamento
de novo. O portão é reexecutado inteiro, não só o item que falhou — os commits de correção podem
ter quebrado outro.

Com o merge feito, registre no relatório e no campo `pullRequest` da tarefa o número e a URL do PR.

## Passo 8 — parar

Informe o que terminou, **com o link do PR**, e qual é a próxima. **Não comece a próxima.**

## Quando bloquear

Estado `blocked` com motivo em `blockers` quando: falta decisão que não é sua, uma dependência
externa não está disponível, ou um critério de aceite se revelou impossível como escrito.

Bloquear é um resultado legítimo. Marcar `done` sem cumprir não é.
