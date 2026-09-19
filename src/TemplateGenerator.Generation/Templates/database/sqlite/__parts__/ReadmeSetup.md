
## Banco de dados

Esta combinação usa **SQLite** com EF Core. O banco é um arquivo no diretório do projeto de API, e
a cadeia de conexão já vem pronta em `appsettings.json`:

```json
"ConnectionStrings": {
  "Default": "Data Source=app.db"
}
```

Não há servidor a subir, usuário a criar nem senha a guardar. O arquivo `app.db` está no
`.gitignore` e não vai para o controle de versão.

### Aplicar a migração

A migração inicial fica em `Migrations/`, ao lado do `AppDbContext`, e é **específica do SQLite** —
a migração de outro provedor não serve aqui.

A ferramenta de linha de comando do EF Core vem fixada em `.config/dotnet-tools.json`, na raiz do
projeto. Restaure-a — isso não instala nada na sua máquina, fica tudo dentro da pasta do projeto:

```bash
dotnet tool restore
```

Depois aplique a migração:

```bash
dotnet ef database update --project __PersistenceProjectDir__ --startup-project __ApiProjectDir__ --context AppDbContext
```

`--project` é onde as migrações moram; `--startup-project` é o projeto que sobe a aplicação e
carrega a configuração — é dele que sai a cadeia de conexão. Passar os dois deixa o comando igual
em qualquer arquitetura, mesmo quando apontam para o mesmo projeto. `--context` diz a qual
`DbContext` a migração pertence: um projeto pode hospedar mais de um, e nomeá-lo deixa o comando
igual nos dois casos.

Isso cria `app.db` e a tabela `Items`. Reaplicar o que já está aplicado não faz nada, então repetir
o comando é seguro.

**A aplicação não aplica migração ao subir.** Se você executar sem este passo, a primeira chamada a
`/items` falha dizendo que a tabela não existe. O esquema do banco muda quando você manda mudar, e
não porque a aplicação reiniciou.

O pacote de design time já está declarado, então `dotnet ef migrations add` também funciona sem
acrescentar dependência nenhuma.

### Os dados sobrevivem ao reinício

Diferente da opção "sem banco de dados": crie um item, pare com `Ctrl+C`, suba de novo e chame
`GET /items` — o item continua lá.
