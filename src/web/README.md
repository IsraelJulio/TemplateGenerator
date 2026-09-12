# Frontend da plataforma

Aplicação Angular standalone que configura um projeto .NET e baixa o ZIP gerado.
Sem NgModules, sem biblioteca de componentes de terceiros, sem Tailwind, CSS próprio.

O visual acabado e os oito estados de [`docs/design/visual-spec.md`](../../docs/design/visual-spec.md)
são a tarefa T02. O que existe aqui é a estrutura e o contrato: montagem dinâmica do formulário
a partir do catálogo, leitura genérica das restrições e as chamadas aos dois endpoints.

## Comandos

| Comando         | O que faz                                                                           |
| --------------- | ----------------------------------------------------------------------------------- |
| `npm start`     | Sobe o servidor de desenvolvimento em <http://localhost:4200> com proxy para a API. |
| `npm run build` | Compila para `dist/web`.                                                            |
| `npm test`      | Roda os testes de unidade em modo headless (Vitest + jsdom).                        |

## Proxy para a API em desenvolvimento

`ng serve` encaminha tudo que começa com `/api` para a API geradora, segundo
[`proxy.conf.json`](proxy.conf.json):

```json
{ "/api": { "target": "http://localhost:5080", "secure": false } }
```

O alvo é o perfil `http` de
[`src/TemplateGenerator.Api/Properties/launchSettings.json`](../TemplateGenerator.Api/Properties/launchSettings.json).
**Se o perfil mudar de porta, `proxy.conf.json` precisa mudar junto.**

Para trabalhar com as duas pontas, em dois terminais:

```bash
dotnet run --project src/TemplateGenerator.Api --launch-profile http
npm start --prefix src/web
```

Com o proxy no lugar, o frontend chama caminhos relativos (`/api/template-options`,
`/api/templates`) e não precisa saber onde a API está. Em produção as duas pontas são servidas
pela mesma origem. Para apontar para outro host sem mexer em código, forneça o token
`API_BASE_URL` ([`src/app/core/catalog/api-base-url.ts`](src/app/core/catalog/api-base-url.ts)).

## Como o catálogo comanda a tela

O frontend não conhece nenhum valor de opção e nenhuma regra de compatibilidade (RF-02).
Tudo vem de `GET /api/template-options`:

| Arquivo                                            | Papel                                                    |
| -------------------------------------------------- | -------------------------------------------------------- |
| `src/app/core/catalog/template-options.model.ts`   | Os tipos do contrato HTTP, um a um.                      |
| `src/app/core/catalog/template-catalog.service.ts` | As duas chamadas e a leitura do `ProblemDetails`.        |
| `src/app/core/catalog/constraints.ts`              | Interpretação **genérica** das restrições do catálogo.   |
| `src/app/core/form/template-form.builder.ts`       | Montagem do Reactive Form a partir dos campos recebidos. |
| `src/app/features/configurator/`                   | A tela.                                                  |

`constraints.ts` não menciona `authentication`, `identity` nem `database`. Para saber se um valor
está disponível ele simula a escolha e pergunta às restrições do catálogo — por isso uma restrição
nova no backend aparece na tela sem uma linha de código a mais aqui. Há um teste que prova isso
(`constraints.spec.ts`, "interpreta uma restrição nova sem alteração de código").

A validação do nome do projeto (`src/app/core/validation/project-name.validator.ts`) é a única
regra duplicada, e de propósito: ela existe para dar resposta imediata enquanto a pessoa digita.
O backend revalida e é quem decide.

## Testes

`npm test` roda o Vitest em jsdom, sem navegador. Os testes usam o catálogo de exemplo de
`src/app/testing/catalog.fixture.ts`, que reproduz o exemplo de
[`docs/architecture/http-contract.md`](../../docs/architecture/http-contract.md) — nenhum teste
depende da API estar no ar.

A verificação em navegador, com teclado e captura de tela, fica no relatório da tarefa.
