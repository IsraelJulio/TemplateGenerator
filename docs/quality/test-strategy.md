# Estratégia de testes

## As três camadas da matriz

Compilar e executar as 32 combinações a cada rodada é caro demais para ser hábito — e um teste
que ninguém roda não protege nada. A matriz é **estratificada**. Ver
[ADR-0002](../decisions/adr-0002-validacao-estratificada.md).

### Camada 1 — estática, **as 32 combinações**, sempre

Gera o ZIP e inspeciona o conteúdo. **Sem `restore`, sem `build`.** Segundos, roda em todo commit.

Verifica:

- O ZIP gera sem erro e o manifesto bate com as opções pedidas.
- Determinismo: gerar duas vezes produz o **mesmo SHA-256** (RNF-02).
- Nenhuma dependência de opção não marcada no `.csproj` (ex.: sem Swashbuckle quando
  `swagger = false`; sem Npgsql quando `database ≠ postgresql`).
- Referências de projeto respeitam o diagrama de `generated-projects.md`.
- Todo arquivo obrigatório presente.
- Nenhum caminho fora da raiz, nenhum caminho duplicado.

### Camada 2 — compilação, **~9 combinações** (pairwise)

`dotnet build` sobre um conjunto que cobre **todos os pares de valores** entre os quatro eixos.
Para fatores 2×3×3×2 o mínimo teórico é 9; o conjunto escolhido fica registrado em código com a
prova de cobertura de pares.

Um `NUGET_PACKAGES` compartilhado entre as gerações é obrigatório aqui — sem ele, cada restore
baixa tudo de novo.

### Camada 3 — execução, **~6 combinações**

Sobe o projeto e exercita comportamento real. Só onde há comportamento novo, não combinatória:

| Combinação | O que prova |
|---|---|
| simple + none + none | CRUD público, volatilidade após reinício (RF-16) |
| simple + sqlite + identity | persistência real, cadastro/login/renovação (RF-17, RF-18) |
| clean + postgresql + identity | persistência real em PG, Clean equivalente |
| clean + sqlite + jwt | validação de JWT e os quatro casos de rejeição (RF-19) |
| simple + none + jwt | JWT sem banco |
| clean + postgresql + none | Clean + PG sem autenticação |

Cada uma é executada **com `swagger = true` e `swagger = false` alternados**, de modo que RF-20
seja exercitado nos dois estados em runtime.

### Camada 4 — completa, sob demanda

`dotnet build` nas **32**. Roda em T11 e por invocação explícita (`--full`), não no ciclo normal.

## Provedor OIDC de teste

Ver [ADR-0006](../decisions/adr-0006-oidc-in-process.md). Sem container: um emissor
**in-process** hospedado por `WebApplicationFactory`, expondo `/.well-known/openid-configuration`
e um JWKS, assinando com chave gerada no próprio teste.

Isso é o que permite testar os quatro casos de rejeição de RF-19 de forma determinística:
assinatura inválida, emissor errado, audiência errada e token expirado.

## PostgreSQL nos testes

Serviço nativo `postgresql-x64-18`, já em execução. Cada teste usa um **database descartável**
com nome único, criado e derrubado pelo próprio teste. Sem container, sem banco compartilhado
entre execuções.

Se o serviço não estiver disponível, os testes de PostgreSQL **falham com mensagem explícita** —
não são silenciosamente pulados.

## Frontend

- Testes de formulário e de integração dos componentes Angular.
- **Playwright** para o fluxo ponta a ponta: preencher a tela, baixar o ZIP e inspecionar o
  conteúdo baixado.
- Verificações de erro inline, envio duplicado (RF-07), navegação por teclado, contraste, e
  layout em celular e desktop (RNF-10).

Playwright baixa navegadores na primeira execução — conta como dependência de rede e precisa
estar no README.

## Regra que vale para tudo

**Nenhum resultado apenas planejado é registrado como aprovado.** O relatório da tarefa carrega a
evidência de execução: o comando, o exit code e a saída que comprova o critério.
Ver [`../conventions.md`](../conventions.md#evidência-em-relatório).
