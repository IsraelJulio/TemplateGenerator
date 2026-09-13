# Projetos gerados

> A tela mostra uma **projeção** desta árvore, derivada no frontend. Este documento é a fonte; a
> projeção o segue, e não o contrário. Ver
> [ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md). A regra de nomes abaixo é a
> decisão que ADR-0010 deixou para T03/T04: a **Simples** foi decidida em T03 e a **Clean** em T04.
> Não há mais nome de pasta em aberto.

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

### Clean Architecture — **decidida em T04**

**Os quatro projetos se chamam `<ProjectName>.Api`, `<ProjectName>.Application`,
`<ProjectName>.Domain` e `<ProjectName>.Infrastructure`. O sufixo é concatenado sempre, sem
exceção. Não há deduplicação.**

```
<ProjectName>.sln
src/<ProjectName>.Api/<ProjectName>.Api.csproj                        namespace raiz: <ProjectName>.Api
src/<ProjectName>.Application/<ProjectName>.Application.csproj        namespace raiz: <ProjectName>.Application
src/<ProjectName>.Domain/<ProjectName>.Domain.csproj                  namespace raiz: <ProjectName>.Domain
src/<ProjectName>.Infrastructure/<ProjectName>.Infrastructure.csproj  namespace raiz: <ProjectName>.Infrastructure
tests/<ProjectName>.Tests/<ProjectName>.Tests.csproj
```

#### A regra, escrita para não precisar de interpretação

1. **O nome de cada projeto é `<projectName>` + `.` + o sufixo da camada**, com `<projectName>` na
   forma já aparada e validada. A concatenação é **incondicional**.
2. **O motor não compara, não normaliza caixa e não remove nada.** Não existe comparação exata nem
   *case-insensitive*, porque não existe comparação. Nenhum caso de borda é examinado porque nenhum
   caso de borda é reconhecido.
3. **Nome da pasta = nome do `.csproj` = `RootNamespace` = `AssemblyName`.** Os quatro são a mesma
   string, sempre. Não há regra que mexa em um sem mexer nos outros, porque não há regra.
4. **Nenhum marcador novo.** O único marcador de valor envolvido continua sendo `__ProjectName__`
   ([`generation-engine.md`](generation-engine.md)); o sufixo é texto literal escrito no **caminho**
   do template — `architecture/clean/src/__ProjectName__.Api/__ProjectName__.Api.csproj`. Nenhuma
   linha de C# calcula nome de projeto.

Os quatro casos que T04 tinha de responder, respondidos:

| `projectName` | Pasta do projeto de API | Namespace raiz do projeto de API |
|---|---|---|
| `Acme.Billing` | `src/Acme.Billing.Api/` | `Acme.Billing.Api` |
| `Acme.Billing.Api` | `src/Acme.Billing.Api.Api/` | `Acme.Billing.Api.Api` |
| `Acme.API` | `src/Acme.API.Api/` | `Acme.API.Api` |
| `Acme.Api.Api` | `src/Acme.Api.Api.Api/` | `Acme.Api.Api.Api` |

#### Por que não deduplicar

ADR-0010 avisou que inventar deduplicação seria "trocar um palpite por outro". T04 tem o que T02 não
tinha — pacote real, amarração que verifica e a experiência da decisão da Simples — e a conclusão,
com esses três na mão, continua sendo a mesma. Quatro razões, em ordem de peso.

1. **Deduplicar enfraquece justamente a função que dá existência ao sufixo.** A decisão da Simples
   veio de perguntar *para que serve o sufixo*: desambiguar irmãos. Em Clean há quatro irmãos, e o
   que os torna legíveis é serem **uniformes** — `<nome>.<camada>`, quatro vezes. Deduplicar quebra a
   uniformidade em exatamente um deles: com `Acme.Billing.Api`, `src/` passaria a conter
   `Acme.Billing.Api/`, `Acme.Billing.Api.Application/`, `Acme.Billing.Api.Domain/` e
   `Acme.Billing.Api.Infrastructure/` — três projetos com marca de camada e um sem. Quem abre a
   pasta precisa **conhecer a regra** para saber que o projeto sem marca é o de API. O nome dobrado é
   feio; o nome deduplicado é ambíguo. Feio é melhor que ambíguo quando a única função do sufixo é
   tirar ambiguidade.
2. **Ela resolve um dos três nomes que a motivam e estraga os outros dois.** São três os nomes que
   fazem alguém querer a regra, e ela só serve para o primeiro. `Acme.Billing.Api` é o caso limpo, e
   nele a regra funciona — ao custo da razão 1. `Acme.Api.Api` **sai dobrado de qualquer jeito**: uma
   passada de remoção devolve `Acme.Api.Api`, ainda com o par repetido, e uma remoção em laço é
   indefensável, porque apagaria texto que a pessoa escreveu de propósito. E `Acme.API` não tem
   resposta boa: comparação exata não dispara e produz `Acme.API.Api`, com a mesma palavra em duas
   grafias na mesma linha — pior que dobrada; comparação *case-insensitive* dispara e obriga a
   escolher qual das duas grafias sobrevive, e **as duas escolhas descartam informação que a pessoa
   digitou**. Uma regra cuja razão de existir é a aparência, e que piora a aparência em dois dos três
   nomes que a justificam, não se paga.
3. **Ela custa exatamente o que a decisão da Simples comemorou não custar.** Hoje o nome do projeto
   sai do caminho do template, e o motor tem três marcadores de valor. Deduplicar cria um quarto,
   calculado em C# a partir do primeiro, mais uma regra de caixa, mais o mesmo cálculo repetido na
   projeção do frontend, no contrato gerado a partir do ZIP e em todo template futuro que cite o
   projeto de API. São cinco lugares obrigados a concordar sobre a mesma regra, para sempre, em troca
   de um ponto de aparência.
4. **O nome dobrado é visível antes do download, não depois.** A "estrutura prevista" da tela mostra
   `src/Acme.Billing.Api.Api/` no instante em que a pessoa digita o nome — e essa árvore é conferida
   contra o ZIP real por teste (ADR-0010). Quem não gosta troca o nome na tela, sem baixar nada. Um
   nome surpreendente que aparece *antes* da escolha é um custo de aparência; o mesmo nome escondido
   por uma regra que ninguém leu seria um custo de entendimento.

**A assimetria com a Simples é o mesmo princípio, não uma contradição.** Nos dois casos a pergunta
foi "o sufixo desambigua alguma coisa aqui?". Na Simples a resposta é não, e por isso ele não é
acrescentado; em Clean é sim, e por isso ele é acrescentado **sempre** — um desambiguador aplicado
só às vezes não desambigua.

**Se um dia isto mudar**, muda como qualquer outra regra de forma: aqui, e com os testes de
`ZipStructureContractTests` que fixam `Acme.Billing.Api` em `clean` caindo junto. Eles existem para
esse nome não mudar em silêncio.

## Arquitetura Simples

Um projeto Web API, organizado em pastas: `Endpoints`, `Models`, `Services`, `Persistence`.

## Clean Architecture

Quatro projetos com **referências de projeto declaradas** estritamente nesta direção:

```
Api ──▶ Application ──▶ Domain
 │                        ▲
 └────▶ Infrastructure ───┘
```

**Cada seta é um `<ProjectReference>` no `.csproj`, não "quem enxerga quem".** Referência de projeto
é transitiva: o `Api` **usa** tipos do `Domain` — o `Program.cs` gerado faz
`using <ProjectName>.Domain.Abstractions` — sem declarar a aresta, porque
`Api → Application → Domain` já os entrega. Ler o diagrama como "o `Api` não conhece o `Domain`"
seria ler errado, e é por isso que a legenda diz *declaradas*.

- `Domain` — entidades, regras **e as portas que `Infrastructure` implementa**. **Sem referência de
  projeto e sem pacote de infraestrutura.**
- `Application` — casos de uso e os tipos de entrada e saída deles. Depende só de `Domain`.
- `Infrastructure` — implementa as portas declaradas em `Domain` (persistência, identidade).
- `Api` — composição, endpoints, configuração.

### Onde a porta mora, e por quê — **decidido em T04**

**Toda porta que `Infrastructure` implementa é declarada em `Domain`.** `IItemStore` mora em
`Domain`; `ItemService` e os tipos que ele recebe e devolve moram em `Application`.

Isto corrige uma contradição que este documento carregou até T04 e que o `template-engineer`
escalou ao escrever o template, em vez de escolher sozinho — corretamente. O texto anterior dizia
que as portas ficavam em `Application` **e** que `Infrastructure` as implementava, e as duas coisas
juntas **são impossíveis em .NET**: quem implementa uma interface referencia o assembly que a
declara, logo `Application` estaria obrigando uma aresta `Infrastructure ──▶ Application` que o
diagrama não tem. Com o critério "qualquer aresta fora do diagrama é indevida", as duas metades do
parágrafo se reprovavam mutuamente.

As duas saídas eram legítimas — declarar a porta em `Application` e **acrescentar a quinta aresta**
ao diagrama, que é a forma mais comum nos templates .NET de mercado; ou declarar a porta em `Domain`
e **manter o diagrama exato**, que é a convenção de repositório do DDD. Escolhemos a segunda, por
três razões, em ordem de peso.

1. **Ela mantém a afirmação mais forte, e afirmação forte aqui é o produto.** Com a porta em
   `Domain`, `Infrastructure` **não pode enxergar** `Application` — e essa proibição é verificável
   lendo o `.csproj`, sem `restore` e sem `build`, nas 32 combinações. O que ela impede é concreto:
   um adaptador chamando um caso de uso, que é a inversão de controle que a arquitetura existe para
   proibir. Com a porta em `Application`, essa proibição **deixa de ser verificável na camada 1** —
   a aresta passa a ser permitida, e "o adaptador não chama o caso de uso" vira convenção sem
   verificador. Este projeto tem por critério que decisão arquitetural sem verificação executável
   não está pronta; entre duas leituras defensáveis de "Clean", a que sobra verificada vale mais.
2. **O custo dela não se materializa dentro do escopo declarado.** O argumento contrário é bom e
   precisa ser respondido de frente: com a porta em `Domain`, **`Domain` passa a ser o único lugar
   onde uma porta pode morar**, porque é só isso que `Infrastructure` enxerga — e uma porta que não
   seja conceito de domínio (um relógio, um enviador de e-mail) seria empurrada para dentro da
   camada que o critério de aceite 2 mais protege. A pergunta certa é então: **o que falta do MVP
   puxa alguma porta?** Conferido, item a item, e a resposta é não:
   - `database/sqlite` e `database/postgresql` acrescentam `AppDbContext` em `Infrastructure`, com
     `DbSet<Item>` — precisa de `Domain` e de mais nada;
   - `auth/identity` usa os endpoints nativos (`MapIdentityApi`) e `AppUser : IdentityUser`, que é
     tipo de pacote de infraestrutura e por isso já está projetado em
     `Infrastructure/Identity/AppUser.cs` — **não há porta a declarar**;
   - `auth/jwt` é validação por `Authority`/`Audience`, composição pura, sem porta;
   - RF-15 fixa dados compartilhados **sem regra de proprietário**, o que elimina o `ICurrentUser`
     que seria a porta mais provável da autenticação.

   A premissa geral de que "autenticação puxa mais portas que persistência" é verdadeira no mundo e
   **falsa para o que resta deste produto**. T05 a T08 cabem no diagrama sem uma aresta nova.
3. **A `Api` não leva teste arquitetural dentro do ZIP.** A verificação de arestas é da **camada 1
   da nossa suíte** ([`../quality/test-strategy.md`](../quality/test-strategy.md)), lendo o `.csproj`
   gerado — ela não é entregue ao pacote. Então a restrição extra não dispara na cara de quem
   receber o projeto e acrescentar uma porta própria: essa pessoa muda o que quiser no projeto dela,
   sem nada nosso reclamando. O custo da escolha 1 fica **inteiramente do nosso lado**, onde
   sabemos pagá-lo.

**Quando revisitar, e o que custa.** O sinal é uma porta que `Infrastructure` precise implementar e
que **não** seja conceito de domínio. No dia em que ela aparecer, a saída é declarar a porta em
`Application` e **acrescentar `Infrastructure ──▶ Application` ao diagrama acima** — uma seta, uma
linha na lista de arestas permitidas do teste, e os arquivos de porta de lugar. Fica registrado aqui
para que quem chegar nesse ponto saiba que é uma mudança prevista, com preço conhecido, e não uma
violação a contornar em silêncio. **O que não vale é declarar a porta em `Application` e deixar o
diagrama como está**: é exatamente a contradição que T04 veio consertar.

### O conjunto de arestas, para o teste do critério 4

"Referência arquitetural indevida" é **qualquer aresta fora desta lista**. A comparação é por
**igualdade de conjunto**, não por "não contém" — um pacote que perdesse uma referência legítima
precisa falhar tanto quanto um que ganhasse uma indevida.

**Permitidas — e todas as quatro são usadas**, em `<ProjectReference>` **declarada** no `.csproj`:

| De | Para |
|---|---|
| `Api` | `Application` |
| `Api` | `Infrastructure` |
| `Application` | `Domain` |
| `Infrastructure` | `Domain` |

**Proibidas**, e cada uma é a negação de algo que importa:

- **`Domain` → qualquer coisa.** `Domain` tem **zero** `<ProjectReference>`. É o critério de aceite
  2 e é a afirmação mais forte do conjunto.
- `Application` → `Infrastructure`, `Application` → `Api` — o caso de uso não conhece adaptador nem
  transporte.
- `Infrastructure` → `Application`, `Infrastructure` → `Api` — o adaptador não chama caso de uso.
  É a proibição que a decisão acima comprou.
- **`Api` → `Domain` declarada.** Não está no diagrama, logo não pode ser declarada. **Isto não
  impede o `Api` de usar tipos do `Domain`:** referência de projeto é transitiva por padrão no SDK
  moderno, e `Api → Application → Domain` já entrega os tipos. Declarar a aresta não mudaria nada em
  compilação e faria o `.csproj` afirmar uma dependência que o diagrama nega — por isso ela é
  proibida como **declaração**, não como uso. Se algum template um dia precisar dela declarada, isso
  é mudança de diagrama, não exceção de teste.

**O projeto de testes fica fora do diagrama, de propósito.** Ele não é camada: pode referenciar
qualquer projeto de produção, e a regra que vale é a inversa — **nada pode referenciar o projeto de
testes**. Afirmar as arestas dele só congelaria o acidente de hoje (`Tests → Application`) e
quebraria no primeiro teste que precisasse de outro projeto.

**A comparação é por papel, não por nome.** As arestas são entre `.Api`, `.Application`, `.Domain`,
`.Infrastructure` e `.Tests`, qualquer que seja o `projectName` — a regra de nomes está na seção
"Nomes de projeto e de pasta" e não pode ser reimplementada no teste.

**Assert de sanidade, obrigatório**, pelo mesmo motivo de [ADR-0008](../decisions/adr-0008-fronteira-por-grafo-de-restore.md)
e de [ADR-0011](../decisions/adr-0011-contribuicao-por-marcador.md), e mais necessário agora que
[ADR-0012](../decisions/adr-0012-combinacao-sem-template.md) faz a maioria das combinações recusar:
o teste **falha** se não encontrar nenhum pacote de `clean`, se algum deles não tiver os quatro
`.csproj`, ou se o conjunto de arestas lido da matriz inteira vier vazio. "Nenhuma aresta indevida"
é verdade por vacuidade num pacote sem projeto.

Na arquitetura **Simples** não há diagrama a verificar: há um projeto de código e um de testes.

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

**Texto vigente**, em `config-summary.html` desde T04:

> O que o ZIP contém para esta combinação. A suíte do gerador compara esta árvore com o pacote real
> e falha se divergirem.

Quatro coisas a conferir nele:

- **"deve conter" virou "contém".** A árvore deixou de ser uma obrigação declarada e passou a ser um
  fato verificado para tudo que a tela mostra.
- **Não cita valor de opção, não varia por arquitetura e não sabe quais combinações estão
  amarradas** — o confinamento de [ADR-0010](../decisions/adr-0010-estrutura-prevista-e-projecao.md)
  e a ausência de segunda fonte de verdade seguem intactos.
- **Ele repousa sobre uma precondição de outro papel:** a tela **desabilita** os valores
  indisponíveis ([ADR-0012](../decisions/adr-0012-combinacao-sem-template.md)). Se isso deixar de
  ser verdade, a pessoa alcança uma combinação sem template e **esta frase passa a mentir**. Quem
  mexer no consumo de `unavailable` mexe nesta frase junto.
- **Ele não volta a precisar de edição** quando T05 a T08 escreverem os fragmentos que faltam: cada
  fragmento acende o valor, a amarração o alcança sozinha e a frase continua verdadeira sem uma
  letra a mais.

### Como se chegou nele, e por que o texto anterior caiu

**Texto anterior**, vigente de T03 até T04:

> O que o ZIP deve conter para esta combinação. Onde o template já existe, a suíte do gerador
> compara esta árvore com o pacote real e falha se divergirem; nas demais combinações, ela ainda é
> uma previsão derivada da documentação.

Três coisas nesse texto eram deliberadas:

- **Não pede conferência à pessoa.** O texto anterior — *"O motor de geração entra em outra etapa,
  então confira a árvore contra o pacote quando ele existir"* — virou falso no instante em que a
  amarração passou a rodar: ele delegava ao leitor um trabalho que a suíte passou a fazer sozinha, a
  cada execução.
- **Admitia a assimetria sem enumerar combinação.** Em T03 só `simple/none/none` estava amarrada; as
  demais eram previsão. A frase dizia isso por uma condição ("onde o template já existe") em vez de
  uma lista, e era **essa** a qualidade que se esperava fazê-la sobreviver a T04. Sobreviveu à
  amarração da Clean, como previsto, e caiu por outro motivo — ver adiante.
- **Não variava por arquitetura**, e isso foi pesado. Variar exigiria que o módulo autorizado soubesse
  *quais* combinações estão amarradas — uma segunda fonte de verdade sobre o estado da
  implementação, do tipo exato que ADR-0010 registra ter derivado em silêncio três vezes. Além
  disso, a fronteira real não é o eixo `architecture`: é a conjunção dos três eixos, então uma frase
  que dissesse "conferida" para toda a arquitetura Simples estaria mentindo sobre
  `simple/sqlite/identity`. Uma frase honesta e estável vale mais que uma frase que pisca ao trocar
  um rádio.

Nenhum valor de opção aparece no texto, então a regra de confinamento de ADR-0010 continua intacta:
o `.html` segue sem citar `simple`, `clean` ou qualquer outro.

**O que derrubou o texto anterior foi ADR-0012, e não a amarração da Clean.** A segunda qualidade da
lista — "admite a assimetria sem enumerar combinação, e por isso não precisa mudar quando T04 amarrar
mais combinações" — estava certa para o que ela previa e foi vencida por outra coisa: **mudou o que a
tela consegue mostrar.** Com a disponibilidade consumida, a tela desabilita todo valor sem fragmento,
a seleção que o resumo enxerga é sempre uma combinação **disponível**, e toda combinação disponível
está amarrada por construção. A metade *"nas demais combinações, ela ainda é uma previsão"* passou a
descrever um caso que a pessoa não alcança.

Manter aquela frase seria **o espelho do erro que T03 corrigiu**. Lá o texto afirmava uma garantia
que não existia; aqui ele semearia uma dúvida que não existe mais — dizer "talvez isto seja um
palpite" sobre uma árvore que é sempre conferida. **Incerteza falsa custa o mesmo que garantia
falsa:** ensina a pessoa a desconfiar do que está certo, e a próxima afirmação verdadeira da tela já
nasce sem crédito.

**A lição, que vale além desta frase:** um texto de interface que descreve o **estado da
implementação** tem prazo de validade, mesmo quando escrito com o cuidado de não enumerar casos. Foi
a segunda troca em duas tarefas. A frase atual é a primeira que não descreve estado nenhum — ela
afirma uma propriedade que um teste mantém verdadeira —, e é por isso que se espera que sobreviva.
