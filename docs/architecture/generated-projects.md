# Projetos gerados

> A tela mostra uma **projeção** desta árvore, derivada no frontend. Este documento é a fonte; a
> projeção o segue, e não o contrário. Ver
> [ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md). A regra de nomes abaixo é a
> decisão que ADR-0010 deixou para T03/T04; a **Simples** está decidida, a **Clean** não.

## Conteúdo obrigatório de todo ZIP

- `<ProjectName>.sln`
- Projeto(s) de código conforme a arquitetura
- Um projeto de testes com testes básicos que passam
- `appsettings.json` e `appsettings.Development.json` **sem credenciais reais**
- `requests.http` com exemplos de chamada, incluindo autenticação quando houver
- `.gitignore`, `.editorconfig`, `global.json` fixando o SDK
- `README.md` específico da combinação (RF-21)
- `.templategenerator/manifest.json` (RF-22)

## Nomes de projeto e de pasta

`projectName` é a raiz de tudo: da solução, dos projetos, dos namespaces e do nome do ZIP.

### Arquitetura Simples — **decidida em T03**

**O projeto Web API se chama exatamente `<ProjectName>`. Nada é acrescentado ao nome.**

```
<ProjectName>.sln
src/<ProjectName>/<ProjectName>.csproj      namespace raiz: <ProjectName>
tests/<ProjectName>.Tests/<ProjectName>.Tests.csproj
```

`Acme.Billing` ⇒ `src/Acme.Billing/`. `Acme.Billing.Api` ⇒ `src/Acme.Billing.Api/`. É exatamente o
que `dotnet new webapi -n <nome>` produziria, nos dois casos.

**Por quê, e por que isto não é uma regra de deduplicação.** ADR-0010 avisa que inventar
deduplicação de sufixo seria "trocar um palpite por outro", e o aviso está certo. A saída não é
deduplicar melhor: é **perguntar para que serve o sufixo**. Em Clean, `.Api` existe para distinguir
um dos quatro projetos irmãos — `.Api`, `.Application`, `.Domain`, `.Infrastructure`. É um
desambiguador. Na Simples **não há irmão**: há um projeto de código, e nada a desambiguar. O sufixo
não tem função, então não é acrescentado — e o `.Api` dobrado simplesmente não chega a existir,
porque nada é concatenado. Nenhuma regra condicional, nenhuma comparação de sufixo, nenhum caso
especial para `Acme.Billing.Api`.

Três consequências que confirmam a escolha:

- O nome do projeto é o nome que a pessoa digitou. Quem chamou de `Acme.Billing` recebe
  `Acme.Billing`, e não um `Acme.Billing.Api` que não pediu.
- O namespace raiz é `<ProjectName>` e o token `__ProjectName__` basta para tudo. Não nasce um
  segundo marcador nem uma regra em C# para calculá-lo
  ([`generation-engine.md`](generation-engine.md)).
- Um único projeto de código chamado `<ProjectName>` e um de teste chamado `<ProjectName>.Tests`
  cobrem o ZIP inteiro, sem colisão de nome.

**A assimetria com `.Tests` é deliberada.** `.Tests` é acrescentado sempre, porque ali ele *tem*
função: sem ele, o projeto de teste e o de produção teriam o mesmo nome. Um `projectName` que já
termine em `.Tests` produz `Foo.Tests.Tests` — caso degenerado, explicitamente pedido por quem
digitou o nome, e que não colide com nada.

### Clean Architecture — **em aberto, decide T04**

Os quatro projetos são `<ProjectName>.Api`, `<ProjectName>.Application`, `<ProjectName>.Domain` e
`<ProjectName>.Infrastructure`. Aqui os sufixos **têm** função e não podem ser abandonados, então a
ambiguidade de ADR-0010 sobrevive: `Acme.Billing.Api` produz hoje `src/Acme.Billing.Api.Api/`.

T04 escolhe entre duas saídas, e nenhuma delas é a da Simples:

1. **Aceitar o nome dobrado**, tratando-o como consequência visível de um nome que a pessoa
   escolheu. Custo zero em regra, custo em aparência.
2. **Deduplicar o sufixo**, e então escrever a regra sem ambiguidade: comparação exata ou
   *case-insensitive*, o que acontece com `Acme.API`, o que acontece com `Acme.Api.Api`, e como o
   namespace raiz acompanha.

Até T04 decidir, a projeção da tela continua mostrando `<ProjectName>.Api` para Clean, e isto
**é** o comportamento documentado — não uma divergência.

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
