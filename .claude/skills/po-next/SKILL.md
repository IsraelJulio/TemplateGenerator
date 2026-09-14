---
name: po-next
description: Selecionar, executar, retomar e fechar uma tarefa do backlog do TemplateGenerator. Use para "PO, execute a proxima tarefa", "continue o projeto", "retome de onde parou" ou ao fechar uma tarefa em andamento.
---

# po-next

Ponto de entrada da execucao de tarefas. **Carregue o minimo, na ordem, e pare quando tiver o
suficiente** — ADR-0014.

Nao leia o backlog inteiro. Nao abra `docs/architecture/` "para ter contexto". O passo 1 diz o que
a tarefa precisa; carregue so isso.

## 1. Descobrir a tarefa

```powershell
powershell -File scripts/task-status.ps1
```

Devolve a tarefa corrente com estado, `roles`, `context[]`, `verifications`, a posicao do git e a
validacao do backlog. **Esta saida substitui a leitura de `docs/backlog.json`.**

Confira o disco contra o que ele disse (`git status`, `git log --oneline -10`). **O disco manda.**

## 2. Carregar so o necessario

Nesta ordem, e so ate onde precisar:

1. O `context[]` da tarefa — os documentos que ela declara. Vazio significa que ela nao depende de
   documento de dominio: nao saia procurando.
2. `docs/roles/<papel>.md` de **cada papel em `roles`** — e so esses.
3. `docs/reports/<ID>.md`, **se** a tarefa ja estiver `in_progress`. E o que a sessao anterior
   deixou; nao redescubra o que esta la.
4. O codigo, achado por busca antes de leitura.

## 3. Abrir, se ainda nao estiver aberta

```powershell
powershell -File scripts/task-start.ps1 -Id <ID>
```

Marca `in_progress`, carimba `startedAt`, cria o relatorio. Depois acione o papel `git-flow` com
**"abertura da tarefa `<ID>`"** para criar a branch. Nenhum trabalho acontece em `main`.

## 4. Executar

Delegue quando houver ganho real; execute direto quando a mudanca for pequena e sequencial — a
regra esta em [`docs/roles/po.md`](../../../docs/roles/po.md#quando-delegar).

## 5. Verificar

```powershell
powershell -File scripts/task-verify.ps1
```

Roda os comandos padrao, guarda o log completo em arquivo e devolve resumo, erros e exit code.
Verificacao especifica da tarefa continua sendo trabalho seu.

Registre no relatorio: comando, exit code e o trecho que comprova o criterio — nao o log inteiro.

## 6. Reviewer

Passo **separado**, sempre, depois das verificacoes. Nao acumule com a implementacao e nao pule
para economizar contexto.

## 7. Fechar

```powershell
powershell -File scripts/task-finish.ps1 -Id <ID> -PullRequest <URL>
```

Confere a estrutura do relatorio e marca `done`. Depois: commit na branch, e acione o `git-flow`
com **"fechamento da tarefa `<ID>`"**. Portao reprovado significa tarefa **nao** fechada.

## 8. Parar

Informe o que terminou, com o link do PR, e qual e a proxima. **Nao comece a proxima.**

Tudo que a proxima sessao precisa tem de estar no repositorio — backlog, relatorio, commits, PR.
O historico desta conversa nao e estado.

---

**Recursos, se precisar de mais que o acima:**

- [`docs/playbooks/task-execution.md`](../../../docs/playbooks/task-execution.md) — cenarios,
  armadilhas e proibicoes de cada passo
- [`docs/context-discipline.md`](../../../docs/context-discipline.md) — o que carregar e o que nao
- [`docs/roles/po.md`](../../../docs/roles/po.md) — limites do papel
- `AGENTS.md` secao 4 — o contrato

Esta skill nao contem regra propria. Se voce sentir vontade de escrever uma regra aqui, ela
pertence a `docs/`.
