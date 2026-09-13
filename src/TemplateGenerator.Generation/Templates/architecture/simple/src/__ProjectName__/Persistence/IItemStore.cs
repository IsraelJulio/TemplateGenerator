using __ProjectName__.Models;

namespace __ProjectName__.Persistence;

/// <summary>
/// O armazenamento de <see cref="Item"/>, visto por quem o usa.
/// </summary>
/// <remarks>
/// A interface pertence à aplicação; a implementação vem da opção de banco escolhida na geração
/// e está em <c>ItemStore.cs</c>. É assíncrona porque um provedor de banco é assíncrono — manter
/// a mesma assinatura nas duas situações evita que trocar de armazenamento mude os endpoints.
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
