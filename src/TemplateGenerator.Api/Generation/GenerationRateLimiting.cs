using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TemplateGenerator.Api.Endpoints;

namespace TemplateGenerator.Api.Generation;

/// <summary>
/// Os limites de geração ligados ao pipeline: <c>429</c> em <c>ProblemDetails</c>, com
/// <c>Retry-After</c> (RNF-04, docs/architecture/http-contract.md).
/// </summary>
/// <remarks>
/// <para>
/// Os dois limitadores são <strong>encadeados</strong> e valem só para
/// <c>POST /api/templates</c>. <c>GET /api/health</c> fica de fora por um motivo prático: ele
/// existe para o cliente distinguir "API fora do ar" de "rota ausente", e um health limitado
/// responderia <c>429</c> exatamente quando alguém precisa saber se o servidor está vivo.
/// </para>
/// <para>
/// Os valores são lidos de <see cref="IOptions{TOptions}"/> a cada requisição, e não capturados
/// na inicialização. É o que permite a um teste fixar um limite de 1 sem depender da ordem em que
/// o host de teste monta a configuração — e o que deixa a seção <c>Generation:Limits</c> valer
/// por si, sem uma segunda cópia dos números dentro do limitador.
/// </para>
/// <para>
/// A origem é o endereço da conexão, e não <c>X-Forwarded-For</c>. Um limite baseado em cabeçalho
/// que o próprio cliente escreve não é limite: trocar o valor a cada requisição bastaria para
/// zerar a contagem. Atrás de proxy reverso, a forma certa é configurar <c>ForwardedHeaders</c>
/// com a lista de proxies confiáveis — decisão de implantação, que não está tomada aqui e não
/// deve ser fingida por default.
/// </para>
/// </remarks>
public static class GenerationRateLimiting
{
    /// <summary>Partição usada quando a conexão não expõe endereço.</summary>
    /// <remarks>
    /// Acontece no servidor de teste in-process. Todas essas requisições caem na mesma partição:
    /// sem endereço, o mais seguro é tratá-las como uma origem só.
    /// </remarks>
    public const string UnknownOriginPartition = "sem-origem";

    /// <summary>Partição do limite de concorrência — uma só, para o processo inteiro.</summary>
    public const string ConcurrencyPartition = "gerações simultâneas";

    private const string BypassPartition = "sem-limite";

    /// <summary>
    /// Registra o limitador de geração, ligado à seção <c>Generation:Limits</c>.
    /// </summary>
    public static IServiceCollection AddGenerationRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<GenerationLimits>()
            .Bind(configuration.GetSection(GenerationLimits.SectionPath))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PerOriginLimiter(),
                ConcurrencyLimiter());

            // O token do middleware não é repassado: `IProblemDetailsService.TryWriteAsync` não o
            // aceita, e um cancelamento durante a escrita da recusa não tem nada a desfazer.
            options.OnRejected = (context, _) => RejectAsync(context);
        });

        return services;
    }

    /// <summary>Diz se a requisição é uma geração — o único alvo dos limites.</summary>
    public static bool IsGenerationRequest(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.Equals(
                GeneratorEndpoints.RoutePrefix + GeneratorEndpoints.TemplatesRoute,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Janela fixa por origem.</summary>
    private static PartitionedRateLimiter<HttpContext> PerOriginLimiter() =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!IsGenerationRequest(context))
            {
                return RateLimitPartition.GetNoLimiter(BypassPartition);
            }

            GenerationLimits limits = LimitsOf(context);
            string origin = context.Connection.RemoteIpAddress?.ToString() ?? UnknownOriginPartition;

            return RateLimitPartition.GetFixedWindowLimiter(origin, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.RequestsPerWindow,
                Window = limits.Window,

                // Sem fila: o excedente é recusado na hora. Enfileirar faria o cliente esperar sem
                // saber por quê, e um download que demora é indistinguível de um servidor travado.
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });
        });

    /// <summary>Gerações simultâneas no processo, sem fila.</summary>
    private static PartitionedRateLimiter<HttpContext> ConcurrencyLimiter() =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!IsGenerationRequest(context))
            {
                return RateLimitPartition.GetNoLimiter(BypassPartition);
            }

            GenerationLimits limits = LimitsOf(context);

            return RateLimitPartition.GetConcurrencyLimiter(
                ConcurrencyPartition,
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = limits.MaxConcurrentGenerations,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
        });

    private static GenerationLimits LimitsOf(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<GenerationLimits>>().Value;

    private static async ValueTask RejectAsync(OnRejectedContext context)
    {
        HttpContext http = context.HttpContext;

        // O default do middleware é 503. O contrato diz 429, que é a resposta que informa "tente
        // de novo", e não "o servidor está indisponível".
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        GenerationLimits limits = LimitsOf(http);

        // A janela fixa sabe dizer quanto falta; o limite de concorrência não — ele depende de
        // outra requisição terminar, e aí vale o valor configurado.
        TimeSpan retryAfter =
            context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan fromLease)
                ? fromLease
                : limits.RetryAfter;

        int seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        IProblemDetailsService? problemDetails =
            http.RequestServices.GetService<IProblemDetailsService>();

        if (problemDetails is null)
        {
            return;
        }

        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Type = ProblemTypes.TooManyRequests,
                Title = "Limite de geração atingido",
                Status = StatusCodes.Status429TooManyRequests,
                Detail =
                    "Há gerações demais em curso ou pedidos demais desta origem. " +
                    $"Tente novamente em {seconds} segundo(s).",
                Extensions = { ["retryAfterSeconds"] = seconds },
            },
        }).ConfigureAwait(false);
    }
}
