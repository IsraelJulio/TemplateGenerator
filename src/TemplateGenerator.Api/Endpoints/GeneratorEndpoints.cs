using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
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
    /// <para>
    /// Sem parâmetro e sem negociação: o catálogo é o mesmo para todo mundo, e é ele a fonte de
    /// verdade das opções (RF-02).
    /// </para>
    /// <para>
    /// Junto vai o membro <c>unavailable</c>: quais valores ainda não têm template, como DADO, para
    /// a tela desabilitar o que vier desabilitado e mostrar a razão sem codificar um único valor
    /// (ADR-0012). Ele é <strong>derivado</strong> dos fragmentos — quando alguém escrever o
    /// template que falta, a opção acende sozinha.
    /// </para>
    /// </remarks>
    private static Ok<TemplateOptionsResponse> GetTemplateOptions(
        [FromServices] TemplateAvailability availability) =>
        TypedResults.Ok(TemplateOptionsResponse.From(TemplateCatalog.Current, availability));

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
    /// <strong>A ordem das duas recusas</strong> (ADR-0012, item 4): primeiro a validação inteira
    /// — nome do projeto, pertinência ao catálogo e a restrição <c>identity-requires-database</c>
    /// —, e só depois a disponibilidade. Nunca ao contrário: antes da validação a derivação mente.
    /// Um <c>architecture: "banana"</c> não tem fragmento, logo seria recusado com "o template
    /// desta opção ainda não foi escrito" para um valor que <em>não existe no catálogo</em>, e a
    /// resposta verdadeira é <c>400, o valor não pertence ao catálogo</c>. Inverter a ordem
    /// transforma um erro claro de quem chamou num defeito inventado do servidor.
    /// </para>
    /// <para>
    /// <strong>E a recusa entra antes de <see cref="GeneratedArchiveResult"/> existir</strong>:
    /// aquele resultado escreve <c>Content-Type: application/zip</c> e
    /// <c>Content-Disposition: attachment</c> como primeira coisa que faz, e uma recusa depois
    /// disso sairia com cabeçalho de download em cima. O motor impõe a mesma recusa do lado dele,
    /// perguntando ao mesmo <see cref="TemplateAvailability"/>: o que se duplica é a imposição,
    /// não a verdade.
    /// </para>
    /// </remarks>
    private static Results<ValidationProblem, ProblemHttpResult, GeneratedArchiveResult> CreateTemplate(
        TemplateRequestBody? body,
        [FromServices] IGenerationEngine engine,
        [FromServices] TemplateAvailability availability)
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

        IReadOnlyList<string> unavailable = availability.UnavailableFields(request);

        if (unavailable.Count > 0)
        {
            return NotImplemented(unavailable);
        }

        return new GeneratedArchiveResult(request, engine);
    }

    /// <summary>
    /// A resposta <c>501</c> de uma combinação sem template (ADR-0012, item 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>errors</c>, e não uma extensão nova, é quem diz <strong>qual campo</strong> causou a
    /// recusa: é a mesma estrutura do <c>400</c>, o mesmo caminho de código e a mesma marcação
    /// <em>inline</em> na tela. Uma extensão paralela seria um segundo formato para a mesma
    /// informação. Uma entrada por campo indisponível, na ordem dos campos no catálogo, cada uma
    /// com a mesma frase — a mesma que o catálogo publica em <c>unavailable</c>.
    /// </para>
    /// <para>
    /// <strong>Sem <c>detail</c>:</strong> com <c>errors</c> preenchido, um <c>detail</c> genérico
    /// só repetiria em prosa o que já está endereçado ao campo, e o frontend trata <c>detail</c>
    /// como o que se mostra <em>quando não há</em> erro de campo.
    /// </para>
    /// </remarks>
    private static ProblemHttpResult NotImplemented(IReadOnlyList<string> fields)
    {
        // Ordenado, e não `Dictionary`: a ordem das entradas é a ordem dos campos no catálogo, e
        // ela é declarada — não um efeito colateral de como as chaves caem nos buckets.
        OrderedDictionary<string, string[]> errors = new(StringComparer.Ordinal);

        foreach (string field in fields)
        {
            errors[field] = [TemplateAvailability.UnavailableReason];
        }

        return TypedResults.Problem(new HttpValidationProblemDetails(errors)
        {
            Type = ProblemTypes.GenerationNotImplemented,
            Title = "Esta combinação ainda não gera projeto",
            Status = StatusCodes.Status501NotImplemented,
        });
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
