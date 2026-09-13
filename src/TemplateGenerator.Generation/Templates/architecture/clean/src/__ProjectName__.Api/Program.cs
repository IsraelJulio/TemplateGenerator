using __ProjectName__.Api.Endpoints;
using __ProjectName__.Application.Items;
using __ProjectName__.Domain.Abstractions;
using __ProjectName__.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Erros saem como ProblemDetails, o mesmo formato que a validação dos endpoints devolve.
builder.Services.AddProblemDetails();
__ProgramServices__

builder.Services.AddScoped<ItemService>();

WebApplication app = builder.Build();

// `StatusCodeSelector` não é detalhe: um corpo JSON malformado chega como
// `BadHttpRequestException`, que já carrega o 400. Sem esta linha o tratador converteria em 500
// um erro que é de quem chamou, não do servidor.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});

app.UseStatusCodePages();
__ProgramMiddleware__

app.MapHealth();
app.MapItems();

app.Run();
