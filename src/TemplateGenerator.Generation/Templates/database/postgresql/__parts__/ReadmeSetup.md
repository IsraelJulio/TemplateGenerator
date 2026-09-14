
## Banco de dados

Esta combinação usa **PostgreSQL** com EF Core e o provedor Npgsql.

### 1. Configurar a conexão

`appsettings.json` traz a cadeia de conexão **sem senha**, de propósito — um arquivo versionado não
carrega credencial:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=__ProjectName__;Username=postgres"
}
```

Informe a senha pelo ambiente, que é o caminho que não mexe em arquivo nenhum do projeto. Em Linux,
macOS ou Git Bash:

```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=__ProjectName__;Username=postgres;Password=SUA_SENHA"
```

No PowerShell:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=__ProjectName__;Username=postgres;Password=SUA_SENHA"
```

Os dois sublinhados são a forma como o ASP.NET Core lê uma chave aninhada do ambiente: eles
substituem o `:` de `ConnectionStrings:Default`. A variável vale para o terminal em que você a
definiu, então defina-a no mesmo terminal em que for executar a aplicação.

Ajustar `Host`, `Port`, `Database` e `Username` para o seu servidor é esperado. O que não deve
mudar é o nome da chave: o projeto lê `ConnectionStrings:Default`.

### 2. Aplicar a migração

A migração inicial fica em `Migrations/`, ao lado do `AppDbContext`, e é **específica do
PostgreSQL** — a migração de outro provedor não serve aqui.

A ferramenta de linha de comando do EF Core vem fixada em `.config/dotnet-tools.json`, na raiz do
projeto. Restaure-a — isso não instala nada na sua máquina, fica tudo dentro da pasta do projeto:

```bash
dotnet tool restore
```

Depois aplique a migração, no mesmo terminal em que você definiu a variável do passo 1:

```bash
dotnet ef database update --project __PersistenceProjectDir__ --startup-project __ApiProjectDir__
```

`--project` é onde as migrações moram; `--startup-project` é o projeto que sobe a aplicação e
carrega a configuração — é dele que sai a cadeia de conexão. Passar os dois deixa o comando igual
em qualquer arquitetura, mesmo quando apontam para o mesmo projeto.

O comando cria o banco, se ele ainda não existir, e a tabela `Items`. Para criá-lo na primeira vez,
o usuário da conexão precisa ter permissão de `CREATEDB` — o usuário `postgres` tem. Reaplicar o
que já está aplicado não faz nada, então repetir o comando é seguro.

**A aplicação não aplica migração ao subir.** Se você executar sem este passo, a primeira chamada a
`/items` falha dizendo que a tabela não existe. O esquema do banco muda quando você manda mudar, e
não porque a aplicação reiniciou.

O pacote de design time já está declarado, então `dotnet ef migrations add` também funciona sem
acrescentar dependência nenhuma.

### 3. Os dados sobrevivem ao reinício

Diferente da opção "sem banco de dados": crie um item, pare com `Ctrl+C`, suba de novo e chame
`GET /items` — o item continua lá, e continua lá mesmo que a aplicação mude de máquina.
