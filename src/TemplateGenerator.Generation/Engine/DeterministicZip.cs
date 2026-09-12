using System.IO.Compression;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// O empacotador: escreve um <see cref="GenerationPlan"/> como ZIP, seguindo as cinco regras do
/// ADR-0003.
/// </summary>
/// <remarks>
/// <list type="number">
///   <item><description><see cref="Epoch"/> em toda entrada.</description></item>
///   <item><description>Entradas na ordem ordinal do plano.</description></item>
///   <item><description><see cref="Compression"/> explícito e fixo.</description></item>
///   <item><description>Conteúdo já normalizado por <see cref="TextContent"/>.</description></item>
///   <item><description>Nada de GUID, relógio ou caminho de máquina no conteúdo.</description></item>
/// </list>
/// </remarks>
public static class DeterministicZip
{
    /// <summary>
    /// A data de toda entrada: <c>1980-01-01T00:00:00Z</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O formato ZIP grava data em MS-DOS, cuja época começa em 1980.
    /// <c>ZipArchiveEntry.LastWriteTime</c> <strong>lança exceção</strong> abaixo disso:
    /// <c>DateTime.MinValue</c> e a época Unix (1970) não servem.
    /// </para>
    /// <para>
    /// O deslocamento é <c>Zero</c> e não o da máquina. A conversão para data DOS usa a parte
    /// local do <c>DateTimeOffset</c> como está, então um deslocamento de máquina faria o mesmo
    /// pacote sair com datas diferentes em fusos diferentes — e o hash junto.
    /// </para>
    /// </remarks>
    public static readonly DateTimeOffset Epoch = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// O nível de compressão, explícito. <c>Optimal</c> é o default hoje, e é exatamente por isso
    /// que ele precisa estar escrito: um default que mude de valor numa versão futura do runtime
    /// muda o hash de todos os pacotes sem uma linha de diff.
    /// </summary>
    public const CompressionLevel Compression = CompressionLevel.Optimal;

    /// <summary>
    /// Escreve <paramref name="plan"/> em <paramref name="destination"/>.
    /// </summary>
    /// <param name="plan">Plano já resolvido e validado.</param>
    /// <param name="destination">Destino dos bytes. Não é fechado aqui.</param>
    /// <param name="cancellationToken">Cancelamento cooperativo.</param>
    public static async Task WriteAsync(
        GenerationPlan plan,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(destination);

        // Tudo assíncrono: o corpo de resposta do Kestrel recusa escrita síncrona
        // (`AllowSynchronousIO` é falso por padrão), e ligar a escrita síncrona para caber um
        // `ZipArchive` antigo prenderia uma thread do pool por download.
        ZipArchive archive = await ZipArchive.CreateAsync(
            new ForwardOnlyStream(destination),
            ZipArchiveMode.Create,
            leaveOpen: false,
            entryNameEncoding: null,
            cancellationToken).ConfigureAwait(false);

        await using (archive.ConfigureAwait(false))
        {
            foreach (GeneratedFile file in plan.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ZipArchiveEntry entry = archive.CreateEntry(file.Path, Compression);
                entry.LastWriteTime = Epoch;

                Stream target = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);

                await using (target.ConfigureAwait(false))
                {
                    await target.WriteAsync(file.Content, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
