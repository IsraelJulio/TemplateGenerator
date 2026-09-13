# ADR-0012 — Combinação sem template não responde `200`

**Data:** 2026-09-13 · **Estado:** aceita, **implementação pendente em T04** · **Levantada por:**
papel `qa` em T03, decidida pelo `architect` na mesma tarefa

## Contexto

Ao fim de T03 existe template para uma combinação: `simple` + `none` + `none`, nos dois valores de
`swagger`. Os diretórios `architecture/clean`, `database/sqlite`, `database/postgresql`,
`auth/identity` e `auth/jwt` contêm apenas `.gitkeep`.

O motor compõe pela **união dos fragmentos selecionados**
([`../architecture/generation-engine.md`](../architecture/generation-engine.md)). Um fragmento vazio
não é erro: ele simplesmente não contribui. A consequência é que as 16 combinações de `clean` geram
um ZIP com **cinco arquivos** — `.editorconfig`, `.gitignore`, `global.json`, `requests.http` e
`.templategenerator/manifest.json` — sem `.sln`, sem `.csproj`, sem `Program.cs` e sem `README.md`.

E respondem **`200 OK` com `Content-Disposition: attachment`**.

[`../product/vision.md`](../product/vision.md) promete "um ZIP compilável". Metade do catálogo
entrega o contrário disso, **afirmando que entregou**. Não é pendência interna: sai pela porta da
frente, com o cabeçalho que manda o navegador salvar o arquivo.

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
- **Limitação nomeada: "contribui pelo menos um arquivo" não é "está completo".** Um fragmento pela
  metade — T04 no meio do caminho — acenderia o valor e produziria pacote incompleto. Quem fecha esse
  buraco é a verificação de conteúdo obrigatório da camada 1, não a derivação. A derivação responde
  "alguém começou a escrever isto"; a camada 1 responde "e terminou".
- **Enquanto isto não for implementado, o defeito está de pé e registrado.** T03 fecha com ele. O
  `reviewer` não deve tratá-lo como resolvido por existir esta ADR — existe decisão, não conserto.
