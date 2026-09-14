# ADR-0014 — Divulgação progressiva de contexto

- **Estado:** aceita
- **Data:** 2026-09-14
- **Decide sobre:** o que um agente carrega para executar uma tarefa, e quem decide isso

## Contexto

O manual de operação cresceu junto com o projeto. Antes desta decisão, uma sessão que recebia
*"PO, execute a próxima tarefa"* carregava, **antes de saber qual era a tarefa**:

| Arquivo | Linhas |
|---|---|
| `.claude/agents/po.md` (ou o prompt equivalente no Codex) | 20 |
| `AGENTS.md` inteiro | 150 |
| `docs/playbooks/po-next.md` | 98 |
| `docs/backlog.json` inteiro — 13 tarefas, todos os critérios de aceite | 384 |
| `docs/roles/po.md` | 72 |
| **Total** | **724 linhas, ~44 KB** |

Disso, o que a tarefa realmente exigia era uma fração: o backlog carregava os critérios de aceite
das treze tarefas para executar uma; `AGENTS.md` carregava a tabela de versões do ambiente e as
convenções de commit em toda tarefa, inclusive nas que não commitam nada de especial.

Três consequências, em ordem de gravidade:

1. **Correção.** Contexto não é armazenamento, é orçamento de atenção. Cada token compete com
   todos os outros. Uma sessão que carregou 44 KB de documento meio-relevante raciocina pior sobre
   os 4 KB que importavam. Este é um problema de qualidade antes de ser de custo.
2. **Custo.** Esse preâmbulo é pago de novo em toda requisição da sessão.
3. **Deriva.** Quando tudo está sempre carregado, ninguém precisa declarar o que a tarefa depende.
   A dependência vira implícita, e a próxima sessão redescobre do zero.

O problema **não** era excesso de documentação. Os documentos são bons e específicos. O problema
era **não haver nenhum mecanismo que dissesse qual deles importa agora**.

## Decisão

Adotamos **divulgação progressiva**: o agente carrega, em ordem, apenas o mínimo necessário para o
passo atual.

```
CLAUDE.md / AGENTS.md   invariantes e roteador
docs/backlog.json       estado, lido por script, não despejado no contexto
tarefa corrente         objetivo, roles, context[], verifications
context[] da tarefa     só os documentos que a tarefa declara
docs/roles/<papel>.md   só os papéis em roles
relatório               só se a tarefa está in_progress
código                  achado por busca, lido por região
```

Quatro mudanças concretas sustentam isso:

### 1. `AGENTS.md` vira roteador, não enciclopédia

Fica com os **invariantes** (o que vale em toda tarefa), o **roteador** (onde achar o resto) e o
**fluxo resumido**. O detalhe sai para documentos carregados sob demanda:
`docs/playbooks/task-execution.md`, `docs/conventions.md`, `docs/environment.md`,
`docs/context-discipline.md`.

**Nenhuma regra foi removida** — cada uma passou a morar num lugar que se carrega quando é
relevante. O fluxo dos oito passos **permanece em `AGENTS.md`**, porque uma sessão sem subagentes
precisa conseguir executá-lo lendo só esse arquivo (critério de aceite de T00, e item do checklist
de paridade de `interop.md`).

### 2. A tarefa declara o que precisa: campo `context[]`

Cada tarefa do backlog ganha uma lista dos documentos necessários para executá-la. O princípio é
**contexto mínimo suficiente, não contexto máximo possível**: vazio é uma resposta legítima, e
significa "esta tarefa não depende de documento de domínio", não "ninguém preencheu".

O campo é **opcional**: ausente é tratado como vazio, para que tarefa escrita antes desta decisão
continue válida. E é **corrigido por quem sente a falta** — quem descobrir no meio da execução que
um documento era necessário o acrescenta ali, em vez de deixar a próxima sessão redescobrir.

### 3. O trabalho mecânico vira script

`scripts/*.ps1` fazem o que era feito à mão e não exige julgamento: achar a tarefa corrente,
validar o backlog, carimbar `startedAt`/`finishedAt`, criar o relatório pelo template, conferir a
estrutura do relatório no fechamento, resolver links de documentação, rodar as verificações padrão
guardando o log completo em arquivo e devolvendo só o resumo.

A fronteira é explícita: **script faz trabalho mecânico, não substitui raciocínio.** Nenhum deles
julga se um critério de aceite foi cumprido, revisa código ou decide arquitetura. `task-finish.ps1`
recusa o obviamente incompleto; ele não aprova nada.

O ganho maior não é o comando poupado — é que `task-status.ps1` devolve **40 linhas sobre uma
tarefa** onde antes se carregavam 384 linhas de backlog.

### 4. Evidência passa a ser "comando + exit code + trecho probatório"

A regra anterior pedia *"a saída real, completa"*. Em tarefas como T10, "completa" significa a
saída da camada 1 nas 32 combinações — dezenas de milhares de linhas, que ninguém lê e que
degradam o raciocínio de quem carregar.

A regra nova exige **o comando literal, o exit code e a saída que comprova o critério**, com o log
completo guardado em arquivo. Isso não afrouxa a comprovação: `Passed! - Failed: 0, Passed: 147`
com exit code 0 continua sendo evidência, e *"os testes passaram"* continua não sendo. O que muda é
que o ruído deixa de ser obrigatório.

## Alternativas descartadas

**`.claudeignore`.** Não existe na versão atual do Claude Code (2.1.270); o mecanismo suportado é
`permissions.deny` com regras `Read(...)` em `settings.json`. Criar um arquivo que a ferramenta
ignora em silêncio seria pior que não ter nada — daria a impressão de proteção sem a proteção.

**Resumir os documentos de `docs/`.** Trocaria um problema de volume por um de fidelidade, e é
exatamente o tipo de compressão com perda que ADR-0007 evita. Os documentos continuam completos; o
que muda é **quando** são carregados.

**Deixar o agente decidir sozinho o que ler.** É o estado anterior, e foi o que produziu o
problema: sem uma declaração explícita, o comportamento seguro é ler tudo.

**Um subagente por papel, sempre.** Cada despacho custa, e para mudança pequena e sequencial o
custo não se paga. Subagente passa a ser usado quando há ganho real — contexto especializado,
trabalho independente, paralelização, isolamento de contexto ruidoso. O `reviewer` e o `git-flow`
são exceção permanente: a independência deles é arquitetural, não uma otimização, e **não** se
abre mão dela para economizar contexto (ADR-0013).

## Consequências

**Ganhamos**

- O preâmbulo obrigatório antes de saber qual é a tarefa cai de 724 para ~180 linhas.
- A dependência documental de cada tarefa fica **escrita**, em vez de implícita.
- Validações que eram manuais e repetidas a cada tarefa (backlog consistente, links resolvem,
  relatório estruturado) passam a ser executáveis e a falhar sozinhas.
- Uma sessão consegue fechar uma tarefa sem carregar o projeto inteiro, o que torna realista a
  meta de **uma tarefa, uma sessão**.

**Perdemos, ou aceitamos**

- **Mais arquivos.** O manual passa de um documento grande para um roteador e vários documentos
  pequenos. Quem quiser ler tudo de uma vez tem mais paradas. Aceito: ler tudo de uma vez é
  justamente o que a decisão desencoraja.
- **`context[]` pode ficar defasado.** Um documento renomeado quebra a lista. Mitigado por
  `backlog-validate.ps1`, que reprova caminho inexistente.
- **`context[]` pode ficar errado sem ninguém notar** — uma tarefa que precisava de um documento
  não listado é executada com informação a menos. Não há verificação automática possível para
  isso; a mitigação é cultural e está escrita: quem sentir a falta acrescenta.
- **Dependência de PowerShell** para os scripts. É do processo, não do produto: nada em `src/`,
  `tests/` ou nos ZIPs gerados depende deles, e o fluxo continua executável à mão se eles sumirem.
  Os scripts são ASCII puro porque o PowerShell 5.1 lê arquivo sem BOM na codepage do console e
  quebra em qualquer byte não-ASCII — verificado nesta máquina.

**Não muda**

Os critérios de aceite, a exigência de verificação executada, o `reviewer` como passo separado, o
portão de sete itens do `git-flow`, a propriedade do backlog (ADR-0004) e a equivalência entre
Claude Code e Codex. Esta decisão é sobre **quando** o conhecimento é carregado, não sobre **se**
ele vale.
