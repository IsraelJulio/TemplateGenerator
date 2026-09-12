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
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.12 | NuGet |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | NuGet |
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 | NuGet |
| `Swashbuckle.AspNetCore` | 10.2.3 | NuGet, **apenas UI** — ver [ADR-0001](../decisions/adr-0001-swagger.md) |
| PostgreSQL | 18, serviço nativo `postgresql-x64-18` | instalado |

Versões **exatas**, sem intervalo e sem `*`. Um `Directory.Packages.props` centraliza as versões
da plataforma. Os projetos gerados carregam versões literais nos `.csproj` — eles precisam ser
autocontidos.

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
