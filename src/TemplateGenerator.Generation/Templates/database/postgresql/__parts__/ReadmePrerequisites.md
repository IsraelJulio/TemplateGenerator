
Além do SDK, esta combinação precisa de um **servidor PostgreSQL em execução** — nativo, sem
container. O cliente `psql` é opcional: o banco e a tabela são criados quando você aplica a
migração, desde que o usuário da conexão possa criar banco (`CREATEDB`).

Não é preciso certificado HTTPS de desenvolvimento: a aplicação sobe em HTTP.
