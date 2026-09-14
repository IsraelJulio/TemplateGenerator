# Playbook: execução de uma tarefa

O procedimento detalhado dos oito passos resumidos na **seção 4 de
[`../../AGENTS.md`](../../AGENTS.md)**. Aquela seção é o contrato; esta é a letra miúda.

Carregue este documento quando for **executar** uma tarefa. Para saber apenas *qual* é a próxima
tarefa, `AGENTS.md` §4 basta.

Regra de contexto que atravessa todos os passos: **carregue o que o passo atual precisa, quando
precisa.** Ver [`../context-discipline.md`](../context-discipline.md).

---

## Passo 1 — ler o estado

```powershell
powershell -File scripts/task-status.ps1
```

O script responde qual é a tarefa corrente, em que estado ela está, quais papéis ela pede, o que
está no `context[]` dela e quais verificações ela exige. Ele **não decide nada** — só lê o
`docs/backlog.json` e a branch atual.

Se a tarefa já estiver `in_progress`, leia também `docs/reports/<ID>.md`: é o que a sessão
anterior deixou. **Não releia o que já está no relatório** — ele existe justamente para você não
ter de redescobrir.

## Passo 2 — inspecionar a realidade

```bash
git status
git log --oneline -10
git branch --show-current
```

Compare com o backlog. **O disco manda.** Cenários e o que fazer:

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

O `in_progress`, o relatório e a branch nascem **no mesmo ato**.

```powershell
powershell -File scripts/task-start.ps1 -Id <ID>
```

O script marca `in_progress`, preenche `startedAt`, cria `docs/reports/<ID>.md` a partir do
template e imprime o nome de branch a usar. Ele **não cria a branch** — isso é do papel `git-flow`,
que você aciona com "abertura da tarefa `<ID>`".

Confirme antes de seguir:

```bash
git branch --show-current    # feat/<ID>-… ou fix/<ID>-…, nunca main
```

O erro que custa caro é abrir a branch depois: os commits nascem em `main` e o PR vira
`cherry-pick` manual.

## Passo 5 — carregar o contexto da tarefa e executar

Nesta ordem, e **só até onde precisar**:

1. O `context[]` da tarefa — os documentos que ela declara necessários. Se estiver vazio, a tarefa
   não precisa de documento de domínio: não saia procurando.
2. `docs/roles/<papel>.md` para **cada papel em `roles`** — e só esses.
3. O código relevante, achado por busca antes de leitura (ver
   [`../context-discipline.md`](../context-discipline.md)).

**Não carregue `docs/architecture/` inteiro, nem todas as ADR, "para ter contexto".** Se um
documento não está no `context[]` e não foi apontado por um role, ele provavelmente não é
necessário — e se for, acrescente-o ao `context[]` da tarefa, para a próxima sessão já saber.

**Delegar ou executar direto** — ver [`../roles/po.md`](../roles/po.md), seção "Quando delegar".
O `reviewer` é sempre passo separado, nas duas ferramentas.

**Codex:** antes de cada bloco, anuncie no relatório *"assumindo papel `backend`, conforme
docs/roles/backend.md"*. Isso dá o mesmo rastro que a delegação daria.

## Passo 6 — verificar

Rode **todas** as `verifications` da tarefa. Para os comandos padrão do repositório:

```powershell
powershell -File scripts/task-verify.ps1
```

Ele roda build e testes, guarda a saída **completa** em arquivo e devolve ao terminal o resumo, os
erros e o exit code. O log completo é a evidência; o resumo é o que entra no contexto.

Cole no relatório o comando, o **exit code** e o trecho que comprova o critério. Regra de evidência
em [`../conventions.md`](../conventions.md#evidência-em-relatório). Se um comando falhou, o
relatório mostra a falha; não rode de novo até passar sem explicar o que mudou.

Depois, e só depois, execute o papel `reviewer`.

## Passo 7 — fechar

Antes de marcar `done`, percorra `acceptanceCriteria` item a item e escreva, para cada um, **qual
evidência do relatório o comprova**. Um critério sem evidência apontável impede o fechamento.

```powershell
powershell -File scripts/task-finish.ps1 -Id <ID> -PullRequest <URL>
```

O script recusa fechar se o relatório não tiver as seções obrigatórias ou se faltar evidência de
verificação. Ele não julga o mérito do critério — isso é seu.

Commit **na branch da tarefa**. Só então acione o `git-flow` com "fechamento da tarefa `<ID>`":
ele abre o PR, percorre o portão de sete itens e mescla — ou reprova.

**Reprovação não é formalidade a contornar.** Se ele devolver MUDANÇAS SOLICITADAS, a tarefa não
está fechada: volte ao passo que falhou, corrija com o papel dono do defeito, e peça o fechamento
de novo. O portão é reexecutado inteiro, não só o item que falhou.

Com o merge feito, registre o número e a URL do PR no relatório e no campo `pullRequest`.

## Passo 8 — informar e parar

Diga o que terminou, **com o link do PR**, e qual é a próxima. **Não comece a próxima.**

Ao encerrar, todo o estado precisa estar no repositório: backlog, relatório, commits, PR. A próxima
sessão começa lendo o disco, nunca o histórico desta conversa — ver
[`../conventions.md`](../conventions.md#uma-tarefa-uma-sessão).

---

## Proibições

- Não marque `done` nada que esteja apenas planejado, escrito ou "deveria funcionar".
- Não pule o passo 6 porque "é óbvio que funciona".
- Não invente critérios de aceite nem remova critérios que não conseguiu cumprir — se não deu,
  o estado é `blocked`, com o motivo em `blockers`.
- Não reescreva trabalho existente sem ler antes o que já está lá.
- Não adicione dependência paga, com licença restritiva, ou que exija container
  ([ADR-0005](../decisions/adr-0005-sem-containers.md)).

## Quando bloquear

Estado `blocked` com motivo em `blockers` quando: falta decisão que não é sua, uma dependência
externa não está disponível, ou um critério de aceite se revelou impossível como escrito.

Bloquear é um resultado legítimo. Marcar `done` sem cumprir não é.

## Quem escreve o backlog

Por convenção, **só o papel PO edita `docs/backlog.json` e `docs/reports/`**
([ADR-0004](../decisions/adr-0004-propriedade-do-backlog.md)). Isso é convenção, não trava
técnica — o `reviewer` verifica o cumprimento. Se você está atuando como outro papel e precisa
mudar o backlog, volte ao papel PO explicitamente antes de editar.
