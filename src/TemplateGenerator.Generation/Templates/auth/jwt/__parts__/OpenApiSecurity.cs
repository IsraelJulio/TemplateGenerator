options =>
{
    // Com autenticação, o documento declara o esquema bearer, e é ele que dá à interface o botão
    // Authorize. O token desta combinação é um JWT emitido pelo seu provedor, e o esquema diz isso.
    const string bearer = "Bearer";

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[bearer] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Um JWT emitido pelo provedor de Jwt:Authority, sem o prefixo 'Bearer '.",
        };

        return Task.CompletedTask;
    });

    // O requisito vai só nas operações que exigem autorização — o CRUD de /items —, e não em
    // /health, que é público. É o mesmo metadado que o ASP.NET Core consulta para responder 401.
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(bearer, context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    });
}
