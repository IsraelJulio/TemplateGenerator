
## Swagger

Com a aplicação em execução no ambiente `Development`:

- Documento OpenAPI: <http://localhost:5100/openapi/v1.json>
- Interface: <http://localhost:5100/swagger>

O documento é gerado pelo `Microsoft.AspNetCore.OpenApi`, nativo do .NET 10, e a interface vem do
`Swashbuckle.AspNetCore`, que aqui entra **apenas pela UI**. Fora de `Development` nenhum dos dois
é exposto.
