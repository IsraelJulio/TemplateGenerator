# CLAUDE.md

**Leia [`AGENTS.md`](AGENTS.md) primeiro.** Ele é o manual de operação canônico deste projeto e
vale igualmente para Claude Code e Codex. Este arquivo contém apenas o que é específico do
Claude Code.

Não duplique regras aqui. Se uma regra mudar, mude em `AGENTS.md` ou no documento
correspondente em `docs/`.

## Específico do Claude Code

- **Agente padrão:** `po`, definido em `.claude/settings.json`. Alternativa explícita:
  `claude --agent po`.
- **Papéis:** `.claude/agents/*.md`. Cada um é um invólucro fino sobre `docs/roles/<papel>.md`,
  que é onde a responsabilidade está realmente descrita.
- **Skills locais:** `po-next`, `git-flow`, `dotnet-templates`, `verify-mvp`. Invólucros sobre
  `docs/playbooks/*.md`. Acessíveis como `/po-next` etc.
- **Skills de terceiros:** `frontend-design` (Anthropic) e `senior-frontend` (davila7), ambas
  fixadas em commit e com licença preservada. Procedência e ressalvas em
  [`docs/THIRD-PARTY.md`](docs/THIRD-PARTY.md) — **leia antes de usar `senior-frontend`.**
- **Delegação:** o PO delega aos especialistas. Especialistas **não** criam novas cadeias de
  agentes. Especialistas herdam o modelo da sessão (`model: inherit`).
- **`git-flow`:** agente de branch, pull request e merge, acionado **só pelo PO**, duas vezes por
  tarefa (ADR-0013). É o único sem ferramenta de escrita — de propósito, para não conseguir
  consertar código e fazer o próprio portão passar.

## Atalho mental

"PO, execute a próxima tarefa" → siga a seção 4 de `AGENTS.md`, sem exceção.
