using System.Collections.Frozen;
using System.Reflection;
using System.Text;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// A origem de produção dos fragmentos: os arquivos de
/// <c>src/TemplateGenerator.Generation/Templates/</c>, embutidos no assembly em tempo de
/// compilação.
/// </summary>
/// <remarks>
/// <para><strong>O contrato com quem escreve template</strong> (papel <c>template-engineer</c>):</para>
/// <list type="number">
///   <item>
///     <description>
///     O arquivo entra no pacote pelo simples fato de existir sob
///     <c>Templates/&lt;fragmento&gt;/</c>. O <c>.csproj</c> usa um curinga
///     (<c>Templates\**\*</c>) — acrescentar um fragmento <strong>não</strong> exige mudar C#
///     nem o <c>.csproj</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///     O caminho do arquivo dentro do fragmento é o caminho dentro do ZIP. <c>common/README.md</c>
///     vira <c>README.md</c> na raiz do pacote.
///     </description>
///   </item>
///   <item>
///     <description>
///     O único nome ignorado é <c>.gitkeep</c>, que é plumbing de repositório e não fragmento.
///     Um <c>.gitignore</c> ou <c>.editorconfig</c> de template entra normalmente.
///     </description>
///   </item>
/// </list>
/// <para>
/// Recurso embutido, e não arquivo copiado para <c>bin/</c>, por três motivos: a requisição não
/// toca o sistema de arquivos (RNF-01 e RNF-04 ficam triviais de sustentar); o conjunto de
/// templates não pode ser alterado por nada que apareça no diretório de saída; e o mesmo binário
/// se comporta igual rodando pelo <c>dotnet run</c>, publicado ou dentro de
/// <c>WebApplicationFactory</c>.
/// </para>
/// <para>
/// O nome lógico de cada recurso é fixado no <c>.csproj</c> como
/// <c>templates/&lt;caminho relativo com '/'&gt;</c>. Sem isso o nome default do MSBuild
/// substituiria cada separador por ponto e seria impossível distinguir diretório de extensão em
/// <c>Templates.common.global.json</c>.
/// </para>
/// </remarks>
public sealed class EmbeddedTemplateSource : ITemplateSource
{
    /// <summary>
    /// Prefixo do nome lógico de todo recurso de template. Precisa concordar com o
    /// <c>LogicalName</c> declarado em <c>TemplateGenerator.Generation.csproj</c>.
    /// </summary>
    public const string ResourcePrefix = "templates/";

    private readonly Assembly _assembly;
    private readonly FrozenDictionary<string, IReadOnlyList<TemplateFile>> _byFragment;

    /// <summary>
    /// Cria uma origem sobre os recursos de <paramref name="assembly"/>.
    /// </summary>
    public EmbeddedTemplateSource(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _assembly = assembly;

        // A leitura acontece uma vez: o conjunto é imutável e compartilhado entre requisições.
        // Nada aqui depende de diretório de trabalho, de cultura ou de relógio.
        _byFragment = ReadAll()
            .GroupBy(file => file.Fragment, StringComparer.Ordinal)
            .ToFrozenDictionary(
                group => group.Key,
                group => (IReadOnlyList<TemplateFile>)
                    [.. group.OrderBy(file => file.Path, StringComparer.Ordinal)],
                StringComparer.Ordinal);

        ResourcePaths =
        [
            .. _assembly
                .GetManifestResourceNames()
                .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                .Select(name => name[ResourcePrefix.Length..])
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>A origem de produção.</summary>
    public static EmbeddedTemplateSource Default { get; } =
        new(typeof(EmbeddedTemplateSource).Assembly);

    /// <summary>
    /// Todo caminho de template embutido, relativo a <c>Templates/</c> e ordenado.
    /// </summary>
    /// <remarks>
    /// Existe para o teste que verifica que nenhum arquivo ficou fora dos eixos declarados. Um
    /// diretório com o nome errado seria, sem esse teste, apenas um fragmento que nunca entra em
    /// pacote nenhum — e isso não falha em lugar algum.
    /// </remarks>
    public IReadOnlyList<string> ResourcePaths { get; }

    /// <inheritdoc />
    public IReadOnlyList<TemplateFile> Read(string fragment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fragment);

        return _byFragment.TryGetValue(fragment, out IReadOnlyList<TemplateFile>? files)
            ? files
            : [];
    }

    private IEnumerable<TemplateFile> ReadAll()
    {
        foreach (string resource in _assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string relative = resource[ResourcePrefix.Length..];
            string? fragment = TemplateAxes.FragmentOf(relative);

            if (fragment is null)
            {
                // Arquivo fora de qualquer eixo declarado. Ignorar aqui e falhar no teste é
                // proposital: o motor não decide o que é template válido, o teste decide.
                continue;
            }

            yield return new TemplateFile(
                fragment,
                relative[(fragment.Length + 1)..],
                ReadText(resource));
        }
    }

    private string ReadText(string resource)
    {
        using Stream stream = _assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"O recurso embutido '{resource}' foi listado mas não pôde ser aberto.");

        // `detectEncodingFromByteOrderMarks: true` derruba o BOM se algum template tiver sido
        // salvo com ele; a normalização de TextContent cuida do resto (ADR-0003, item 4).
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }
}
