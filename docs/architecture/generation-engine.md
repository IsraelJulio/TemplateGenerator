# Motor de geração

## Fluxo

```
Requisição → Validação → Seleção de fragmentos → Composição → Empacotamento → Stream ZIP
```

Nenhuma etapa executa processo externo. **Não há `dotnet restore`, `dotnet build` nem `dotnet new`
durante a requisição** (RNF-01). O motor só lê recursos embutidos, substitui marcadores e escreve
entradas de ZIP.

As duas primeiras etapas acontecem **inteiramente antes do primeiro byte** ir para o destino:
`GenerationPlan` resolve o pacote inteiro na memória e só então `DeterministicZip` escreve. É o que
permite uma recusa virar `400` de verdade — depois do primeiro byte o status já foi enviado e o
cliente receberia um ZIP truncado com `200` em cima (RNF-03).

## Onde os templates moram

Os templates vivem em `src/TemplateGenerator.Generation/Templates/` organizados por eixo:

```
Templates/
├─ common/                  o que existe em toda combinação
├─ architecture/simple/
├─ architecture/clean/
├─ database/none/
├─ database/sqlite/
├─ database/postgresql/
├─ auth/none/
├─ auth/identity/
├─ auth/jwt/
└─ swagger/enabled/
```

`swagger` é eixo booleano e tem **um** diretório: `swagger = false` é a *ausência* do fragmento, não
um `swagger/disabled` vazio existindo por simetria (RF-20).

Os **valores** vêm do catálogo, não de uma lista em C#. Publicar `database/mysql` no catálogo passa
a exigir o diretório correspondente sem tocar em `TemplateAxes`. O que está escrito em C# é só a
correspondência entre nome de campo e nome de diretório, que não é dedutível — o campo se chama
`authentication` e o diretório se chama `auth`.

### Carga: recurso embutido, não arquivo em disco

Os arquivos de `Templates/**` são **embutidos no assembly** em tempo de compilação
(`EmbeddedResource` com curinga no `.csproj` de `TemplateGenerator.Generation`), e não copiados para
`bin/`. Três razões:

- a requisição não toca o sistema de arquivos, e RNF-01 e RNF-04 ficam triviais de sustentar;
- o conjunto de templates não pode ser alterado por nada que apareça no diretório de saída;
- o mesmo binário se comporta igual rodando por `dotnet run`, publicado ou dentro de
  `WebApplicationFactory`.

O nome lógico de cada recurso é fixado como `templates/<caminho relativo com '/'>`. Sem isso o nome
default do MSBuild trocaria cada separador de diretório por ponto, e `Templates.common.global.json`
não permitiria distinguir diretório de extensão.

**Acrescentar um fragmento é criar o arquivo.** O curinga pega; nenhuma mudança em C# e nenhuma
mudança no `.csproj` são necessárias. `.gitkeep` é o único nome ignorado — é plumbing de
repositório, não fragmento; um `.gitignore` ou `.editorconfig` de template entra normalmente.

Um arquivo sob `Templates/` que não caia em nenhum fragmento declarado pelo catálogo é **ignorado
pelo motor e denunciado por teste**. O motor não decide o que é template válido; o teste decide. Sem
esse teste, um diretório com o nome errado seria só um fragmento que nunca entra em pacote nenhum, e
isso não falha em lugar algum.

Fragmentos são arquivos de **texto UTF-8**. Conteúdo binário não é suportado: tudo passa pela
normalização de determinismo, e nenhum item do conteúdo obrigatório de um ZIP
([`generated-projects.md`](generated-projects.md)) é binário.

## Marcadores

A sintaxe é **`__Nome__`**: dois sublinhados, um identificador ASCII começando por letra
(`[A-Za-z][A-Za-z0-9]*`), dois sublinhados. O padrão é deliberadamente restrito e sem sublinhado
interno, para que `__A____B__` não case como um marcador só.

Vale tanto no **conteúdo** quanto no **caminho** do arquivo — é assim que
`src/__ProjectName__/__ProjectName__.csproj` vira o diretório do projeto.

Marcador explícito, **nunca interpolação de string em C#**: o template precisa ser um arquivo
legível e revisável por diff, e um `$"..."` devolveria o template para dentro do código
([`platform.md`](platform.md)).

**Marcador desconhecido é erro**, não texto que passa adiante. Um `__ProjectNme__` digitado errado
sobreviveria em silêncio até o `.csproj` gerado, e o defeito apareceria como falha de compilação no
projeto de outra pessoa, longe da causa.

### Nenhum comentário de template cita um marcador pelo nome

**Regra de T04, achada ao escrever a Clean.** O motor substitui marcador **em qualquer posição do
texto** — ele não sabe o que é comentário, porque ADR-0011, item 9, é justamente a decisão de o
motor não interpretar nada. Então um comentário que **explique** um marcador citando-o pelo nome
recebe, ali dentro, o valor dele.

O caso real: um `<!-- … __ApiPackageReferences__ … -->` no `.csproj` do `Domain` virou o
`<ItemGroup>` inteiro do Swagger **dentro do comentário**, e o projeto gerado passou a falhar com
`MSB4025: An XML comment cannot contain '--'`. **Só aparecia com `swagger = true`**, porque com
`swagger = false` o marcador resolvia vazio e o comentário continuava um comentário.

A regra é geral e não tem exceção:

> Comentário de template **não cita marcador pelo nome**. Para falar de um, descreva-o
> (`a contribuição de PackageReferences`) ou quebre a forma (`__ ApiPackageReferences __`).

Vale para todo formato, não só XML: o mesmo texto num `//` de C# injetaria várias linhas dentro de
um comentário de uma linha só, e num `#` de `.gitignore` transformaria a explicação em regra.

**Duas guardas, porque a regra sozinha depende de alguém lembrar:**

1. **No template**, ao lado da guarda da regra 7b: um marcador conhecido citado dentro de um
   comentário XML de um arquivo hospedeiro é violação. O reconhecedor é de **linha**, como o de 7b —
   entre `<!--` e `-->` na mesma linha, ou numa linha depois de `<!--` sem `-->` fechado — e o
   alcance dele precisa estar escrito onde ele mora: comentário de C#, de Markdown e de outros
   formatos ficam de fora, e isso é limite declarado, não descuido.
2. **No pacote**, a rede geral: **todo arquivo gerado de extensão XML — `.csproj` à frente — tem de
   ser XML válido**, com mensagem dizendo qual combinação e qual arquivo. Ela não depende de
   reconhecer a causa e pega qualquer estrago da mesma família. É barata: a camada 1 já abre esses
   arquivos como XML para outras verificações.

A guarda 2 é a que teria pegado este defeito; a 1 é a que o pega **antes** de virar pacote.

### Marcadores de valor

Vêm da requisição e do catálogo. São três:

| Marcador | Valor |
|---|---|
| `__ProjectName__` | `projectName`, já normalizado. Ex.: `Acme.Billing` |
| `__TargetFramework__` | `dotnetVersion`. Ex.: `net10.0` |
| `__TemplateVersion__` | versão do catálogo vigente. Ex.: `1.0.0` |

### Marcadores de contribuição

Ver [ADR-0011](../decisions/adr-0011-contribuicao-por-marcador.md), que é onde a decisão está
registrada com as alternativas descartadas.

Um fragmento pode carregar arquivos que **não viram entrada do ZIP** e cujo conteúdo se torna o
valor de um marcador. Eles moram em `__parts__/` na raiz do fragmento:

```
Templates/swagger/enabled/__parts__/ApiPackageReferences.xml
Templates/database/sqlite/__parts__/ApiPackageReferences.xml
```

O motor concatena as contribuições dos fragmentos **selecionados**, na ordem de seleção, e
substitui. As regras, todas verificadas por teste:

1. **Caminho:** exatamente `__parts__/<Nome>.<ext>`, dois segmentos. A extensão existe para o editor
   colorir e é ignorada. `__parts__` em qualquer outra posição do caminho é defeito.
2. **Nome:** `<Nome>` vira `__<Nome>__` e precisa casar `[A-Za-z][A-Za-z0-9]*`. No máximo um arquivo
   por marcador por fragmento.
3. **Marcador conhecido:** o conjunto é a união sobre **todos** os fragmentos do repositório, não só
   os selecionados. Sem contribuição selecionada, o valor é a string vazia; um marcador que ninguém
   declara em lugar nenhum continua sendo erro.
4. **Disjunção:** um nome que já seja marcador de valor é defeito.
5. **Ordem:** a ordem de seleção — `common`, `architecture/*`, `database/*`, `auth/*`,
   `swagger/enabled`. Declarada, estável, e portanto parte do hash.
6. **Aninhamento:** a contribuição passa pelos marcadores de *valor* antes de entrar; um marcador de
   *contribuição* dentro de uma contribuição é erro. Não há recursão.
7. **Regra da linha:** marcador sozinho na linha **e começando na coluna 0** substitui a linha
   inteira, incluindo a quebra. Zero contribuições ⇒ a linha some. Cada contribuição carrega a
   própria indentação; o motor não reindenta. Marcador sozinho na linha e **indentado** é defeito.
7b. **O invólucro pertence à contribuição.** Não envolva o marcador em `<ItemGroup>` nem em nenhum
   outro par de abre-fecha no arquivo hospedeiro: o motor apaga a linha do marcador, mas não sabe
   apagar o invólucro em volta dela, e o pacote sairia com um grupo vazio. Cada contribuição traz o
   próprio invólucro completo.
8. **Qualquer outra posição** — no meio de uma linha ou no **caminho** — é substituição literal, com
   `\n` entre contribuições.
9. **A contribuição é texto inerte.** Sem condicional, sem laço, sem expressão. O motor não avalia,
   não ordena por conteúdo e não desduplica.

## Composição

A composição é a **união ordenada** dos fragmentos aplicáveis. Colisão de caminho entre dois
fragmentos é **defeito de template**, não resolução silenciosa em tempo de execução: o motor recusa
a geração com o caminho e os dois fragmentos culpados na mensagem, e a camada 1 transforma isso em
falha de teste.

A ordem de seleção é declarada e estável, mas **não é precedência**. Nenhum fragmento vence outro;
não existe sobrescrita.

### A regra do `.csproj`

**O `.csproj` de um projeto pertence ao fragmento de `architecture`.** É a arquitetura que decide
quantos projetos existem, como se chamam e onde ficam. Os demais eixos contribuem suas
`PackageReference` pelo marcador `__ApiPackageReferences__`.

O resultado é um `.csproj` com as dependências **literais**, o que é o que permite à camada 1 —
estática, sem restore — afirmar RF-20 e RNF-06 lendo um arquivo. O porquê, e por que as saídas com
`.props` importado foram recusadas, está em
[ADR-0011](../decisions/adr-0011-contribuicao-por-marcador.md).

A mesma regra vale para tudo cujo **caminho** dependa da arquitetura: `Program.cs`, a `.sln` e o
conteúdo das pastas de projeto. Os nomes exatos das pastas estão em
[`generated-projects.md`](generated-projects.md).

### A regra do hospedeiro mínimo

**Decisão de T04.** Um arquivo hospedeiro cujo conteúdo, com **zero contribuições**, deixa de ser um
arquivo válido daquele formato **pertence ao fragmento que o preenche**, não ao de arquitetura.

É a regra 7b de [ADR-0011](../decisions/adr-0011-contribuicao-por-marcador.md) generalizada do
invólucro para o arquivo inteiro, e ela nasce de um caso real:
`Persistence/ItemStore.cs` mora hoje em `architecture/simple` e existe só para hospedar
`__ItemStoreImplementation__`. Sem contribuição de `database/*`, o que sai no pacote é um `.cs` com
`using` e `namespace` e mais nada — um arquivo que **compila** e que só falha ao executar
([ADR-0012](../decisions/adr-0012-combinacao-sem-template.md), "`simple` + banco").

A pergunta a fazer diante de cada arquivo novo de template é curta: **com nenhuma contribuição, isto
ainda é um arquivo?** Se a resposta é não, o arquivo é do outro eixo.

- `Program.cs` e o `.csproj` **passam**: sem contribuição nenhuma continuam sendo um programa e um
  projeto válidos, só que menores. Permanecem em `architecture/*`.
- `ItemStore.cs` **não passa**: sem contribuição ele não é nada. Pertence a `database/*`, que é quem
  sabe como um item é guardado — e aí cada banco traz o seu, **inteiro**.

**Correção de T04: "inteiro" não quer dizer "sem marcador nenhum".** A primeira redação desta regra
dizia que o arquivo mudado de eixo viria "sem marcador", e isso é forte demais — o
`template-engineer` esbarrou nisso ao escrever a Clean e tinha razão. Um `ItemStore.cs` que mora em
`database/*` precisa saber **em que namespace ele nasce**, e namespace é decisão da arquitetura, não
do banco. O que a regra proíbe é o arquivo depender de marcador para ter **a própria substância**;
marcador que traz o que pertence a **outro** eixo — namespace, caminho — é o mecanismo funcionando
como deve.

A forma precisa, e é ela que o `template-engineer` aplica:

> O hospedeiro tem de trazer o conteúdo **do próprio eixo** escrito nele. Pode carregar marcador
> alimentado por outro eixo.

E há um critério mais afiado, que dispensa julgamento no caso comum: **um marcador alimentado
exclusivamente por um eixo sempre selecionado — `common` ou `architecture/*` — nunca resolve
vazio.** `TemplateAxes.Select` sempre escolhe os dois, então a contribuição sempre existe e o caso
que esta regra teme não chega a ser possível. Um `__PersistenceHeader__` alimentado só por
`architecture/*` é seguro por construção, não por sorte.

**Marcador de caminho que varia por arquitetura: use o mecanismo que já existe.** Um arquivo de
`database/*` que precise cair dentro da pasta de um projeto cujo nome depende da arquitetura resolve
isso com um marcador **no caminho**, que é o item 8 de
[ADR-0011](../decisions/adr-0011-contribuicao-por-marcador.md) e já tem teste — `__ApiProjectDir__`
é o exemplo nomeado ali. É o uso pretendido; não invente mecanismo novo para isso.

Um cuidado de nome, e vale conferir antes de T05: **o marcador precisa dizer o que resolve.** Se em
`clean` ele resolver para o projeto de `Infrastructure` — que é onde a implementação da porta mora,
pela seção "Clean Architecture" de [`generated-projects.md`](generated-projects.md) — então
`__ApiProjectDir__` está misnomeado, porque o valor dele não é o diretório do `Api`. Nesse caso o
nome certo descreve o papel (`__PersistenceProjectDir__`), e trocar custa renomear dois arquivos de
`__parts__`. Marcador com nome que mente é a classe de defeito que ADR-0011 evita dando o nome do
marcador ao arquivo: o nome é a documentação.

A regra não substitui a verificação de casca da camada 1; ela evita o caso em vez de detectá-lo. As
duas ficam, porque a segunda alcança também o arquivo que alguém escrever amanhã sem ler esta.

### O manifesto

O manifesto (RF-22) fica em `.templategenerator/manifest.json` dentro do ZIP e contém
`templateVersion` e as opções escolhidas — e **nada mais**, para não quebrar o determinismo. "Gerado
em" e "gerado por" estão descartados: data e máquina são exatamente o que quebra o SHA-256 estável.

É escrito **pelo motor**, não por um fragmento, e entra por último, de modo que um fragmento que
reivindique esse caminho seja relatado como o que é — um fragmento invadindo um caminho do motor.

## Validação (RNF-03)

Antes de escrever qualquer byte:

1. `projectName` conforme [`../product/option-matrix.md`](../product/option-matrix.md).
2. Cada valor de campo pertence ao catálogo.
3. A restrição `identity-requires-database` é satisfeita.
4. Todo caminho de saída, depois dos marcadores, continua **dentro** da raiz virtual do ZIP.
5. Nenhum caminho duplicado no conjunto final.

Falha nos itens 1 a 3 ⇒ `ProblemDetails` 400 e **nenhuma escrita**. Falha nos itens 4 e 5 é defeito
de *template*, não de requisição: a configuração está certa e quem está errado é o repositório.

**Entra um passo entre o 3 e o 4, em T04** (ainda não implementado): a recusa por combinação
**indisponível**, decidida em [ADR-0012](../decisions/adr-0012-combinacao-sem-template.md). A posição
é a decisão, não um detalhe — **depois** da validação inteira, porque um valor fora do catálogo
também não tem fragmento e responder "o template não existe" a um valor inexistente inverteria a
culpa; e **antes** de qualquer composição, porque nada pode ter sido escrito. A especificação está em
ADR-0012, seção "Forma de implementação", item 4. Enquanto o código não a tiver, a lista acima
continua descrevendo o comportamento corrente.

A verificação de caminho é **léxica**, sem tocar o sistema de arquivos. Um caminho de ZIP não é um
caminho de máquina, e resolver contra o disco local traria a cultura, o drive corrente e o limite de
comprimento do Windows para dentro de uma decisão que precisa ser a mesma em qualquer máquina
(RNF-02). O que ela recusa:

- caminho vazio, absoluto, ou terminando em `/` — o pacote carrega apenas arquivos;
- os caracteres `< > : " | ? * \`. O `:` cobre de uma vez a letra de drive (`C:/x`) e o *alternate
  data stream* (`arquivo.txt:oculto`);
- caractere de controle e qualquer coisa fora do ASCII imprimível;
- segmento vazio (`//`) e os segmentos de travessia `.` e `..`. A checagem é **por segmento**:
  `Acme..Api.cs` é nome legítimo, `a/../b` não é. Com `..` e `.` fora, o caminho já é a própria forma
  normalizada e a profundidade nunca decresce — "continua dentro da raiz" deixa de ser uma conta e
  vira uma propriedade do formato aceito;
- segmento terminando em espaço ou ponto, que o Windows apaga ao extrair, fundindo dois arquivos em
  um;
- nome reservado do Windows, com ou sem extensão (`NUL.txt` não é arquivo comum).

O separador é sempre `/`: é o que a especificação do formato ZIP (APPNOTE, 4.4.17.1) manda gravar, e
uma entrada com `\` é lida como nome de arquivo no Linux e como diretório no Windows — o mesmo
pacote extrai diferente em cada lugar.

## Determinismo (RNF-02)

Ver [ADR-0003](../decisions/adr-0003-zip-deterministico.md). Em resumo, para que o SHA-256 do
ZIP seja estável:

- `LastWriteTime` fixo em `1980-01-01T00:00:00Z` para toda entrada — o formato ZIP usa data DOS
  e **lança exceção abaixo de 1980**. O deslocamento é `Zero`, e não o da máquina: a conversão para
  data DOS usa a parte local do `DateTimeOffset` como está, então um fuso diferente mudaria o hash.
- Entradas ordenadas por caminho com comparação **ordinal**.
- `CompressionLevel` fixo e **explícito**. `Optimal` é o default hoje, e é por isso mesmo que
  precisa estar escrito: um default que mude numa versão futura do runtime mudaria o hash de todos
  os pacotes sem uma linha de diff.
- Conteúdo em UTF-8 **sem BOM**, quebras de linha `LF`, arquivo terminando em nova linha. A
  normalização acontece **no motor**, não na disciplina de quem escreve template: um arquivo salvo
  com CRLF por um editor do Windows quebraria o determinismo sem mudar uma linha de conteúdo.
- Nada de `Guid.NewGuid()`, `DateTime.Now`, caminho de máquina ou ordem de dicionário não ordenado
  dentro do conteúdo gerado.

## Empacotamento

`System.IO.Compression.ZipArchive` escrevendo direto no corpo da resposta, com API assíncrona: o
corpo do Kestrel recusa escrita síncrona por padrão, e ligar `AllowSynchronousIO` prenderia uma
thread do pool por download. Sem arquivo temporário em disco, sem diretório compartilhado entre
requisições — é isso que dá o isolamento exigido em RNF-04.

## Limites

Número máximo de gerações simultâneas e limite de requisições por origem, ambos configuráveis,
respondendo `429` com `Retry-After` quando atingidos. Os limites são da **API**, não do motor: a
biblioteca de geração não conhece HTTP. Nomes de configuração e valores default em
[`http-contract.md`](http-contract.md).
