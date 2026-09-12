# Motor de geração

## Fluxo

```
Requisição → Validação → Seleção de fragmentos → Composição → Empacotamento → Stream ZIP
```

Nenhuma etapa executa processo externo. **Não há `dotnet restore`, `dotnet build` nem `dotnet new`
durante a requisição** (RNF-01). O motor só lê arquivos de template, substitui tokens e escreve
entradas de ZIP.

## Composição

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

A composição é a união ordenada dos fragmentos aplicáveis. Conflito de caminho entre dois
fragmentos é **erro de build dos testes**, não resolução silenciosa em tempo de execução: se dois
fragmentos escrevem o mesmo arquivo, o template está errado e um teste precisa falhar.

Substituição de tokens por marcador explícito (ex.: `__ProjectName__`), nunca por interpolação
de string em C#.

## Validação (RNF-03)

Antes de escrever qualquer byte:

1. `projectName` conforme `docs/product/option-matrix.md`.
2. Cada valor de campo pertence ao catálogo.
3. A restrição `identity-requires-database` é satisfeita.
4. Todo caminho de saída, depois de normalizado, continua **dentro** da raiz virtual do ZIP.
   Rejeitar `..`, caminho absoluto, raiz de drive e alternate data stream.
5. Nenhum caminho duplicado no conjunto final.

Falha em qualquer item ⇒ `ProblemDetails` 400 e **nenhuma escrita**.

## Determinismo (RNF-02)

Ver [ADR-0003](../decisions/adr-0003-zip-deterministico.md). Em resumo, para que o SHA-256 do
ZIP seja estável:

- `LastWriteTime` fixo em `1980-01-01T00:00:00Z` para toda entrada — o formato ZIP usa data DOS
  e **lança exceção abaixo de 1980**.
- Entradas ordenadas por caminho com comparação **ordinal**.
- `CompressionLevel` fixo e explícito.
- Conteúdo em UTF-8 **sem BOM**, quebras de linha `LF`, arquivo terminando em nova linha.
- Nada de `Guid.NewGuid()`, `DateTime.Now` ou caminho de máquina dentro do conteúdo gerado.

O manifesto (RF-22) fica em `.templategenerator/manifest.json` dentro do ZIP e contém
`templateVersion` e as opções escolhidas — e nada mais, para não quebrar o determinismo.

## Empacotamento

`System.IO.Compression.ZipArchive` escrevendo direto no corpo da resposta. Sem arquivo temporário
em disco, sem diretório compartilhado entre requisições — é isso que dá o isolamento exigido em
RNF-04.

Limites: número máximo de gerações simultâneas e limite de requisições por origem, ambos
configuráveis, respondendo `429` com `Retry-After` quando atingidos.
