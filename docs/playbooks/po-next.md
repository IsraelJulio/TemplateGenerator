# Playbook: po-next

Selecionar, executar, retomar e fechar uma tarefa. É o procedimento invocado por
*"PO, execute a próxima tarefa"* — e por `/po-next` no Claude Code.

O procedimento canônico é a **seção 4 de [`../../AGENTS.md`](../../AGENTS.md)**. Este documento
detalha os pontos onde se costuma errar.

## Passo 2 — inspecionar a realidade

```bash
git status
git log --oneline -10
```

Compare com `docs/backlog.json`. **O disco manda.** Cenários e o que fazer:

| Situação | Ação |
|---|---|
| Backlog diz `in_progress`, disco não tem nada | Voltar a tarefa para `pending`, registrar no relatório |
| Backlog diz `pending`, disco tem trabalho feito | Marcar `in_progress` e **retomar**, nunca recomeçar do zero |
| Backlog diz `done`, verificação não passa mais | Reabrir como `blocked` com o motivo |
| Há mudanças não commitadas de origem desconhecida | **Ler antes de tocar.** Nunca descartar trabalho existente |

## Passo 3 — selecionar

```
se existe tarefa in_progress → retomar ELA
senão → primeira pending, em ordem, com todas as dependsOn em done
senão → parar e explicar o que bloqueia
```

Nunca duas `in_progress`. Nunca pular a ordem porque outra tarefa parece mais fácil.

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

Atualize `state`, `finishedAt` e `report`. Commit.

## Passo 8 — parar

Informe o que terminou e qual é a próxima. **Não comece a próxima.**

## Quando bloquear

Estado `blocked` com motivo em `blockers` quando: falta decisão que não é sua, uma dependência
externa não está disponível, ou um critério de aceite se revelou impossível como escrito.

Bloquear é um resultado legítimo. Marcar `done` sem cumprir não é.
