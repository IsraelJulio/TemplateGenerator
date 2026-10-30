# TemplateGenerator

Configure um projeto .NET em uma tela e baixe um ZIP compilável, com exemplos funcionais e um
README específico para aquela combinação.

**Estado atual:** MVP completo. A tela oferece as 32 combinações válidas (arquitetura, banco,
autenticação, Swagger), a API gera o ZIP de forma determinística e cada ZIP traz o README com os
comandos exatos daquela combinação. Tudo roda localmente — não há hospedagem pública.

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

| Ferramenta | Versão | Para quê |
|---|---|---|
| .NET SDK | **10.0.302** (fixado em `global.json`, aceita só patches 10.0.3xx) | API geradora e testes |
| Node | 24.18.1 | tela Angular |
| npm | 11.16.0 | tela Angular (`packageManager` em `src/web/package.json`) |
| git | 2.55+ | clonar |
| PostgreSQL | 18, **serviço nativo** em execução, com `psql` instalado | testes da camada 3 e projetos gerados com `postgresql` |
| Navegador Chromium do Playwright | baixado por `npm run e2e:install` | só para o teste ponta a ponta |
| ripgrep | qualquer, **no PATH do Windows** | opcional (busca do Codex) |

Não é preciso instalar Angular CLI nem `dotnet-ef` globalmente: o Angular CLI vem pelo `npm ci`
e os projetos gerados que usam migração trazem o `dotnet-ef` num manifesto local
(`dotnet tool restore`, indicado no README de cada ZIP).

**Não é necessário Docker nem Podman.** O projeto é deliberadamente livre de containers — o
PostgreSQL é o serviço nativo e o provedor OIDC dos testes roda dentro do processo de teste. Ver
[ADR-0005](docs/decisions/adr-0005-sem-containers.md).

**Downloads de rede na primeira execução:** pacotes NuGet (`dotnet restore`), pacotes npm
(`npm ci`) e o Chromium do Playwright (`npm run e2e:install`, algumas centenas de MB).

### Se `dotnet` disser "A compatible .NET SDK was not found"

O `global.json` exige o 10.0.302 (ou um patch 10.0.3xx). Se a máquina só tiver outra faixa — por
exemplo 10.0.4xx —, instale o 10.0.302 por usuário com o
[`dotnet-install.ps1`](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script)
oficial e ponha essa pasta à frente do `PATH`, com `DOTNET_ROOT` apontando para ela. Confira com
`dotnet --version` na raiz do repositório. Detalhes em [`docs/environment.md`](docs/environment.md).

### Se você clonou em outro caminho

A camada `.codex/config.toml` só carrega se o diretório estiver marcado como *trusted* no
`~/.codex/config.toml`. Sem isso ela é ignorada **em silêncio** e os `dotnet restore` voltam a
pedir aprovação. Ver [`docs/interop.md`](docs/interop.md), seção 3.

## Como rodar

São **dois processos**, em dois terminais, ambos a partir da raiz do repositório. O frontend fala
com a API por um proxy, então a API precisa estar no ar primeiro.

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

### 3. Gerar um projeto

Na tela, preencha o nome, escolha arquitetura, banco, autenticação, Swagger e versão, e clique em
**Gerar projeto**. O navegador baixa `<NomeDoProjeto>.zip`. Extraia e siga **somente** o
`README.md` que está dentro dele: ele traz os pré-requisitos e os comandos daquela combinação —
migração, execução, como obter um token quando há autenticação.

## Testes

Pré-requisito de todos os comandos `dotnet` abaixo: o SDK da seção anterior ativo na raiz.

```bash
dotnet build                 # zero erro, zero warning (TreatWarningsAsErrors está ligado)
dotnet test                  # camadas 1, 2 e 3 da matriz, API e motor
cd src/web && npm test       # verificações de versão e licença, depois os testes da tela
```

A **camada 3** sobe projetos gerados contra o PostgreSQL nativo, criando e derrubando um database
descartável por teste. A credencial vem do ambiente, pela convenção do `psql` — nunca de arquivo:

```bash
PGUSER=postgres PGPASSWORD=<sua senha> dotnet test
```

Sem `PGPASSWORD`, ou com o serviço parado, esses testes **falham** dizendo o que fazer; não são
pulados. O `psql` não precisa estar no `PATH`: os testes o procuram também na instalação padrão,
`C:\Program Files\PostgreSQL\<versão>\bin`. Para usá-lo você mesmo no terminal, ponha essa pasta
no `PATH`.

A **camada 4** compila as 32 combinações e fica fora do `dotnet test` normal, por ser longa. Rode
explicitamente:

```bash
dotnet test tests/TemplateGenerator.Matrix.Tests --filter "Camada=4" -- xUnit.Explicit=on
```

O teste **ponta a ponta** sobe a API e a tela sozinho (ou reaproveita as que já estiverem no ar),
preenche o formulário, baixa o ZIP e inspeciona o conteúdo, em desktop e celular:

```bash
cd src/web
npm run e2e:install          # uma vez por máquina: baixa o Chromium
npm run e2e
```

Duas linhas da saída do `npm run e2e` aparecem marcadas com `x` e "(falha de propósito)": são
testes que **precisam** falhar para provar que a verificação tem dentes (`test.fail` do
Playwright), e contam como aprovados. O que vale é o resumo `N passed` e o exit code 0.

O `npm ci` pode imprimir avisos `npm warn allow-scripts … npm approve-scripts`. Eles não impedem
nada: a tela sobe e os testes passam sem aprovar script algum.

Estratégia completa em [`docs/quality/test-strategy.md`](docs/quality/test-strategy.md).
