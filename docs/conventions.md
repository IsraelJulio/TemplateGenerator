# Convenções

Regras de forma: idioma, commit, branch, relatório e ciclo de sessão. Os invariantes estão em
[`../AGENTS.md`](../AGENTS.md); aqui está o detalhe que não precisa ser carregado em toda tarefa.

## Idioma

Interface, documentação, commits e relatórios em **português**. Identificadores de código, nomes de
arquivo de código, rotas e mensagens de log em **inglês**.

## Commits

```
<ID>: <resumo no imperativo>
```

Exemplo: `T01: criar estrutura do monorepo`. Um commit por entrega coerente; **não** acumule a
tarefa inteira num commit só — o PR preserva todos eles, e é essa granularidade que
[ADR-0013](decisions/adr-0013-branch-e-pr-por-tarefa.md) protege.

Mudança que não é tarefa do backlog (manual de operação, `docs/`, `.claude/`, `.codex/`) usa o slug
no lugar do ID: `adr-0014: …`.

## Branch

Uma por tarefa — `feat/<ID>-<slug>`, ou `fix/` quando a tarefa é correção. Nasce no passo 4, morre
no merge do passo 7. **Nada é commitado direto em `main`**; `main` só recebe merge commit de pull
request aprovado. Falha do `gh` ou do remoto é bloqueio, não permissão para voltar a commitar em
`main`.

Mudança que não é tarefa do backlog: `chore/<slug>`. Ela também passa por PR, com o portão
adaptado descrito em [`playbooks/git-flow.md`](playbooks/git-flow.md).

## Pull request

Um por tarefa, mesclado com `--merge` (nunca `--squash`, que apagaria os commits individuais
exigidos acima). O autor não pode aprovar o próprio PR no GitHub — o veredito do `git-flow` é um
comentário estruturado e **o merge é o registro da aprovação**.

## Relatório de execução

Todo relatório em `docs/reports/<ID>.md` segue [`reports/_template.md`](reports/_template.md) e
precisa conter, no mínimo:

- o que foi feito e os arquivos tocados;
- **a evidência de cada verificação** (ver abaixo);
- o parecer do `reviewer`;
- a checagem item a item dos critérios de aceite, cada um com a evidência que o comprova;
- o veredito do portão do `git-flow` e o link do PR.

### Evidência em relatório

Um relatório sem evidência de execução não fecha tarefa. Evidência é:

1. **O comando, literal**, como foi executado.
2. **O exit code.**
3. **A saída que comprova o critério** — o resultado do teste, o hash, a contagem, a linha de erro.

Quando o comando produz centenas ou milhares de linhas, o relatório carrega o **trecho probatório**
e uma referência ao log completo, não o despejo inteiro. O que não pode faltar é o exit code e a
parte que sustenta a afirmação.

> **O que isto não autoriza.** Resumir não é parafrasear. "Os testes passaram" não é evidência;
> `Passed! - Failed: 0, Passed: 147` com exit code 0 é. Se o critério fala de um número, o número
> aparece. Se fala de determinismo, os dois hashes aparecem. Na dúvida entre cortar e manter,
> mantenha — [`context-discipline.md`](context-discipline.md#6-onde-não-cortar) diz onde não cortar.

Capturas de tela e logs grandes vão para `docs/reports/<ID>/`, referenciados pelo relatório.

## Uma tarefa, uma sessão

Sempre que possível, **uma tarefa se conclui numa sessão**. Ao encerrar, tudo que a próxima sessão
precisa tem de estar persistido no repositório:

- estado no `docs/backlog.json`;
- relatório em `docs/reports/<ID>.md`;
- commits na branch da tarefa;
- PR aberto, julgado e mesclado.

**O histórico da conversa não é estado.** A próxima sessão começa lendo o disco, e precisa
conseguir retomar sem nenhum conhecimento do que foi conversado antes. Se algo só existe no
diálogo, ele será perdido — escreva no relatório.

Tarefa que não coube numa sessão termina com o relatório dizendo, explicitamente, o que já está
feito e qual é o próximo passo.

## Arquivos

- **Arquivos temporários** nunca no repositório. Use o diretório de trabalho temporário da sessão.
- **Sem segredos** em arquivo versionado, inclusive nos ZIPs gerados.
