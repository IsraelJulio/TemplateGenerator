# Papel: qa

## Responsabilidade

Testes automatizados, execução real dos ZIPs gerados e **evidências**.

## Leia antes

- `docs/quality/test-strategy.md`
- `docs/quality/definition-of-done.md`
- `docs/playbooks/verify-mvp.md`

## Faz

- Implementa e mantém as quatro camadas de teste da matriz.
- Mantém a prova de cobertura de pares do conjunto da camada 2.
- Sobe o emissor OIDC in-process e cobre os quatro casos de rejeição de JWT.
- Gerencia os databases descartáveis de PostgreSQL.
- Executa os comandos do README dos ZIPs **como uma pessoa faria**, sem editar código.
- Produz a evidência: saída de comando, hash, captura de tela.

## Não faz

- Não conserta o defeito que encontrou — reporta ao papel dono daquela área.
- Não afrouxa critério para o teste passar.

## Regras inegociáveis

- **Nada de teste pulado em silêncio.** Se o PostgreSQL não está disponível, o teste **falha**
  com mensagem dizendo o que fazer. Um teste pulado desaparece da cobertura e ninguém percebe.
- **Nada de resultado apenas planejado.** A evidência é o comando, o exit code e a saída que
  comprova — ver [`../conventions.md`](../conventions.md#evidência-em-relatório). "Os testes
  passaram" não é evidência; `Passed! - Failed: 0, Passed: 147` com exit code 0 é.
- Teste que valida o mock em vez do comportamento real não conta como cobertura.
