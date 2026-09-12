# Projetos gerados

> A tela já mostra uma **projeção** desta árvore, derivada no frontend enquanto não há ZIP real;
> amarrá-la ao pacote gerado — e decidir o nome da pasta do projeto Web API na arquitetura Simples —
> é obrigação de T03/T04. Ver [ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md).

## Conteúdo obrigatório de todo ZIP

- `<ProjectName>.sln`
- Projeto(s) de código conforme a arquitetura
- Um projeto de testes com testes básicos que passam
- `appsettings.json` e `appsettings.Development.json` **sem credenciais reais**
- `requests.http` com exemplos de chamada, incluindo autenticação quando houver
- `.gitignore`, `.editorconfig`, `global.json` fixando o SDK
- `README.md` específico da combinação (RF-21)
- `.templategenerator/manifest.json` (RF-22)

## Arquitetura Simples

Um projeto Web API, organizado em pastas: `Endpoints`, `Models`, `Services`, `Persistence`.

## Clean Architecture

Quatro projetos com dependências estritamente nesta direção:

```
Api ──▶ Application ──▶ Domain
 │                        ▲
 └────▶ Infrastructure ───┘
```

- `Domain` — entidades e regras. **Sem referência de projeto e sem pacote de infraestrutura.**
- `Application` — casos de uso e interfaces (portas). Depende só de `Domain`.
- `Infrastructure` — implementa as portas (persistência, identidade).
- `Api` — composição, endpoints, configuração.

Um teste arquitetural verifica essas direções (RF-11 / T04). "Referência arquitetural indevida" é
qualquer aresta fora do diagrama.

## Comportamento comum

Ambas as arquiteturas usam Minimal APIs e entregam:

- **Saúde:** `GET /health`, **sempre público**, mesmo com autenticação ligada (RF-12).
- **CRUD de `Item`:** `GET /items`, `GET /items/{id}`, `POST /items`, `PUT /items/{id}`,
  `DELETE /items/{id}`. `Item` tem identificador e `title` obrigatório.
- **Dados compartilhados** entre usuários autenticados, sem regra de proprietário (RF-15).
- **Público sem autenticação, protegido com autenticação** (RF-14): sem token ⇒ `401`;
  token válido ⇒ `200`, independentemente de claims.

## Por banco

| `database` | Armazenamento | Caminho inicial do README |
|---|---|---|
| `none` | Em memória, volátil (RF-16) | `dotnet restore` e `dotnet run` |
| `sqlite` | EF Core + SQLite, migração inicial, connection string local pronta | aplicar migração e executar |
| `postgresql` | EF Core + Npgsql, migração inicial | configurar conexão, aplicar migração, executar |

Migrações são **específicas do provider** — a migração de SQLite não serve para PostgreSQL.
Cada combinação com banco carrega a sua.

## Por autenticação

### `identity` — ASP.NET Core Identity nativo

- Usa os endpoints nativos (`MapIdentityApi`): cadastro, login bearer e renovação.
- **Não exige segredo JWT.** Os tokens emitidos são *bearer tokens próprios do Identity*, opacos
  ao consumidor — não são JWT de terceiros. O README precisa dizer isso com essas palavras, para
  não induzir alguém a procurar uma chave de assinatura que não existe.
- Envio de e-mail (confirmação, recuperação) **exige configuração adicional e não faz parte do
  fluxo garantido do MVP**. O README diz isso explicitamente.
- Exige `database ∈ {sqlite, postgresql}`.

### `jwt` — provedor externo

- Valida tokens com `Authority` e `Audience`, configurados pela pessoa desenvolvedora em
  `appsettings.json`.
- **O template não hospeda provedor de identidade** e não emite token.
- Rejeita assinatura, emissor, audiência e validade incorretos (RF-19).
- O README acrescenta um passo: apontar `Authority`/`Audience` para o provedor existente.

## Swagger

Ver [ADR-0001](../decisions/adr-0001-swagger.md). Quando `swagger = true`:

- `Microsoft.AspNetCore.OpenApi` gera o documento (nativo do .NET 10).
- `Swashbuckle.AspNetCore` fornece **apenas a interface**, consumindo esse documento.
- Ambos habilitados por padrão **só em Development**.

Quando `swagger = false`, nem o documento, nem a UI, nem as duas dependências aparecem no
`.csproj` (RF-20).

## README gerado

Precisa cobrir, para aquela combinação exata: pré-requisitos, instalação, configuração,
migrações, execução, autenticação e teste do CRUD. **Todo comando do README tem que rodar sem
modificar código-fonte** — isso é verificado na camada 3 de testes.
