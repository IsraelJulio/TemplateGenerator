# ADR-0004 — Propriedade do backlog é convenção, não trava

**Data:** 2026-09-12 · **Estado:** aceita

## Contexto

O plano dizia "somente PO atualizará o backlog e os relatórios". Não existe forma limpa de impor
isso tecnicamente: no Claude Code não se nega escrita por caminho **por agente**, e no Codex a
sessão é única — o papel PO e os demais são a mesma entidade.

Além disso, o projeto precisa ser editável pelo Codex, onde não há PO separado para impor nada.

## Decisão

**A regra vale como convenção explícita, verificada pelo `reviewer`**, não como bloqueio técnico.

- `AGENTS.md` seção 4 declara a regra e diz o que fazer quando outro papel precisa mexer no
  backlog: voltar ao papel PO explicitamente antes de editar.
- O checklist do `reviewer` inclui conferir se `docs/backlog.json` e `docs/reports/` foram
  alterados dentro do papel PO.
- No Codex, a troca de papel é anunciada no relatório, o que dá o mesmo rastro.

## Alternativas descartadas

- **Hook `PreToolUse` bloqueando escrita em `docs/backlog.json`:** funcionaria só no Claude Code,
  criaria divergência de comportamento entre as ferramentas e atrapalharia edição manual legítima.
- **`disallowedTools` nos agentes especialistas:** tiraria a ferramenta de escrita inteira, não
  só desse caminho.

## Consequências

- A garantia é processual. Um agente distraído consegue escrever no backlog — o `reviewer`
  detecta no diff.
- Em troca, o mesmo procedimento funciona igual nas duas ferramentas e você consegue editar o
  backlog à mão quando quiser, sem lutar contra um hook.
