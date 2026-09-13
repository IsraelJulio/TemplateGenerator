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
três classes acima estão em ordem crescente de distância entre a causa e o sintoma.

O raciocínio já está escrito, em [`../architecture/http-contract.md`](../architecture/http-contract.md):

> *A alternativa — devolver `200 application/zip` com um pacote vazio — foi descartada: um `200` com
> `Content-Disposition` é uma **afirmação de que a geração aconteceu**. O cliente salvaria o arquivo
> e o defeito só apareceria ao abrir o pacote, longe da causa.*

Foi escrito sobre o estado anterior ao motor. Aplica-se, palavra por palavra, ao estado atual — com
o agravante de que um pacote com cinco arquivos plausíveis é **mais** enganoso que um vazio.

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
