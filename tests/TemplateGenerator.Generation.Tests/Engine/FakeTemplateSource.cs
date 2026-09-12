using TemplateGenerator.Generation.Engine;

namespace TemplateGenerator.Generation.Tests.Engine;

/// <summary>
/// Um conjunto mínimo de fragmentos, montado no próprio teste.
/// </summary>
/// <remarks>
/// <para>
/// O motor é exercitado contra esta origem, e não contra <c>Templates/</c>, de propósito. O teste
/// aqui é do <strong>mecanismo</strong>: composição, marcador, caminho, conflito, ordem. Amarrá-lo
/// ao template de produção faria cada arquivo novo de template quebrar um teste de motor, e
/// esconderia o mecanismo atrás do conteúdo.
/// </para>
/// <para>
/// O template de produção é verificado na camada 1 da matriz
/// (tests/TemplateGenerator.Matrix.Tests), que é onde ele pertence.
/// </para>
/// </remarks>
internal sealed class FakeTemplateSource : ITemplateSource
{
    private readonly Dictionary<string, List<TemplateFile>> _byFragment = new(StringComparer.Ordinal);

    /// <summary>Acrescenta um arquivo a um fragmento.</summary>
    public FakeTemplateSource With(string fragment, string path, string content = "conteúdo\n")
    {
        if (!_byFragment.TryGetValue(fragment, out List<TemplateFile>? files))
        {
            files = [];
            _byFragment[fragment] = files;
        }

        files.Add(new TemplateFile(fragment, path, content));

        return this;
    }

    /// <inheritdoc />
    public IReadOnlyList<TemplateFile> Read(string fragment) =>
        _byFragment.TryGetValue(fragment, out List<TemplateFile>? files) ? files : [];
}
