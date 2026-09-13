# __ProjectName__

Web API em ASP.NET Core com **Minimal APIs**, gerada pelo TemplateGenerator (versão de template
`__TemplateVersion__`).

| | |
|---|---|
| Arquitetura | Simples — um projeto de código e um de testes |
| Framework alvo | `__TargetFramework__` |

Este README descreve **esta combinação exata**. Todo comando abaixo roda como está, sem editar
nenhum arquivo de código.

## Pré-requisitos

- .NET SDK 10.0.302 ou mais recente da mesma linha. A versão exigida está fixada em `global.json`.

```bash
dotnet --version
```

Nada mais: esta combinação não usa banco de dados, não precisa de container e não exige
certificado HTTPS de desenvolvimento — a aplicação sobe em HTTP.

## Estrutura

```
__ProjectName__.sln
global.json                      SDK fixado
requests.http                    exemplos de chamada
src/__ProjectName__/
  Program.cs                     composição da aplicação
  Endpoints/                     as rotas
  Models/                        Item e os contratos de entrada e saída
  Persistence/                   IItemStore e a implementação do armazenamento
  Services/                      as regras de Item
tests/__ProjectName__.Tests/     testes automatizados
```

## Instalação

```bash
dotnet restore
```
__ReadmeSetup__

## Executar

```bash
dotnet run --project src/__ProjectName__
```

A aplicação sobe em <http://localhost:5100>, no ambiente `Development` — o endereço e o ambiente
estão em `src/__ProjectName__/Properties/launchSettings.json`. Para parar, `Ctrl+C`.

## Verificar a saúde

`GET /health` é público e não exige autenticação em nenhuma combinação:

```bash
curl http://localhost:5100/health
```

Resposta: `{"status":"ok"}`.

## Testar o CRUD de `Item`

Com a aplicação em execução, em outro terminal. Em Linux, macOS ou Git Bash:

```bash
curl -i -X POST http://localhost:5100/items -H 'Content-Type: application/json' -d '{"title":"Primeiro item"}'
curl http://localhost:5100/items
curl http://localhost:5100/items/1
curl -i -X PUT http://localhost:5100/items/1 -H 'Content-Type: application/json' -d '{"title":"Item renomeado"}'
curl -i -X DELETE http://localhost:5100/items/1
```

No PowerShell, use `curl.exe` e aspas duplas escapadas:

```powershell
curl.exe -i -X POST http://localhost:5100/items -H "Content-Type: application/json" -d '{\"title\":\"Primeiro item\"}'
curl.exe http://localhost:5100/items
```

O que esperar:

| Rota | Sucesso | Quando não existe |
|---|---|---|
| `GET /items` | `200` com a lista | — |
| `GET /items/{id}` | `200` com o item | `404` |
| `POST /items` | `201` com `Location` | — |
| `PUT /items/{id}` | `204` | `404` |
| `DELETE /items/{id}` | `204` | `404` |

`title` é obrigatório e aceita no máximo 200 caracteres: um corpo sem `title` responde `400` com
um `ProblemDetails` de validação.

O arquivo `requests.http` na raiz traz as mesmas chamadas no formato que o Visual Studio executa
nativamente e que o VS Code executa com a extensão REST Client.

## Testes automatizados

```bash
dotnet test
```

Os testes cobrem as regras de `Item` — criação, listagem, busca, substituição, remoção e a
obrigatoriedade de `title` — contra um armazenamento de teste, sem subir a aplicação.
__ReadmeUsage__

## Próximos passos

Este projeto é um ponto de partida, não um produto final. O que ele deliberadamente não traz:
autorização por papel, paginação, versionamento de API, observabilidade e pipeline de publicação.
