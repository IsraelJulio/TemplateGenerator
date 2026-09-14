# <ID> — <título da tarefa>

- **Estado:** in_progress | blocked | done
- **Início:** <ISO 8601>
- **Fim:** <ISO 8601>
- **Ferramenta:** Claude Code | Codex
- **Branch:** `feat/<ID>-<slug>`
- **Pull request:** <URL e número, preenchido no fechamento — ADR-0013>

## Objetivo

<copiado de docs/backlog.json>

## Execução por papel

### Papel: <nome>
<o que foi feito, arquivos tocados>

<repetir por papel; no Codex, anunciar a troca de papel aqui>

## Verificações

Para cada item de `verifications` da tarefa: **o comando literal, o exit code e a saída que
comprova o critério**. Quando a saída tem centenas de linhas, cole o trecho probatório e aponte o
log completo — regra em [`../conventions.md`](../conventions.md#evidência-em-relatório).

```
$ <comando>
<trecho que comprova — o resultado do teste, o hash, a contagem, a linha de erro>
exit code: <n>
```

> Resumir não é parafrasear. "Os testes passaram" não é evidência; `Passed! - Failed: 0,
> Passed: 147` com exit code 0 é. Se o critério fala de um número, o número aparece aqui.

## Critérios de aceite

| # | Critério | Evidência | OK |
|---|---|---|---|
| 1 | <critério> | <qual saída acima comprova> | ✅ / ❌ |

## Parecer do reviewer

<aprovado, ou reprovado com a lista do que falta — nunca só "está bom">

## Portão do git-flow

<veredito dos sete itens e o link do PR; se reprovou alguma vez, o que faltava e o que corrigiu>

## Bloqueios

<vazio, ou o que impede e o que seria preciso para destravar>

## Próxima tarefa

<ID e por quê — sem iniciá-la>
