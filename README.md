# TemplateGenerator

Configure um projeto .NET em uma tela e baixe um ZIP compilável, com exemplos funcionais e um
README específico para aquela combinação.

**Estado atual:** estrutura e contrato de pé (T01). A API responde o catálogo e valida a
configuração; a tela consome o catálogo. **O motor de geração ainda não existe** — uma
configuração válida responde `501`, e isso vira o ZIP em T03.

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

São **dois processos**, em dois terminais. O frontend fala com a API por um proxy, então a API
precisa estar no ar primeiro.

### 1. API geradora

```bash
dotnet run --project src/TemplateGenerator.Api --launch-profile http
```

Sobe em `http://localhost:5080`. Confira:

```bash
curl http://localhost:5080/api/health          # {"status":"ok"}
curl http://localhost:5080/api/template-options
```

### 2. Tela

```bash
cd src/web
npm ci          # ci, não install — ver ADR-0009
npm start
```

Abre em `http://localhost:4200`. O `proxy.conf.json` encaminha `/api` para a porta 5080 — se
você mudar a porta da API, mude o proxy junto.

### Verificar

```bash
dotnet build                 # zero erro, zero warning (TreatWarningsAsErrors está ligado)
dotnet test                  # 129 testes
cd src/web && npm test       # 42 testes
```

### O que esperar hoje

Preencher a tela e clicar em **Gerar projeto** responde `501 Not Implemented`, com a explicação
no corpo. Isso é o comportamento correto de T01: a configuração é validada de verdade, mas o
motor de geração chega em T03. A tela ainda está sem acabamento visual — isso é T02.
