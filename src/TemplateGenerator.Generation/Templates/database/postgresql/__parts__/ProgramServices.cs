
// Persistência em PostgreSQL com EF Core: a sessão com o banco e a implementação da porta de
// armazenamento entram juntas, do projeto que implementa a porta. A cadeia de conexão vem de
// `ConnectionStrings:Default`, em appsettings.json ou na variável de ambiente equivalente.
builder.Services.AddPersistence(builder.Configuration.GetConnectionString("Default"));
