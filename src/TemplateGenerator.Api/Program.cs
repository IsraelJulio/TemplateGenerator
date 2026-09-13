using TemplateGenerator.Api.Endpoints;
using TemplateGenerator.Api.Generation;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Engine;

// Composição mínima da API geradora.
//
// O gerador é stateless: sem banco, sem sessão, sem cookie, sem autenticação
// (docs/architecture/http-contract.md). Este arquivo só compõe — as rotas moram em
// Endpoints/GeneratorEndpoints.cs.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ProblemDetails (RFC 9457) é o formato de erro do contrato, inclusive para falhas não tratadas.
builder.Services.AddProblemDetails();

// Quais valores do catálogo têm template, derivado dos fragmentos embutidos (ADR-0012). Uma
// instância, calculada uma vez: os templates são recursos embutidos e imutáveis, e a varredura não
// pode acontecer por requisição. É a MESMA instância que o motor usa — os dois lados da recusa
// perguntam à mesma verdade.
builder.Services.AddSingleton(TemplateAvailability.Current);

// O motor de geração. Singleton porque ele não tem estado: catálogo e templates são imutáveis e
// compartilhados, e tudo que pertence a uma geração vive na pilha daquela requisição (RNF-04).
builder.Services.AddSingleton<IGenerationEngine, GenerationEngine>();

// Limite de requisições por origem e de gerações simultâneas, com 429 e Retry-After (RNF-04).
builder.Services.AddGenerationRateLimiter(builder.Configuration);

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

// Antes do roteamento das rotas de geração: recusar cedo é o ponto de um limite.
app.UseRateLimiter();

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
