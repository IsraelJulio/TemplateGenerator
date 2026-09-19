
// Validação de JWT emitido por um provedor externo. Esta aplicação não hospeda provedor de
// identidade e não emite token: ela só confere o que chega. `Authority` é o emissor — de onde saem
// o documento de descoberta OIDC e as chaves públicas de assinatura — e `Audience` é o
// destinatário que o token precisa declarar. Os dois vêm de `Jwt:Authority` e `Jwt:Audience`, em
// appsettings.json ou no ambiente; enquanto estiverem vazios, nenhum token é aceito e todo o CRUD
// responde 401.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];

        // Buscar os metadados do emissor por HTTP só é aceito em Development, onde apontar para um
        // emissor local é comum. Em qualquer outro ambiente o endereço precisa ser HTTPS.
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        // As quatro conferências que decidem o que é 401 (RF-19), escritas uma a uma em vez de
        // herdadas em silêncio do default. Emissor e chaves de assinatura saem do documento de
        // descoberta do `Authority`; a audiência sai da configuração acima.
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;

        // Sem tolerância de relógio: um token vencido é recusado no instante em que vence. O
        // default da biblioteca são cinco minutos de folga, e nesses cinco minutos um token
        // expirado continuaria valendo. Se os relógios da sua infraestrutura não estiverem
        // sincronizados, é aqui que a folga volta.
        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
    });

// Autorização binária (RF-14, RF-15): basta o token ser válido. Não há política por papel nem por
// claim, e os dados são compartilhados entre todos os usuários autenticados.
builder.Services.AddAuthorization();
