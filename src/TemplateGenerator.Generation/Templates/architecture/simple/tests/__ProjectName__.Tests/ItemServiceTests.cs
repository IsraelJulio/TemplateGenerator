using __ProjectName__.Models;
using __ProjectName__.Services;
using Xunit;

namespace __ProjectName__.Tests;

/// <summary>
/// As regras do CRUD de <c>Item</c>, exercitadas sem subir a aplicação.
/// </summary>
public sealed class ItemServiceTests
{
    private readonly ItemService _service = new(new FakeItemStore());

    [Fact]
    public async Task Criar_devolve_o_item_com_identificador()
    {
        Item created = await _service.CreateAsync(new ItemInput("Primeiro item"), TestContext.Current.CancellationToken);

        Assert.True(created.Id > 0);
        Assert.Equal("Primeiro item", created.Title);
    }

    [Fact]
    public async Task Listar_devolve_o_que_foi_criado()
    {
        await _service.CreateAsync(new ItemInput("Um"), TestContext.Current.CancellationToken);
        await _service.CreateAsync(new ItemInput("Dois"), TestContext.Current.CancellationToken);

        IReadOnlyList<Item> all = await _service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Um", "Dois"], all.Select(item => item.Title));
    }

    [Fact]
    public async Task Buscar_por_identificador_inexistente_devolve_nulo()
    {
        Assert.Null(await _service.FindAsync(404, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Substituir_troca_o_titulo()
    {
        Item created = await _service.CreateAsync(new ItemInput("Antes"), TestContext.Current.CancellationToken);

        Assert.True(await _service.ReplaceAsync(created.Id, new ItemInput("Depois"), TestContext.Current.CancellationToken));

        Item? found = await _service.FindAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal("Depois", found.Title);
    }

    [Fact]
    public async Task Substituir_item_inexistente_devolve_falso()
    {
        Assert.False(await _service.ReplaceAsync(404, new ItemInput("Qualquer"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remover_tira_o_item_da_listagem()
    {
        Item created = await _service.CreateAsync(new ItemInput("Descartável"), TestContext.Current.CancellationToken);

        Assert.True(await _service.DeleteAsync(created.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await _service.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remover_item_inexistente_devolve_falso()
    {
        Assert.False(await _service.DeleteAsync(404, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Titulo_vazio_e_recusado(string? title)
    {
        IDictionary<string, string[]>? errors = ItemService.Validate(new ItemInput(title));

        Assert.NotNull(errors);
        Assert.Contains("title", errors.Keys);
    }

    [Fact]
    public void Titulo_longo_demais_e_recusado()
    {
        string title = new('a', ItemService.TitleMaxLength + 1);

        Assert.NotNull(ItemService.Validate(new ItemInput(title)));
    }

    [Fact]
    public void Titulo_valido_passa_e_perde_os_espacos_das_pontas()
    {
        Assert.Null(ItemService.Validate(new ItemInput("  Um título  ")));
    }

    [Fact]
    public async Task O_titulo_e_guardado_sem_espaco_nas_pontas()
    {
        Item created = await _service.CreateAsync(new ItemInput("  Aparado  "), TestContext.Current.CancellationToken);

        Assert.Equal("Aparado", created.Title);
    }
}
