---
name: po-next
description: Selecionar, executar, retomar e fechar uma tarefa do backlog do TemplateGenerator. Use para "PO, execute a proxima tarefa", "continue o projeto", "retome de onde parou" ou ao fechar uma tarefa em andamento.
---

# po-next

O procedimento canonico e a **secao 4 de [`AGENTS.md`](../../../AGENTS.md)**; o detalhe esta em
[`docs/playbooks/task-execution.md`](../../../docs/playbooks/task-execution.md).

**Carregue o minimo, na ordem, e pare quando tiver o suficiente** (ADR-0014). Nao leia o backlog
inteiro; nao abra `docs/architecture/` "para ter contexto".

Comece por:

```powershell
powershell -File scripts/task-status.ps1
```

Ele devolve a tarefa corrente com `context[]`, `roles` e `verifications` — **isso substitui ler
`docs/backlog.json`**. Depois carregue so o `context[]` da tarefa, os `docs/roles/<papel>.md` dos
papeis em `roles`, e o relatorio se a tarefa ja estiver `in_progress`.

Leia, conforme o passo em que estiver:

1. [`docs/playbooks/task-execution.md`](../../../docs/playbooks/task-execution.md) — os oito
   passos em detalhe, com os comandos de `scripts/` e as armadilhas de cada um
2. [`docs/context-discipline.md`](../../../docs/context-discipline.md) — o que carregar e o que nao
3. [`docs/roles/po.md`](../../../docs/roles/po.md) — limites do papel e quando delegar

Regras que nao se negociam, e que valem mesmo que voce nao abra nenhum dos arquivos acima: uma
tarefa `in_progress` por vez; o disco manda sobre o backlog; `reviewer` e `git-flow` sao sempre
passos separados; nada vira `done` sem evidencia de execucao no relatorio; informe e pare.

Esta skill nao contem regra propria. Se voce sentir vontade de escrever uma regra aqui, ela
pertence a `docs/`.
