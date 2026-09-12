using TemplateGenerator.Generation;

namespace TemplateGenerator.Api.Endpoints;

/// <summary>
/// A resposta <c>200 application/zip</c> de <c>POST /api/templates</c>: o pacote escrito direto
/// no corpo da resposta.
/// </summary>
/// <remarks>
/// <para>
/// Sem arquivo temporário e sem diretório compartilhado entre requisições (RNF-04). O motor
/// resolve o pacote inteiro na memória <em>desta</em> requisição e escreve as entradas do ZIP no
/// corpo; duas gerações simultâneas não têm um byte em comum.
/// </para>
/// <para>
/// <c>Content-Length</c> não é declarado. Seria possível — o plano conhece o conteúdo antes de
/// comprimir —, mas não o tamanho <em>comprimido</em>, e anunciar um número errado é pior que não
/// anunciar nenhum: o cliente cortaria o download. A resposta sai em <em>chunked</em>.
/// </para>
/// </remarks>
internal sealed class GeneratedArchiveResult : IResult
{
    /// <summary>O tipo de mídia do pacote, como manda o contrato.</summary>
    public const string ContentType = "application/zip";

    private readonly GenerationRequest _request;
    private readonly IGenerationEngine _engine;

    public GeneratedArchiveResult(GenerationRequest request, IGenerationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(engine);

        _request = request;
        _engine = engine;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.ContentType = ContentType;

        // O nome do projeto já passou por ProjectNameValidator: ASCII, sem '/', sem '\', sem
        // caractere de controle e sem aspas. É por isso que ele pode ir entre aspas no cabeçalho
        // sem codificação RFC 5987 (docs/product/option-matrix.md, "Por que ASCII").
        // Escrito literalmente, e não por `ContentDisposition.ToString()`, porque o contrato
        // fixa a forma com aspas e o tipo do framework decide sozinho quando aspas são
        // necessárias.
        httpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{_request.ProjectName}.zip\"";

        // Um pacote gerado é sempre a resposta daquela configuração, e o cliente que pedir de novo
        // precisa receber a versão corrente dos templates.
        httpContext.Response.Headers.CacheControl = "no-store";

        await _engine
            .WriteArchiveAsync(_request, httpContext.Response.Body, httpContext.RequestAborted)
            .ConfigureAwait(false);
    }
}
