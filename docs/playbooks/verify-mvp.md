# Playbook: verify-mvp

Como executar as camadas de teste da matriz, validar os projetos extraídos e registrar evidência.

Base: [`../quality/test-strategy.md`](../quality/test-strategy.md) e
[`../quality/definition-of-done.md`](../quality/definition-of-done.md).

## Antes de começar

```bash
dotnet --version          # esperado: 10.0.302
dotnet tool list --global # esperado: dotnet-ef 10.0.12
sc query postgresql-x64-18 # esperado: STATE : 4 RUNNING
```

Se o PostgreSQL não estiver rodando, **não prossiga e não pule os testes dele** — suba o serviço
ou registre bloqueio.

Defina um `NUGET_PACKAGES` compartilhado antes das camadas 2 e 4. Sem isso, cada geração baixa o
mundo de novo.

## Camada 1 — as 32, estática

Para cada uma das 32 combinações:

1. Gerar o ZIP.
2. Gerar **de novo** e comparar o **SHA-256** — precisa ser idêntico (RNF-02).
3. Extrair e verificar:
   - manifesto bate com as opções pedidas;
   - todos os arquivos obrigatórios presentes (`dotnet-templates.md`, seção 3);
   - nenhuma dependência de opção desmarcada no `.csproj`;
   - referências de projeto respeitam o diagrama da Clean Architecture;
   - nenhum caminho fora da raiz, nenhum caminho duplicado.

Erro esperado também é resultado: nomes inválidos e a combinação proibida
(`identity` + `database = none`) precisam ser **rejeitados** com `ProblemDetails`.

## Camada 2 — pairwise, build

Para cada combinação do conjunto pairwise: extrair, `dotnet restore`, `dotnet build`. Zero aviso
tratado como erro, zero falha.

O conjunto precisa vir acompanhado da **prova de que cobre todos os pares** de valores entre os
quatro eixos.

## Camada 3 — execução

Para cada combinação da lista de execução, seguindo **os comandos do README gerado**, sem editar
código-fonte:

1. Aplicar migração quando houver banco.
2. Subir a aplicação.
3. `GET /health` → `200`, **mesmo com autenticação ligada**.
4. CRUD completo de `Item`.
5. **Reiniciar** e conferir: `none` perdeu os dados; `sqlite`/`postgresql` mantiveram.
6. Com `identity`: cadastrar, logar (bearer) e renovar.
7. Com `jwt`: token válido passa; e os **quatro** casos de rejeição — assinatura, emissor,
   audiência, expiração — respondem `401`.
8. Sem token em combinação autenticada → `401`.
9. Swagger presente ou ausente conforme a seleção.

## Camada 4 — completa, sob demanda

`dotnet build` nas 32. Roda em T11 e por invocação explícita. Longa: planeje o tempo.

## Frontend

- Testes de formulário e integração.
- Playwright: preencher, baixar, **inspecionar o ZIP baixado**.
- Erros inline, envio duplicado, teclado, contraste, celular e desktop.

Playwright baixa navegadores na primeira execução.

## Registro de evidência

No relatório da tarefa:

- De cada comando: o comando literal, o **exit code** e a saída que comprova o critério. Log
  completo em arquivo quando for grande — ver
  [`../conventions.md`](../conventions.md#evidência-em-relatório).
- Os hashes comparados na camada 1.
- Captura de tela para o que é visual.
- A lista do que **não** foi executado e por quê.

**Nenhum resultado apenas planejado é registrado como aprovado.**
