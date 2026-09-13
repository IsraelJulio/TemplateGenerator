
## Banco de dados

Esta combinação **não usa banco de dados**. Não há cadeia de conexão para configurar nem migração
para aplicar: os itens ficam em memória, dentro do processo da aplicação.

**Os dados são perdidos quando a aplicação reinicia** (RF-16). É o comportamento pedido para a
opção "sem banco de dados", e dá para conferir: crie um item, pare com `Ctrl+C`, suba de novo e
chame `GET /items` — a lista volta vazia.

Para que os dados sobrevivam ao reinício, gere o projeto com SQLite ou PostgreSQL.
