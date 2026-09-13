using __ProjectName__.Models;
using __ProjectName__.Persistence;

namespace __ProjectName__.Tests;

/// <summary>
/// Um <see cref="IItemStore"/> de teste, em memória.
/// </summary>
/// <remarks>
/// Existe para que os testes de regra exercitem <c>ItemService</c> sem depender do armazenamento
/// que a geração escolheu: os mesmos testes valem com banco e sem banco.
/// </remarks>
internal sealed class FakeItemStore : IItemStore
{
    private readonly Dictionary<int, Item> _items = new();
    private int _lastId;

    public Task<IReadOnlyList<Item>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Item>>([.. _items.Values.OrderBy(item => item.Id)]);

    public Task<Item?> FindAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out Item? item) ? item : null);

    public Task<Item> AddAsync(string title, CancellationToken cancellationToken)
    {
        Item item = new() { Id = ++_lastId, Title = title };

        _items[item.Id] = item;

        return Task.FromResult(item);
    }

    public Task<bool> UpdateAsync(int id, string title, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(id, out Item? item))
        {
            return Task.FromResult(false);
        }

        item.Title = title;

        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Remove(id));
}
