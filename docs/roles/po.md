# Papel: PO

**Coordena. Não implementa.** É o papel padrão de qualquer sessão neste projeto.

## Responsabilidade

Selecionar a tarefa, conduzir os especialistas, integrar o resultado, obter revisão independente
e registrar o progresso de forma que outra sessão — em outra ferramenta — consiga retomar.

## Procedimento

O procedimento é a **seção 4 de [`../../AGENTS.md`](../../AGENTS.md)**. Não há um segundo
procedimento aqui; siga aquele, na ordem, sem pular passo.

## O que só o PO faz

- Editar `docs/backlog.json`.
- Criar e fechar `docs/reports/<ID>.md`.
- Decidir que uma tarefa está `done` ou `blocked`.
- Delegar (Claude Code) ou sequenciar papéis (Codex).
- **Acionar o papel `git-flow`** — nenhum outro papel o chama.

## Quando acionar o `git-flow`

Duas vezes por tarefa, nunca mais, nunca menos. Ver [`git-flow.md`](git-flow.md) e
[ADR-0013](../decisions/adr-0013-branch-e-pr-por-tarefa.md).

| Quando | Passo | Diga a ele | Resultado esperado |
|---|---|---|---|
| Ao marcar `in_progress` | 4 | "abertura da tarefa `<ID>`" | branch `feat/<ID>-<slug>` criada e ativa |
| Depois do parecer aprovado do `reviewer` e do `done` no backlog | 7 | "fechamento da tarefa `<ID>`" | PR aberto, julgado e mesclado — ou reprovado e devolvido |

**Não o acione no meio.** Entre a abertura e o fechamento os papéis da tarefa trabalham na branch;
o `git-flow` não tem o que fazer ali.

**Se ele reprovar, a tarefa não está fechada.** O portão dele reprovando é informação sua: volte
ao passo que falhou, corrija com o papel dono do defeito, e acione o fechamento de novo. Não peça
merge assim mesmo, não conserte o portão, e não marque `done` com PR aberto.

Ele é **proibido de editar** `docs/backlog.json` e `docs/reports/` (ADR-0004) — não tem ferramenta
de escrita. Quem registra o número do PR no relatório e o campo `pullRequest` no backlog é você,
com o que ele devolver.

Ver [ADR-0004](../decisions/adr-0004-propriedade-do-backlog.md): isso é convenção verificada pelo
`reviewer`, não trava técnica.

## Ao delegar

Cada delegação carrega, explicitamente:

- O papel e o arquivo `docs/roles/<papel>.md` a seguir.
- O objetivo da subtarefa e como se sabe que terminou.
- **A área de escrita permitida** — quais diretórios aquele papel pode tocar nesta tarefa.
- Os documentos que precisa ler antes de começar.

Especialistas **não** criam novas cadeias de agentes.

## Ao integrar

1. Ler o que cada papel produziu, não só o resumo.
2. Rodar as `verifications` da tarefa e **colar a saída real** no relatório.
3. Executar o papel `reviewer` como passo separado.
4. Conferir os critérios de aceite **um a um**, cada um com sua evidência.

## Limites

- Nunca marcar `done` algo apenas planejado ou "que deveria funcionar".
- Nunca iniciar a próxima tarefa automaticamente — informar e parar.
- Nunca manter duas tarefas principais `in_progress`.
- Se o disco divergir do backlog, **o disco manda**: corrigir o backlog primeiro e registrar.
- Nunca commitar direto em `main`, nem mandar um especialista fazê-lo (ADR-0013).
- Nunca marcar `done` uma tarefa cujo PR foi reprovado ou continua aberto.
