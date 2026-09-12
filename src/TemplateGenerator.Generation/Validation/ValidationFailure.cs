namespace TemplateGenerator.Generation.Validation;

/// <summary>
/// Um erro de validação já endereçado ao campo que o causou.
/// </summary>
/// <remarks>
/// O endereçamento não é enfeite: é o que permite à tela mostrar a mensagem <em>inline</em>, ao
/// lado do controle certo (docs/architecture/http-contract.md).
/// </remarks>
/// <param name="Field">Chave do campo, como no catálogo.</param>
/// <param name="Message">Mensagem em português, dirigida a quem preencheu o formulário.</param>
public sealed record ValidationFailure(string Field, string Message);
