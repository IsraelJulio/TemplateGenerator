# ADR-0005 — Sem containers; Podman opcional

**Data:** 2026-09-12 · **Estado:** aceita

## Contexto

O ambiente corporativo **não permite Docker**. Permite Podman, que não está instalado
(disponível via `winget install RedHat.Podman`).

Os dois usos que normalmente pediriam container são:

- **PostgreSQL** para os testes de persistência.
- **Provedor OIDC** para os testes de JWT externo.

Verificado no ambiente: o PostgreSQL **18** já roda como serviço nativo (`postgresql-x64-18`,
estado `RUNNING`), com `psql` em `C:\Program Files\PostgreSQL\18\bin`.

## Decisão

**Nenhuma tarefa do backlog pode depender de container.**

- PostgreSQL → serviço nativo já instalado, com database descartável por teste.
- OIDC → emissor in-process, ver [ADR-0006](adr-0006-oidc-in-process.md).

Podman fica registrado como caminho **opcional** para quem quiser isolar o PostgreSQL num outro
ambiente. Nada no backlog, nos testes ou no README pode assumi-lo.

## Alternativas descartadas

- **Instalar Podman e usar Testcontainers:** acrescentaria instalação, tempo de subida por teste
  e uma dependência que o ambiente corporativo pode restringir depois — sem ganho, já que o
  PostgreSQL nativo está disponível.

## Consequências

- Os testes de PostgreSQL dependem do serviço local estar em execução. Quando não estiver, eles
  **falham com mensagem explícita** dizendo o que fazer — nunca são pulados em silêncio, porque
  um teste pulado passa despercebido e some da cobertura.
- Reproduzir o ambiente em outra máquina exige PostgreSQL instalado. Isso entra no README e é
  verificado em T11.
