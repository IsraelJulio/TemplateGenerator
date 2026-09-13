# Frontend da plataforma

Aplicação Angular standalone que configura um projeto .NET e baixa o ZIP gerado.
Sem NgModules, sem biblioteca de componentes de terceiros, sem Tailwind, CSS próprio.

O visual acabado e os oito estados de [`docs/design/visual-spec.md`](../../docs/design/visual-spec.md)
são a tarefa T02. O que existe aqui é a estrutura e o contrato: montagem dinâmica do formulário
a partir do catálogo, leitura genérica das restrições e as chamadas aos dois endpoints.

## Comandos

| Comando               | O que faz                                                                           |
| --------------------- | ----------------------------------------------------------------------------------- |
| `npm start`           | Sobe o servidor de desenvolvimento em <http://localhost:4200> com proxy para a API. |
| `npm run build`       | Compila para `dist/web`.                                                            |
| `npm test`            | Roda os testes de unidade em modo headless (Vitest + jsdom).                        |
| `npm run e2e`         | Roda o fluxo ponta a ponta no Chromium, contra a API de verdade.                    |
| `npm run e2e:install` | Baixa o navegador do Playwright. **Uma vez**, antes do primeiro `npm run e2e`.      |
| `npm run e2e:typecheck` | Confere os tipos de `e2e/` — o Playwright transpila sem checar.                   |

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

### Fluxo ponta a ponta (`npm run e2e`)

Playwright, em `e2e/download.e2e.ts`. É o oposto do parágrafo acima: **nada é dublado**. O
`playwright.config.ts` sobe as duas pontas antes de abrir o navegador — `dotnet run` na API
geradora no perfil `http` e o `ng serve` com o proxy apontado para ela — e o teste preenche a
tela, clica em "Gerar projeto", **abre o ZIP que o Chromium salvou em disco** e confere o
conteúdo contra a árvore que a tela tinha acabado de mostrar. O mesmo fluxo roda em dois
tamanhos, `desktop` e `celular`.

Ele existe porque o critério de aceite 11 de T03 pede exatamente isso: até T02 o contrato
respondia `501` a uma configuração válida, então "download concluído" só podia ser demonstrado
com dublê de rede. Com o `200 application/zip` real, um dublê deixou de bastar.

**Duas dependências de rede, as duas previstas em
[`docs/quality/test-strategy.md`](../../docs/quality/test-strategy.md):**

1. `npm run e2e:install` baixa o Chromium do Playwright (~300 MB) na primeira execução, para fora
   do repositório (`%LOCALAPPDATA%\ms-playwright`).
2. Atrás de um proxy que reescreve TLS, esse download falha com
   `UNABLE_TO_GET_ISSUER_CERT_LOCALLY`, porque o Node não lê a loja de certificados do Windows por
   padrão. A saída é `NODE_OPTIONS=--use-system-ca npm run e2e:install` — o certificado corporativo
   já está na loja do sistema, e nada precisa ser desligado nem versionado.

O `dotnet run` da primeira execução compila a API e pode levar mais de um minuto; o timeout de
subida é de 180 s. Um servidor já no ar é reaproveitado em vez de duplicado.
