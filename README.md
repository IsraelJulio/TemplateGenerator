# TemplateGenerator

Configure um projeto .NET em uma tela e baixe um ZIP compilável, com exemplos funcionais e um
README específico para aquela combinação.

**Estado atual:** preparação concluída (T00). A implementação começa em T01 — ainda não há
código de aplicação no repositório.

## Para pessoas

- O que é e por quê: [`docs/product/vision.md`](docs/product/vision.md)
- O que exatamente se pode escolher: [`docs/product/option-matrix.md`](docs/product/option-matrix.md)
- Como o projeto é conduzido: [`AGENTS.md`](AGENTS.md)
- Por que as decisões foram tomadas assim: [`docs/decisions/`](docs/decisions/)

## Para agentes

Leia [`AGENTS.md`](AGENTS.md). É o manual de operação canônico e vale tanto para Claude Code
quanto para Codex.

Para continuar o projeto, abra uma sessão na raiz do repositório e diga:

> PO, execute a próxima tarefa.

O estado do trabalho está em [`docs/backlog.json`](docs/backlog.json). Equivalência entre as duas
ferramentas: [`docs/interop.md`](docs/interop.md).

## Pré-requisitos do ambiente

| Ferramenta | Versão | Obrigatória |
|---|---|---|
| .NET SDK | 10.0.302 | sim |
| `dotnet-ef` | 10.0.12 (`dotnet tool install --global dotnet-ef`) | sim, a partir de T05 |
| Node | 24.18.1 | sim |
| Angular CLI | 22.1.2 | sim |
| PostgreSQL | 18, serviço nativo em execução | sim, a partir de T05 |
| git | 2.55+ | sim |
| ripgrep | qualquer, **no PATH do Windows** | recomendado (busca do Codex) |

**Não é necessário Docker nem Podman.** O projeto é deliberadamente livre de containers — ver
[ADR-0005](docs/decisions/adr-0005-sem-containers.md).

O Playwright baixa navegadores na primeira execução (algumas centenas de MB) — conta como
dependência de rede a partir de T09.

### Se você clonou em outro caminho

A camada `.codex/config.toml` só carrega se o diretório estiver marcado como *trusted* no
`~/.codex/config.toml`. Sem isso ela é ignorada **em silêncio** e os `dotnet restore` voltam a
pedir aprovação. Ver [`docs/interop.md`](docs/interop.md), seção 3.

## Como rodar

A ser preenchido em T01 e verificado em T11, num checkout limpo.
