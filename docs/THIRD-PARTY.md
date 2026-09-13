# Componentes de terceiros

Tudo gratuito e de licença permissiva (RNF-09). Toda entrada carrega **origem, versão ou commit,
e licença**. Dependência nova sem linha aqui é reprovada pelo `reviewer`.

## Skills

### `frontend-design` — Anthropic

- **Origem:** `anthropics/skills`, caminho `skills/frontend-design`
- **Commit fixado:** `41bbe19d1a1a7eaab5e7bb9050a417e5c6cffc8f`
- **Local:** `.claude/skills/frontend-design/`
- **Licença:** preservada em `LICENSE.txt` junto aos arquivos
- **Arquivos:** `SKILL.md` (9.390 B), `LICENSE.txt` (10.174 B) — tamanhos conferidos contra a API
  do GitHub no momento da cópia
- **Uso:** método de design para o papel `frontend`, em conjunto com `docs/design/visual-spec.md`

### `senior-frontend` — davila7

- **Origem:** `davila7/claude-code-templates`, caminho
  `cli-tool/components/skills/development/senior-frontend`
- **Commit fixado:** `45291d0c56fa0a5a96177f98573bacc12dc74774`
- **Local:** `.claude/skills/senior-frontend/`
- **Licença:** MIT (Daniel "San" Ávila), preservada em `LICENSE`
- **Copiado:** `SKILL.md` e os três arquivos de `references/`

> ⚠️ **Não use como guia de implementação neste projeto.**
>
> Dois motivos, ambos verificados na cópia:
>
> 1. **Stack errada.** A skill é orientada a React, Next.js e Tailwind, e recomenda Docker,
>    Kubernetes e Terraform. Aqui a stack é Angular standalone com CSS próprio, e o projeto é
>    explicitamente sem containers ([ADR-0005](decisions/adr-0005-sem-containers.md)).
> 2. **Conteúdo de referência é placeholder.** Os três arquivos de `references/` têm ~1,6 KB cada
>    e contêm texto genérico do tipo "Pattern 1: Best Practice Implementation", "Scenario 1",
>    "// Implementation details". Não há conteúdo aproveitável.
>
> Os três scripts Python (`component_generator.py`, `bundle_analyzer.py`,
> `frontend_scaffolder.py`) **não foram copiados**: são scaffolders de componentes React, e trazer
> código executável não revisado que gera a stack errada não tem uso aqui.
>
> A skill fica no repositório porque foi pedida no plano do projeto. A recomendação registrada é
> **removê-la**; `frontend-design` + `docs/design/visual-spec.md` cobrem o papel `frontend`.

## Fontes

Fixadas em T02. Hospedadas localmente, **sem CDN**, em `src/web/public/fonts/`, com o texto da
licença ao lado dos arquivos. Ambas são variáveis (eixo de peso 400–700), no subset **latino**
(`U+0000–00FF` e afins), que cobre todos os acentos do português.

### Newsreader

- **Origem do desenho:** `productiontype/Newsreader`
- **Origem do binário:** Google Fonts, família `Newsreader`, **versão `v26`** do catálogo, subset
  `latin` — é o corte que a API do Google serve já em `woff2` variável
- **Commit de origem do texto da licença:** `cfcb4f7af0e52c25e8df2a2431814c8e5fe2e155`
- **Local:** `src/web/public/fonts/newsreader/`
- **Licença:** SIL Open Font License 1.1, preservada em `OFL.txt` (4.394 B) — texto conferido na
  cópia: *"Copyright 2020 The Newsreader Project Authors"*
- **Arquivo:** `newsreader-latin-variable.woff2` (132.000 B)
  - `sha256 6e4f2958c3a7c4a80acde4e5a679abe7e01bc1e30b92be3c7a8b696ef401d101`
  - `sha256 fdfad38143ec470553cae82a1e45320bdd1b9ec70415d37bd0171051d8a4ded8` (`OFL.txt`)
- **Uso:** títulos, rótulos de campo e nome do projeto no resumo

### Public Sans

- **Origem do desenho:** `uswds/public-sans`
- **Origem do binário:** Google Fonts, família `Public Sans`, **versão `v21`** do catálogo, subset
  `latin`
- **Commit de origem dos textos de licença:** `d3df3455fb94643925f816276e81b231bc31619f`
- **Local:** `src/web/public/fonts/public-sans/`
- **Licença:** SIL Open Font License 1.1, preservada em `OFL.txt` (4.390 B) — texto conferido na
  cópia: *"Copyright 2015 The Public Sans Project Authors"*
- **Arquivo:** `public-sans-latin-variable.woff2` (26.832 B)
  - `sha256 5ed4d31c988e73b258894244f209069ebe77dc7e564861954b21198b6de90d68`
  - `sha256 157a9e77f7580246e97c769490e2e977ae94399f9d30f4556015c41fe8c28bac` (`OFL.txt`)
  - `sha256 82f0d3cad45f264192db156360b4a710fe7060885f6aa261e6539f13cb9eb0d9` (`LICENSE.md`)
- **Uso:** corpo, controles, resumo e árvore de estrutura

> **Por que Public Sans carrega dois arquivos de licença.** O `LICENSE.md` do repositório de
> origem (6.709 B) foi copiado junto porque diz uma coisa que o `OFL.txt` sozinho não diz: a
> família é uma *Modified Version* da Libre Franklin, e as modificações da GSA, por serem obra do
> governo dos EUA, estão em **CC0 1.0**, não sob a OFL. O próprio documento conclui, com estas
> palavras, que na prática o uso se dá **sob a OFL 1.1** — que é a licença registrada acima. Sem o
> `LICENSE.md`, um auditor futuro veria só metade da procedência.

Nenhuma das duas entra em ZIP gerado: são da plataforma, não dos projetos gerados.

## Pacotes NuGet — plataforma

Fixados em T01, no `Directory.Packages.props`. Versões exatas, sem intervalo nem curinga
(RNF-06), conferidas pelo `reviewer`:

| Pacote | Versão | Licença |
|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 | MIT |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | MIT |
| `Microsoft.NET.Test.Sdk` | 18.10.0 | MIT |
| `xunit.v3` | 3.2.2 | Apache-2.0 |
| `xunit.runner.visualstudio` | 3.1.5 | Apache-2.0 |

## Pacotes NuGet — projetos gerados

Entram a partir de T03 e **não** aparecem no `Directory.Packages.props`: são versões literais nos
`.csproj` compostos pelos templates, porque o ZIP precisa ser autocontido. Licenças a confirmar
ao fixar cada versão:

| Pacote | Versão prevista | Licença |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.12 | MIT |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | PostgreSQL License |
| `Swashbuckle.AspNetCore` | 10.2.3 | MIT |

## Pacotes npm — plataforma

506 pacotes no `package-lock.json`, contando transitivas. Auditoria do lockfile, **reexecutada em
T03** — eram 502 até a entrada das quatro dependências de teste ponta a ponta da seção seguinte:

| Licença | Pacotes |
|---|---|
| MIT | 432 |
| ISC | 25 |
| BSD-2-Clause | 12 |
| **MPL-2.0** | **12** |
| Apache-2.0 | 13 |
| BSD-3-Clause | 6 |
| MIT-0 (2) / CC-BY-4.0 / CC0-1.0 / BlueOak-1.0.0 / 0BSD | 6 |
| **Total** | **506** |

A variação de T01 para T03 é exatamente `+3` Apache-2.0 (os três pacotes do Playwright) e `+1` MIT
(`fflate`).

**Nenhum pacote sem licença declarada.** A coluna conta **pacotes**, não nomes de licença — a
última linha agrupa 5 licenças distintas em 6 pacotes, porque `MIT-0` aparece duas vezes. A
tabela tem de somar exatamente o total; se não somar, está errada.

### Decisão sobre os 12 MPL-2.0

Os doze são `lightningcss` 1.33.0 e seus binários por plataforma, todos marcados `dev` no
lockfile — fazem parte do toolchain de build do Angular.

MPL-2.0 é *copyleft fraco*, com obrigação **por arquivo**: recai sobre modificações nos arquivos
do próprio `lightningcss`, não sobre código que apenas o usa como ferramenta. Nós não o
modificamos, não o redistribuímos e ele não entra em nenhum ZIP gerado.

**Decisão: aceito**, como **exceção explícita à RNF-09** — não como reclassificação da MPL-2.0
como permissiva, que ela não é. Um copyleft fraco restrito a arquivos de uma ferramenta de build
que não redistribuímos não cria obrigação sobre este projeto.

> **Esta decisão expira se a plataforma passar a ser redistribuída junto com suas dependências**
> (um pacote incluindo `node_modules`, uma imagem, um instalador). Ela se apoia inteiramente em
> "não redistribuímos", e o ZIP gerado — único canal de distribuição do projeto — não contém
> `lightningcss`. Se essa premissa mudar, a decisão precisa ser refeita, não herdada.

> ⚠️ **502 dependências transitivas não se mantêm à mão.** Esta tabela é um retrato de T01. A
> manutenção precisa virar script de auditoria que **falhe** diante de licença fora da lista
> permitida — está registrado como tarefa em `backlog.json`.

## Pacotes npm — teste ponta a ponta

Entraram em **T03**, para cumprir o critério de aceite que exige o download exercitado pela tela
**sem dublê de rede**. Todos são `devDependencies`, fixados por versão exata, e **nenhum deles entra
em ZIP gerado**.

| Pacote | Versão | Origem | Licença | Texto da licença |
|---|---|---|---|---|
| `@playwright/test` | 1.63.0 | `registry.npmjs.org`, Microsoft | Apache-2.0 | `node_modules/@playwright/test/LICENSE` |
| `playwright` | 1.63.0 | idem | Apache-2.0 | `node_modules/playwright/LICENSE` |
| `playwright-core` | 1.63.0 | idem | Apache-2.0 | `node_modules/playwright-core/LICENSE` |
| `fflate` | 0.8.3 | `registry.npmjs.org`, Arjun Barrett | MIT | `node_modules/fflate/LICENSE` |

**São quatro linhas para duas dependências declaradas.** `playwright` e `playwright-core` entram
**por `@playwright/test`**, não por escolha própria — ficam registrados para que a próxima auditoria
não os trate como sobra. `fflate` não tem nenhuma dependência transitiva.

Por que cada um: o Playwright é a ferramenta que `docs/quality/test-strategy.md` já nomeava para o
fluxo ponta a ponta; `fflate` **abre o ZIP baixado dentro do teste**, e sem ele a verificação
conferiria apenas o cabeçalho do arquivo, enquanto o critério pede inspecionar o conteúdo.

**ADR-0005 não é violada.** O Playwright roda nativo e sobe os dois servidores com `dotnet run` e
`npm start`; não há container em passo nenhum. A ressalva é de **procedência de binário**, não de
container — ver a linha do Chromium na seção seguinte.

## Ferramentas do ambiente

PostgreSQL 18 e ripgrep são pré-requisitos do ambiente, não dependências redistribuídas — entram
no README da raiz, não aqui.

**Chromium, baixado pelo Playwright.** `npm run e2e:install` executa `playwright install chromium`,
que puxa um binário de navegador de `cdn.playwright.dev` para fora do repositório
(`%LOCALAPPDATA%\ms-playwright`). Cai na mesma categoria: pré-requisito de ambiente, não
redistribuído, e nada dele entra em ZIP gerado.

Fica registrado aqui — e não só na tabela de comandos de `src/web/README.md` — porque é **download
de binário de terceiro**, que alguém vai querer auditar, e porque quem lê esta página é justamente
quem faz essa pergunta. Chromium é BSD-3-Clause com componentes de licenças permissivas adicionais;
o Playwright distribui uma compilação própria.

Nota de ambiente, verificada em T03: atrás do proxy corporativo o download falha com
`UNABLE_TO_GET_ISSUER_CERT_LOCALLY`, porque o Node não lê a loja de certificados do Windows por
padrão. A saída é `NODE_OPTIONS=--use-system-ca` — **sem desligar verificação de certificado**.
