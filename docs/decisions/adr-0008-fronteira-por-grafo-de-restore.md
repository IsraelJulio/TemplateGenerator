# ADR-0008 — Fronteira de `Generation` verificada pelo grafo de restore

**Data:** 2026-09-12 · **Estado:** aceita · **Proposta por:** papel `architect` em T01

## Contexto

`TemplateGenerator.Generation` precisa ser uma biblioteca pura — sem ASP.NET Core e sem provider
de banco ([`../architecture/platform.md`](../architecture/platform.md)). É essa pureza que
permite testar as 32 combinações sem subir a API.

A forma óbvia de verificar isso seria inspecionar as referências do assembly compilado
(`GetReferencedAssemblies()`). O problema: o compilador **elimina referências não usadas**. Uma
`PackageReference` declarada no `.csproj` e ainda não consumida em código compila sem aviso e
**não aparece** no assembly. Esse é justamente o caso realista de erosão — alguém adiciona o
pacote "para usar daqui a pouco" e a fronteira cai em silêncio.

## Decisão

O teste arquitetural lê **três fontes**, e a decisiva é o grafo de restore:

1. O `.csproj` como **XML** — SDK usado e `FrameworkReference` declaradas.
2. `obj/project.assets.json` — o grafo de restore completo, com toda a transitividade.
3. As referências do assembly compilado.

A leitura do grafo é dirigida a `libraries`, `targets`, `projectFileDependencyGroups` e
`project.frameworks.*.{dependencies,frameworkReferences}` — deliberadamente **não** a
`centralPackageVersions` (que é o catálogo do `Directory.Packages.props`, não dependência de
ninguém) nem a `packagesToPrune`.

## Prova de que funciona

Executada em T01, com as duas violações introduzidas de propósito:

| Violação | Detectada por |
|---|---|
| `<FrameworkReference Include="Microsoft.AspNetCore.App" />` | `.csproj` + grafo |
| `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />`, **sem uso em código** | **só o grafo** |

A segunda é a que justifica a decisão: `GetReferencedAssemblies()` **não** teria falhado nela.
Saídas completas em [`../reports/T01.md`](../reports/T01.md).

## Alternativas descartadas

- **Só referências do assembly compilado:** não detecta dependência declarada e não usada — a
  prova B passaria.
- **Só inspeção do `.csproj`:** não detecta dependência **transitiva** proibida.
- **Comentário no `.csproj` documentando a regra:** não é verificação.

## Consequências

- O teste depende de `obj/project.assets.json`, **formato interno do NuGet**. Se mudar, o teste
  precisa ser reescrito.
- Mitigação obrigatória: um **assert de sanidade** — se a leitura vier vazia, o teste **falha**
  dizendo que precisa ser reescrito, em vez de passar sem ter olhado nada. Um verificador que
  silenciosamente para de verificar é pior que nenhum.
- O teste exige que o projeto tenha sido restaurado antes de rodar.
