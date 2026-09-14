using Microsoft.EntityFrameworkCore;
__PersistenceHeader__

/// <summary>
/// A sessão com o banco: hoje, uma tabela só — a de <see cref="Item"/>.
/// </summary>
/// <remarks>
/// <para>
/// O mapeamento repete, no banco, as mesmas regras que o serviço de <c>Item</c> aplica na entrada:
/// <c>Title</c> é obrigatório e tem no máximo 200 caracteres. Escrever isso nos dois lugares é
/// deliberado — a validação devolve <c>400</c> com uma mensagem em português, e a coluna garante
/// que nada entre por outro caminho.
/// </para>
/// <para>
/// A migração inicial em <c>Migrations/</c> foi gerada a partir deste mapeamento e é
/// <strong>específica do PostgreSQL</strong>: a de outro provedor não serve aqui.
/// </para>
/// </remarks>
/// <param name="options">As opções que a composição da aplicação já resolveu.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Os itens do CRUD.</summary>
    public DbSet<Item> Items => Set<Item>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Item>(item =>
        {
            item.HasKey(entity => entity.Id);
            item.Property(entity => entity.Title).IsRequired().HasMaxLength(200);
        });
    }
}
