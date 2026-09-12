using TemplateGenerator.Api.Endpoints;

// Composição mínima da API geradora.
//
// O gerador é stateless: sem banco, sem sessão, sem cookie, sem autenticação
// (docs/architecture/http-contract.md). Este arquivo só compõe — as rotas moram em
// Endpoints/GeneratorEndpoints.cs.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ProblemDetails (RFC 9457) é o formato de erro do contrato, inclusive para falhas não tratadas.
builder.Services.AddProblemDetails();

// Documento OpenAPI nativo do .NET 10 (ADR-0001). Só em Development.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

WebApplication app = builder.Build();

// Um corpo malformado é entrada hostil, não defeito do servidor (RNF-03).
//
// O binding de Minimal APIs sinaliza isso com BadHttpRequestException, que já carrega o status
// certo — JSON quebrado, UTF-8 inválido, corpo truncado. Sem este seletor, o tratador padrão
// ignora esse status e responde 500: o cliente deixa de distinguir "mandei errado" de "o
// servidor quebrou", e o log enche de erro de aplicação por causa de requisição malformada.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Ponto de extensão: toda rota da plataforma entra por aqui.
app.MapGeneratorEndpoints();

app.Run();

/// <summary>
/// Exposto para que <c>WebApplicationFactory&lt;Program&gt;</c> consiga hospedar esta aplicação
/// nos testes de integração. Ver tests/TemplateGenerator.Api.Tests.
/// </summary>
public partial class Program;
