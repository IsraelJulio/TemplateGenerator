# ADR-0002 — Validação estratificada da matriz

**Data:** 2026-09-12 · **Estado:** aceita

## Contexto

O plano pedia "gerar, extrair, restaurar, compilar e executar as 32 combinações". Restore e build
de 32 projetos .NET, repetidos a cada rodada, levam de dezenas de minutos a horas. Um conjunto de
testes caro demais deixa de ser executado — e um teste que não roda não protege nada.

## Decisão

Quatro camadas, com custo crescente e escopo decrescente:

| Camada | Escopo | Custo | Quando |
|---|---|---|---|
| 1 — estática | **as 32** | segundos | todo commit |
| 2 — build | **~9** (pairwise, cobre todos os pares) | minutos | todo commit |
| 3 — execução | **~6** (onde há comportamento novo) | minutos | todo commit |
| 4 — build completo | **as 32** | longo | T11 e sob demanda (`--full`) |

A "matriz completa" do plano continua honrada: a camada 1 cobre as 32 sempre, e a camada 4 cobre
as 32 com compilação no aceite final.

Um `NUGET_PACKAGES` compartilhado entre gerações é obrigatório nas camadas 2 e 4.

Detalhes e o conjunto de cada camada em [`../quality/test-strategy.md`](../quality/test-strategy.md).

## Alternativas descartadas

- **32 com build sempre:** o custo tornaria o ciclo inviável e o conjunto seria abandonado.
- **Só amostragem:** perderia a garantia de que toda combinação ao menos gera um ZIP correto.

## Consequências

- Um defeito que só apareça na compilação de uma combinação fora do pairwise pode passar até T11
  ou até uma execução `--full`. Aceito conscientemente: a camada 1 já detecta erros de composição,
  dependência e estrutura, que são a maioria.
- O conjunto pairwise precisa carregar, no código, a **prova de que cobre todos os pares**. Se
  alguém acrescentar um valor a um eixo, o conjunto precisa ser recalculado.
