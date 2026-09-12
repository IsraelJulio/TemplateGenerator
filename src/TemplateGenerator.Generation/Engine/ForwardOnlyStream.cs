namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Envoltório de escrita que esconde a capacidade de <c>Seek</c> do destino.
/// </summary>
/// <remarks>
/// <para>
/// Parece supérfluo e não é. <c>ZipArchive</c> grava um arquivo de duas maneiras: com stream
/// posicionável, ele escreve o cabeçalho local, grava os dados e <em>volta</em> para preencher
/// CRC e tamanhos; sem, ele liga o bit 3 do <em>general purpose flag</em> e grava um
/// <em>data descriptor</em> depois dos dados. Os dois pacotes extraem igual — e têm
/// <strong>bytes diferentes</strong>.
/// </para>
/// <para>
/// O corpo de resposta do Kestrel não é posicionável; um <c>MemoryStream</c> de teste é. Sem este
/// envoltório, o SHA-256 medido no teste não seria o SHA-256 que o cliente baixa, e o critério de
/// aceite do ADR-0003 valeria para um artefato que ninguém recebe. Forçar a forma não posicionável
/// — a de produção — em toda chamada elimina a diferença.
/// </para>
/// <para>
/// O envoltório também <strong>elimina a escrita síncrona</strong> no destino. O fecho de cada
/// entrada do ZIP esvazia o buffer do <c>DeflateStream</c> por <c>Dispose</c>, e o
/// <c>Stream.DisposeAsync</c> padrão chama justamente esse <c>Dispose</c> síncrono — mesmo quando
/// a entrada foi aberta por <c>OpenAsync</c>. O corpo de resposta do ASP.NET Core recusa escrita
/// síncrona por padrão (<c>AllowSynchronousIO = false</c>), e a alternativa seria ligar essa
/// permissão na resposta inteira. Traduzir aqui a escrita síncrona para a assíncrona do destino
/// mantém a permissão desligada e deixa a exigência dentro do motor, em vez de virar uma condição
/// que todo chamador precisa lembrar de satisfazer.
/// </para>
/// <para>
/// Escrita apenas, e sem repassar <c>Dispose</c>: quem abriu o destino é quem o fecha.
/// </para>
/// </remarks>
internal sealed class ForwardOnlyStream : Stream
{
    private readonly Stream _destination;

    public ForwardOnlyStream(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("O destino do pacote precisa aceitar escrita.", nameof(destination));
        }

        _destination = destination;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _destination.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _destination.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        _destination
            .WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        // Um `ReadOnlySpan<byte>` não atravessa `await`; a cópia é o preço de traduzir a escrita
        // síncrona. Acontece uma vez por entrada, no fecho, com o resto de um bloco de deflate.
        Write(buffer.ToArray(), 0, buffer.Length);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        _destination.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        _destination.WriteAsync(buffer, cancellationToken);
}
