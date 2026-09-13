using __ProjectName__.Models;
using __ProjectName__.Persistence;

namespace __ProjectName__.Services;

/// <summary>
/// As regras de <see cref="Item"/>: o que é um título aceitável e o que cada operação do CRUD
/// faz. Os endpoints só traduzem isto para HTTP.
/// </summary>
/// <param name="store">O armazenamento da opção de banco escolhida na geração.</param>
public sealed class ItemService(IItemStore store)
{
    /// <summary>Tamanho máximo aceito para <c>title</c>.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>
    /// Verifica o corpo de uma criação ou substituição.
    /// </summary>
    /// <returns>
    /// <c>null</c> quando o corpo é válido; caso contrário, os erros no formato que
    /// <c>Results.ValidationProblem</c> espera.
    /// </returns>
    public static IDictionary<string, string[]>? Validate(ItemInput input)
    {
        string title = Normalize(input);

        if (title.Length == 0)
        {
            return Error("O campo 'title' é obrigatório.");
        }

        if (title.Length > TitleMaxLength)
        {
            return Error($"O campo 'title' aceita no máximo {TitleMaxLength} caracteres.");
        }

        return null;
    }

    /// <summary>Todos os itens.</summary>
    public Task<IReadOnlyList<Item>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    /// <summary>O item de <paramref name="id"/>, ou <c>null</c>.</summary>
    public Task<Item?> FindAsync(int id, CancellationToken cancellationToken) =>
        store.FindAsync(id, cancellationToken);

    /// <summary>Cria um item. O corpo já precisa ter passado por <see cref="Validate"/>.</summary>
    public Task<Item> CreateAsync(ItemInput input, CancellationToken cancellationToken) =>
        store.AddAsync(Normalize(input), cancellationToken);

    /// <summary>Substitui o título de um item. <c>false</c> quando ele não existe.</summary>
    public Task<bool> ReplaceAsync(int id, ItemInput input, CancellationToken cancellationToken) =>
        store.UpdateAsync(id, Normalize(input), cancellationToken);

    /// <summary>Remove um item. <c>false</c> quando ele não existe.</summary>
    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        store.RemoveAsync(id, cancellationToken);

    /// <summary>
    /// O título como ele é guardado: sem espaço nas pontas, nunca <c>null</c>. Aparar aqui, e não
    /// em cada chamador, é o que faz a validação e a gravação enxergarem exatamente o mesmo texto.
    /// </summary>
    private static string Normalize(ItemInput input) => input?.Title?.Trim() ?? string.Empty;

    private static IDictionary<string, string[]> Error(string message) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["title"] = [message],
        };
}
