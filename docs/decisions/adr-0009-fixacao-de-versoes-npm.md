# ADR-0009 — RNF-06 no npm é cumprido pelo lockfile

**Data:** 2026-09-12 · **Estado:** aceita · **Levantada por:** papel `frontend` em T01

## Contexto

RNF-06 exige dependências "fixadas por versão exata, sem intervalo". No .NET isso é literal: o
`Directory.Packages.props` carrega versões exatas.

No frontend, o Angular CLI gera `package.json` com `^` e `~` — `@angular/core: ^22.1.0`,
`typescript: ~6.0.2`. À primeira vista contraria RNF-06.

## Decisão

**No npm, RNF-06 é cumprido pelo `package-lock.json` versionado, com instalação por `npm ci`.**

- O `package.json` mantém os intervalos gerados pelo CLI.
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

## Consequências

- `package-lock.json` **nunca** pode ficar fora do commit. Um lockfile ausente ou desatualizado
  reabre a variação que RNF-06 proíbe — o `reviewer` verifica isso.
- Todo comando de instalação em documentação, verificação e T11 usa `npm ci`. `npm install` só
  ao mudar dependência de propósito.
- A distinção vale só para o npm. No .NET, e sobretudo nos `.csproj` **gerados dentro do ZIP**,
  RNF-06 continua literal: versão exata escrita no arquivo, verificada por teste da camada 1
  (ver critério de T03).
