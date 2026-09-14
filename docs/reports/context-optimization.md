# Otimização de contexto — divulgação progressiva

- **Data:** 2026-09-14
- **Tipo:** mudança do manual de operação (`chore/`), não tarefa do backlog
- **Branch:** `chore/progressive-disclosure-de-contexto`
- **Decisão:** [ADR-0014](../decisions/adr-0014-divulgacao-progressiva-de-contexto.md)
- **Escopo:** processo, contexto, documentação e automação de fluxo. **Nada em `src/` ou `tests/`.**

> **Sobre os números.** Tudo abaixo é medido em **linhas e bytes de documento**, contados por
> comando neste repositório. **Não há telemetria de tokens neste projeto**, e por isso **nenhuma
> economia de tokens é afirmada**. A redução é **estrutural** — menos material carregado por
> execução — e o efeito real sobre consumo ainda precisa ser medido em uso.

---

## Antes

### Estrutura

`AGENTS.md` acumulava seis coisas: o produto, os invariantes, o roteador de documentação, o
procedimento detalhado dos oito passos, a tabela de versões do ambiente e as convenções de commit
e relatório. Tudo carregado em toda tarefa, inclusive nas que não tocavam nenhum desses assuntos.

O `docs/backlog.json` era lido inteiro — 13 tarefas, com todos os critérios de aceite das treze —
para executar uma.

Nenhuma tarefa declarava de que documento dependia. A dependência era implícita, e cada sessão a
redescobria.

### O que uma execução padrão mandava carregar

Antes de a sessão saber **qual** era a tarefa:

| Arquivo | Linhas | Bytes |
|---|---:|---:|
| `.claude/agents/po.md` | 20 | 958 |
| `AGENTS.md` | 150 | 9.117 |
| `docs/playbooks/po-next.md` | 98 | 4.234 |
| `docs/backlog.json` | 384 | 27.276 |
| `docs/roles/po.md` | 72 | 3.174 |
| **Total** | **724** | **44.759** |

Comando: `wc -lc .claude/agents/po.md AGENTS.md docs/playbooks/po-next.md docs/backlog.json docs/roles/po.md`

### Inventário

| Item | Quantidade |
|---|---:|
| Agentes (`.claude/agents/`) | 8 |
| Skills locais | 4 (+2 de terceiros) |
| Documentos em `docs/` | 43 `.md` + 1 `.json` |
| Scripts de automação | **0** |
| Passos/agentes numa tarefa simples | 8 passos, até 2 acionamentos de `git-flow` + 1 `reviewer` + N especialistas |

### Pontos de desperdício identificados

1. **Backlog inteiro em contexto** para executar uma tarefa — os critérios de aceite de doze
   tarefas irrelevantes competindo por atenção com os da tarefa corrente.
2. **`AGENTS.md` como enciclopédia**: a tabela de versões do .NET e do PostgreSQL carregada numa
   tarefa que só mexe em CSS.
3. **Dependência documental implícita.** Sem `context[]`, o comportamento seguro do agente é ler
   tudo que parece relacionado.
4. **Verificação mecânica feita à mão, toda vez**: validar o backlog, resolver links entre
   documentos, conferir se o relatório tem as seções obrigatórias. Trabalho determinístico
   consumindo raciocínio.
5. **Exigência de saída "real, completa"** no relatório. Em T10 isso significaria a saída da
   camada 1 nas 32 combinações — dezenas de milhares de linhas que ninguém lê.
6. **Sem exclusão de artefatos de build** na configuração da ferramenta.
7. **Duplicação** entre `AGENTS.md` §4 e `docs/playbooks/po-next.md`, que descreviam o mesmo
   procedimento em dois lugares.

---

## Depois

### Nova estrutura

```
CLAUDE.md              bootstrap do Claude Code (37 linhas)
AGENTS.md              invariantes + roteador + fluxo (76 linhas)
  ├─ docs/context-discipline.md      o que carregar e o que não  [NOVO]
  ├─ docs/conventions.md             commit, branch, relatório    [NOVO]
  ├─ docs/environment.md             versões da máquina           [NOVO]
  └─ docs/playbooks/task-execution.md  os 8 passos em detalhe     [NOVO]
scripts/*.ps1          trabalho mecânico                          [NOVO]
docs/backlog.json      estado + context[] por tarefa              [CAMPO NOVO]
```

`docs/playbooks/po-next.md` foi **removido**: seu conteúdo foi para `task-execution.md`, que agora
é o único lugar onde o procedimento detalhado mora. A skill `po-next` aponta para lá.

### Divulgação progressiva

```
AGENTS.md            invariantes e roteador
task-status.ps1      a tarefa corrente — substitui ler o backlog inteiro
context[] da tarefa  só os documentos que ela declara
roles da tarefa      só os papéis em roles
relatório            só se in_progress
código               busca antes de leitura, por região
```

### O que uma execução padrão manda carregar agora

| Arquivo | Linhas | Bytes |
|---|---:|---:|
| `.claude/agents/po.md` | 24 | 1.394 |
| `AGENTS.md` | 76 | 5.340 |
| saída de `scripts/task-status.ps1` | 43 | 1.393 |
| **Total** | **143** | **8.127** |

**44.759 → 8.127 bytes de material carregado antes de a sessão saber o que fazer: −82%.**

O resto (`context[]`, roles, relatório) é carregado depois, e é *específico da tarefa* — que é
exatamente o material que se quer na janela.

### Comparação direta

| Métrica | Antes | Depois | Δ |
|---|---:|---:|---|
| `CLAUDE.md` | 29 linhas | 37 linhas | +8 |
| `AGENTS.md` | 150 linhas / 9.117 B | 76 linhas / 5.340 B | **−49% linhas** |
| Preâmbulo obrigatório | 724 linhas / 44.759 B | 143 linhas / 8.127 B | **-82% bytes** |
| Agentes | 8 | 8 | — |
| Skills locais | 4 | 4 | — |
| Skills de usuário | 0 | 1 (`token-efficiency`) | +1 |
| Scripts | 0 | 7 (951 linhas) | +7 |
| Documentos em `docs/` | 43 `.md` | 47 `.md` | +5 novos, −1 removido |

`CLAUDE.md` cresceu 8 linhas — ganhou a regra de exclusão de contexto e a nota da skill de
usuário, ambas específicas do Claude Code e que não cabiam em `AGENTS.md`. Continua dentro do
limite de 40 linhas do checklist de paridade.

### Scripts criados

Todos em PowerShell 5.1, **ASCII puro** (ver *Riscos*), e todos fazendo só trabalho mecânico.

| Script | O que faz | Substituía |
|---|---|---|
| `task-status.ps1` | Acha a tarefa corrente e imprime estado, `roles`, `context[]`, `verifications`, posição do git | ler o backlog inteiro |
| `backlog-validate.ps1` | JSON válido, ids únicos, dependências existentes, sem ciclo, ≤1 `in_progress`, `context[]` e `report` resolvem, `roles` têm documento | conferência manual a cada tarefa |
| `task-start.ps1` | `in_progress`, `startedAt`, relatório pelo template; recusa se houver outra `in_progress` ou dependência pendente | edição manual do JSON |
| `task-verify.ps1` | Roda build/testes, guarda o log **completo** em arquivo, devolve resumo + erros + exit code | despejar o log no contexto |
| `task-finish.ps1` | Confere estrutura do relatório, marca `done`, `finishedAt`, `pullRequest` | conferência manual |
| `docs-links.ps1` | Todo link relativo e toda âncora entre documentos | conferência manual a cada tarefa de doc |
| `_common.ps1` | Helpers; edita o backlog por substituição pontual, nunca reserializando | — |

**A fronteira é explícita e está escrita em cada script:** nenhum deles julga se um critério de
aceite foi cumprido, revisa código ou decide arquitetura. `task-finish.ps1` recusa o obviamente
incompleto; **ele não aprova nada.**

**Prova de que o portão morde**, contra um relatório recém-criado pelo template:

```
$ powershell -File scripts/task-finish.ps1 -Id T05 -Check
task-finish - T05
  criterios: 6 na tarefa, 1 na tabela do relatorio
  ERRO   relatorio ainda tem 4 marcador(es) do template por preencher (ex.: <ISO 8601>)
  ERRO   tabela de criterios tem 1 linha(s) numerada(s) para 6 criterio(s) da tarefa
  ERRO   parecer do reviewer nao registra aprovacao - sem "aprovado" no texto, nao fecha
3 problema(s) estrutural(is). Tarefa NAO fechada.
exit code: 1
```

**E prova de que ele não morde quem está certo**, contra os relatórios reais já fechados:

```
$ powershell -File scripts/task-finish.ps1 -Id T01 -Check
task-finish - T01
  criterios: 7 na tarefa, 7 na tabela do relatorio
  AVISO  sem secao "Portao do git-flow" - aceito por ser tarefa anterior a ADR-0013
  OK     estrutura do relatorio completa, sem criterio em falta, parecer presente.
exit code: 0
```

T00, T01, T02 e T03 passam, com a contagem de critérios batendo exatamente (12/12, 7/7, 8/8,
12/12). **T04 reprova, e corretamente** — ver *Defeito encontrado em T04*, abaixo.

### Campo `context[]`

Preenchido tarefa a tarefa, por análise, não por padrão. Distribuição:

| Tarefa | Documentos | Tarefa | Documentos |
|---|---:|---|---:|
| T00 | 1 | T06 | 3 |
| T01 | 3 | T07 | 3 |
| T02 | 2 | T08 | 3 |
| T03 | 4 | T09 | 3 |
| T04 | 3 | T09b | **1** |
| T05 | 4 | T10 | 5 |
| | | T11 | 3 |

T09b recebeu **um** documento porque é uma correção estreita de um *flake* de e2e — a tentação de
listar os cinco documentos de frontend foi recusada. O princípio registrado é **contexto mínimo
suficiente, não contexto máximo possível**.

O campo é **opcional**: ausente é tratado como vazio, e por isso nenhuma tarefa antiga quebrou.
`backlog-validate.ps1` reprova caminho inexistente, então ele não pode apodrecer em silêncio.

### Skill instalada

| | |
|---|---|
| **Skill** | `token-efficiency` |
| **Origem** | `denfry/claude-skills`, `skills/token-efficiency` |
| **Commit fixado** | `cc73299f5d9e5c1a800cf65015093bd527faf0e5` (2026-07-22) |
| **Licença** | MIT, preservada |
| **Local** | `~/.claude/skills/token-efficiency/` — **nível de usuário, fora deste repositório** |
| **Registro de versão** | `~/.claude/skills/token-efficiency/PINNED.md` |

Não é dependência do projeto: o fluxo de `AGENTS.md` funciona igual sem ela, e o Codex não a vê.
Procedência completa em [`../THIRD-PARTY.md`](../THIRD-PARTY.md).

**Segunda candidata avaliada e recusada:** `valorisa/Claude-Skills` → `token-optimization`.
Sobreposição alta no que importa aqui; o que tem de próprio (invalidação de cache, escolha de
modelo, auditoria de MCP) é configuração de uma vez, não hábito por turno; gatilhos largos demais;
e vem de uma coleção de 38 skills. Limite adotado: **no máximo duas skills de otimização; hoje uma
basta.**

### Hooks: três instalados, um recusado

O dry-run do instalador oficial (`python hooks/install.py --dry-run`) revelou que ele liga
**quatro** hooks, enquanto o `hooks/README.md` do próprio projeto documenta **três**.

| Hook | Evento | Decisão |
|---|---|---|
| `efficiency_core.py` | `UserPromptSubmit` | **instalado** |
| `trajectory_guard.py` | `PostToolUse` | **instalado** |
| `session_report.py` | `Stop` | **instalado** |
| `context_budget.py` | `SessionStart` | **RECUSADO** |

**Auditoria dos três instalados**, sobre o código no commit fixado:

- Nenhum import de rede (`urllib`, `requests`, `socket`), nenhum `subprocess`, nenhum `exec`/`eval`.
- Escrevem apenas em `~/.claude/state/token-efficiency/`.
- Métricas gravadas são **contagens** (turnos, buscas, arquivos lidos, tokens) — não conteúdo de
  prompt.
- Toda entrada engole exceção e sai com código 0. Confirmado por execução com payload inválido:
  produziu silêncio e `exit=0`, não turno quebrado.

**Por que `context_budget.py` foi recusado:**

1. **Alcance.** Varre `~/.claude/projects/*/*.jsonl` — transcrições de **todos os projetos da
   máquina** nos últimos 30 dias — e lê `~/.claude.json`. Esta é máquina corporativa com outros
   repositórios. Local ou não, o alcance excede o que uma skill de eficiência precisa.
2. **Ação autônoma.** Com `--fix` ou `TOKEN_EFFICIENCY_AUTOFIX=1`, move skills e agentes para
   `~/.claude/skills-disabled/`, marca plugins como `false` e remove servidores MCP de
   `~/.claude.json`. Reversível, mas automático sobre a configuração da ferramenta.
3. **Ruído mesmo em modo relatório.** Injeta contexto em `SessionStart` pedindo ao modelo que
   mencione a poda ao usuário — custo e distração em toda sessão nova.
4. **Não documentado** no `hooks/README.md` do próprio projeto.

Nada disso é malicioso e o código é honesto sobre o que faz. Mas **segurança e previsibilidade têm
prioridade sobre economia de tokens**: o arquivo **não foi copiado**, e `install.py` também não —
ele ligaria os quatro. Os três aceitos foram escritos à mão em `~/.claude/settings.json`, com
backup em `settings.json.bak-pre-token-efficiency`.

### Exclusão de contexto

**`.claudeignore` não existe** na versão instalada (Claude Code 2.1.270, extensão VS Code).
Verificado na documentação oficial de settings, que não menciona `.claudeignore` nem
`ignorePatterns`. **Não foi criado** — um arquivo que a ferramenta ignora em silêncio seria pior
que nada.

O mecanismo suportado é `permissions.deny` com regras `Read(...)`, acrescentado a
`.claude/settings.json`: `bin/`, `obj/`, `node_modules/`, `dist/`, `.angular/`, `TestResults/`,
`test-results/`, `playwright-report/`, `blob-report/`, `coverage/`, `*.min.js`, `*.map`, `*.zip`,
`*.dll`, `*.pdb`, `*.woff2`, `package-lock.json`, mais os padrões de segredo (`.env`, `*.pfx`,
`*.key`, `appsettings.*.local.json`).

**Limite conhecido e registrado:** regras `deny` valem para as ferramentas internas de leitura e
busca, e **não** para o que passa por `Bash` (`cat`, `type`). Não são um sandbox.

---

## Riscos e trade-offs

| Risco | Gravidade | Mitigação |
|---|---|---|
| `context[]` **incompleto** — tarefa executada com informação a menos | **A maior desta mudança.** Não há verificação automática possível | Cultural e escrita: quem sentir a falta acrescenta. Item novo no checklist do `reviewer` |
| `context[]` defasado por arquivo renomeado | Baixa | `backlog-validate.ps1` reprova caminho inexistente |
| Mais arquivos para navegar | Baixa | É o custo pretendido da divulgação progressiva |
| Dependência de PowerShell | Baixa | Do processo, não do produto. Nada em `src/`, `tests/` ou nos ZIPs depende. O fluxo continua executável à mão |
| Scripts quebram com byte não-ASCII | **Real, e já ocorreu** | PowerShell 5.1 lê arquivo sem BOM na codepage 437 e falha o parse. Os scripts são ASCII puro, e há item no checklist de paridade |
| Regra de evidência mais frouxa que "saída completa" | Média | A regra nova exige comando + exit code + trecho probatório, e diz explicitamente que "os testes passaram" não é evidência |
| Skill de terceiro pode mudar de comportamento | Baixa | Fixada em commit; `PINNED.md` traz o comando de diff para reauditoria |
| Hook injeta "no preamble" que poderia encurtar relatórios | Baixa | Governa **respostas**, não arquivos de relatório. O §6 da skill protege explicitamente verificação, ressalva e evidência |

### Item que não pôde ser verificado

O **`codex` CLI não está instalado nesta máquina** (verificado em 2026-09-14). O item
`codex doctor` do checklist de paridade fica **pendente, nunca marcado como cumprido** — o portão
recusa por omissão. Está registrado em `docs/environment.md` e no próprio checklist.

Isso **não** significa que a paridade quebrou: nada nesta mudança depende de mecanismo exclusivo do
Claude Code. `context[]` é JSON, os scripts são PowerShell, a disciplina de contexto é Markdown, e
`AGENTS.md` continua executável por uma sessão sem subagentes. Mas a afirmação precisa ser
**verificada** numa máquina com Codex antes de ser feita.

---

## Parecer do reviewer

**Primeira rodada: REPROVADO**, com 10 itens. A revisão independente encontrou defeitos reais que
esta sessão não teria achado sozinha. Os principais, todos corrigidos:

| # | Achado | Correção |
|---|---|---|
| 1 | Quatro documentos citavam `AGENTS.md` §7 e §8, seções que deixaram de existir — inclusive o **item 6 do portão de sete itens** | Apontados para `conventions.md#commits` e `environment.md`. `test-strategy.md` citava "seção 5", que passou a apontar para a coisa errada |
| 2 | A regra antiga de evidência ("saída real, completa") sobreviveu em 5 documentos, incluindo a **definição de pronto** | Unificados em `conventions.md#evidência-em-relatório` |
| 3 | A tabela "Antes" deste relatório trazia bytes que **não eram** a saída do comando que ela citava | Remedido contra o conteúdo de `main`; 4 das 5 linhas estavam erradas e não somavam o total |
| 4a | `task-finish.ps1` reprovava relatório **correto**: bastava a palavra "reprovado" aparecer, e o template **exige** registrar rodadas reprovadas | Passou a decidir pela presença de aprovação, não pela ausência da palavra |
| 4b | A guarda do veredito do git-flow não casava **"MUDANÇAS SOLICITADAS"** (cedilha), e a seção nem era conferida quanto ao veredito | Cedilha por `[char]0x00E7`; veredito conferido, e a seção passou a ser exigida só de tarefa posterior a ADR-0013 |
| 5 | O caminho de sucesso do script nunca tinha sido exercitado, e **os 5 relatórios fechados reprovavam** | Corrigido e exercitado: T00–T03 passam |
| 6 | O bug de `[regex]::Replace` estático ("corrigido") **continuava em `_common.ps1`**, na função que escreve o backlog | Corrigido nos dois pontos, com a forma de instância |
| 7 | `po-next/SKILL.md` foi de 18 para 93 linhas e virou procedimento — invólucro com regra própria, exatamente o que o projeto proíbe | Reduzido a 34 linhas de ponteiro |
| 8 | Um hook injetou **afirmação falsa** no contexto do reviewer | Ver abaixo — **não resolvido** |
| 9 | Lacunas em `context[]`: ADR-0011 faltando em T05–T08, `vision.md` em T11, `environment.md` em T05 | Acrescentados, depois de conferir que as afirmações procediam |
| 10 | Menores: escape inválido de aspas, concatenação com `+` que o PowerShell descarta, `Select-Object` sobre `Write-Host` (no-op), contagem de critérios fora da seção | Corrigidos |

O parecer também julgou a mudança da regra de evidência: **"em princípio, não [enfraquece]; como
entregue, sim"** — porque a regra antiga seguia viva em cinco documentos e a ambiguidade sempre se
resolve pela versão mais frouxa. Com o achado 2 corrigido, a ressalva cai.

### Defeito encontrado em T04, que não é desta mudança

Ao exercitar o portão contra os relatórios reais, `task-finish.ps1` reprovou **T04**:

```
$ powershell -File scripts/task-finish.ps1 -Id T04 -Check
  ERRO   parecer do reviewer vazio ou curto demais para ser parecer
exit code: 1
```

A seção `## Parecer do reviewer` de `docs/reports/T04.md` contém literalmente `<pendente>`. O
parecer verdadeiro existe, dentro de `### Papel: reviewer`, e o commit `e9f157c` se chama *"fechar
tarefa com parecer aprovado do reviewer"* — mas **a seção obrigatória nunca foi preenchida**, e a
tarefa está `done`.

**Não foi remendado aqui, de propósito.** Preencher aquela seção exigiria eu afirmar um parecer que
não presenciei, e reescrever o relatório de uma tarefa fechada não é decisão de uma mudança de
processo. Fica registrado como achado, para o dono decidir. É, em si, evidência de que o portão
novo pega o que a conferência manual deixou passar.

### Achado 8 — o hook que mentiu para o reviewer, ainda aberto

O `trajectory_guard.py` avisou **quatro vezes** ao reviewer que ele "editou este arquivo neste
mesmo turno e está lendo de volta", ao ler arquivos **pela primeira vez**.

Causa, confirmada no estado do próprio hook
(`~/.claude/state/token-efficiency/<session>.json`): o subagente **herda `session_id` e
`prompt_id` do pai**, e `trajectory_guard.py` compara `state["edited"][path] == prompt_id`. Todo
arquivo que o PO editou no turno é anunciado ao subagente como editado por ele:

```
turn: 0 | warnings_emitted: 4 | waste: {'reread': 0, 'blind_read': 0, 'recheck': 4}
edited:  /docs/roles/reviewer.md -> 166cd924-...   (editado pelo PO, não pelo reviewer)
```

O efeito é específico e grave: **um hook pressiona o revisor a não ler o diff** — a única coisa que
ele existe para fazer. E atinge justamente o reviewer, que por desenho nasce no mesmo turno do
trabalho que revisa.

A auditoria de instalação não tinha como pegar isto: não é o que o hook *faz* (rede, escrita,
subprocess — tudo limpo), é o que ele *afirma*.

> **Estado: aberto.** A remoção do hook foi tentada e **bloqueada**, corretamente, por exigir
> aprovação explícita para reverter uma instalação já feita. A decisão é do dono do ambiente. Até
> lá, os três hooks seguem ligados, e este relatório é o registro de que `trajectory_guard.py`
> emite afirmação falsa em sessão com subagente.
>
> Remoção, quando autorizada: apagar a entrada `PostToolUse` de `~/.claude/settings.json` (backup
> em `settings.json.bak-pre-token-efficiency`) e o arquivo
> `~/.claude/skills/token-efficiency/hooks/trajectory_guard.py`. Os outros dois não têm este
> problema: `efficiency_core.py` injeta texto fixo e `session_report.py` fala por `systemMessage`.

---

## Validação executada

```
$ powershell -File scripts/backlog-validate.ps1
backlog-validate - 13 tarefas
  OK     JSON valido, ids unicos, dependencias existentes, sem ciclo, <=1 in_progress,
         context[] e report resolvem no disco, roles tem documento.
exit code: 0

$ powershell -File scripts/docs-links.ps1
docs-links - 74 arquivos, 223 link(s) relativo(s)
  OK     todo link relativo resolve, e toda ancora existe no destino.
exit code: 0
```

Durante a validação, `docs-links.ps1` **reprovou de verdade** duas vezes antes de passar: apontou
uma ADR-0014 ainda inexistente e uma âncora `#quando-delegar` que não existia em `roles/po.md`.
Ambas foram corrigidas escrevendo o que faltava. Um validador que nunca reprovou não é validador.

Os testes dos scripts também acharam **três defeitos reais**, todos corrigidos:

1. `-replace` do PowerShell não aceita contador — a data de *Fim* era preenchida na abertura.
2. O 4º argumento de `[regex]::Replace` estático é `RegexOptions`, não contagem.
3. A limpeza ASCII estripou o caractere `❌` de um regex de `task-finish.ps1`, o que faria o portão
   acusar **todo** critério como não cumprido. Agora é referenciado por `[char]0x274C`.

### Checklist da seção 17 do pedido

| Item | Resultado |
|---|---|
| `CLAUDE.md` funcional | ✅ 37 linhas, dentro do limite de 40 |
| `AGENTS.md` mantém invariantes | ✅ 10 invariantes explícitos na seção 2 |
| Nenhuma regra perdida | ✅ cada regra removida foi realocada; ver mapa na ADR-0014 |
| Links internos válidos | ✅ `docs-links.ps1`, 223 links, exit 0 |
| `docs/backlog.json` válido | ✅ `backlog-validate.ps1`, exit 0 |
| Tarefas antigas utilizáveis | ✅ nenhum campo removido; `context[]` é aditivo |
| `context[]` opcional/compatível | ✅ ausente = vazio, documentado em `$schemaNotes` |
| Codex segue o mesmo processo | ⚠️ **estruturalmente sim; `codex doctor` não verificável aqui** |
| `/po-next` executável | ✅ skill reescrita, aponta para `task-execution.md` |
| Roles acessíveis | ✅ 8 papéis, todos com documento; validado pelo script |
| Skills acessíveis | ✅ 4 locais + 2 de terceiros + 1 de usuário |
| Reports válidos | ✅ template atualizado; relatórios existentes intactos |
| `git-flow` funcionando | ✅ exercitado nesta sessão no PR #1 (outra mudança, `chore/adr-0013`), aberto, julgado e mesclado. O PR **desta** branch é o próximo passo |
| Nenhuma dependência de produto | ✅ `src/` e `tests/` intocados |
| Nenhum segredo | ✅ ao contrário: novas regras `deny` sobre `.env`, `*.key`, `*.pfx` |
| Nenhuma dependência de container | ✅ ADR-0005 preservada |

---

## Como usar o novo fluxo

Sessão nova, na raiz do repositório:

> PO, execute a próxima tarefa.

Ou `/po-next` no Claude Code.

Comandos, se quiser conduzir à mão:

```powershell
powershell -File scripts/task-status.ps1              # qual é a tarefa e o que ela precisa
powershell -File scripts/task-status.ps1 -All         # visão geral do backlog
powershell -File scripts/task-start.ps1 -Id T05       # abrir
powershell -File scripts/task-verify.ps1              # build + testes, com log em arquivo
powershell -File scripts/task-verify.ps1 -Only e2e    # inclui o Playwright, que é lento
powershell -File scripts/task-finish.ps1 -Id T05 -Check   # conferir sem escrever
powershell -File scripts/task-finish.ps1 -Id T05 -PullRequest <URL>
powershell -File scripts/backlog-validate.ps1         # consistência do backlog
powershell -File scripts/docs-links.ps1               # links e âncoras
```

A branch e o PR continuam sendo do papel `git-flow`, acionado pelo PO duas vezes por tarefa.

---

## O que ainda pode ser otimizado

1. **Medir de verdade.** Com o `session_report.py` instalado, `~/.claude/state/token-efficiency/metrics.jsonl`
   passa a acumular tokens por turno. Depois de algumas tarefas, dá para comparar com o baseline
   estrutural deste relatório e substituir "−82% de bytes carregados" por consumo real.
2. **`verifications` executáveis.** Hoje são prosa em português, e por isso `task-verify.ps1` só
   roda os comandos padrão do repositório. Um campo `verificationCommands` opcional deixaria o
   script executar a verificação específica da tarefa. Não foi feito aqui para não inventar
   esquema antes de haver necessidade.
3. **`docs/THIRD-PARTY.md` tem 280 linhas** e entra no `context[]` de T10. Vale dividir entre
   "política de licenças" e "inventário", carregando só a metade relevante.
4. **A auditoria de licenças npm continua manual**, e já está inscrita como critério de T10 — é
   exatamente o tipo de trabalho mecânico que esta mudança converteu em script nos outros casos.
5. **`senior-frontend` continua vendorizada** e a recomendação registrada em `THIRD-PARTY.md`
   continua sendo removê-la. São ~210 linhas de skill de stack errada ocupando o catálogo.
6. **Os relatórios de T00 a T04 não têm `context[]` retroativo** de seus próprios aprendizados.
   Não é problema — eles estão fechados — mas se alguma for reaberta, o campo precisará ser
   revisto.
