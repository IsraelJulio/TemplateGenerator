# __ProjectName__

Web API em ASP.NET Core com **Minimal APIs**, gerada pelo TemplateGenerator (versão de template
`__TemplateVersion__`).

| | |
|---|---|
| Arquitetura | Clean — quatro projetos de código e um de testes |
| Framework alvo | `__TargetFramework__` |

Este README descreve **esta combinação exata**. Todo comando abaixo roda como está, sem editar
nenhum arquivo de código.

## Pré-requisitos

- .NET SDK 10.0.302 ou mais recente da mesma linha. A versão exigida está fixada em `global.json`.

```bash
dotnet --version
```
__ReadmePrerequisites__

## Estrutura

```
__ProjectName__.sln
global.json                              SDK fixado
requests.http                            exemplos de chamada
src/__ProjectName__.Api/                 composição, endpoints e configuração
src/__ProjectName__.Application/         casos de uso
src/__ProjectName__.Domain/              entidades, regras e as portas
src/__ProjectName__.Infrastructure/      implementação das portas
tests/__ProjectName__.Tests/             testes automatizados
```

O nome de cada projeto é o nome que você digitou **mais o sufixo da camada**, sempre, sem exceção.
Os quatro irmãos só são legíveis porque são uniformes: quem abre `src/` sabe qual é qual sem
precisar conhecer regra nenhuma.

### A direção das dependências

```
Api ──▶ Application ──▶ Domain
 │                        ▲
 └────▶ Infrastructure ───┘
```

- **`Domain`** — `Items/Item.cs` e a porta `Abstractions/IItemStore.cs`. **Não referencia nenhum
  projeto e nenhum pacote de infraestrutura.** É a regra que dá sentido a todas as outras, e dá
  para conferir abrindo `src/__ProjectName__.Domain/__ProjectName__.Domain.csproj`: ele não tem um
  único `ProjectReference`.
- **`Application`** — `Items/ItemService.cs`, os casos de uso do CRUD. Depende só do domínio.
- **`Infrastructure`** — `Persistence/ItemStore.cs`, que implementa a porta. Depende só do
  domínio, porque é lá que a porta está declarada.
- **`Api`** — `Program.cs` e `Endpoints/`. É o único projeto onde `Application` e
  `Infrastructure` se encontram; é ele que injeta a implementação na porta.

Uma seta fora desse desenho — o domínio referenciando a infraestrutura, por exemplo — é o erro que
esta arquitetura existe para tornar impossível de cometer por acidente: o compilador recusa.

### A API não muda com a arquitetura

As rotas, os códigos de status e as mensagens de erro são **os mesmos** da arquitetura Simples.
Quem consome o serviço não tem como saber qual arquitetura o gerou. O que a Clean muda é como o
código está dividido, não o contrato.

## Instalação

```bash
dotnet restore
```
__ReadmeSetup__

## Executar

```bash
dotnet run --project src/__ProjectName__.Api
```

A aplicação sobe em <http://localhost:5100>, no ambiente `Development` — o endereço e o ambiente
estão em `src/__ProjectName__.Api/Properties/launchSettings.json`. Para parar, `Ctrl+C`.

## Verificar a saúde

`GET /health` é público e não exige autenticação em nenhuma combinação:

```bash
curl http://localhost:5100/health
```

Resposta: `{"status":"ok"}`.

## Testar o CRUD de `Item`
__ReadmeCrudAuth__

Com a aplicação em execução, em outro terminal. Em Linux, macOS ou Git Bash:

```bash
curl -i -X POST http://localhost:5100/items -H 'Content-Type: application/json'__ReadmeCrudAuthHeader__ -d '{"title":"Primeiro item"}'
curl http://localhost:5100/items__ReadmeCrudAuthHeader__
curl http://localhost:5100/items/1__ReadmeCrudAuthHeader__
curl -i -X PUT http://localhost:5100/items/1 -H 'Content-Type: application/json'__ReadmeCrudAuthHeader__ -d '{"title":"Item renomeado"}'
curl -i -X DELETE http://localhost:5100/items/1__ReadmeCrudAuthHeader__
```

No PowerShell, use `curl.exe` e mande o corpo JSON pelo **pipe**, e não em `-d`. O motivo é do
shell, não do `curl`: o PowerShell entrega os argumentos a um programa nativo sem reconstruir as
aspas, então um corpo com espaço — `"Primeiro item"` — chega partido em dois e o servidor responde
`400`. Passando o corpo por `stdin` com `--data-binary '@-'`, ele não percorre a linha de comando e
chega inteiro.

A primeira linha do bloco é parte da correção, não enfeite: o Windows PowerShell escreve no `stdin`
de um programa nativo em **US-ASCII** por padrão, e sem ela um título com acento — `"Primeiro
ítem"` — chegaria ao servidor com `?` no lugar da letra. Essa falha é **silenciosa**: o corpo segue
sendo JSON válido, a resposta é `201` e o dado gravado é que está corrompido. A linha é idempotente
e vale pela sessão: repita-a em cada terminal novo.

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
'{"title":"Primeiro item"}' | curl.exe -i -X POST http://localhost:5100/items -H "Content-Type: application/json"__ReadmeCrudAuthHeader__ --data-binary '@-'
curl.exe http://localhost:5100/items__ReadmeCrudAuthHeader__
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
obrigatoriedade de `title` — contra um dublê da porta de persistência, sem subir a aplicação. É o
que a divisão em camadas compra: os casos de uso são testáveis sem HTTP e sem armazenamento.
__ReadmeUsage__

## Próximos passos

Este projeto é um ponto de partida, não um produto final. O que ele deliberadamente não traz:
autorização por papel, paginação, versionamento de API, observabilidade e pipeline de publicação.
