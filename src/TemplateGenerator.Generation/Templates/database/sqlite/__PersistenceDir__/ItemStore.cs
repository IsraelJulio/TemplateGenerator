using Microsoft.EntityFrameworkCore;
__PersistenceHeader__

/// <summary>
/// Armazenamento de <see cref="Item"/> em <strong>SQLite</strong>, sobre EF Core.
/// </summary>
/// <remarks>
/// <para>
/// O banco é um arquivo local, apontado pela cadeia de conexão <c>Default</c>. Diferente do
/// armazenamento em memória, <strong>os dados sobrevivem ao reinício</strong> da aplicação.
/// </para>
/// <para>
/// As leituras usam <c>AsNoTracking</c> porque nada do que sai daqui volta para ser gravado no
/// mesmo escopo: o CRUD lê para responder HTTP e grava a partir de uma busca própria. As escritas
/// buscam com rastreamento, justamente para que <c>SaveChangesAsync</c> enxergue a alteração.
/// </para>
/// </remarks>
/// <param name="context">A sessão com o banco, com tempo de vida de requisição.</param>
public sealed class ItemStore(AppDbContext context) : IItemStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Item>> ListAsync(CancellationToken cancellationToken) =>
        await context.Items
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<Item?> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<Item> AddAsync(string title, CancellationToken cancellationToken)
    {
        Item item = new() { Title = title };

        context.Items.Add(item);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return item;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(int id, string title, CancellationToken cancellationToken)
    {
        Item? item = await context.Items
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return false;
        }

        item.Title = title;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken)
    {
        Item? item = await context.Items
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return false;
        }

        context.Items.Remove(item);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
