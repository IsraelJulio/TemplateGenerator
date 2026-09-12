using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Engine;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>
/// Um pacote gerado, aberto para inspeção: os bytes, o hash e o conteúdo de cada arquivo.
/// </summary>
/// <remarks>
/// <para>
/// É a ferramenta da <strong>camada 1</strong> (docs/quality/test-strategy.md): gerar o ZIP e
/// olhar dentro, sem <c>restore</c> e sem <c>build</c>. Nada aqui escreve em disco — o pacote
/// nasce e morre na memória do teste.
/// </para>
/// <para>
/// Quem afirma o conteúdo obrigatório de cada combinação é o papel <c>qa</c>. Esta classe só
/// entrega o conteúdo.
/// </para>
/// </remarks>
public sealed class GeneratedPackage
{
    private readonly Dictionary<string, string> _files;

    private GeneratedPackage(
        GenerationRequest request,
        byte[] bytes,
        IReadOnlyList<string> paths,
        Dictionary<string, string> files)
    {
        Request = request;
        Bytes = bytes;
        Paths = paths;
        _files = files;
    }

    /// <summary>A configuração que gerou o pacote.</summary>
    public GenerationRequest Request { get; }

    /// <summary>O ZIP inteiro.</summary>
    public byte[] Bytes { get; }

    /// <summary>Os caminhos das entradas, na ordem em que foram gravadas.</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>O SHA-256 do ZIP, em hexadecimal — o critério de RNF-02 (ADR-0003).</summary>
    public string Sha256 => Convert.ToHexString(SHA256.HashData(Bytes));

    /// <summary>Gera e abre o pacote de <paramref name="request"/>.</summary>
    public static async Task<GeneratedPackage> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        GenerationEngine engine = new();

        using MemoryStream buffer = new();

        await engine.WriteArchiveAsync(request, buffer, cancellationToken);

        byte[] bytes = buffer.ToArray();

        List<string> paths = [];
        Dictionary<string, string> files = new(StringComparer.Ordinal);

        using ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            paths.Add(entry.FullName);

            using StreamReader reader = new(entry.Open(), Encoding.UTF8);

            files[entry.FullName] = await reader.ReadToEndAsync(cancellationToken);
        }

        return new GeneratedPackage(request, bytes, paths, files);
    }

    /// <summary>O conteúdo do arquivo em <paramref name="path"/>.</summary>
    /// <exception cref="KeyNotFoundException">Quando o pacote não traz esse arquivo.</exception>
    public string Read(string path) => _files.TryGetValue(path, out string? content)
        ? content
        : throw new KeyNotFoundException(
            $"O pacote de {Describe(Request)} não traz '{path}'. Traz: " +
            string.Join(", ", Paths));

    /// <summary>Diz se o pacote contém <paramref name="path"/>.</summary>
    public bool Contains(string path) => _files.ContainsKey(path);

    /// <summary>Descrição curta de uma combinação, para mensagem de falha.</summary>
    public static string Describe(GenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return $"{request.Architecture}/{request.Database}/{request.Authentication}" +
            $"/swagger={(request.Swagger ? "on" : "off")}";
    }
}
