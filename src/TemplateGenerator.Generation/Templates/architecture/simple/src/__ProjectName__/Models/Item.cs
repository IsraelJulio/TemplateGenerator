namespace __ProjectName__.Models;

/// <summary>
/// O recurso do CRUD: um identificador e um <c>title</c> obrigatório (RF-13).
/// </summary>
/// <remarks>
/// Classe com propriedades de leitura e escrita, e não <c>record</c>: é a forma que qualquer
/// provedor de persistência materializa sem ajuste.
/// </remarks>
public sealed class Item
{
    /// <summary>Identificador, atribuído pelo armazenamento.</summary>
    public int Id { get; set; }

    /// <summary>Título do item. Obrigatório.</summary>
    public string Title { get; set; } = string.Empty;
}
