using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Validation;

namespace TemplateGenerator.Api.Endpoints;

/// <summary>
/// Ponto de extensão único da API geradora. Tudo que for rota entra por aqui — o
/// <c>Program.cs</c> só compõe e chama.
/// </summary>
/// <remarks>
/// <para>Os dois endpoints do contrato (docs/architecture/http-contract.md):</para>
/// <list type="bullet">
///   <item>
///     <description>
///     <c>GET /api/template-options</c> — catálogo completo, com as restrições como DADO
///     (a restrição <c>identity-requires-database</c> inclusive). O frontend não codifica valor
///     nem regra de compatibilidade.
///     </description>
///   </item>
///   <item>
///     <description>
///     <c>POST /api/templates</c> — 200 com <c>application/zip</c> e
///     <c>Content-Disposition: attachment</c>; 400 em <c>application/problem+json</c> (RFC 9457)
///     com os erros endereçados ao campo que os causou; 429 com <c>Retry-After</c> nos limites
///     (<see cref="TemplateGenerator.Api.Generation.GenerationRateLimiting"/>).
///     </description>
///   </item>
///   <item>
///     <description>
///     <c>GET /api/health</c> — sinal de vida da plataforma, para que o cliente distinga
///     "API fora do ar" de "rota ausente".
///     </description>
///   </item>
/// </list>
/// <para>
/// A camada aqui é fina de propósito: ela transporta. Catálogo e validação moram em
/// <c>TemplateGenerator.Generation</c>, que não conhece HTTP — é o que permite exercitar as 32
/// combinações sem subir a Api (docs/architecture/platform.md).
/// </para>
/// </remarks>
public static class GeneratorEndpoints
{
    /// <summary>Prefixo comum das rotas da API geradora.</summary>
    public const string RoutePrefix = "/api";

    /// <summary>Rota do catálogo.</summary>
    public const string TemplateOptionsRoute = "/template-options";

    /// <summary>Rota da geração.</summary>
    public const string TemplatesRoute = "/templates";

    /// <summary>Rota do sinal de vida da plataforma.</summary>
    public const string HealthRoute = "/health";

    /// <summary>
    /// Registra as rotas da API geradora.
    /// </summary>
    /// <param name="endpoints">Construtor de rotas da aplicação.</param>
    /// <returns>O grupo de rotas, para encadeamento.</returns>
    public static RouteGroupBuilder MapGeneratorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup(RoutePrefix);

        group.MapGet(HealthRoute, GetHealth)
            .WithName("GetHealth");

        group.MapGet(TemplateOptionsRoute, GetTemplateOptions)
            .WithName("GetTemplateOptions");

        group.MapPost(TemplatesRoute, CreateTemplate)
            .WithName("CreateTemplate");

        return group;
    }

    /// <summary>
    /// <c>GET /api/health</c> — sinal de vida da plataforma geradora.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe por um motivo concreto: sem ele, o cliente só descobre que a API caiu falhando o
    /// próprio <c>GET /api/template-options</c>, e uma rota ausente e um servidor fora do ar
    /// chegam na tela como o mesmo erro.
    /// </para>
    /// <para>
    /// Público, sem autenticação — como todo o resto: o gerador não identifica ninguém.
    /// </para>
    /// <para>
    /// Não confundir com o <c>/health</c> dos projetos GERADOS (RF-12), que é conteúdo de
    /// template e assunto de T03. Este aqui é da plataforma.
    /// </para>
    /// </remarks>
    private static Ok<HealthResponse> GetHealth() => TypedResults.Ok(new HealthResponse("ok"));

    /// <summary>
    /// <c>GET /api/template-options</c> — devolve o catálogo inteiro: campos, rótulos em
    /// português, padrões e restrições.
    /// </summary>
    /// <remarks>
    /// Sem parâmetro e sem negociação: o catálogo é o mesmo para todo mundo, e é ele a fonte de
    /// verdade das opções (RF-02).
    /// </remarks>
    private static Ok<TemplateOptionsCatalog> GetTemplateOptions() =>
        TypedResults.Ok(TemplateCatalog.Current);

    /// <summary>
    /// <c>POST /api/templates</c> — valida a configuração pedida e, quando ela é válida, gera o
    /// ZIP direto no corpo da resposta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A validação é soberana: revalida tudo, independentemente do que o frontend tenha
    /// verificado (docs/architecture/http-contract.md).
    /// </para>
    /// <para>
    /// Validação e geração acontecem em dois momentos separados, e essa separação é a razão de o
    /// resultado ser um <see cref="GeneratedArchiveResult"/> em vez de escrita aqui dentro:
    /// enquanto esta função executa, nenhum byte foi para o corpo, então uma recusa ainda
    /// consegue virar <c>400</c> em <c>application/problem+json</c> (RNF-03). Depois do primeiro
    /// byte, o status já foi enviado e não há mais como dizer "não".
    /// </para>
    /// <para>
    /// Até T02 o caminho feliz respondia <c>501</c>, porque o motor não existia e um <c>200</c>
    /// com pacote vazio teria afirmado uma geração que não aconteceu. O motor existe desde T03 e
    /// o <c>501</c> deixou de ser emitido.
    /// </para>
    /// </remarks>
    private static Results<ValidationProblem, GeneratedArchiveResult> CreateTemplate(
        TemplateRequestBody? body,
        [FromServices] IGenerationEngine engine)
    {
        // Corpo ausente não é um caso à parte: é um corpo em que nenhum campo veio, e a resposta
        // útil é a mesma lista de campos obrigatórios.
        TemplateRequestBody payload = body ?? new TemplateRequestBody();

        TemplateOptionsCatalog catalog = TemplateCatalog.Current;

        // O nome segue normalizado: é a forma aparada que a validação examina e que o motor de
        // geração vai receber (docs/product/option-matrix.md, "Espaço em branco").
        GenerationRequest request = new(
            ProjectNameValidator.Normalize(payload.ProjectName),
            payload.Architecture ?? string.Empty,
            payload.Database ?? string.Empty,
            payload.Authentication ?? string.Empty,
            payload.Swagger ?? false,
            payload.DotnetVersion ?? string.Empty);

        // Só o booleano precisa ser anunciado: a ausência de um texto já se reconhece pelo vazio.
        string[] omittedFields = payload.Swagger is null ? [CatalogFields.Swagger] : [];

        ValidationResult validation =
            GenerationRequestValidator.Validate(catalog, request, omittedFields);

        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(
                ToProblemErrors(validation),
                title: "Configuração inválida",
                type: ProblemTypes.InvalidConfiguration);
        }

        return new GeneratedArchiveResult(request, engine);
    }

    /// <summary>
    /// Converte o resultado da validação para o formato do membro <c>errors</c> do
    /// <c>ProblemDetails</c>. É transporte: nenhuma mensagem é reescrita aqui.
    /// </summary>
    private static Dictionary<string, string[]> ToProblemErrors(ValidationResult validation)
    {
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in validation.Errors)
        {
            errors[entry.Key] = [.. entry.Value];
        }

        return errors;
    }
}
