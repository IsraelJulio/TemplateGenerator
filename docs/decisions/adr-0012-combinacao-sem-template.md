# ADR-0012 — Combinação sem template não responde `200`

**Data:** 2026-09-13 · **Estado:** aceita, **implementação pendente em T04** · **Levantada por:**
papel `qa` em T03, decidida pelo `architect` na mesma tarefa

## Contexto

Ao fim de T03 existe template para uma combinação: `simple` + `none` + `none`, nos dois valores de
`swagger`. Os diretórios `architecture/clean`, `database/sqlite`, `database/postgresql`,
`auth/identity` e `auth/jwt` contêm apenas `.gitkeep`.

O motor compõe pela **união dos fragmentos selecionados**
([`../architecture/generation-engine.md`](../architecture/generation-engine.md)). Um fragmento vazio
não é erro: ele simplesmente não contribui. E há **duas** formas de não contribuir, com gravidades
diferentes — foi subestimar a segunda que fez a primeira versão desta ADR registrar 16 de 32 quando
o número real é **30 de 32**.

1. **Não contribuir arquivo**: o fragmento não traz entrada de ZIP. O pacote sai com menos arquivos,
   e a falta é visível.
2. **Não contribuir texto**: o fragmento não traz `__parts__/`, e todo marcador que ele alimentaria
   vira **string vazia** (ADR-0011, item 3). O pacote sai com a **contagem certa de arquivos** e
   buracos dentro deles. A falta é invisível.

### As três classes de defeito

| Classe | Combinações | O que sai |
|---|---|---|
| `simple/none/none` | **2** | correto, verificado ponta a ponta |
| `simple/none/jwt` | 2 | sobe, CRUD funciona, **sem autenticação e sem aviso** |
| `simple` + `sqlite`/`postgresql` | 12 | 23 arquivos, compila, testes verdes, **crash no `dotnet run`** |
| `clean` (qualquer) | 16 | 5 arquivos, não é projeto |

**Duas das 32 combinações entregam o que o produto promete.** Todas as outras 30 respondem
`200 OK` com `Content-Disposition: attachment` — o cabeçalho que manda o navegador salvar o arquivo.
[`../product/vision.md`](../product/vision.md) promete "um ZIP compilável".

### `clean` — 16 combinações

ZIP com **cinco arquivos** — `.editorconfig`, `.gitignore`, `global.json`, `requests.http` e
`.templategenerator/manifest.json` — sem `.sln`, sem `.csproj`, sem `Program.cs` e sem `README.md`.
É o caso óbvio, e o menos perigoso dos três: ninguém confunde cinco arquivos com um projeto.

### `simple` + banco — 12 combinações

O pior em termos de **engano**, e pior que o da Clean **pelo argumento desta própria ADR**. O pacote
traz 23 arquivos, `.sln`, `.csproj`, `README.md` e projeto de testes. Mas `database/sqlite` e
`database/postgresql` não contribuem `__ItemStoreImplementation__` nem `__ProgramServices__`, então
o `Persistence/ItemStore.cs` entregue é isto, **inteiro**:

```csharp
using Acme.Models;

namespace Acme.Persistence;
```

Um arquivo sem classe. E o que acontece a seguir é o problema:

| Comando | Resultado |
|---|---|
| `dotnet build` | **`Build succeeded. 0 Warning(s)`** |
| `dotnet test` | **13 passed** |
| `dotnet run` | **crash** — `Unable to resolve service for type 'IItemStore' while attempting to activate 'ItemService'` |

Os testes gerados exercitam `ItemService` contra um duplo, sem subir a aplicação, então passam. **Os
dois comandos que o README manda rodar para conferir saúde ficam verdes**, e a falha só aparece no
terceiro. Esta ADR descarta "deixar como está" dizendo que *"um pacote com cinco arquivos plausíveis
é mais enganoso que um vazio"*; um pacote de 23 arquivos que compila limpo e passa nos próprios
testes é mais enganoso ainda.

### `simple` + `database: none` + `jwt` — 2 combinações, e é o grave

**A pessoa marca JWT na tela, recebe `200`, e recebe uma API com todos os endpoints públicos.**

O fragmento `auth/jwt` não contribui nada. Não há `__ProgramServices__` registrando autenticação,
não há middleware, e não há aviso. O projeto **compila, sobe e o CRUD funciona** — não há sintoma
algum. As seções do `README.md` gerado são:

```
## Pré-requisitos · ## Estrutura · ## Instalação · ## Banco de dados · ## Executar
## Verificar a saúde · ## Testar o CRUD de `Item` · ## Testes automatizados · ## Próximos passos
```

**Não há seção de autenticação.** A seção "## Autenticação" vem de
`auth/none/__parts__/ReadmeSetup.md`, e `auth/jwt` não tem o arquivo correspondente. Então o único
lugar do pacote que diria "esta combinação não tem autenticação" é justamente o lugar que só existe
quando a pessoa **não** pediu autenticação.

É o único dos três com **forma de defeito de segurança**: os outros dois falham ruidosamente (não
compila, não roda); este entrega uma API pública a quem pediu uma API protegida, e nada no pacote,
na tela ou na resposta HTTP contradiz isso. RF-14 exige `401` sem token; o pacote responde `200`.

É também o caso que **mais justifica a decisão abaixo**, porque é o único em que nenhuma verificação
posterior do lado de quem recebe o pacote acusaria o problema.

### O raciocínio já estava escrito

Em [`../architecture/http-contract.md`](../architecture/http-contract.md):

> *A alternativa — devolver `200 application/zip` com um pacote vazio — foi descartada: um `200` com
> `Content-Disposition` é uma **afirmação de que a geração aconteceu**. O cliente salvaria o arquivo
> e o defeito só apareceria ao abrir o pacote, longe da causa.*

Foi escrito sobre o estado anterior ao motor. Aplica-se, palavra por palavra, ao estado atual — e as
três classes acima estão em ordem crescente de distância entre a causa e o sintoma, com o agravante
de que um pacote com cinco arquivos plausíveis é **mais** enganoso que um vazio.

## Decisão

**Uma combinação sem template não produz pacote.** Duas camadas, porque o cliente da tela e o
cliente da API são pessoas diferentes com necessidades diferentes.

### 1. O catálogo diz o que está disponível — como dado

`GET /api/template-options` passa a marcar cada valor de opção como disponível ou não, e a dizer
por quê. É **dado**, como as `constraints` já são: o frontend desabilita o que vier desabilitado e
mostra a razão, sem codificar um único valor (RF-02 continua literal).

### 2. A disponibilidade é **derivada dos fragmentos**, nunca escrita à mão

> **Um valor de eixo está disponível se, e somente se, o fragmento dele contribui pelo menos um
> arquivo** — entrada de ZIP ou contribuição de `__parts__`. Um diretório com apenas `.gitkeep` não
> contribui nada, logo o valor não está disponível.

Esta é a parte que decide a qualidade da decisão. Uma lista de "o que já foi implementado" escrita
em C# ou no catálogo **vai** ficar desatualizada — é a mesma classe de deriva que
[ADR-0010](adr-0010-estrutura-prevista-e-projecao.md) documentou três vezes e que
[ADR-0008](adr-0008-fronteira-por-grafo-de-restore.md) documentou uma. Derivar do repositório
elimina a segunda fonte: quando T04 escrever `architecture/clean/...`, `clean` acende sozinho.
Nenhuma edição de catálogo, nenhuma linha de C#, nenhuma chance de alguém esquecer.

### 3. A API recusa a combinação com `501`, mesmo sem a tela

O backend é soberano e quem chama a API direto não passa pela tela. Uma requisição cuja combinação
inclua um valor indisponível responde **`501 Not Implemented`** em `ProblemDetails`, com
`type: .../problems/generation-not-implemented`, e **nunca** `200`.

`501` e não `400`: a configuração está **correta**. Ela passou pela validação, pertence ao catálogo
e satisfaz as restrições. Quem está incompleto é o servidor, e `400` diria à pessoa que a escolha
dela está errada — repetindo, num lugar novo, o erro que o contrato já recusou uma vez. É a mesma
frase de antes, agora com escopo menor: *a configuração passou pela validação, o motor para ela não
existe.*

O `problem+json` carrega quais campos causaram a recusa, para a mensagem poder ser específica
("A Clean Architecture entra em uma etapa seguinte") em vez de genérica.

## Forma de implementação — especificação de T04

**Acrescentada pelo `architect` em T04.** A decisão acima não mudou; o que faltava era a **forma**,
e ela atravessa `backend`, `frontend` e `qa`. Sem isto escrito, cada papel inventa a sua e as três
divergem no ponto em que se encontram.

Esta seção **não** é descrição de comportamento corrente. Ela vira descrição — e migra para
[`../architecture/http-contract.md`](../architecture/http-contract.md) — quando o código a tiver,
pela mesma regra que a primeira consequência desta ADR já fixa.

### 1. Onde a derivação mora

Em `TemplateGenerator.Generation`, ao lado de `TemplateAxes` e sobre a mesma `ITemplateSource` que
`EmbeddedTemplateSource` já implementa. **Nada de HTTP entra aqui**; a fronteira de
[`../architecture/platform.md`](../architecture/platform.md) continua valendo.

Um tipo novo — `Engine/TemplateAvailability.cs` — com uma única entrada: catálogo + origem de
fragmentos ⇒ o conjunto dos pares `(campo, valor)` **indisponíveis**. Ele não recebe lista nenhuma,
não tem constante de valor e não conhece `simple`, `clean`, `sqlite` ou qualquer outro: as duas
únicas fontes são o catálogo (quais valores existem) e a origem (quais fragmentos têm conteúdo).

**As quatro regras da derivação**, e cada uma existe porque a sua ausência quebra alguma coisa hoje:

- **R1 — contribuir arquivo.** Um valor `v` do campo `f` está **disponível** se, e somente se, o
  fragmento `<diretório de f>/<v>` tiver pelo menos um arquivo que seja **(a)** entrada de ZIP — um
  caminho fora de `__parts__/` — **ou (b)** uma contribuição `__parts__/` de conteúdo **não vazio**.
  A alínea (b) não é enfeite: `database/none`, `auth/none` e `swagger/enabled` **só** têm
  `__parts__/`, e sem ela a derivação declararia indisponível tudo que hoje funciona.
- **R1.1 — "não vazio" é o mesmo "não vazio" do motor.** `TemplateContributions` já descarta a
  contribuição cujo conteúdo, normalizado e sem quebras finais, tem comprimento zero. A derivação
  **usa esse mesmo teste**, exposto de um lugar só. Duas noções de vazio seriam duas respostas para
  "este fragmento contribui?".
- **R1.2 — `.gitkeep` não conta**, e isso já é verdade sem código novo: o `.csproj` de
  `TemplateGenerator.Generation` o exclui do `EmbeddedResource`, então um fragmento que só tenha
  `.gitkeep` chega à derivação com **zero** arquivos. Não escreva uma segunda exclusão.
- **R2 — valor sem fragmento está sempre disponível.** Quando a seleção de um valor não implica
  fragmento nenhum, não há ausência que se possa confundir com template incompleto, e o valor é
  disponível por definição. São dois casos hoje: a posição `false` de um eixo booleano — `swagger`
  ligado é `swagger/enabled`, desligado é a **ausência** do fragmento
  ([`../architecture/generation-engine.md`](../architecture/generation-engine.md)) — e todo valor de
  um campo sem diretório de eixo, que é `dotnetVersion`. **Sem R2 a derivação marcaria
  `swagger: false` indisponível e recusaria metade da matriz, inclusive as duas combinações que hoje
  funcionam de ponta a ponta.** É o erro mais fácil de cometer nesta tarefa.
- **R3 — combinação.** Uma combinação está disponível quando **todos** os valores que ela seleciona
  estão disponíveis. `common` não é valor de campo e não entra nesta conta; um `common` vazio quebra
  tudo e é assunto do assert de sanidade de R4.
- **R4 — nada de lista.** Nenhuma constante, nenhum `switch`, nenhuma entrada de catálogo escrita à
  mão diz o que está implementado. É a decisão 2 desta ADR e é o que a torna diferente de um
  comentário.

**Uma instância, calculada uma vez** — templates são recursos embutidos e imutáveis, e o gerador é
*stateless*. Ela é registrada como singleton e injetada onde for preciso; o construtor que aceita
uma origem arbitrária existe para o teste compor um repositório mínimo, como `GenerationEngine` já
faz.

### 2. O campo novo no catálogo

`GET /api/template-options` ganha **um membro de topo**, irmão de `constraints`, chamado
`unavailable`:

```jsonc
{
  "templateVersion": "1.0.0",
  "fields":      { /* … inalterado … */ },
  "constraints": [ /* … inalterado … */ ],
  "unavailable": [
    { "field": "database",       "value": "sqlite",     "reason": "O template desta opção ainda não foi escrito." },
    { "field": "database",       "value": "postgresql", "reason": "O template desta opção ainda não foi escrito." },
    { "field": "authentication", "value": "identity",   "reason": "O template desta opção ainda não foi escrito." },
    { "field": "authentication", "value": "jwt",        "reason": "O template desta opção ainda não foi escrito." }
  ]
}
```

**O que isto responde, ponto a ponto:**

- **`fields` não muda, em nada.** A ordem continua sendo a ordem da tela (regra de serialização 1) e
  `type` continua admitindo só `"choice"` e `"boolean"` (regra 3). Nenhuma das duas regras do
  contrato é tocada, e um cliente que ignore o membro novo se comporta exatamente como hoje: é
  acréscimo, e a regra 8 já autoriza extensão.
- **Por que fora de `values`, que seria o lugar óbvio.** Porque o eixo booleano **não tem** `values`.
  Se a disponibilidade morasse dentro de cada valor, `swagger` ficaria sem onde dizê-la, e no dia em
  que `swagger/enabled` esvaziasse a tela não teria como desabilitar o interruptor. Um formato que
  não consegue exprimir um dos cinco campos está errado no desenho, não no caso raro.
- **Por que uma lista só de indisponíveis, e não um `available` por valor.** Porque o estado final do
  produto é `"unavailable": []` — uma linha que se lê como "está tudo implementado" — em vez de um
  `"available": true` repetido em nove lugares para sempre. O risco de uma lista vazia significar
  "derivação quebrada" em vez de "tudo pronto" é real e é coberto pelo assert de sanidade do item 5.
- **A ordem é estável e declarada:** ordem dos campos no catálogo, e dentro de cada campo a ordem dos
  valores no catálogo. Mesma disciplina da regra 1.
- **`value` carrega o tipo do campo** — texto no campo de escolha, booleano no interruptor.
- **`reason` é uma frase só, igual para todos, derivada e não escrita por valor.** Um motivo
  específico por opção ("A Clean Architecture entra em uma etapa seguinte") teria de ser escrito à
  mão em algum lugar — exatamente a segunda fonte de verdade que a decisão 2 existe para não criar —
  e ficaria órfão no dia em que o valor acendesse. A especificidade já está na tela **pela posição**:
  a frase aparece colada ao rótulo da opção, que o catálogo já manda.
- **O frontend não codifica valor nenhum** para consumir isto: ele casa `field`/`value` contra o que
  o próprio catálogo lhe entregou. RF-02 continua literal.

**O que o `frontend` faz com o membro:** `evaluateAvailability` passa a ter **duas** origens de
"desabilitado" — a restrição de catálogo, que já existe, e a indisponibilidade, que é nova — e a
forma de saída (`{ value, disabled, reason }`) **não muda**, então a marcação da tela não muda.
Quando as duas incidirem sobre o mesmo valor, **a indisponibilidade vence**: ela não depende do
resto da seleção, logo é verdadeira faça a pessoa o que fizer, enquanto a mensagem de restrição
sugeriria um conserto que não resolveria nada.

### 3. A forma do `501`

```json
{
  "type": "https://templategenerator.local/problems/generation-not-implemented",
  "title": "Esta combinação ainda não gera projeto",
  "status": 501,
  "errors": {
    "architecture": ["O template desta opção ainda não foi escrito."]
  }
}
```

- **`type`** é o mesmo URI que existiu até T02. Restaurar `ProblemTypes.GenerationNotImplemented`
  com o mesmo valor é deliberado: o significado — "a configuração é válida, quem está incompleto é o
  servidor" — é o mesmo, com escopo menor. Um URI novo faria um cliente antigo tratar como
  desconhecido um caso que ele já sabia tratar.
- **`errors`, e não uma extensão nova**, é quem diz **qual campo** causou a recusa. É o que a ADR
  pede ("carrega quais campos causaram") e é o que a tela já sabe posicionar *inline*: a mesma
  estrutura do `400`, o mesmo caminho de código, a mesma marcação. Uma extensão paralela seria um
  segundo formato para a mesma informação.
- **Uma entrada por campo indisponível**, na ordem dos campos no catálogo. Quando dois campos
  estiverem indisponíveis, as duas entradas aparecem, cada uma com a mesma frase.
- **A frase de `errors` é a mesma `reason` do catálogo**, saída da mesma derivação. A pessoa lê as
  mesmas palavras tendo aprendido pela tela ou pela recusa.
- **Sem `detail`.** Com `errors` preenchido, um `detail` genérico só repetiria em prosa o que já está
  endereçado ao campo — e o ramo de `400` do frontend já trata `detail` como o que se mostra
  *quando não há* erro de campo. O ramo de `501` passa a fazer o mesmo.
- **`Content-Type: application/problem+json`**, como manda a regra 6 do contrato: é por ele que o
  cliente, que pediu o corpo como binário para receber o ZIP, distingue erro de arquivo.
- **Nenhum cabeçalho de download.** `Content-Disposition` e `Content-Type: application/zip` **não**
  podem aparecer numa resposta de recusa. Ver o item 4, que é onde isso se garante.

### 4. Onde a recusa entra na ordem — e por quê

**Depois da validação inteira**: depois da checagem do `projectName`, depois da pertinência ao
catálogo e **depois** da restrição `identity-requires-database`. Nunca antes.

Três razões, e a terceira sozinha decide:

1. **É o que a própria decisão afirma.** O `501` se justifica dizendo que "a configuração está
   correta: passou pela validação, pertence ao catálogo e satisfaz as restrições". Responder `501` a
   uma configuração que *não* passou tornaria essa frase falsa no exato caso em que ela é a
   justificativa.
2. **O `400` é acionável e o `501` não é.** Antecipar o `501` esconderia da pessoa o único problema
   que ela consegue consertar, em troca de mostrar primeiro o que ela não pode.
3. **Antes da validação, a derivação mente.** Um `architecture: "banana"` não tem fragmento — logo a
   derivação o chamaria de indisponível e a API responderia `501 "o template desta opção ainda não
   foi escrito"` para um valor que **não existe no catálogo**. A resposta verdadeira é `400, o valor
   não pertence ao catálogo`. Inverter a ordem transforma um erro claro de quem chamou num defeito
   inventado do servidor.

**O caso que o PO levantou, respondido:** `clean` + `identity` + `database: none` responde **`400`**,
com a mensagem `O Identity nativo precisa de um banco para persistir os usuários.` endereçada a
`authentication` — a regra 5 do contrato manda endereçar ao campo de `when`. Só depois de a pessoa
escolher um banco é que ela vê o `501` em `architecture`. Pela tela isso nem chega ao servidor: a
checagem local de restrição já bloqueia o envio, e é o mesmo texto.

**Em que dois lugares a recusa é imposta**, e por que não são duas fontes de verdade:

- **No endpoint**, antes de `GeneratedArchiveResult` existir. É obrigatório que seja **antes**:
  aquele resultado escreve `Content-Type: application/zip` e `Content-Disposition: attachment` como
  primeira coisa que faz, e uma recusa depois disso ou sai com cabeçalho de download em cima, ou
  depende de o corpo ainda não ter começado — exatamente a fragilidade que RNF-03 existe para não
  ter.
- **No motor**, dentro de `GenerationPlan.Resolve`, logo depois da validação e antes da composição,
  lançando uma exceção própria ao lado de `GenerationRequestRejectedException`. Sem este lado,
  **quem chama o motor direto continua recebendo o pacote defeituoso** — e quem chama o motor direto
  é a camada 1 inteira, que é justamente quem precisa provar a recusa sem subir HTTP.

Os dois perguntam ao **mesmo** `TemplateAvailability`. O que se duplica é a imposição, não a
verdade — e é a duplicação que faz "nunca `200`" valer também para um endpoint que alguém acrescente
amanhã.

### 5. O que a camada 1 verifica, nos dois sentidos

Esta ADR já exige os dois sentidos; aqui está o que cada um afirma, para o `qa` não ter de adivinhar
os casos de borda.

| # | Afirmação | Falha quando |
|---|---|---|
| 1 | Para **toda** combinação disponível, o pacote traz o conteúdo obrigatório de [`../architecture/generated-projects.md`](../architecture/generated-projects.md) | um fragmento acendeu sem estar completo |
| 2 | Para **toda** combinação disponível, nenhum arquivo gerado é só preâmbulo (a verificação de casca) | um marcador resolveu vazio onde vazio não é estado legítimo |
| 3 | Para **toda** combinação indisponível, o motor recusa — exceção do item 4, não pacote | alguém devolve pacote incompleto por um caminho que não é o endpoint |
| 4 | Para **toda** combinação indisponível, `POST /api/templates` responde `501`, com `problem+json`, com o campo em `errors`, **sem** `Content-Disposition` e **sem** `application/zip` | a recusa entrou tarde demais no pipeline |
| 5 | A soma "disponíveis + indisponíveis" é a matriz válida inteira, e o conjunto de disponíveis calculado pela derivação coincide com o calculado combinação a combinação por R3 | a derivação e a recusa discordam |
| 6 | **Assert de sanidade:** existe pelo menos uma combinação disponível **e**, enquanto houver fragmento vazio no repositório, pelo menos uma indisponível | a derivação passou a responder sempre a mesma coisa — o caso em que 1 a 4 passam por vacuidade |

O item 6 não é zelo: sem ele, uma derivação que devolvesse "tudo indisponível" faria os itens 1 e 2
passarem sem olhar pacote nenhum, e uma que devolvesse "tudo disponível" faria os itens 3 e 4
passarem sem recusar nada. Um verificador que silenciosamente para de verificar é pior que nenhum —
é a lição de [ADR-0008](adr-0008-fronteira-por-grafo-de-restore.md) e de
[ADR-0010](adr-0010-estrutura-prevista-e-projecao.md), pela quarta vez.

**O item 6 tem data de validade, e isso é bom:** quando o último fragmento for escrito (T08), não
haverá mais combinação indisponível e a segunda metade dele precisa cair junto. Ela é condicionada
à existência de fragmento vazio justamente para cair sozinha, sem ninguém precisar lembrar.

### 6. O ramo `notImplemented` do frontend: qual é o caso real

Há uma objeção óbvia a fazer aqui, e é melhor respondê-la do que deixar `frontend` e `qa` tropeçarem
nela: **se a tela desabilita o que está indisponível, como é que a tela recebe um `501`?**

Recebe, e o caso é real: **o catálogo é buscado uma vez, no carregamento da página.** Uma aba aberta
antes de uma implantação — ou antes de qualquer mudança no conjunto de fragmentos — segue com a
disponibilidade de ontem, e o servidor responde a verdade de hoje. É o mesmo motivo pelo qual o ramo
de `400` existe embora a tela já valide localmente: *"toda validação do frontend é conveniência; o
backend revalida tudo e é quem decide"*. O que mudou é o escopo — de "o motor de geração ainda não
existe" para "esta combinação ainda não gera projeto" —, não a existência do caso.

A mensagem precisa dizer a segunda coisa, e as escolhas da pessoa continuam intactas (RF-06). O
`errors` do item 3 é o que permite à tela marcar **o campo** que causou a recusa, em vez de só
mostrar um aviso geral.

## Alternativas descartadas

**Deixar como está, com o risco aceito e escrito.** Foi considerada de verdade: T04 está logo
adiante e o defeito tem prazo curto. Descartada porque o custo não é interno. Quem baixa um pacote
de cinco arquivos e o abre não sabe que está olhando uma pendência de backlog — sabe que o produto
mentiu. E "tem prazo curto" é a justificativa que todo débito apresenta no dia em que nasce.

O caso `simple/none/jwt` sozinho já encerraria a discussão: ali o prazo não é curto, é
**indeterminado**, porque o pacote não tem sintoma. Ele funciona. Alguém pode levá-lo para produção
achando que tem autenticação, e o defeito não expira quando T04 fechar — ele expira quando essa
pessoa descobrir, se descobrir. Um débito que o próprio produto esconde do dono não é débito
aceitável.

**Recusar com `400`.** Descartada acima: culpa o cliente por uma lacuna do servidor, e polui a
validação de campo com uma mensagem que não é sobre o campo.

**Esconder `clean` do catálogo inteiramente**, em vez de marcá-lo indisponível. Descartada: a pessoa
não descobriria que a Clean existe e está a caminho, e um catálogo cujo conteúdo muda de tamanho
conforme o estado da implementação é pior de versionar e de testar que um catálogo estável com uma
marca. Além disso a tela perderia a chance de dizer *por quê*.

**Marcar a disponibilidade à mão no catálogo.** Descartada pela regra 2: é a segunda fonte de
verdade que esta decisão existe para não criar.

## Consequências

- **É mudança de contrato HTTP**, nos dois endpoints. Cabe a T04, e
  [`../architecture/http-contract.md`](../architecture/http-contract.md) só passa a descrevê-la como
  comportamento corrente quando o código a tiver — documento não corre na frente de código, que é a
  regra que esta mesma tarefa aplicou ao `501` antigo.
- **O `501` volta, com escopo menor.** `ProblemTypes.GenerationNotImplemented` foi removido em T03 e
  precisa ser restaurado. Isso muda a nota deixada para T09 em `http-contract.md`: o ramo
  `notImplemented` do frontend **não** é código morto — ele passa a ter um caso real e mais estreito,
  "esta combinação ainda não gera projeto", e a mensagem precisa dizer isso em vez de "o motor de
  geração ainda não existe".
- **A camada 1 verifica os dois sentidos**, e isso não é opcional: para toda combinação **disponível**,
  o pacote traz o conteúdo obrigatório de
  [`../architecture/generated-projects.md`](../architecture/generated-projects.md); para toda
  combinação **indisponível**, a geração é recusada. Só o primeiro lado deixaria a derivação
  envelhecer sem ninguém notar — e um verificador que silenciosamente para de verificar é pior que
  nenhum.
- **Limitação nomeada: "contribui pelo menos um arquivo" não é "está completo"** — e o caso
  `simple` + banco mostra que a consequência dessa lacuna é pior do que parecia quando a escrevi.
  A derivação responde *"alguém começou a escrever isto"*. Um fragmento pela metade — T04 no meio do
  caminho — acenderia o valor.
- **E a mitigação que eu tinha indicado não basta.** Eu havia dito que a verificação de "conteúdo
  obrigatório presente" da camada 1 fecharia esse buraco. **Não fecha.** Em `simple` + banco, *todo*
  arquivo obrigatório está presente: `.sln`, `.csproj`, `README.md`, `Program.cs`, o projeto de
  testes, e o próprio `Persistence/ItemStore.cs`. O que falta está **dentro** do arquivo. Presença é
  satisfeita por uma casca, e uma casca compila.
  **A camada 1 precisa, além da presença, de uma verificação de casca**, e ela é estática e barata:
  para toda combinação disponível, nenhum arquivo gerado pode ser só preâmbulo — um `.cs` sem
  nenhuma declaração de tipo, um arquivo cujo conteúdo inteiro seja `using`, `namespace`, comentário
  e espaço em branco, é um marcador que resolveu vazio num lugar onde vazio não é estado legítimo.
  É o único sintoma de (a) que aparece **sem restore, sem build e sem executar**: `dotnet build` e
  `dotnet test` ficam verdes, então as camadas 2 e 3 chegam tarde ou não chegam.
- **Corolário para quem escreve template:** um marcador cujo valor vazio produz arquivo inválido é
  um marcador que **não devia estar sozinho no arquivo**. `ItemStore.cs` existe hoje como hospedeiro
  de um único `__ItemStoreImplementation__`; um hospedeiro que sem contribuição não é nada deveria
  pertencer ao fragmento que o preenche, não ao de arquitetura. Isso é desenho de template e cabe a
  T04 revisar ao escrever `database/sqlite`.
- **Enquanto isto não for implementado, o defeito está de pé e registrado.** T03 fecha com ele. O
  `reviewer` não deve tratá-lo como resolvido por existir esta ADR — existe decisão, não conserto.
