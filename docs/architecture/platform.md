# Plataforma

## Estrutura do monorepositório

```
TemplateGenerator/
├─ AGENTS.md, CLAUDE.md, README.md
├─ .claude/, .codex/
├─ docs/
├─ src/
│  ├─ TemplateGenerator.Api/          ASP.NET Core, Minimal APIs, os dois endpoints
│  ├─ TemplateGenerator.Generation/   biblioteca: catálogo, validação, composição, ZIP
│  ├─ TemplateGenerator.Generation/Templates/   arquivos-fonte dos templates, versionados
│  └─ web/                            aplicação Angular
├─ tests/
│  ├─ TemplateGenerator.Generation.Tests/   unidade (xUnit)
│  ├─ TemplateGenerator.Api.Tests/          integração (WebApplicationFactory)
│  └─ TemplateGenerator.Matrix.Tests/       as três camadas da matriz
└─ TemplateGenerator.sln
```

`TemplateGenerator.Generation` **não** referencia ASP.NET Core nem banco de dados. É uma
biblioteca pura: entra configuração, sai um fluxo de bytes. Isso é o que permite testar as 32
combinações sem subir a API.

## Versões fixadas

| Componente | Versão | Fonte |
|---|---|---|
| .NET SDK / TFM | 10.0.302 / `net10.0` | instalado no ambiente |
| `dotnet-ef` | 10.0.12 | global tool |
| Node | 24.18.1 | instalado |
| Angular CLI | 22.1.2 | instalado |
| PostgreSQL | 18, serviço nativo `postgresql-x64-18` | instalado |

### Pacotes da plataforma (`Directory.Packages.props`, fixados em T01)

| Pacote | Versão | Licença |
|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 | MIT |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | MIT |
| `Microsoft.NET.Test.Sdk` | 18.10.0 | MIT |
| `xunit.v3` | 3.2.2 | Apache-2.0 |
| `xunit.runner.visualstudio` | 3.1.5 | Apache-2.0 |

### Pacotes dos projetos gerados (a partir de T03)

Versões literais nos `.csproj` compostos pelos templates — **fora** do `Directory.Packages.props`,
porque o ZIP precisa ser autocontido.

| Pacote | Versão | Observação |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.12 | |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | |
| `Swashbuckle.AspNetCore` | 10.2.3 | **apenas UI** — ver [ADR-0001](../decisions/adr-0001-swagger.md) |

Versões **exatas**, sem intervalo e sem `*`. Um `Directory.Packages.props` centraliza as versões
da plataforma. Os projetos gerados carregam versões literais nos `.csproj` — eles precisam ser
autocontidos, e por isso **não** aparecem no `Directory.Packages.props`.

No frontend a regra se cumpre de outra forma: lockfile versionado + `npm ci`. Ver
[ADR-0009](../decisions/adr-0009-fixacao-de-versoes-npm.md).

## Armadilha operacional: limpeza de `bin/`

Um `dotnet clean` ou uma limpeza recursiva de `bin/` e `obj/` na raiz **apaga também
`src/web/node_modules/**/bin/`**, inclusive `@angular/cli/bin/ng.js`. O sintoma é `npm test`
falhando com `MODULE_NOT_FOUND` logo depois de um build .NET, e a recuperação custa um
`npm ci` inteiro. Aconteceu em T01.

Qualquer script de limpeza no repositório **precisa excluir `node_modules`**.

## Decisões estruturais

- **Minimal APIs** na plataforma e nos projetos gerados. Sem controllers.
- **O gerador é stateless**: não tem banco, não guarda nada, não identifica ninguém.
- **Templates são arquivos versionados no repositório**, não strings dentro de código C#. Isso
  permite revisar um template por diff.
- **Angular standalone + Reactive Forms + CSS próprio.** Sem framework de UI de terceiros.
  Fontes hospedadas localmente.

## Execução local

Documentada no `README.md` da raiz e verificada em T11 num checkout limpo. O critério é que a
pessoa consiga subir a plataforma seguindo só o README, sem editar código.
