
// ASP.NET Core Identity nativo, com os usuários no mesmo banco da aplicação. Os tokens emitidos
// são bearer tokens próprios do Identity, opacos para quem os recebe: não existe segredo de
// assinatura para configurar, e nenhum JWT de terceiros está envolvido.
builder.Services.AddIdentityStores(builder.Configuration.GetConnectionString("Default"));

builder.Services
    .AddIdentityApiEndpoints<AppUser>()
    .AddEntityFrameworkStores<AppIdentityDbContext>();

// Autorização binária (RF-14, RF-15): basta o token ser válido. Não há política por papel nem por
// claim, e os dados são compartilhados entre todos os usuários autenticados.
builder.Services.AddAuthorization();
