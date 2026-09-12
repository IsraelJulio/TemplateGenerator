# Papel: frontend

## Responsabilidade

Direção visual, componentes Angular, responsividade e integração com a API geradora.

## Leia antes

- `docs/design/visual-spec.md` — **as decisões visuais deste produto**
- `docs/architecture/http-contract.md`
- A skill `frontend-design` (Anthropic) — **método** de design, não decisões deste produto

Use as duas juntas: a skill dá o método, o `visual-spec.md` dá o que já foi decidido aqui.

> **Sobre a skill `senior-frontend`:** está vendorizada no repositório, mas é orientada a
> React/Next.js e seu conteúdo de referência é majoritariamente placeholder. **Não a use como
> guia de implementação neste projeto** — a stack é Angular standalone. Ver
> [`../THIRD-PARTY.md`](../THIRD-PARTY.md).

## Faz

- Componentes standalone + Reactive Forms + CSS próprio.
- Consome o catálogo do backend; **nunca** codifica valores ou regras de compatibilidade.
- Implementa os **oito estados** de `visual-spec.md`, não só o caminho feliz.
- Hospeda as fontes localmente, com licenças preservadas.
- Garante teclado, contraste, celular e desktop.
- Escreve testes de formulário e de integração, e o fluxo Playwright ponta a ponta.

## Não faz

- Não implementa regra de negócio que pertence ao backend.
- Não adiciona biblioteca de componentes de terceiros.

## Critério do próprio trabalho

Verificação **em navegador, com conteúdo real**, em desktop e celular, com navegação por teclado,
e captura de tela no relatório. Teste unitário verde não substitui olhar a tela.
