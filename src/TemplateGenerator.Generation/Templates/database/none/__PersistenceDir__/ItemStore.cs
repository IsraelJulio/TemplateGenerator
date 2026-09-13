__PersistenceHeader__

/// <summary>
/// Armazenamento <strong>em memória</strong> de <see cref="Item"/>.
/// </summary>
/// <remarks>
/// Os itens vivem no processo da aplicação: não há arquivo, não há banco e <strong>os dados são
/// perdidos quando a aplicação reinicia</strong> (RF-16). É o comportamento pedido para a opção
/// "sem banco de dados", não uma limitação — para persistir, gere o projeto com SQLite ou
/// PostgreSQL.
///
/// O acesso é protegido por um <see cref="Lock"/> porque o serviço é registrado como singleton e
/// o servidor atende requisições em paralelo.
/// </remarks>
public sealed class ItemStore : IItemStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Item> _items = new();
    private int _lastId;

    /// <inheritdoc />
    public Task<IReadOnlyList<Item>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Item>>([.. _items.Values.OrderBy(item => item.Id)]);
        }
    }

    /// <inheritdoc />
    public Task<Item?> FindAsync(int id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_items.TryGetValue(id, out Item? item) ? item : null);
        }
    }

    /// <inheritdoc />
    public Task<Item> AddAsync(string title, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Item item = new() { Id = ++_lastId, Title = title };

            _items[item.Id] = item;

            return Task.FromResult(item);
        }
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(int id, string title, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out Item? item))
            {
                return Task.FromResult(false);
            }

            item.Title = title;

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(int id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_items.Remove(id));
        }
    }
}
