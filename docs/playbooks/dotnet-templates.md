# Playbook: dotnet-templates

Como compor um projeto gerado, garantir compatibilidade entre opções e saber o que é obrigatório
no ZIP.

Referências: [`../architecture/generated-projects.md`](../architecture/generated-projects.md) e
[`../architecture/generation-engine.md`](../architecture/generation-engine.md).

## 1. Resolver a configuração

1. Validar `projectName` (`../product/option-matrix.md`).
2. Conferir a restrição `identity-requires-database`.
3. Resolver a lista de fragmentos aplicáveis.

## 2. Selecionar fragmentos

```
common/
+ architecture/<simple|clean>/
+ database/<none|sqlite|postgresql>/
+ auth/<none|identity|jwt>/
+ swagger/enabled/          (só se swagger = true)
```

**Conflito de caminho entre dois fragmentos é defeito**, não precedência. Um teste falha.

## 3. Checklist do conteúdo obrigatório

- [ ] `<ProjectName>.sln`
- [ ] Projeto(s) conforme a arquitetura
- [ ] Projeto de testes com testes que passam
- [ ] `appsettings.json` + `appsettings.Development.json`, **sem credencial real**
- [ ] `requests.http` com exemplos, incluindo autenticação quando houver
- [ ] `.gitignore`, `.editorconfig`, `global.json`
- [ ] `README.md` específico da combinação
- [ ] `.templategenerator/manifest.json`

## 4. Checklist por opção

**`database = none`** — armazenamento em memória; o README avisa que os dados somem no reinício.

**`database = sqlite`** — pacote SQLite, `DbContext`, migração inicial **de SQLite**, connection
string local pronta; README com o passo de aplicar a migração.

**`database = postgresql`** — Npgsql, migração inicial **de PostgreSQL**, connection string sem
senha real; README com o passo de configurar a conexão antes da migração.

**`auth = none`** — CRUD público; nenhum pacote de autenticação no `.csproj`.

**`auth = identity`** — `MapIdentityApi`, endpoints nativos, **sem segredo JWT**. README explica
que os tokens são *bearer próprios do Identity*, não JWT de terceiros, e que envio de e-mail
exige configuração adicional fora do fluxo garantido do MVP.

**`auth = jwt`** — `JwtBearer` com `Authority` e `Audience` em `appsettings.json`, sem provedor
embutido; README acrescenta o passo de apontar para o provedor existente.

**`swagger = true`** — `Microsoft.AspNetCore.OpenApi` (documento) + `Swashbuckle.AspNetCore` (só
UI), habilitados por padrão em Development. Ver [ADR-0001](../decisions/adr-0001-swagger.md).

**`swagger = false`** — **nenhum dos dois pacotes** no `.csproj`.

## 5. Checklist de determinismo

Ver [ADR-0003](../decisions/adr-0003-zip-deterministico.md).

- [ ] `LastWriteTime` = `1980-01-01T00:00:00Z` em toda entrada (abaixo de 1980 **lança exceção**)
- [ ] Entradas ordenadas por caminho, comparação **ordinal**
- [ ] `CompressionLevel` explícito
- [ ] UTF-8 sem BOM, `LF`, arquivo termina em nova linha
- [ ] Nenhum GUID, data de geração ou caminho de máquina no conteúdo

## 6. Checklist de segurança

- [ ] Todo caminho normalizado permanece dentro da raiz virtual
- [ ] Nenhum caminho duplicado
- [ ] Nenhuma credencial real em qualquer arquivo
- [ ] `/health` público mesmo com autenticação ligada
