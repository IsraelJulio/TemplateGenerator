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
- `Properties/launchSettings.json` no projeto Web API — ele **fixa a porta** que o `README.md` e o
  `requests.http` citam. Sem ele o projeto sobe numa porta escolhida pelo runtime e todo exemplo de
  chamada do pacote passa a apontar para o lugar errado, o que contraria RF-21 ("todo comando do
  README tem que rodar sem modificar código-fonte")
- `requests.http` com exemplos de chamada, incluindo autenticação quando houver
- `.gitignore`, `.editorconfig`, `global.json` fixando o SDK
- `README.md` específico da combinação (RF-21)
- `.templategenerator/manifest.json` (RF-22), no formato fixado adiante

Esta lista é **obrigação**, e cada item é afirmado por teste da camada 1 em toda combinação
disponível. É por isso que ela é curta: entra aqui o que, faltando, quebra uma promessa do produto.

### Este documento não é o inventário do pacote

**Decisão de T03.** O pacote real traz mais arquivos do que os listados acima — em `simple` hoje,
por exemplo, `Models/ItemInput.cs` e `Models/HealthResponse.cs`, além do `Properties/`. Tentar
manter aqui a enumeração completa foi rejeitado: é a mesma classe de omissão que deixou o nome da
pasta em aberto e custou uma correção em T03, só que multiplicada por 32 combinações e atualizada à
mão a cada arquivo novo. Um inventário escrito à mão erra e ninguém percebe.

**A enumeração de registro é o pacote**, e ela é extraída dele, não escrita:
[`../../src/web/src/app/core/summary/zip-structure.contract.json`](../../src/web/src/app/core/summary/zip-structure.contract.json)
é gerado a partir do ZIP real a cada execução da suíte
([ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md), "Estado em T03"). Quem quiser
saber exatamente o que sai numa combinação amarrada lê esse arquivo ou gera o pacote.

O que **este** documento fixa, e continua fixando, é o que não se deriva do pacote: a obrigação
acima, a **forma** de cada arquitetura, os nomes de projeto e de pasta, o comportamento por eixo e o
formato do manifesto. Acrescentar um arquivo dentro de uma pasta já descrita é trabalho de template
e não pede edição aqui; mudar a **forma** — uma pasta nova, um projeto novo, um item da obrigação —
pede.

## Formato do manifesto

`.templategenerator/manifest.json` é **contrato**: há consumidores fora do motor, incluindo o teste
de ponta a ponta, que lê `templateVersion` e `options.projectName`.

```json
{
  "templateVersion": "1.0.0",
  "options": {
    "projectName": "Acme.Billing",
    "architecture": "simple",
    "database": "none",
    "authentication": "none",
    "swagger": true,
    "dotnetVersion": "net10.0"
  }
}
```

- **Exatamente duas chaves no topo**, nesta ordem: `templateVersion` e `options`.
- `options` carrega **os seis campos do `POST`**, nesta ordem, com os valores já validados e
  normalizados — `projectName` é a forma aparada.
- **Nada mais.** Sem "gerado em", sem "gerado por", sem máquina. Data e host são exatamente o que
  quebraria o SHA-256 estável do [ADR-0003](../decisions/adr-0003-zip-deterministico.md), e o
  manifesto viraria a razão de duas gerações iguais diferirem.
- **A ordem das chaves é conteúdo do arquivo** e entra no hash. Ela é escrita à mão no motor, não
  deduzida de dicionário.
- **Mudar a forma do manifesto exige subir `templateVersion`.** Acrescentar uma chave muda o hash de
  todos os pacotes; o manifesto é o próprio lugar onde essa mudança fica registrada.

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

## A frase da tela

A tela mostra a "estrutura prevista" (RF-04) e precisa dizer o que essa árvore vale. O texto é
fixado aqui, e não no componente, porque ele afirma o estado da amarração entre projeção e pacote —
e quem é dono desse estado é este documento, não o frontend
([ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md)).

**Texto vigente**, em `config-summary.html`:

> O que o ZIP deve conter para esta combinação. Onde o template já existe, a suíte do gerador
> compara esta árvore com o pacote real e falha se divergirem; nas demais combinações, ela ainda é
> uma previsão derivada da documentação.

Três coisas nesse texto são deliberadas:

- **Não pede conferência à pessoa.** O texto anterior — *"O motor de geração entra em outra etapa,
  então confira a árvore contra o pacote quando ele existir"* — virou falso no instante em que a
  amarração passou a rodar: ele delegava ao leitor um trabalho que a suíte passou a fazer sozinha, a
  cada execução.
- **Admite a assimetria sem enumerar combinação.** Hoje só `simple/none/none` está amarrada; as
  demais são previsão. A frase diz isso por uma condição ("onde o template já existe") em vez de uma
  lista, e por isso **não precisa mudar** quando T04 amarrar mais combinações.
- **Não varia por arquitetura**, e isso foi pesado. Variar exigiria que o módulo autorizado soubesse
  *quais* combinações estão amarradas — uma segunda fonte de verdade sobre o estado da
  implementação, do tipo exato que ADR-0010 registra ter derivado em silêncio três vezes. Além
  disso, a fronteira real não é o eixo `architecture`: é a conjunção dos três eixos, então uma frase
  que dissesse "conferida" para toda a arquitetura Simples estaria mentindo sobre
  `simple/sqlite/identity`. Uma frase honesta e estável vale mais que uma frase que pisca ao trocar
  um rádio.

Nenhum valor de opção aparece no texto, então a regra de confinamento de ADR-0010 continua intacta:
o `.html` segue sem citar `simple`, `clean` ou qualquer outro.
