# ADR-0003 — ZIP determinístico por hash

**Data:** 2026-09-12 · **Estado:** aceita

## Contexto

RNF-02 exige que a mesma configuração, na mesma versão de template, produza o mesmo ZIP. Sem
cuidado explícito, dois downloads idênticos diferem byte a byte — os timestamps das entradas
carregam a hora da geração. Um determinismo "aproximado" não é testável; um determinismo por
hash é.

## Decisão

O critério é **SHA-256 do arquivo ZIP idêntico**. Para isso:

1. **`LastWriteTime` fixo em `1980-01-01T00:00:00Z`** em toda entrada.
2. **Ordenação ordinal** dos caminhos antes de escrever — nunca ordenação dependente de cultura,
   nunca a ordem em que o motor descobriu os arquivos.
3. **`CompressionLevel` explícito e fixo.**
4. **UTF-8 sem BOM**, quebras de linha `LF`, arquivo terminando em nova linha.
5. **Nada de não-determinístico no conteúdo**: sem `Guid.NewGuid()`, sem `DateTime.Now`, sem
   caminho de máquina, sem ordem de dicionário não ordenado.

### Por que 1980

O formato ZIP grava data no formato MS-DOS, cuja época começa em **1980-01-01**. Um valor
anterior faz `ZipArchiveEntry.LastWriteTime` lançar exceção. `DateTime.MinValue` e a época Unix
(1970) **não servem**.

## Alternativas descartadas

- **Determinismo só por conteúdo de arquivo** (comparar os arquivos extraídos, não o ZIP): mais
  fácil, mas não detecta variação de metadados e não dá um critério de aceite de uma linha.
- **Normalizar depois de gerar:** acrescenta uma etapa que pode ser esquecida.

## Consequências

- Os projetos extraídos carregam data de 1980. É visível e pode estranhar; é o preço do hash
  estável e não afeta build nem execução.
- Um teste da camada 1 gera cada combinação **duas vezes** e compara os hashes.
- Mudar a versão do template muda o hash **de propósito** — por isso o manifesto carrega
  `templateVersion`.
