namespace __ProjectName__.Application.Items;

/// <summary>
/// O corpo aceito por <c>POST /items</c> e <c>PUT /items/{id}</c>.
/// </summary>
/// <param name="Title">
/// Título do item. Chega anulável de propósito: quem decide que ele é obrigatório é
/// <c>ItemService.Validate</c>, para que a resposta seja um <c>400</c> com ProblemDetails em vez
/// de uma exceção de desserialização.
/// </param>
public sealed record ItemInput(string? Title);
