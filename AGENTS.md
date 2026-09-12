# TemplateGenerator — Manual de Operação

> **Este é o documento canônico de operação do projeto.** Vale para qualquer agente, em
> qualquer ferramenta (Claude Code, Codex ou outra). `CLAUDE.md` aponta para cá.
> Se houver conflito entre este arquivo e qualquer configuração de ferramenta, **este arquivo vence**.

## 1. O produto em um parágrafo

Aplicação web onde a pessoa configura um projeto .NET (nome, arquitetura, banco, autenticação,
Swagger, versão) e baixa um ZIP compilável, com exemplos funcionais e um README específico para
aquela combinação. A plataforma geradora é Angular + ASP.NET Core; o ZIP entregue contém apenas
o backend .NET. São **32 combinações válidas**.

Detalhes em [`docs/product/vision.md`](docs/product/vision.md) e
[`docs/product/requirements.md`](docs/product/requirements.md).

## 2. Regra de ouro

**A fonte da verdade é `docs/`, não a memória de nenhum agente e não a configuração de nenhuma
ferramenta.** Arquivos em `.claude/` e `.codex/` são invólucros finos que apontam para `docs/`.
Ao mudar uma regra, mude o documento em `docs/` — nunca só o invólucro.

Estado do trabalho: **`docs/backlog.json` é a única fonte de status.** Nada é "feito" fora dele.

## 3. Mapa dos documentos

| Preciso de… | Leia |
|---|---|
| Visão, escopo, premissas | `docs/product/vision.md` |
| Requisitos funcionais | `docs/product/requirements.md` |
| As 32 combinações e suas regras | `docs/product/option-matrix.md` |
| Monorepo, versões, estrutura | `docs/architecture/platform.md` |
| Contrato HTTP da API geradora | `docs/architecture/http-contract.md` |
| Motor de geração do ZIP | `docs/architecture/generation-engine.md` |
| O que vai dentro de cada ZIP | `docs/architecture/generated-projects.md` |
| Direção visual e estados da tela | `docs/design/visual-spec.md` |
| Definição de pronto | `docs/quality/definition-of-done.md` |
| Estratégia de testes (as 3 camadas) | `docs/quality/test-strategy.md` |
| Decisões técnicas e seus porquês | `docs/decisions/` |
| O que cada papel faz | `docs/roles/` |
| Procedimentos passo a passo | `docs/playbooks/` |
| Equivalência entre ferramentas | `docs/interop.md` |
| Relatórios de execução | `docs/reports/` |

## 4. Como dar continuidade ao projeto

Este é **o** procedimento. Vale igual no Claude Code e no Codex. Quando alguém disser
*"PO, execute a próxima tarefa"*, *"continue o projeto"* ou equivalente, faça exatamente isto:

1. **Ler o estado.** Abra `docs/backlog.json`. Abra este arquivo. Se houver tarefa
   `in_progress`, abra o relatório dela em `docs/reports/`.
2. **Inspecionar a realidade.** Rode `git status` e `git log --oneline -10`. Compare o que existe
   no disco com o que o backlog afirma. **O disco manda sobre o backlog**: se divergirem, corrija
   o backlog antes de qualquer outra coisa e registre a correção.
3. **Selecionar.** Se há tarefa `in_progress`, retome-a — não comece outra. Se não há, pegue a
   primeira `pending` cujas `dependsOn` estejam todas `done`. Se nenhuma qualificar, pare e
   explique o que bloqueia. **No máximo uma tarefa principal em andamento.**
4. **Registrar o início.** Mude o estado para `in_progress`, preencha `startedAt`, crie
   `docs/reports/<ID>.md` a partir de `docs/reports/_template.md`.
5. **Executar por papéis.** Siga `docs/roles/<papel>.md` para cada papel listado em `roles` da
   tarefa. Se a ferramenta suportar subagentes, delegue; se não, execute os papéis em sequência
   na mesma sessão. **O resultado esperado é o mesmo nos dois casos** — veja `docs/interop.md`.
6. **Verificar.** Rode tudo que estiver em `verifications` da tarefa. Cole a saída real no
   relatório. Depois execute o papel `reviewer` como passo separado, com olhar independente.
7. **Fechar.** Marque `done` apenas com os `acceptanceCriteria` **comprovados por saída de
   comando colada no relatório**. Preencha `finishedAt` e `report`. Faça commit.
8. **Informar e parar.** Diga o que terminou e qual é a próxima tarefa. **Não inicie a próxima
   automaticamente.**

### Proibições

- Não marque `done` nada que esteja apenas planejado, escrito ou "deveria funcionar".
- Não pule o passo 6 porque "é óbvio que funciona".
- Não invente critérios de aceite nem remova critérios que não conseguiu cumprir — se não deu,
  o estado é `blocked`, com o motivo em `blockers`.
- Não reescreva trabalho existente sem ler antes o que já está lá.
- Não adicione dependência paga, com licença restritiva, ou que exija container. Veja
  `docs/decisions/adr-0005-sem-containers.md`.

### Quem escreve o backlog

Por convenção, **só o papel PO edita `docs/backlog.json` e `docs/reports/`**. Isso é convenção,
não trava técnica: qualquer agente consegue escrever nesses arquivos. O papel `reviewer` verifica
o cumprimento. Se você está atuando como outro papel e precisa mudar o backlog, volte ao papel PO
explicitamente antes de editar.

## 5. Formato do relatório de execução

Todo relatório em `docs/reports/<ID>.md` segue `docs/reports/_template.md` e precisa conter,
no mínimo: o que foi feito, os arquivos tocados, **a saída real dos comandos de verificação**,
o parecer do reviewer e a checagem item a item dos critérios de aceite. Relatório sem saída de
comando colada não fecha tarefa.

## 6. Equivalência entre ferramentas

| Conceito | Claude Code | Codex | Fonte comum |
|---|---|---|---|
| Manual de operação | `CLAUDE.md` → aponta pra cá | `AGENTS.md` (este arquivo) | este arquivo |
| Papéis / especialistas | `.claude/agents/*.md` | prosa em `docs/roles/` executada em sequência | `docs/roles/*.md` |
| Procedimentos | `.claude/skills/*/SKILL.md` | prosa em `docs/playbooks/` | `docs/playbooks/*.md` |
| Agente padrão | `.claude/settings.json` → `"agent": "po"` | papel PO assumido por padrão (seção 4) | seção 4 deste arquivo |
| Configuração local | `.claude/settings.json` | `.codex/config.toml` | — |
| Estado do trabalho | `docs/backlog.json` | `docs/backlog.json` | `docs/backlog.json` |

**Consequência prática:** no Claude Code o PO delega a especialistas; no Codex uma sessão única
percorre os mesmos papéis na mesma ordem, lendo os mesmos `docs/roles/*.md`. A entrega, os
critérios de aceite e o formato do relatório **não mudam**. Detalhes e checklist de paridade em
[`docs/interop.md`](docs/interop.md).

## 7. Ambiente

Verificado em 2026-09-12, Windows 11:

| Ferramenta | Versão | Observação |
|---|---|---|
| .NET SDK | 10.0.302 | |
| `dotnet-ef` | 10.0.12 | global tool |
| Node | 24.18.1 | |
| npm | 11.16.0 | |
| Angular CLI | 22.1.2 | |
| PostgreSQL | 18 (serviço `postgresql-x64-18`) | nativo, sem container; `psql` em `C:\Program Files\PostgreSQL\18\bin` |
| git | 2.55.0 | |

**Não há Docker neste ambiente e não deve haver dependência de container.** PostgreSQL roda
nativo; o provedor OIDC dos testes roda in-process. Podman é permitido mas opcional — nada no
backlog pode depender dele.

## 8. Convenções

- **Idioma:** interface, documentação, commits e relatórios em **português**. Identificadores de
  código, nomes de arquivo de código, rotas e mensagens de log em **inglês**.
- **Commits:** `<ID>: <resumo no imperativo>`, ex. `T01: criar estrutura do monorepo`.
  Um commit por entrega coerente; não acumule a tarefa inteira em um commit só.
- **Branch:** `main`. Trabalho direto em `main` é aceitável neste projeto de uma pessoa.
- **Arquivos temporários** nunca no repositório.
- **Sem segredos** em arquivos versionados, inclusive nos ZIPs gerados.
