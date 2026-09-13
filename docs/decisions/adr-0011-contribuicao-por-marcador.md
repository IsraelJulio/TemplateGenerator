# ADR-0011 — Arquivo que varia por mais de um eixo: dono único + contribuição por marcador

**Data:** 2026-09-12 · **Estado:** aceita · **Levantada por:** papel `backend` em T03, decidida pelo
`architect` na mesma tarefa

## Contexto

O modelo de composição de [`../architecture/generation-engine.md`](../architecture/generation-engine.md)
é **união de arquivos**: cada caminho do ZIP pertence a exatamente um fragmento, e dois fragmentos
que escrevam o mesmo caminho são defeito de template, não precedência silenciosa.

O modelo não tem resposta para o caso mais comum do produto: **um arquivo que varia por vários eixos
ao mesmo tempo**. O `.csproj` do projeto Web API precisa mudar por `swagger` (duas
`PackageReference`, [ADR-0001](adr-0001-swagger.md)), por `database` (EF Core + provider) e por
`authentication` (Identity). O mesmo vale, com força maior, para `Program.cs` (registro de serviços
e middleware) e para `appsettings.json` (cadeia de conexão, `Authority`/`Audience`).

O `backend` implementou o motor, parou nesse ponto e levantou duas saídas sem escolher entre elas.
Nenhuma das duas foi adotada; ver "Alternativas descartadas".

### O que decide a questão

A camada 1 da matriz é **estática, sem `restore` e sem `build`**
([`../quality/test-strategy.md`](../quality/test-strategy.md)), e é ela que precisa afirmar, nas 32
combinações, "nenhuma dependência de opção não marcada" (RF-20) e "toda versão exata" (RNF-06).

[ADR-0008](adr-0008-fronteira-por-grafo-de-restore.md) já julgou a leitura textual do `.csproj`
**insuficiente** para a fronteira de `TemplateGenerator.Generation` e promoveu o grafo de restore a
fonte decisiva. A camada 1 **não tem grafo de restore**: ela não restaura nada. Logo, a única coisa
que a camada 1 consegue afirmar é o que estiver **literalmente escrito** no `.csproj` gerado.

Disso sai o critério que descarta metade das saídas: **qualquer mecanismo que tire o
`PackageReference` de dentro do `.csproj` transfere a verificação de RF-20 e RNF-06 para uma camada
que, naquele momento, não existe.** Não é uma questão de gosto de sintaxe; é a diferença entre um
teste que lê um arquivo e um teste que precisaria reimplementar a avaliação de `Import` e
`Condition` do MSBuild para responder a mesma pergunta.

## Decisão

Duas regras. A união de arquivos fica **intacta**: nenhum caminho de ZIP passa a ter dois donos.

### Regra 1 — o `.csproj` pertence ao fragmento de arquitetura

Quem decide **quantos** projetos existem, **como se chamam** e **onde ficam** é a arquitetura. Em
Simples há um projeto; em Clean há quatro, com um diagrama de referências a respeitar. Os `.csproj`
são, portanto, arquivos do eixo `architecture`, e não de `common`:

```
Templates/architecture/simple/src/__ProjectName__/__ProjectName__.csproj
Templates/architecture/clean/src/__ProjectName__.Api/__ProjectName__.Api.csproj
Templates/architecture/clean/src/__ProjectName__.Domain/__ProjectName__.Domain.csproj
…
```

Um dono, um caminho, zero colisão. O mesmo vale para `Program.cs`, para a `.sln` e para tudo cujo
**caminho** dependa da arquitetura — o que, depois da regra de nomes de
[`../architecture/generated-projects.md`](../architecture/generated-projects.md), é quase tudo que
mora dentro de uma pasta de projeto.

### Regra 2 — os outros eixos contribuem **texto**, por marcador

Um fragmento pode carregar arquivos que **não viram entrada do ZIP** e cujo conteúdo passa a ser o
valor de um marcador. Eles moram em um diretório reservado dentro do fragmento:

```
Templates/swagger/enabled/__parts__/ApiPackageReferences.xml
Templates/database/sqlite/__parts__/ApiPackageReferences.xml
Templates/auth/identity/__parts__/ApiPackageReferences.xml
```

Os três alimentam o mesmo marcador `__ApiPackageReferences__`, que o `.csproj` da regra 1 carrega.
O motor **concatena** as contribuições dos fragmentos **selecionados**, na ordem de seleção, e
substitui. O resultado é um `.csproj` com as `PackageReference` **literais**, exatamente como se
alguém as tivesse digitado ali.

#### Especificação

1. **Caminho.** Exatamente `__parts__/<Nome>.<ext>`, dois segmentos, na raiz do fragmento. A
   extensão existe só para o editor colorir a sintaxe e é ignorada. `__parts__` em qualquer outra
   posição do caminho é defeito — sem essa regra, `docs/__parts__/x` seria ambíguo entre arquivo do
   pacote e contribuição.
2. **Nome do marcador.** `<Nome>` vira `__<Nome>__` e precisa casar `[A-Za-z][A-Za-z0-9]*`, a mesma
   forma de `TemplateTokens`. No máximo **um** arquivo por marcador por fragmento — dois arquivos
   com o mesmo nome e extensões diferentes seriam ordem indefinida, logo defeito.
3. **Marcador conhecido.** O conjunto de marcadores de contribuição é a união sobre **todos** os
   fragmentos do repositório, não só os selecionados. Um marcador que nenhum fragmento declara em
   lugar nenhum continua sendo **erro**, como hoje — a proteção contra `__ApiPackgeReferences__`
   digitado errado não se perde. Para uma combinação em que nenhum fragmento selecionado contribui,
   o valor é a **string vazia**.
4. **Colisão com marcador de valor.** Um nome que já seja marcador de valor (`ProjectName`,
   `TargetFramework`, `TemplateVersion`) é defeito. Os dois conjuntos são disjuntos.
5. **Ordem.** A ordem de concatenação é a ordem de seleção de fragmentos, que é declarada e estável:
   `common`, `architecture/*`, `database/*`, `auth/*`, `swagger/enabled`. Determinística por
   construção (ADR-0003, item 5); não é ordem alfabética de pacote, e o `.csproj` gerado sai
   agrupado por eixo, que é como um humano lê a diferença entre duas combinações.
6. **Substituição de valor dentro da contribuição.** O conteúdo de uma contribuição passa pelos
   marcadores de **valor** (`__ProjectName__` e companhia) antes de ser inserido — é o que permite
   uma `ProjectReference` contribuída. Um marcador de **contribuição** dentro de uma contribuição é
   erro, e cai sozinho na regra 3: não há aninhamento, e portanto não há recursão.
7. **Regra da linha.** Quando o marcador é a única coisa da linha **e começa na coluna 0**, o motor
   substitui a linha inteira, **incluindo a quebra de linha**. Zero contribuições ⇒ a linha some,
   sem deixar linha em branco órfã. Cada contribuição carrega a **própria indentação**; o motor não
   reindenta nada. Um marcador sozinho na linha **indentado** é defeito, com mensagem dizendo isto —
   errar aqui produziria indentação quebrada em silêncio.
7b. **O invólucro pertence à contribuição, não ao arquivo hospedeiro.** Corolário da regra 7, e
   obrigatório: quem escreve template **não** envolve o marcador em `<ItemGroup>`, `{ }` ou qualquer
   outro par de abre-fecha. Cada contribuição traz o próprio invólucro completo. Caso contrário o
   motor apaga a linha do marcador e o invólucro vazio sobrevive — um `<ItemGroup></ItemGroup>` sem
   nada dentro, entregue a um humano. Vários `<ItemGroup>` num `.csproj` são válidos em MSBuild e
   leem melhor: cada bloco é um eixo, com o próprio comentário.
8. **Em qualquer outra posição** — no meio de uma linha ou **no caminho** do arquivo — a
   substituição é literal, com `\n` entre contribuições. É a forma usada por marcador de uma linha
   só, como `__ApiProjectDir__`.
9. **A contribuição é texto inerte.** O motor não avalia, não ordena por conteúdo, não desduplica e
   não interpreta. Não há condicional, não há laço, não há expressão. Esta é a fronteira que impede
   o mecanismo de virar uma linguagem de template por acréscimo.

#### O que isso custa em C#

Uma mudança localizada, de responsabilidade do papel `backend`: `GenerationPlan.Resolve` passa a
separar os arquivos de `__parts__/` dos demais e a montar o dicionário de marcadores em duas etapas.
`EmbeddedTemplateSource`, `TemplateAxes` e `DeterministicZip` **não mudam** — o curinga do `.csproj`
já embute qualquer arquivo sob `Templates/`, e uma contribuição é só mais um arquivo.

**Acrescentar um valor de eixo continua sem exigir C#:** criar
`Templates/database/mysql/__parts__/ApiPackageReferences.xml` basta. Acrescentar um **ponto de
contribuição** novo também: o marcador nasce do nome do arquivo.

## Como isto sobrevive a ADR-0008

ADR-0008 é a razão de esta ADR existir e precisa ser encarada de frente, não citada de lado.

**O que a camada 1 prova, e prova sozinha:** o conjunto de `PackageReference` **declaradas** em cada
`.csproj` do pacote, lido como XML, é exatamente o esperado para aquela combinação, e toda `Version`
é literal (`^\d+\.\d+\.\d+$`, sem intervalo e sem `*`). Como o conjunto é verificado por
**igualdade**, e não por "não contém Swashbuckle", nenhum pacote inesperado entra sem derrubar o
teste. Isso cobre RF-20 e RNF-06 no que eles dizem: "sem a dependência **no projeto**", "versão
exata".

**O que a camada 1 não prova, dito com todas as letras:** dependência **transitiva**. Se um pacote
legítimo passar a arrastar Swashbuckle, nenhuma leitura de texto vê isso — é exatamente a prova B de
ADR-0008, e ali ela só apareceu no grafo de restore.

**Quem assume:** a **camada 2**, que roda `dotnet build` e portanto tem
`obj/project.assets.json` do projeto **gerado**. É o mesmo instrumento de ADR-0008, aplicado ao
projeto de saída em vez do de entrada. O conjunto pairwise da camada 2 cobre, por construção,
`swagger = false` contra todo valor de `database` e de `authentication`. A **camada 3** fecha pelo
comportamento: com `swagger = false`, `/openapi/v1.json` e `/swagger` não respondem.

Assim a afirmação de RF-20 é dividida e cada pedaço fica na camada que tem como prová-lo:
declarado ⇒ camada 1 (32, segundos); transitivo ⇒ camada 2 (9, com restore); observável ⇒ camada 3.
**Nenhuma camada finge provar o que não consegue.**

**Assert de sanidade, obrigatório**, pela mesma razão de ADR-0008 e de
[ADR-0010](adr-0010-estrutura-prevista-e-projecao.md): o teste da camada 1 **falha** se não achar
nenhum `.csproj` no pacote, ou se a matriz inteira não tiver produzido nenhuma `PackageReference`
para conferir. Um verificador que silenciosamente para de verificar é pior que nenhum — e um
`.csproj` que deixasse de ser gerado, ou um marcador que deixasse de ser preenchido, fariam este
teste passar por vacuidade.

## Alternativas descartadas

**`.csproj` em `common` importando `.props` por eixo, com `<Import Condition="Exists(…)" />`.**
A saída (i) do `backend`, e a mais tentadora: não exige tocar o motor. Descartada por três motivos,
em ordem de peso.

1. **Ela move a dependência para fora do `.csproj`** — e é precisamente o `.csproj` que RF-20 ("sem a
   dependência no projeto") e o critério de aceite 6 de T03 ("todo `PackageReference` **nos `.csproj`
   gerados**") nomeiam. Para continuar verificando, a camada 1 teria de seguir `Import`, resolver
   caminho relativo e avaliar `Condition` — reimplementar um pedaço do MSBuild dentro de um teste
   cujo valor inteiro é ser rápido e simples. É a piora exata que ADR-0008 ensina a não aceitar:
   trocar uma verificação direta por uma indireta, sem ganhar a fonte decisiva em troca.
2. **A condição é cerimônia morta.** `Exists(…)` só pode ser falsa se o gerador tiver decidido não
   escrever o arquivo — decisão já tomada em tempo de geração. O projeto entregue carregaria uma
   ramificação que nunca ramifica, e quem o recebesse gastaria tempo entendendo uma condição sem
   segundo caso.
3. **O pacote é produto para um humano.** Um `.csproj` com três `Import` condicionais não é o
   projeto .NET idiomático que a pessoa esperava ao clicar em "gerar".

**Merge/append entre fragmentos.** A saída (ii): dois fragmentos escrevem o mesmo caminho e o motor
combina. Descartada. Contradiz o modelo de união, e trocá-lo custa mais do que parece: "colisão é
defeito" é o que faz um erro de template aparecer como falha de teste com nome e caminho, em vez de
um arquivo silenciosamente sobrescrito. Além disso, merge de verdade é **por formato** — XML tem
`ItemGroup`, JSON tem vírgula, C# tem ordem de instrução —, e um motor que faz isso precisa de um
parser por formato, com regra de precedência e de ordenação em cada um. A contribuição por marcador
entrega o mesmo resultado com o ponto de inserção **declarado no template**, que é revisável por
diff, em vez de inferido por um algoritmo.

**`PackageReference` com `Condition` de propriedade MSBuild** (`Condition="'$(UseSwagger)'=='true'"`,
a propriedade vindo de um `.props` por eixo). Pior que (i) e recusada com firmeza: a dependência
ficaria **textualmente presente no `.csproj` mesmo com `swagger = false`**. RF-20 leria como violado
por qualquer pessoa, por qualquer `grep` e por qualquer auditoria ingênua — o arquivo passaria a
afirmar o contrário do que o pacote faz.

**Blocos condicionais dentro do próprio arquivo** (`__IF_SWAGGER__ … __END__` no `.csproj` de
`common`). Descartada por duas razões. Inverte o layout de diretórios: o conhecimento do eixo
`swagger` passaria a morar num arquivo de `common`, e `common` viraria o arquivo que conhece todas as
opções — o oposto do que "fragmento por eixo" existe para conseguir. E é uma linguagem de template:
o primeiro `IF` puxa o `ELSE`, que puxa o operador de comparação, e cada um deles é código C# novo,
contra a exigência de que um valor de eixo não custe C#.

**Um `.csproj` por combinação.** Trinta e dois arquivos quase iguais, com a diferença entre eles
invisível no diff. É a duplicação que o modelo de fragmentos existe para evitar.

## Consequências

- **O `.csproj` gerado continua sendo um `.csproj` comum**, literal e autocontido, e a camada 1
  continua sendo uma leitura de XML. É o ponto inteiro da decisão.
- **O mecanismo não é específico do `.csproj`.** `Program.cs` usa `__ProgramUsings__`,
  `__ProgramServices__` e `__ProgramMiddleware__`; `appsettings.json` usa
  `__AppSettingsSections__`. Sem isso, T04 bateria na mesma parede quatro vezes.
- **JSON é a aresta afiada conhecida.** Vírgula de separação não perdoa: o `appsettings.json`
  precisa ser montado com o marcador em posição tal que cada contribuição carregue a própria vírgula
  e exista um par estável **depois** dele. T04 confirma isso ao escrever o primeiro fragmento com
  banco; T03 não tem contribuição de `appsettings.json`.
- **Um novo caso de defeito de template**, com teste próprio: `__parts__` mal posicionado, nome de
  marcador inválido, colisão com marcador de valor, marcador indentado sozinho na linha. Todos
  falham na camada 1, que é onde um erro de template tem de aparecer.
- **A ordem de concatenação vira conteúdo do arquivo e, portanto, entra no SHA-256.** Trocar a ordem
  de seleção de fragmentos muda o hash de todos os pacotes. Ela está declarada em
  `TemplateAxes.Select` e agora também aqui.
- **Risco nomeado: o marcador pode virar linguagem por acréscimo.** A regra 9 é a cerca — e, como em
  ADR-0010, é cerca, não muro. Qualquer proposta de condicional, laço ou expressão dentro de uma
  contribuição precisa de ADR própria; esta não autoriza.
- **A alternativa de médio prazo que esta ADR não fecha:** se um dia o número de pontos de
  contribuição de `Program.cs` ficar grande a ponto de o arquivo de `common` virar um esqueleto de
  marcadores, o sinal é que a composição deveria ser por **arquivo por eixo** com registro
  automático (cada fragmento contribui um `IServiceCollection` extension próprio, e o `Program.cs`
  chama uma lista). Isso é desenho de projeto gerado, não de motor, e cabe a T04 decidir se
  precisa.
