# Definição de pronto

## Para qualquer tarefa

Uma tarefa só vira `done` quando **todos** os itens abaixo são verdadeiros:

1. Todos os `acceptanceCriteria` da tarefa em `docs/backlog.json` estão cumpridos.
2. Cada critério tem, no relatório, **o comando, o exit code e a saída que o comprova** — não
   uma afirmação de que funciona, nem uma paráfrase. Regra e limites em
   [`../conventions.md`](../conventions.md#evidência-em-relatório).
3. Todas as `verifications` da tarefa foram executadas e passaram.
4. O papel `reviewer` deu parecer, registrado no relatório, como passo separado da execução.
5. O relatório `docs/reports/<ID>.md` está completo conforme `_template.md`.
6. As mudanças estão commitadas.
7. Nenhum arquivo temporário, segredo ou credencial entrou no repositório.

Se um critério não foi cumprido, o estado é **`blocked`** com o motivo em `blockers` — nunca
`done` parcial, nunca critério removido para caber.

## Para a preparação (T00)

- `AGENTS.md`, `CLAUDE.md` e os documentos de `docs/` existem e são coerentes entre si.
- Todo link relativo entre documentos resolve para um arquivo existente.
- Todo agente em `.claude/agents/` aponta para um `docs/roles/*.md` existente.
- Toda skill local aponta para um `docs/playbooks/*.md` existente.
- Skills de terceiros estão fixadas em commit, com licença preservada e procedência registrada.
- `docs/backlog.json` é JSON válido, com IDs únicos, dependências existentes e sem ciclo.
- O checklist de paridade de `docs/interop.md` passa inteiro.
- **Distinção obrigatória:** a revisão estrutural dos arquivos e a confirmação de que o Claude
  Code realmente carrega os agentes são verificações **separadas**. Uma não substitui a outra.

## Para o MVP

O aceite final é o critério de sucesso de `docs/product/vision.md`:

> Uma pessoa consegue iniciar o gerador seguindo a documentação, escolher uma combinação, baixar
> o ZIP e executar o projeto realizando somente as configurações e comandos indicados no README.

Mais, concretamente:

- Camada 1 passa nas **32** combinações.
- Camada 2 passa no conjunto pairwise.
- Camada 3 passa nas combinações de execução, incluindo comportamento após reinício, Identity
  (cadastro/login/renovação) e os quatro casos de rejeição de JWT.
- Camada 4 (build nas 32) passa na execução de T11.
- Testes de frontend e o fluxo Playwright passam.
- A validação em ambiente limpo de T11 foi feita **num checkout novo**, não no diretório de
  trabalho.
