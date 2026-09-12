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

A definir em T02, com os arquivos e as licenças versionados junto:

| Fonte | Origem | Licença |
|---|---|---|
| Newsreader | `productiontype/Newsreader` | SIL OFL 1.1 — **confirmar o texto na cópia** |
| Public Sans | `uswds/public-sans` | SIL OFL 1.1 — **confirmar o texto na cópia** |

Ambas hospedadas localmente, sem CDN.

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

502 pacotes no `package-lock.json` (T01), contando transitivas. Auditoria do lockfile:

| Licença | Pacotes |
|---|---|
| MIT | 431 |
| ISC | 25 |
| BSD-2-Clause | 12 |
| **MPL-2.0** | **12** |
| Apache-2.0 | 10 |
| BSD-3-Clause | 6 |
| MIT-0 (2) / CC-BY-4.0 / CC0-1.0 / BlueOak-1.0.0 / 0BSD | 6 |
| **Total** | **502** |

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

## Ferramentas do ambiente

PostgreSQL 18 e ripgrep são pré-requisitos do ambiente, não dependências redistribuídas — entram
no README da raiz, não aqui.
