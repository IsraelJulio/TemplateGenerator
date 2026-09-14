# Interoperabilidade Claude Code ↔ Codex

Este documento existe para que **o mesmo pedido produza o mesmo resultado nas duas ferramentas**.
O manual de operação é [`../AGENTS.md`](../AGENTS.md); aqui estão os detalhes mecânicos e o
checklist de paridade.

## 1. O problema

As duas ferramentas leem coisas diferentes:

| | Claude Code | Codex |
|---|---|---|
| Instruções de projeto | `CLAUDE.md` | `AGENTS.md` |
| Papéis/subagentes | `.claude/agents/*.md` | `[agents]` em config, ou nenhum |
| Procedimentos | `.claude/skills/*/SKILL.md` | não lê |
| Config de projeto | `.claude/settings.json` | `.codex/config.toml` |

Se o conhecimento morar nos arquivos de uma ferramenta, a outra começa cega e entrega outra coisa.

## 2. A solução adotada

**Todo conteúdo real mora em `docs/` e em `AGENTS.md`. Os arquivos de ferramenta são ponteiros.**

```
AGENTS.md          ← manual canônico: invariantes + roteador + fluxo (Codex lê direto)
CLAUDE.md          ← ~35 linhas: "leia AGENTS.md" + o que é só do Claude Code
docs/context-discipline.md ← o que carregar e o que não carregar (ADR-0014)
docs/conventions.md, docs/environment.md ← detalhe carregado sob demanda
docs/roles/*.md    ← responsabilidades reais dos papéis
docs/playbooks/*.md ← procedimentos reais
docs/backlog.json  ← estado, igual para os dois; cada tarefa declara seu context[]
scripts/*.ps1      ← trabalho mecânico, igual para os dois
.claude/agents/*.md    → invólucro sobre docs/roles/
.claude/skills/*/SKILL.md → invólucro sobre docs/playbooks/
.codex/config.toml     → só configuração de runtime, zero conhecimento
```

**Divulgação progressiva vale nas duas ferramentas.** Nem o `context[]` das tarefas, nem os
scripts, nem `docs/context-discipline.md` dependem de mecanismo exclusivo do Claude Code: são JSON,
PowerShell e Markdown. O Codex lê o mesmo `AGENTS.md`, roda os mesmos scripts e carrega o mesmo
`context[]` ([ADR-0014](decisions/adr-0014-divulgacao-progressiva-de-contexto.md)).

A única exclusão que **é** específica do Claude Code são as regras `permissions.deny` de
`.claude/settings.json`. Elas não carregam conhecimento nenhum — só impedem leitura de artefato de
build — e sua ausência no Codex não muda entrega alguma.

Regra: **se você precisou escrever a mesma frase em `.claude/` e em `.codex/`, ela estava no lugar
errado — mova para `docs/`.**

## 3. O que `.codex/config.toml` faz

Verificado neste ambiente em 2026-09-12 com `codex doctor`:

| Chave | Por quê | Efeito verificado |
|---|---|---|
| `project_doc_max_bytes = 65536` | `AGENTS.md` tem ~7 KB e não pode ser truncado antes da seção 4 | config carrega sem erro |
| `project_doc_fallback_filenames = ["CLAUDE.md"]` | se `AGENTS.md` sumir, o Codex ainda acha o caminho | — |
| `sandbox_workspace_write.network_access = true` | `dotnet restore` e `npm install` precisam de rede | `network sandbox` passou de `restricted` para **`enabled`** |

**Pré-requisito:** a camada `.codex/` de projeto só é carregada se o diretório estiver marcado
como *trusted* no `~/.codex/config.toml`. Este projeto já está:

```toml
[projects.'c:\users\2273129\documents\projects\templategenerator']
trust_level = "trusted"
```

Se você clonar o repositório em outra máquina ou outro caminho, **precisa marcar como trusted de
novo**, senão o `.codex/config.toml` é ignorado em silêncio e os restores voltam a pedir aprovação.

## 4. Diferença que permanece: delegação

Esta é a única diferença de comportamento que não dá para apagar:

- **Claude Code:** o PO é o agente padrão e delega a `architect`, `backend`, `template-engineer`,
  `frontend`, `qa`, `reviewer` e `git-flow`, cada um com contexto próprio.
- **Codex:** uma sessão única assume o papel PO e **percorre os mesmos papéis em sequência**,
  lendo `docs/roles/<papel>.md` antes de cada bloco de trabalho e anunciando a troca de papel no
  relatório.

O papel `git-flow` (ADR-0013) atravessa bem essa diferença porque é feito só de comandos `git` e
`gh`: no Codex a sessão assume o papel nos passos 4 e 7 e roda o mesmo
[`playbooks/git-flow.md`](playbooks/git-flow.md). **A branch, o PR e o veredito são idênticos nas
duas ferramentas** — o PR não registra qual delas o abriu, e não deveria.

A única perda no Codex é a garantia técnica: no Claude Code o `git-flow` não tem ferramenta de
escrita e por isso **não consegue** consertar código para o próprio portão passar; no Codex isso
volta a ser convenção, como em ADR-0004.

O Codex 0.153 tem subagentes (`[agents]` no config, com `agents.<nome>` apontando para um
`config_file`). Ainda **não** usamos isso: o ganho não compensa manter duas definições de papel
em paralelo, e o modo sequencial já produz a entrega esperada. Se um dia usarmos, os blocos
`agents.<nome>` devem apontar para os mesmos `docs/roles/*.md`, nunca duplicar o conteúdo.

**O que não muda entre as ferramentas:** os critérios de aceite, as verificações executadas, o
formato do relatório, o estado no backlog e o commit. Se o resultado diferir nisso, é defeito.

## 5. Como pedir continuidade

Em qualquer uma das duas, abra a sessão na raiz do repositório e diga:

> PO, execute a próxima tarefa.

Ou, equivalente: *"continue o projeto"*, *"retome de onde parou"*.

No Claude Code, `/po-next` faz o mesmo. No Codex não há slash command de projeto — a frase acima
é o gatilho, e a seção 4 de `AGENTS.md` é o que o agente deve seguir.

### Abertura explícita

```bash
# Claude Code (po já é o agente padrão via .claude/settings.json)
claude --agent po

# Codex
codex -C "C:/Users/2273129/Documents/Projects/TemplateGenerator"
```

## 6. Checklist de paridade

Rode quando mudar qualquer arquivo de agente, skill ou config. Uma resposta "não" é defeito.

Boa parte já é executável:

```powershell
powershell -File scripts/docs-links.ps1        # links e âncoras entre documentos
powershell -File scripts/backlog-validate.ps1  # backlog, context[], roles, dependências
```

- [ ] `CLAUDE.md` continua com menos de 40 linhas e sem regra que não exista em `AGENTS.md`?
- [ ] `AGENTS.md` continua sendo invariantes + roteador + fluxo, sem procedimento detalhado que
      pertença a um playbook?
- [ ] Todo `.claude/agents/*.md` aponta para um `docs/roles/*.md` existente?
- [ ] Todo `.claude/skills/*/SKILL.md` local aponta para um `docs/playbooks/*.md` existente?
- [ ] `scripts/docs-links.ps1` passa — todo link relativo e toda âncora resolvem?
- [ ] **Nenhuma referência em prosa a seção numerada** (`AGENTS.md` §N) aponta para seção que
      mudou de assunto ou deixou de existir? `docs-links.ps1` **não vê** essas referências, porque
      não são links Markdown. Confira com
      `grep -rn "AGENTS\.md.*§" --include='*.md' docs/ .claude/`, ignorando `docs/reports/`, que é
      registro histórico.
- [ ] Ao mudar uma regra, você fez `grep` da redação **antiga** em `docs/` antes de dá-la por
      realocada? Regra que sobrevive em dois lugares se resolve sempre pela versão mais frouxa.
- [ ] `scripts/backlog-validate.ps1` passa — inclusive todo caminho de `context[]`?
- [ ] Todo `context[]` continua **mínimo suficiente**, e não uma lista defensiva de tudo que
      poderia ser útil ([ADR-0014](decisions/adr-0014-divulgacao-progressiva-de-contexto.md))?
- [ ] Os scripts de `scripts/` continuam fazendo só trabalho mecânico, sem julgar critério de
      aceite, revisar código ou decidir arquitetura?
- [ ] Os scripts continuam em **ASCII puro**? (PowerShell 5.1 lê arquivo sem BOM na codepage do
      console e quebra em qualquer byte não-ASCII — ver `docs/environment.md`.)
- [ ] `.codex/config.toml` continua sem nenhuma instrução de comportamento (só runtime)?
- [ ] `AGENTS.md` cabe em `project_doc_max_bytes`?
- [ ] `codex doctor` ainda mostra `network sandbox: enabled` na raiz do projeto?
- [ ] O procedimento da seção 4 de `AGENTS.md` é executável por uma sessão sem subagentes?
- [ ] Nenhum documento em `docs/` menciona um mecanismo exclusivo de uma ferramenta como
      obrigatório? (As regras `permissions.deny` são a exceção conhecida, e não carregam
      conhecimento — ver seção 2.)
- [ ] O fluxo de branch e PR de ADR-0013 é executável nas duas ferramentas, sem depender de
      subagente?
- [ ] `gh auth status` continua autenticado nesta máquina?

> **Item que não pode ser verificado nesta máquina.** O `codex` CLI **não está instalado** aqui
> (verificado em 2026-09-14, ao fechar o PR #1). O item do `codex doctor` fica **pendente**, nunca
> marcado como cumprido — o portão recusa por omissão. Reexecute o checklist numa máquina com o
> Codex instalado antes de afirmar paridade completa.
