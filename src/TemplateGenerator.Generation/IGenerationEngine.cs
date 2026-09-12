namespace TemplateGenerator.Generation;

/// <summary>
/// Fronteira do motor de geração: entra <see cref="GenerationRequest"/>, sai um fluxo de bytes.
/// </summary>
/// <remarks>
/// <para>
/// A assinatura é intencionalmente pobre em tipos de framework. Não há <c>HttpContext</c>,
/// <c>IResult</c> nem <c>DbContext</c> aqui — é isso que permite exercitar as 32 combinações sem
/// subir a Api (docs/architecture/platform.md).
/// </para>
/// <para>
/// A escrita é feita direto no <see cref="Stream"/> de destino, sem arquivo temporário e sem
/// diretório compartilhado entre requisições (RNF-04), e precisa ser determinística: mesma
/// requisição, mesma versão de template, mesmo SHA-256 (ADR-0003).
/// </para>
/// <para>
/// A implementação é entregue em T03. Esta interface existe agora para fixar a fronteira e para
/// dar um ponto de ancoragem ao teste arquitetural.
/// </para>
/// </remarks>
public interface IGenerationEngine
{
    /// <summary>
    /// Escreve no <paramref name="destination"/> o ZIP correspondente à
    /// <paramref name="request"/>.
    /// </summary>
    /// <param name="request">Configuração já validada.</param>
    /// <param name="destination">Destino dos bytes; não é fechado por este método.</param>
    /// <param name="cancellationToken">Cancelamento cooperativo.</param>
    Task WriteArchiveAsync(
        GenerationRequest request,
        Stream destination,
        CancellationToken cancellationToken = default);
}
