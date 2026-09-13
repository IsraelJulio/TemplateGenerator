using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TemplateGenerator.Generation.Catalog;
using TemplateGenerator.Generation.Engine;
using Xunit;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// As cinco regras do ADR-0003, verificadas pelo critério que o próprio ADR define: <strong>o
/// SHA-256 do arquivo ZIP</strong>.
/// </summary>
public sealed class DeterministicZipTests
{
    /// <summary>Um destino que não sabe se posicionar — como o corpo de resposta do Kestrel.</summary>
    private sealed class NonSeekableSink(Stream inner) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            inner.Write(buffer, offset, count);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            inner.WriteAsync(buffer, cancellationToken);
    }

    private static readonly TemplateOptionsCatalog _catalog = TemplateCatalog.Current;

    private static GenerationRequest Request(
        string projectName = "Acme.Billing.Api",
        bool swagger = true) =>
        new(projectName, "simple", "none", "none", swagger, "net10.0");

    private static ITemplateSource Source() => new FakeTemplateSource()
        .With("common", "global.json", "{ \"sdk\": { \"version\": \"10.0.302\" } }")
        .With("common", "src/__ProjectName__/Program.cs", "// __ProjectName__\n__Itens__\n")
        .With("common", "Z.md", "z")
        .With("common", "a.md", "a")
        .With("architecture/simple", "src/__ProjectName__/Endpoints/HealthEndpoints.cs", "// health\n")

        // Contribuições entram no conteúdo de um arquivo de outro fragmento e não viram entrada
        // do ZIP (ADR-0011).
        .With("database/none", $"{TemplateContributions.Directory}/Itens.txt", "// sem banco")
        .With("swagger/enabled", $"{TemplateContributions.Directory}/Itens.txt", "// swagger");

    private static async Task<byte[]> GenerateAsync(
        GenerationRequest? request = null,
        bool seekableDestination = true)
    {
        GenerationEngine engine = new(_catalog, Source());

        using MemoryStream buffer = new();

        Stream destination = seekableDestination ? buffer : new NonSeekableSink(buffer);

        await engine.WriteArchiveAsync(
            request ?? Request(),
            destination,
            TestContext.Current.CancellationToken);

        return buffer.ToArray();
    }

    private static string HashOf(byte[] archive) => Convert.ToHexString(SHA256.HashData(archive));

    [Fact]
    public async Task Mesma_requisicao_produz_o_mesmo_SHA256()
    {
        byte[] first = await GenerateAsync();
        byte[] second = await GenerateAsync();

        Assert.Equal(HashOf(first), HashOf(second));
    }

    [Fact]
    public async Task Destino_posicionavel_e_nao_posicionavel_produzem_o_mesmo_SHA256()
    {
        // Sem o ForwardOnlyStream, o ZipArchive muda de estratégia conforme o destino sabe ou não
        // se posicionar (cabeçalho reescrito x data descriptor) e os bytes saem diferentes. Isso
        // faria o hash medido em teste, sobre um MemoryStream, não ser o hash que o cliente baixa
        // pelo Kestrel — e o critério do ADR-0003 valeria para um artefato que ninguém recebe.
        byte[] seekable = await GenerateAsync(seekableDestination: true);
        byte[] streamed = await GenerateAsync(seekableDestination: false);

        Assert.Equal(HashOf(seekable), HashOf(streamed));
    }

    [Fact]
    public async Task Mudar_uma_opcao_muda_o_SHA256()
    {
        // O manifesto carrega as opções; mudar uma tem que mudar o pacote. Sem isso, dois pacotes
        // diferentes teriam o mesmo hash e o critério de aceite não estaria medindo nada.
        byte[] comSwagger = await GenerateAsync(Request(swagger: true));
        byte[] semSwagger = await GenerateAsync(Request(swagger: false));

        Assert.NotEqual(HashOf(comSwagger), HashOf(semSwagger));
    }

    [Fact]
    public async Task Mudar_o_nome_do_projeto_muda_o_SHA256()
    {
        byte[] acme = await GenerateAsync(Request("Acme.Billing.Api"));
        byte[] contoso = await GenerateAsync(Request("Contoso.Api"));

        Assert.NotEqual(HashOf(acme), HashOf(contoso));
    }

    [Fact]
    public async Task Toda_entrada_carrega_a_data_de_1980()
    {
        using ZipArchive archive = new(new MemoryStream(await GenerateAsync()), ZipArchiveMode.Read);

        Assert.NotEmpty(archive.Entries);
        Assert.All(
            archive.Entries,
            entry => Assert.Equal(DeterministicZip.Epoch.DateTime, entry.LastWriteTime.DateTime));
    }

    [Fact]
    public async Task Entradas_saem_em_ordem_ordinal_de_caminho()
    {
        using ZipArchive archive = new(new MemoryStream(await GenerateAsync()), ZipArchiveMode.Read);

        string[] asWritten = [.. archive.Entries.Select(entry => entry.FullName)];
        string[] ordinal = [.. asWritten.Order(StringComparer.Ordinal)];

        Assert.Equal(ordinal, asWritten);
    }

    [Fact]
    public async Task O_pacote_abre_e_traz_o_manifesto_e_os_fragmentos()
    {
        using ZipArchive archive = new(new MemoryStream(await GenerateAsync()), ZipArchiveMode.Read);

        ZipArchiveEntry manifest =
            Assert.Single(archive.Entries, entry => entry.FullName == GenerationManifest.Path);

        using StreamReader reader = new(manifest.Open(), Encoding.UTF8);

        Assert.Contains(_catalog.TemplateVersion, reader.ReadToEnd(), StringComparison.Ordinal);

        // O nome do projeto é o nome que a pessoa digitou, sem sufixo acrescentado
        // (docs/architecture/generated-projects.md, "Nomes de projeto e de pasta").
        Assert.Contains(
            "src/Acme.Billing.Api/Program.cs",
            archive.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public async Task Arquivo_de_contribuicao_nao_chega_ao_pacote_e_o_texto_dele_chega()
    {
        using ZipArchive archive = new(new MemoryStream(await GenerateAsync()), ZipArchiveMode.Read);

        Assert.DoesNotContain(
            archive.Entries.Select(entry => entry.FullName),
            path => path.Contains(TemplateContributions.Directory, StringComparison.Ordinal));

        ZipArchiveEntry program = Assert.Single(
            archive.Entries,
            entry => entry.FullName == "src/Acme.Billing.Api/Program.cs");

        using StreamReader reader = new(program.Open(), Encoding.UTF8);

        // Na ordem de seleção: database antes de swagger.
        Assert.Equal(
            "// Acme.Billing.Api\n// sem banco\n// swagger\n",
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Nenhuma_entrada_e_diretorio_e_nenhuma_sai_da_raiz()
    {
        using ZipArchive archive = new(new MemoryStream(await GenerateAsync()), ZipArchiveMode.Read);

        Assert.All(archive.Entries, entry =>
        {
            Assert.False(entry.FullName.EndsWith('/'));
            Assert.DoesNotContain("..", entry.FullName.Split('/'));
            Assert.DoesNotContain('\\', entry.FullName);
        });
    }
}
