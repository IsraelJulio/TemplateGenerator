# ADR-0015 — Contribuição lê contribuição de eixo anterior; migração não roda na subida

**Data:** 2026-09-14 · **Estado:** aceita · **Levantada por:** papel `template-engineer` em T05,
decidida pelo `architect` na mesma tarefa

## Contexto

T05 acrescenta `database/sqlite` e `database/postgresql`, e cada um traz uma migração inicial. O
passo "aplicar a migração" precisa aparecer no `README.md` do projeto gerado — é o critério de
aceite 5 da tarefa, e é o que a tabela "Por banco" de
[`../architecture/generated-projects.md`](../architecture/generated-projects.md) promete: `sqlite`
⇒ "aplicar migração e executar"; `postgresql` ⇒ "configurar conexão, aplicar migração, executar".

O comando é `dotnet ef database update` e ele **exige caminho de projeto**. Não existe forma
copiável sem caminho: sem `--project` a ferramenta usa o projeto do diretório corrente, e a raiz da
solução não tem `.csproj`. E o caminho varia por arquitetura:

| Arquitetura | Projeto das migrações | Projeto de subida |
|---|---|---|
| `simple` | `src/<ProjectName>` | `src/<ProjectName>` |
| `clean` | `src/<ProjectName>.Infrastructure` | `src/<ProjectName>.Api` |

**O que varia por banco é a prosa; o que varia por arquitetura é o caminho — e o comando em si não
varia por banco nenhum.** `dotnet ef database update` é o mesmo para SQLite e para PostgreSQL: o
provedor sai do `DbContext`, não da linha de comando. A diferença entre os bancos é a existência da
seção (com `database = none` não há migração), a cadeia de conexão e o pré-requisito de servidor.

### A parede

O `README.md` pertence ao eixo `architecture` — regra 1 de
[ADR-0011](adr-0011-contribuicao-por-marcador.md): quem decide quantos projetos existem e como se
chamam é a arquitetura. O texto de banco chega nele pelo marcador `__ReadmeSetup__`, que é uma
**contribuição** de `database/*`. E a **regra 6** de ADR-0011 proíbe marcador de contribuição
dentro de contribuição: `TemplateTokens.ApplyValues` lança `TemplateDefectException`. Logo
`__ApiProjectDir__` não pode aparecer dentro de `database/sqlite/__parts__/ReadmeSetup.md`.

As duas metades do texto se interpenetram: a **condicionalidade** (esta seção existe?) é do eixo
`database`; o **caminho** é do eixo `architecture`; e as duas precisam sair no mesmo arquivo, que é
do eixo `architecture`. Partir o marcador em dois — prosa antes, comando depois — não resolve: o
comando escrito literalmente no README sobreviveria com `database = none`, porque `architecture` é
sempre selecionada.

Conferido, e vale registrar para não ser reexaminado: **dentro do motor de hoje, ou o arquivo que
combina as duas metades pertence a `database/*`, ou o mecanismo aprende a resolver um nível.** Não
há terceira saída.

### Por que isto não é um caso isolado de T05

O mesmo par volta em T06 e T07. `auth/jwt` precisa mandar apontar `Authority` e `Audience` no
`appsettings.json` **do projeto de API**, e o caminho dele varia por arquitetura; o comando
idiomático para o segredo, `dotnet user-secrets set --project <caminho>`, tem o mesmo problema.
`auth/identity` precisa citar o projeto que hospeda as tabelas de usuário. Uma saída que resolva só
o README de banco é uma saída que será reescrita duas vezes.

O sinal deixado em aberto pela última consequência de ADR-0011 — "se os pontos de contribuição de
`Program.cs` crescerem demais, a composição deveria ser por arquivo por eixo com registro
automático" — **não é este.** Aquele sinal é sobre código, ele já disparou em T05 e já foi
respondido: `PersistenceRegistration.AddPersistence` é exatamente "cada fragmento contribui um
extension próprio e o `Program.cs` chama uma lista", e `__PersistenceDir__/ItemStore.cs` é
exatamente "arquivo por eixo". O que falta não tem versão em código: prosa não tem
`IServiceCollection` onde se registrar. O problema aqui é outro, e é o que esta ADR nomeia:
**uma contribuição precisa citar um fato que pertence a um eixo anterior.**

## Decisão

Duas decisões. A segunda só é possível por causa da primeira, e separá-las deixaria as duas
ilegíveis.

### Decisão 1 — a regra 6 de ADR-0011 passa a permitir leitura **para trás**

> Uma contribuição pode usar um marcador de contribuição **alimentado exclusivamente por eixos
> estritamente anteriores** na ordem de seleção. Qualquer outra referência — ao próprio eixo ou a
> um eixo posterior — continua sendo defeito de template.

A ordem é a que já existe, declarada em `TemplateAxes.Select` e na regra 5 de ADR-0011:

```
common  <  architecture/*  <  database/*  <  auth/*  <  swagger/enabled
```

Dito de outro jeito, que é como quem escreve template deve guardar: **a contribuição de um eixo
anterior comporta-se, para um eixo posterior, exatamente como um marcador de valor.** Há uma ordem
total; lê-se para trás, nunca para a frente e nunca de lado.

#### Especificação

1. **Quem alimenta o marcador é apurado sobre o repositório inteiro**, não sobre os fragmentos
   selecionados — a mesma varredura da regra 3 de ADR-0011, pelo mesmo motivo. Um template é legal
   ou ilegal por si, e não dependendo da combinação pedida. Se `auth/jwt` passasse a alimentar
   `__ApiProjectDir__`, toda referência a ele vinda de `database/*` viraria defeito **em todas as
   combinações**, inclusive nas que não selecionam `auth/jwt`.
2. **Marcador que ninguém alimenta continua sendo erro**, com a mesma mensagem de hoje. A proteção
   contra nome digitado errado não se perde.
3. **Referência ao próprio eixo é defeito**, mesmo entre fragmentos que nunca são selecionados
   juntos (`architecture/simple` citando marcador de `architecture/clean`). A regra é "eixo
   estritamente anterior", sem exceção: dois fragmentos do mesmo eixo não têm ordem entre si, e
   admitir o caso obrigaria a distinguir "mesmo eixo, fragmento diferente" — um segundo conceito
   para um caso que nenhum template quer.
4. **A resolução é em uma passada por eixo**, na ordem acima, e portanto **termina por
   construção**. Não há ponto fixo, não há detecção de ciclo, não há profundidade a limitar: quando
   o eixo *n* é resolvido, todos os eixos anteriores já estão fechados. "Um nível" não é um limite
   arbitrário escolhido para parar a recursão; é o que a ordem total entrega de graça.
5. **O item 9 de ADR-0011 continua intacto.** A contribuição segue sendo texto inerte: sem
   condicional, sem laço, sem expressão. Esta ADR amplia *o que pode ser citado*, e não *o que o
   motor faz com o que leu*. Qualquer proposta de avaliação continua exigindo ADR própria.
6. **Posição.** Dentro de uma contribuição, o marcador lido para trás segue a regra 8 (substituição
   literal, `\n` entre contribuições). A regra 7 — marcador sozinho na coluna 0 consome a linha —
   vale apenas no **arquivo hospedeiro**; dentro de uma contribuição o texto já foi normalizado e
   aparado, e uma segunda regra de linha ali só produziria surpresa.

#### O que isso custa em C#

Localizado, no papel `backend`. `TemplateContributions.Resolve` já varre o repositório inteiro na
fase 1 e já percorre os fragmentos na ordem de seleção na fase 2. A mudança é:

- na fase 1, guardar também **qual eixo** declara cada marcador;
- na fase 2, passar para `TemplateTokens.ApplyValues` — além dos marcadores de valor — o dicionário
  dos marcadores **já fechados** (eixos anteriores), com o valor acumulado;
- a mensagem de `TemplateDefectException` deixa de ser "contribuição dentro de contribuição não
  existe" e passa a nomear o eixo do marcador citado e o eixo de quem citou, dizendo qual dos dois
  vem antes.

`EmbeddedTemplateSource`, `TemplateAxes`, `DeterministicZip` e `GenerationPlan` não mudam.
**Acrescentar um valor de eixo continua sem exigir C#.**

### Decisão 2 — o projeto gerado **não** aplica migração na subida

`PersistenceRegistration.MigratePersistence()` sai do projeto gerado, e com ele a contribuição
`__ProgramMiddleware__` de `database/sqlite` e de `database/postgresql`. `AddPersistence` fica. O
README volta a ensinar `dotnet ef database update` como **comando copiável**, num passo próprio,
antes do passo de execução.

Quatro razões, em ordem de peso.

1. **Ela mudava o comportamento do produto entregue para contornar um limite do motor de
   template.** O impedimento era documental — "uma contribuição não consegue citar um caminho" — e a
   saída adotada foi mexer no que a aplicação faz ao subir. Essa troca é sempre ruim, e é ruim
   independentemente do mérito de migrar na subida: o custo de uma limitação nossa foi transferido
   para o projeto de outra pessoa. A regra que fica: **limitação do motor se paga do nosso lado.**
2. **O template ensina, e o que ele ensina é parte do produto.** O projeto seria entregue com um
   `Database.Migrate()` cujo próprio comentário de documentação manda removê-lo em produção — código
   embarcado com instrução de apagar. Migrar na subida é prática contestada em .NET entregue a
   terceiros por motivos concretos: corrida entre instâncias que sobem juntas, migração destrutiva
   aplicada sem revisão, e `dotnet ef` deixando de ser o caminho aprendido. Quem recebe o pacote
   leva a prática junto.
3. **Ela contradiz o que `generated-projects.md` promete.** A tabela "Por banco" fixa três passos
   para `postgresql` e dois para `sqlite`, com "aplicar migração" explícito nos dois. Com migração
   na subida, `sqlite` teria um passo e `postgresql` dois. Manter a decisão obrigaria a reescrever a
   promessa — e a promessa está certa.
4. **Ela esvazia a verificação da camada 3.** "Todo comando do README roda sem modificar
   código-fonte" (RF-21) é forte porque o README tem comandos. Um README que descreve o caminho
   manual em prosa, sem comando, não tem o que a camada 3 execute: o passo mais importante da
   combinação com banco sairia do alcance do único teste que o defende.

**Simétrico nos dois bancos.** A tentação é manter a migração na subida só em SQLite, onde a
corrida entre instâncias não existe. Recusada: a assimetria entre providers teria como causa algo
que não é o provider, e as duas combinações passariam a ter caminhos iniciais diferentes em
*natureza*, não em grau. A tabela "Por banco" já trata os dois igual.

#### O que o pacote ganha

`.config/dotnet-tools.json`, **um arquivo por fragmento de banco**, fixando `dotnet-ef` em versão
exata. O caminho é a raiz do ZIP e não depende da arquitetura, então é arquivo comum de
`database/sqlite` e de `database/postgresql`.

**A versão do `dotnet-ef` é a mesma nos dois bancos — 10.0.12 — e não acompanha a do provider.** É
uma divergência deliberada de quem escreve template, e vale registrá-la aqui porque a intuição diz o
contrário: em `postgresql` o `.csproj` traz Npgsql e `Microsoft.EntityFrameworkCore.Design` na
versão que o provedor exige, enquanto `dotnet-ef` — a ferramenta de **linha de comando**, não um
pacote do projeto — fica em 10.0.12. A ferramenta opera sobre as migrações e o `DbContext` sem
exigir paridade com o provedor, então usar uma versão de ferramenta só, a mais nova, para os dois
bancos é a escolha mais simples e a que menos coisas obriga a concordar. O que **precisa** casar a
versão do provider é o pacote de design time do `.csproj`, e esse casa; a CLI não é isso. A camada 1
(`DatabaseFragmentMatrixTests`) afirma esta divergência de propósito — se um dia ela deixar de valer,
é lá que a mudança aparece.

Ele existe para que o passo de migração seja `dotnet tool restore` seguido de
`dotnet ef database update …`, e **não** `dotnet tool install --global dotnet-ef`. A instalação
global foi recusada por três motivos: suja estado global da máquina de quem gerou o projeto e da
máquina que roda a camada 3; a versão ficaria fixada em prosa, e não em arquivo — fora do alcance de
qualquer verificação; e reexecutar o comando numa máquina que já tem a ferramenta falha, o que faria
o README ter um comando que não roda duas vezes.

Ele **não vai para `common`**: com `database = none` não há migração a aplicar, e um manifesto de
ferramenta para EF Core num pacote sem EF Core é exatamente a dependência de opção não marcada que
RF-20 recusa.

### O que sai disso, concretamente

Dois marcadores de caminho, ambos alimentados por `architecture/*` e ambos lidos de dentro das
contribuições de `database/*`:

| Marcador | `simple` | `clean` |
|---|---|---|
| `__PersistenceProjectDir__` (novo) | `src/__ProjectName__` | `src/__ProjectName__.Infrastructure` |
| `__ApiProjectDir__` (já existe) | `src/__ProjectName__` | `src/__ProjectName__.Api` |

`__PersistenceProjectDir__` é o nome que
[`../architecture/generated-projects.md`](../architecture/generated-projects.md) já previu, na nota
"um cuidado de nome, e vale conferir antes de T05". Ele é o **projeto**, e não se confunde com
`__PersistenceDir__`, que é a **pasta** `Persistence/` dentro dele.

O comando no README passa os dois sempre, inclusive em `simple`, onde eles resolvem para o mesmo
projeto:

```bash
dotnet ef database update --project __PersistenceProjectDir__ --startup-project __ApiProjectDir__
```

A alternativa seria um `__EfProjectOptions__` alimentado por `architecture/*`, que daria a cada
arquitetura a linha mínima. Recusada: colocaria a sintaxe de uma ferramenta de linha de comando
dentro do fragmento de arquitetura, que pela regra 1 de ADR-0011 sabe **quantos projetos existem,
como se chamam e onde ficam** — e mais nada. Os dois marcadores de caminho são fatos da arquitetura,
e servem também ao `dotnet user-secrets --project` que T07 vai querer. A repetição em `simple` é
correta, não é erro, e uma frase do README a explica sem variar por arquitetura: `--project` é onde
as migrações moram e `--startup-project` é o que sobe a aplicação e carrega a configuração.

## Alternativas descartadas

**Manter a migração na subida** (o que estava no disco ao fim da escalação). É a saída livre de
caminho, e foi uma boa leitura do impedimento: ela contorna a regra 6 sem tocar no motor. Recusada
pelas quatro razões da decisão 2, sendo a primeira a decisiva — ela paga uma limitação nossa com o
comportamento do projeto de outra pessoa. Preço de recusá-la: o caminho inicial de `sqlite` ganha um
comando a mais e o de `postgresql` ganha dois, e o pacote ganha um arquivo.

**Um arquivo próprio do eixo `database`, hospedeiro de verdade** — um `BANCO-DE-DADOS.md` na raiz do
ZIP, onde os marcadores de contribuição resolvem normalmente pela regra 8, citado pelo README. É a
alternativa barata, não exige ADR de mecanismo e não toca o motor; foi a que mais perto chegou.
Recusada por dois motivos.

1. **Ela não escala com os eixos.** O mesmo impedimento reaparece em T06 e T07 — `auth/jwt`
   precisa citar o `appsettings.json` do projeto de API, `auth/identity` precisa citar o projeto das
   tabelas de usuário. A saída se repetiria como `AUTENTICACAO.md`, e o pacote terminaria com três
   documentos na raiz, cada um existindo por causa de um limite de motor que ninguém que recebe o
   ZIP tem como perceber. A forma do produto passaria a ser desenhada pela forma do gerador.
2. **Ela enfraquece RF-21 onde ele é mais forte.** "README específico da combinação" e "todo comando
   do README roda sem modificar código-fonte" valem porque existe **um** documento e porque os
   comandos estão nele. Mover o passo mais importante da combinação com banco para outro arquivo
   deixa a camada 3 com duas opções ruins: seguir a citação para dentro do segundo documento — e aí
   a verificação passa a depender de uma convenção de referência entre arquivos — ou parar de
   verificar o passo. Este projeto tem por critério que decisão arquitetural sem verificação
   executável não está pronta.

Preço de recusá-la: um dia de trabalho de `backend` no motor e uma regra a mais na cabeça de quem
escreve template.

**Promover o caminho a marcador de valor**, calculado em C# a partir de `request.Architecture`. A
saída mais curta de todas — três linhas em `TemplateTokens.For` — e a mais cara no prazo.
`generated-projects.md` recusa explicitamente que o nome de projeto seja calculado em C# ("nenhuma
linha de C# calcula nome de projeto"), e ADR-0011 registra como ganho que "acrescentar um valor de
eixo continua sem exigir C#". Com essa saída, acrescentar `architecture/vertical-slices` passaria a
exigir uma edição em C#, e o nome do projeto teria duas fontes de verdade — o caminho do template e
o cálculo — obrigadas a concordar para sempre. É a razão 3 de "Por que não deduplicar", repetida.

**O `README.md` migrar para o eixo `database`.** Multiplicaria o esqueleto por três, com a diferença
entre as cópias invisível no diff, e ainda exigiria marcadores novos para tudo que é da arquitetura
— estrutura de pastas, diagrama de dependências, nome dos projetos. É a duplicação que o modelo de
fragmentos existe para evitar, agravada por inverter o dono: o eixo `database` passaria a ser quem
descreve a Clean Architecture.

**Aninhamento irrestrito de contribuições**, com detecção de ciclo. Descartada pelo mesmo motivo que
a regra 9 de ADR-0011 existe: abre a porta para um grafo de dependências entre marcadores, que pede
ordenação topológica, que pede mensagem de erro sobre ciclo, que pede documentação sobre o que é
resolvido antes do quê. A ordem total de eixos já existe, já é declarada, já entra no hash e já é a
ordem que o produto tem — usá-la custa nada e proíbe o grafo por construção.

## Consequências

- **A ordem de seleção de fragmentos ganha um segundo significado.** Ela já era ordem de
  concatenação (regra 5 de ADR-0011) e agora é também **ordem de dependência**: `auth/*` pode citar
  fato de `database/*`, e `database/*` não pode citar fato de `auth/*`. A direção casa com o produto
  — a restrição `identity-requires-database` vai no mesmo sentido —, mas isso é confirmação, não
  causa. Trocar a ordem de seleção deixou de ser só trocar o hash: pode virar defeito de template.
  Está declarado em `TemplateAxes.Select`, em ADR-0011 e aqui.
- **Um caso novo de defeito de template**, com teste próprio na camada 1: contribuição citando
  marcador do próprio eixo ou de eixo posterior. A mensagem nomeia os dois eixos e diz qual vem
  antes.
- **O projeto gerado passa a não fazer nenhuma chamada ao banco na subida.** Consequência
  deliberada: a aplicação sobe com o servidor de banco fora do ar, e `GET /health` — público em
  qualquer combinação, RF-12 — responde. Uma sonda de "há migração pendente?" na subida foi
  considerada e recusada: ela devolveria a chamada de banco ao arranque, em versão mais fraca, para
  melhorar uma mensagem de erro que o EF Core já dá. Quem subir sem aplicar a migração recebe o erro
  do EF na primeira requisição a `/items`; o README diz, no passo anterior, que a migração vem
  antes.
- **O caminho inicial com banco ganha um comando.** `sqlite`: `dotnet restore`,
  `dotnet tool restore`, `dotnet ef database update …`, `dotnet run`. `postgresql`: os mesmos, com a
  configuração da conexão antes. É o que a tabela "Por banco" sempre disse.
- **O que defende esta decisão, executável.** Camada 1: os testes de defeito da decisão 1; a
  presença de `.config/dotnet-tools.json` com versão literal **se e somente se** `database ≠ none`;
  e, em toda combinação com banco, o README contendo `dotnet ef database update` com dois caminhos
  que **existem no pacote**. Ainda na camada 1, uma verificação negativa que defende a decisão 2:
  nenhum arquivo do pacote contém `Database.Migrate(`. Camada 3: os comandos do README executados
  **na ordem em que estão escritos**, `dotnet tool restore` e `dotnet ef database update` incluídos,
  antes de subir a aplicação. **Assert de sanidade obrigatório**, pela razão de ADR-0008 e de
  ADR-0011: cada uma dessas verificações falha se a matriz não tiver produzido nenhum README com
  banco para conferir — um verificador que para de verificar em silêncio é pior que nenhum.
- **T06 e T07 chegam com a parede já derrubada.** `auth/*` pode citar `__ApiProjectDir__`,
  `__PersistenceProjectDir__` e qualquer marcador de `common`, `architecture/*` e `database/*` de
  dentro das próprias contribuições.
- **Risco nomeado: a leitura para trás convida a marcadores de granularidade fina.** Nada impede que
  alguém crie um marcador por frase para evitar duplicar prosa entre fragmentos, e o resultado seria
  um README que ninguém consegue ler no repositório. A cerca é a mesma da regra 9: o marcador
  carrega **fato de outro eixo** — um caminho, um nome, um namespace —, não pedaço de frase.
