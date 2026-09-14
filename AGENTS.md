# TemplateGenerator — Manual de Operação

> **Documento canônico de operação**, para qualquer agente em qualquer ferramenta (Claude Code,
> Codex ou outra). `CLAUDE.md` aponta para cá. Em conflito com a configuração de uma ferramenta,
> **este arquivo vence**. Ele é curto de propósito: carrega os invariantes e diz **onde achar** o
> resto. Não leia adiante do que a tarefa pede — [`docs/context-discipline.md`](docs/context-discipline.md).

## 1. O produto

Aplicação web onde a pessoa configura um projeto .NET (nome, arquitetura, banco, autenticação,
Swagger, versão) e baixa um ZIP compilável, com exemplos funcionais e README daquela combinação.
Plataforma em Angular + ASP.NET Core; o ZIP contém só o backend .NET. **32 combinações válidas**.
Ver [`docs/product/vision.md`](docs/product/vision.md).

## 2. Invariantes

1. **A fonte da verdade é `docs/`** — não a memória de um agente, não a config de uma ferramenta. `.claude/` e `.codex/` são invólucros; ao mudar uma regra, mude o documento.
2. **`docs/backlog.json` é a única fonte de status.** Nada é "feito" fora dele.
3. **No máximo uma tarefa principal `in_progress`.**
4. **O disco manda sobre o backlog.** Divergiu, corrija o backlog primeiro e registre.
5. **Nada é commitado em `main`** — uma branch e um PR por tarefa ([ADR-0013](docs/decisions/adr-0013-branch-e-pr-por-tarefa.md)).
6. **Nada vira `done` sem evidência de execução**: comando, exit code e a saída que comprova o critério ([`conventions.md`](docs/conventions.md#evidência-em-relatório)).
7. **O `reviewer` é sempre passo separado**, depois da implementação e das verificações.
8. **Só o papel PO** edita `docs/backlog.json` e `docs/reports/` ([ADR-0004](docs/decisions/adr-0004-propriedade-do-backlog.md)).
9. **Nenhuma dependência paga, restritiva ou que exija container** ([ADR-0005](docs/decisions/adr-0005-sem-containers.md)).
10. **Informe e pare.** Nunca inicie a próxima tarefa automaticamente.

## 3. Onde achar o resto

| Preciso de… | Leia |
|---|---|
| **Executar uma tarefa, passo a passo** | [`docs/playbooks/task-execution.md`](docs/playbooks/task-execution.md) |
| **O que carregar e o que não carregar** | [`docs/context-discipline.md`](docs/context-discipline.md) |
| Commits, branch, relatório, idioma | [`docs/conventions.md`](docs/conventions.md) |
| Versões e ferramentas da máquina | [`docs/environment.md`](docs/environment.md) |
| Papéis, e o portão de branch/PR | `docs/roles/`, [`git-flow.md`](docs/roles/git-flow.md) |
| Demais procedimentos | `docs/playbooks/` |
| Visão, requisitos, as 32 combinações | `docs/product/` |
| Monorepo, contrato HTTP, motor, ZIP gerado | `docs/architecture/` |
| Direção visual e os oito estados da tela | [`docs/design/visual-spec.md`](docs/design/visual-spec.md) |
| Definição de pronto, estratégia de testes | `docs/quality/` |
| Decisões técnicas e seus porquês | `docs/decisions/` |
| Equivalência entre ferramentas | [`docs/interop.md`](docs/interop.md) |
| Relatórios, e componentes de terceiros | `docs/reports/`, [`THIRD-PARTY.md`](docs/THIRD-PARTY.md) |

**Carregue sob demanda.** A tarefa declara em `context[]` o que precisa; os `roles` apontam o
resto. Fora isso, não abra.

## 4. Como dar continuidade ao projeto

Em *"PO, execute a próxima tarefa"*, *"continue o projeto"* ou equivalente — igual no Claude Code
(`/po-next`) e no Codex:

1. **Ler o estado.** `powershell -File scripts/task-status.ps1`. Se há `in_progress`, abra o relatório dela.
2. **Inspecionar a realidade.** `git status`, `git log --oneline -10`. O disco manda.
3. **Selecionar.** Retome a `in_progress`; senão, a primeira `pending` com `dependsOn` toda `done`; senão pare e explique.
4. **Abrir.** `task-start.ps1 -Id <ID>` marca `in_progress`, preenche `startedAt` e cria o relatório; acione o `git-flow` para **criar a branch**.
5. **Carregar e executar.** Leia o `context[]` da tarefa e os `docs/roles/<papel>.md` dos papéis em `roles` — **nada além**. Delegue (Claude Code) ou assuma os papéis em sequência (Codex).
6. **Verificar.** Rode todas as `verifications`; registre comando, exit code e evidência. Depois, como passo separado, o `reviewer`.
7. **Fechar.** Só com todo `acceptanceCriteria` comprovado. `task-finish.ps1`, commit na branch, e acione o `git-flow` para **abrir o PR, julgar e mesclar**. Portão reprovado = tarefa **não** fechada.
8. **Informar e parar**, com o link do PR e qual é a próxima.

Cenários, armadilhas e proibições de cada passo estão em
[`docs/playbooks/task-execution.md`](docs/playbooks/task-execution.md). Esta seção é o contrato;
aquele arquivo é a letra miúda.

## 5. Equivalência entre ferramentas

Claude Code lê `CLAUDE.md`, `.claude/agents/*.md` e `.claude/skills/*/SKILL.md`; o Codex lê este
arquivo. Ambos são ponteiros para `docs/roles/*.md` e `docs/playbooks/*.md`, e ambos usam
`docs/backlog.json` como estado.

A única diferença de comportamento é a **delegação**: no Claude Code o PO delega a especialistas;
no Codex uma sessão única percorre os mesmos papéis, na mesma ordem, lendo os mesmos documentos.
**Critérios de aceite, verificações, relatório e commit não mudam** — se o resultado diferir nisso,
é defeito. Checklist de paridade em [`docs/interop.md`](docs/interop.md).
