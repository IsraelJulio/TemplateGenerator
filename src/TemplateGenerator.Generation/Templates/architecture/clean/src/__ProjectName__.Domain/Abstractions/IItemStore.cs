using __ProjectName__.Domain.Items;

namespace __ProjectName__.Domain.Abstractions;

/// <summary>
/// A <strong>porta</strong> de persistência de <see cref="Item"/>: o armazenamento visto por
/// quem o usa.
/// </summary>
/// <remarks>
/// <para>
/// Ela mora no domínio, e não na infraestrutura, porque é o domínio que diz de que operações ele
/// precisa; quem obedece é a implementação. É essa inversão que permite a
/// <c>__ProjectName__.Infrastructure</c> depender de <c>__ProjectName__.Domain</c> e nunca o
/// contrário — a direção de dependência do diagrama em
/// <c>docs/architecture/generated-projects.md</c>.
/// </para>
/// <para>
/// A implementação vem da opção de banco escolhida na geração e está em
/// <c>__ProjectName__.Infrastructure/Persistence/ItemStore.cs</c>. É assíncrona porque um provedor
/// de banco é assíncrono — manter a mesma assinatura nas duas situações evita que trocar de
/// armazenamento mude os casos de uso.
/// </para>
/// </remarks>
public interface IItemStore
{
    /// <summary>Todos os itens, em ordem de identificador.</summary>
    Task<IReadOnlyList<Item>> ListAsync(CancellationToken cancellationToken);

    /// <summary>O item de <paramref name="id"/>, ou <c>null</c> se não existir.</summary>
    Task<Item?> FindAsync(int id, CancellationToken cancellationToken);

    /// <summary>Cria um item com <paramref name="title"/> e devolve o item já com identificador.</summary>
    Task<Item> AddAsync(string title, CancellationToken cancellationToken);

    /// <summary>Troca o título do item. <c>false</c> quando o item não existe.</summary>
    Task<bool> UpdateAsync(int id, string title, CancellationToken cancellationToken);

    /// <summary>Remove o item. <c>false</c> quando o item não existe.</summary>
    Task<bool> RemoveAsync(int id, CancellationToken cancellationToken);
}
