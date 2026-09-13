
## Autenticação

Esta combinação **não tem autenticação**. Todos os endpoints são públicos, inclusive o CRUD de
`Item`: nenhuma chamada precisa do cabeçalho `Authorization` e não há usuário, cadastro ou token
para obter.

Para exigir token, gere o projeto com Identity (usuários no próprio banco) ou JWT (provedor
externo já existente).
