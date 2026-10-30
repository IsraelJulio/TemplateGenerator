options =>
{
    // Com autenticação, o documento declara o esquema bearer, e é ele que dá à interface o botão
    // Authorize. O token desta combinação é o bearer próprio do Identity, opaco para quem o recebe:
    // o esquema não declara formato JWT, porque o token não é um JWT.
    const string bearer = "Bearer";

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[bearer] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "O accessToken devolvido por POST /auth/login, sem o prefixo 'Bearer '.",
        };

        return Task.CompletedTask;
    });

    // O requisito vai só nas operações que exigem autorização — o CRUD de /items e a gestão da
    // conta em /auth/manage/... —, e não em /health, /auth/register, /auth/login nem /auth/refresh,
    // que são públicos. É o mesmo metadado que o ASP.NET Core consulta para responder 401.
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            // É a resposta real de quem chega sem token válido: 401, sem corpo.
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["401"] = new OpenApiResponse { Description = "Unauthorized" };

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(bearer, context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    });
}
