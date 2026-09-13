# ADR-0009 — RNF-06 no npm é cumprido pelo lockfile

**Data:** 2026-09-12 · **Estado:** aceita · **Levantada por:** papel `frontend` em T01

## Contexto

RNF-06 exige dependências "fixadas por versão exata, sem intervalo". No .NET isso é literal: o
`Directory.Packages.props` carrega versões exatas.

No frontend, o Angular CLI gera `package.json` com `^` e `~` — `@angular/core: ^22.1.0`,
`typescript: ~6.0.2`. À primeira vista contraria RNF-06.

## Decisão

**No npm, RNF-06 é cumprido pelo `package-lock.json` versionado, com instalação por `npm ci`.**

- O `package.json` mantém os intervalos gerados pelo CLI, **dentro do escopo definido abaixo**.
- O `package-lock.json` **é versionado** e fixa a árvore inteira, incluindo transitivas.
- Instalação em verificação e em ambiente limpo usa **`npm ci`**, nunca `npm install` — `ci`
  instala exatamente o lockfile e falha se ele divergir do `package.json`.
- Atualizar dependência é uma mudança deliberada, visível no diff do lockfile.

## Por quê

Fixar exato no `package.json` sem lockfile é **pior**, não melhor: congela o nível de cima e
deixa as transitivas livres. O lockfile fixa a árvore toda, que é o que reprodutibilidade
significa. Além disso, `ng update` depende dos intervalos para funcionar; removê-los quebraria o
caminho suportado de atualização do Angular sem ganho real.

O espírito de RNF-06 é **"a mesma instalação em qualquer máquina"**, não "a string no manifesto
não tem circunflexo". O lockfile entrega isso; o pinning manual não.

## Escopo do intervalo — precisado em T03

A decisão acima nunca disse *quais* dependências ficam com intervalo, e em T03 a omissão apareceu:
o `frontend` acrescentou `@playwright/test` e `fflate`, fixou os dois em versão exata por instrução
do PO, e registrou a tensão com esta ADR em vez de escolher em silêncio — comportamento correto.
A regra que faltava, agora escrita:

**Versão exata por padrão. O intervalo é a exceção, e existe por um motivo só: `ng update`.**

O argumento de "Por quê" é bom mas é **estreito** — ele defende o intervalo porque `ng update`
depende dele para migrar o Angular. Onde `ng update` não atua, o intervalo não compra nada: não há
caminho de atualização assistida para ele proteger, e a faixa aberta só aumenta o que o lockfile
precisa prender de volta.

**Conservam intervalo** (o conjunto que `ng update` migra, e só ele):

`@angular/*`, `@angular-devkit/*`, `@schematics/*`, `typescript`, `rxjs`, `zone.js`, `tslib`.

**Tudo o mais fica exato**, sem `^` e sem `~`. Hoje isso inclui `@playwright/test` e `fflate` (já
exatos) e alcança `jsdom`, `prettier` e `vitest`, que ainda estão com `^` — a correção é pequena e
cabe ao papel `frontend` na próxima tarefa que tocar o `package.json`.

Acrescentar um nome à lista de exceções é **edição deliberada desta ADR**, não inferência: a lista
existe justamente para não crescer sozinha.

O lockfile e o `npm ci` continuam obrigatórios nos dois casos — eles são o que fixa as transitivas,
e nenhuma quantidade de pinning no `package.json` substitui isso.

**Verificação:** uma checagem de que toda dependência do `package.json` fora da lista de exceções
tem versão exata. É trabalho de `frontend`/`qa`; sem ela, esta seção é comentário, não regra — e
uma regra de versão sem verificação é exatamente o que RNF-06 existe para não aceitar.

## Consequências

- `package-lock.json` **nunca** pode ficar fora do commit. Um lockfile ausente ou desatualizado
  reabre a variação que RNF-06 proíbe — o `reviewer` verifica isso.
- Todo comando de instalação em documentação, verificação e T11 usa `npm ci`. `npm install` só
  ao mudar dependência de propósito.
- A distinção vale só para o npm. No .NET, e sobretudo nos `.csproj` **gerados dentro do ZIP**,
  RNF-06 continua literal: versão exata escrita no arquivo, verificada por teste da camada 1
  (ver critério de T03).
