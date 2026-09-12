# ADR-0001 — OpenAPI nativo + Swashbuckle apenas para a UI

**Data:** 2026-09-12 · **Estado:** aceita

## Contexto

O plano dizia, ao mesmo tempo, "usar recursos nativos quando suficientes" e "Swashbuckle para
Swagger". No .NET 10 isso é contraditório: `Microsoft.AspNetCore.OpenApi` gera o documento
OpenAPI nativamente, mas **não fornece interface de usuário**. Swashbuckle faz as duas coisas, e
usá-lo inteiro descartaria o gerador nativo sem motivo.

## Decisão

Nos projetos gerados com `swagger = true`:

- **`Microsoft.AspNetCore.OpenApi`** (10.0.12) gera o documento.
- **`Swashbuckle.AspNetCore`** (10.2.3) fornece **somente a interface**, consumindo esse documento.
- Ambos habilitados por padrão **apenas em Development**.

Com `swagger = false`, nenhum dos dois pacotes aparece no `.csproj`.

## Alternativas descartadas

- **Swashbuckle inteiro:** abandonaria o gerador nativo, que é o caminho suportado no .NET 10.
- **Nativo + Scalar:** UI mais moderna, mas acrescenta uma dependência a menos estabelecida sem
  ganho para o objetivo do MVP.
- **Nativo sem UI:** deixaria o checkbox "Swagger" entregando menos do que a pessoa espera ao
  marcá-lo.

## Consequências

- Duas dependências em vez de uma quando Swagger está ligado. O teste da camada 1 verifica que
  **ambas** somem quando desligado.
- Se o Swashbuckle atrasar em relação a uma versão futura do .NET, só a UI é afetada; o documento
  continua sendo gerado.
