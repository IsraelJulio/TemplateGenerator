namespace TemplateGenerator.Api.Endpoints;

/// <summary>
/// O corpo cru de <c>POST /api/templates</c>, como ele chega do cliente.
/// </summary>
/// <remarks>
/// <para>
/// Todo membro é anulável de propósito. Este tipo representa o que <em>foi enviado</em>, não uma
/// configuração válida: distinguir "veio vazio" de "veio errado" é o que permite responder
/// "informe um valor para o campo X" em vez de validar silenciosamente um padrão que ninguém
/// escolheu.
/// </para>
/// <para>
/// O caso que obriga a isso é <c>swagger</c>: um <c>bool</c> não anulável ausente do JSON chega
/// como <c>false</c>, indistinguível de uma escolha explícita, e o padrão do catálogo é
/// <c>true</c> — a omissão viraria, em silêncio, o oposto do padrão.
/// </para>
/// <para>
/// A conversão para <see cref="TemplateGenerator.Generation.GenerationRequest"/> acontece só
/// depois da validação.
/// </para>
/// </remarks>
public sealed record TemplateRequestBody
{
    /// <summary>Nome do projeto, validado em docs/product/option-matrix.md.</summary>
    public string? ProjectName { get; init; }

    /// <summary>Chave da arquitetura escolhida.</summary>
    public string? Architecture { get; init; }

    /// <summary>Chave do banco escolhido.</summary>
    public string? Database { get; init; }

    /// <summary>Chave da autenticação escolhida.</summary>
    public string? Authentication { get; init; }

    /// <summary>Swagger ligado ou desligado.</summary>
    public bool? Swagger { get; init; }

    /// <summary>Chave da versão do .NET escolhida.</summary>
    public string? DotnetVersion { get; init; }
}
