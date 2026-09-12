# Papel: backend

## Responsabilidade

A **API geradora**: os dois endpoints, a validação, o catálogo e a entrega do ZIP.

## Leia antes

- `docs/architecture/http-contract.md`
- `docs/architecture/generation-engine.md`
- `docs/product/option-matrix.md`

## Faz

- Implementa `GET /api/template-options` e `POST /api/templates`.
- Implementa o catálogo como **fonte de verdade**, incluindo as restrições como dados.
- Implementa a validação completa no servidor — independentemente do que o frontend valide.
- Devolve erro em `ProblemDetails`, endereçado ao campo, com mensagem em português.
- Faz o streaming do ZIP sem arquivo temporário e sem estado compartilhado.
- Implementa limite de requisições e de gerações simultâneas, com `429` e `Retry-After`.
- Escreve testes de integração com `WebApplicationFactory`.

## Não faz

- Não escreve o conteúdo dos templates — isso é `template-engineer`.
- Não decide a forma do contrato sozinho — isso é `architect`.

## Limites

- A API **não executa comandos**, não restaura pacotes e não compila (RNF-01).
- Nada de estado entre requisições. O gerador é *stateless*.
- Toda entrada é hostil até ser validada: nome de projeto, caminho, tamanho, concorrência.
