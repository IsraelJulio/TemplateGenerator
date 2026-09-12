# ADR-0007 — Documentação neutra de ferramenta

**Data:** 2026-09-12 · **Estado:** aceita

## Contexto

O projeto precisa ser continuável tanto pelo Claude Code quanto pelo Codex, produzindo o mesmo
resultado. As ferramentas leem arquivos diferentes: Claude Code lê `CLAUDE.md`,
`.claude/agents/`, `.claude/skills/`; Codex lê `AGENTS.md` e `.codex/`. Codex não enxerga nada
sob `.claude/`.

Se o conhecimento morar nos arquivos de uma ferramenta, a outra começa cega.

## Decisão

**`AGENTS.md` é o manual canônico** e todo conteúdo real mora nele ou em `docs/`. Os arquivos de
ferramenta são invólucros finos:

- `CLAUDE.md` → ponteiro para `AGENTS.md` + o que é exclusivo do Claude Code.
- `.claude/agents/*.md` → invólucro sobre `docs/roles/*.md`.
- `.claude/skills/*/SKILL.md` → invólucro sobre `docs/playbooks/*.md`.
- `.codex/config.toml` → **só configuração de runtime**, zero conhecimento.

Configuração verificada no `.codex/config.toml` do projeto:
`project_doc_max_bytes`, `project_doc_fallback_filenames = ["CLAUDE.md"]` e
`sandbox_workspace_write.network_access = true`.

## Alternativas descartadas

- **Symlink `CLAUDE.md` → `AGENTS.md`:** no Windows exige modo desenvolvedor ou privilégio, e
  quebra ao clonar em configurações comuns.
- **Duplicar o conteúdo nos dois arquivos:** dois documentos divergem em semanas.
- **Usar `[agents]` do Codex espelhando `.claude/agents/`:** manteria duas definições de papel em
  paralelo pelo mesmo motivo que se quer evitar. Fica como evolução possível, apontando para os
  mesmos `docs/roles/*.md`.

## Consequências

- Uma diferença permanece: no Claude Code o PO delega a subagentes; no Codex a sessão percorre os
  papéis em sequência. Entrega, critérios e relatório são idênticos. Registrado em
  [`../interop.md`](../interop.md).
- A camada `.codex/` só carrega se o diretório estiver *trusted* no `~/.codex/config.toml`. Ao
  clonar em outro caminho, precisa ser marcado de novo — senão é ignorado **em silêncio**.
- O checklist de paridade de `docs/interop.md` precisa rodar sempre que um arquivo de ferramenta
  mudar.
