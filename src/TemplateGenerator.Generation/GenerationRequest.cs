namespace TemplateGenerator.Generation;

/// <summary>
/// Configuração pedida por quem gera um projeto. É a entrada do motor e o corpo de
/// <c>POST /api/templates</c> (docs/architecture/http-contract.md).
/// </summary>
/// <remarks>
/// Os valores chegam como texto de propósito: o catálogo é dado, não enum de código, e a
/// validação (T03) é quem decide se pertencem ao catálogo. Ver
/// docs/architecture/generation-engine.md, seção "Validação".
/// </remarks>
public sealed record GenerationRequest(
    string ProjectName,
    string Architecture,
    string Database,
    string Authentication,
    bool Swagger,
    string DotnetVersion);
