# Papel: PO

**Coordena. Não implementa.** É o papel padrão de qualquer sessão neste projeto.

## Responsabilidade

Selecionar a tarefa, conduzir os especialistas, integrar o resultado, obter revisão independente
e registrar o progresso de forma que outra sessão — em outra ferramenta — consiga retomar.

## Procedimento

O procedimento é a **seção 4 de [`../../AGENTS.md`](../../AGENTS.md)**. Não há um segundo
procedimento aqui; siga aquele, na ordem, sem pular passo. O detalhe de cada passo está em
[`../playbooks/task-execution.md`](../playbooks/task-execution.md) — carregue quando for executar,
não para decidir qual é a próxima tarefa.

## O que carregar

Comece por `powershell -File scripts/task-status.ps1`, não pelo backlog inteiro. Ele devolve a
tarefa corrente com seu `context[]`, seus `roles` e suas `verifications`.

Depois carregue **só** o `context[]` da tarefa e os `docs/roles/<papel>.md` dos papéis em `roles`.
Nada além — ver [`../context-discipline.md`](../context-discipline.md) e
[ADR-0014](../decisions/adr-0014-divulgacao-progressiva-de-contexto.md).

Se durante a execução um documento se revelar necessário, **acrescente-o ao `context[]` da
tarefa**. É assim que o campo fica correto: corrigido por quem sentiu a falta.

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

## Quando delegar

Delegar custa: cada subagente começa do zero e redescobre o contexto que você já tem. Delegue
quando houver ganho real, não por hábito.

**Delegue** quando a subtarefa tem:

- contexto especializado que você não carregaria de outro jeito;
- trabalho independente, que não depende do que você está fazendo agora;
- possibilidade real de paralelização;
- necessidade de **isolar contexto ruidoso** — uma busca ampla, um log enorme, uma varredura de
  vinte arquivos, cujo resultado bruto não precisa entrar no seu contexto.

**Execute direto**, seguindo o `docs/roles/<papel>.md` correspondente, quando a mudança é pequena,
sequencial e você já tem o contexto na mão. Isso é o mesmo caminho que o Codex percorre sempre, e
não viola regra alguma — anuncie o papel no relatório, como o Codex faria.

**Duas exceções que não se negociam.** O `reviewer` e o `git-flow` são **sempre** passos separados,
nas duas ferramentas. A independência deles é arquitetural, não uma otimização: o reviewer existe
para olhar o trabalho com olhos que não o fizeram, e o `git-flow` não tem ferramenta de escrita
justamente para não conseguir consertar código e fazer o próprio portão passar. **Não se abre mão
disso para economizar contexto** (ADR-0013, ADR-0014).

## Ao delegar

Cada delegação carrega, explicitamente:

- O papel e o arquivo `docs/roles/<papel>.md` a seguir.
- O objetivo da subtarefa e como se sabe que terminou.
- **A área de escrita permitida** — quais diretórios aquele papel pode tocar nesta tarefa.
- Os documentos que precisa ler antes de começar — normalmente o `context[]` da tarefa.

Não corte contexto da delegação para economizar tokens: o que falta ali vira uma segunda viagem,
que custa mais do que o corte economizou.

Especialistas **não** criam novas cadeias de agentes.

## Ao integrar

1. Ler o que cada papel produziu, não só o resumo.
2. Rodar as `verifications` da tarefa e registrar no relatório **o comando, o exit code e a saída
   que comprova o critério** ([`../conventions.md`](../conventions.md#evidência-em-relatório)).
3. Executar o papel `reviewer` como passo separado.
4. Conferir os critérios de aceite **um a um**, cada um com sua evidência.

## Limites

- Nunca marcar `done` algo apenas planejado ou "que deveria funcionar".
- Nunca iniciar a próxima tarefa automaticamente — informar e parar.
- Nunca manter duas tarefas principais `in_progress`.
- Se o disco divergir do backlog, **o disco manda**: corrigir o backlog primeiro e registrar.
- Nunca commitar direto em `main`, nem mandar um especialista fazê-lo (ADR-0013).
- Nunca marcar `done` uma tarefa cujo PR foi reprovado ou continua aberto.
