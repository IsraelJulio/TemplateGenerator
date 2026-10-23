### Swagger e autenticação

O documento e a interface são **públicos** em `Development`, como `GET /health`: abrir
`/swagger` não pede token, mesmo com `Jwt:Authority` vazio. O CRUD de `Item`, não — e a interface
**não tem o botão *Authorize***, porque o documento OpenAPI desta combinação não declara esquema
de segurança. As rotas de `/items` aparecem na interface, com contrato e exemplos, mas o *Try it
out* delas responde `401`: a interface não tem como mandar o cabeçalho `Authorization`.

Use a interface para ler o contrato e faça as chamadas autenticadas pelos comandos de **Testar o
CRUD de `Item`**, com o token do seu provedor.
