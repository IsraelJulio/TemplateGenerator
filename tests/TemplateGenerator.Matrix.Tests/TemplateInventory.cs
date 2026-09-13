using TemplateGenerator.Generation.Engine;

namespace TemplateGenerator.Matrix.Tests;

/// <summary>Um arquivo de template que hospeda pelo menos um marcador de contribuição.</summary>
/// <param name="Path">Caminho relativo à raiz dos fragmentos, com <c>/</c>.</param>
/// <param name="Content">O texto do template, como está no disco.</param>
internal sealed record TemplateHost(string Path, string Content);

/// <summary>
/// O que os fragmentos do repositório declaram em matéria de contribuição (ADR-0011): quais
/// marcadores existem e quais arquivos os hospedam.
/// </summary>
/// <remarks>
/// <para>
/// Lê o <strong>disco</strong>, e não o recurso embutido, por um motivo: o que a regra 7b regula é
/// o texto que uma pessoa escreve no template. Uma guarda sobre o pacote gerado só vê o sintoma
/// depois do fato, e só nos formatos que ela sabe interpretar.
/// </para>
/// <para>
/// A derivação é a mesma de <see cref="TemplateContributions"/> — o nome do arquivo em
/// <c>__parts__/</c> vira o marcador — mas aqui ela é feita por caminho de arquivo, sem gerar
/// pacote nenhum. Não é uma segunda implementação da regra: é a mesma pergunta feita ao disco em
/// vez de ao motor, e <c>TemplateContributionsTests</c> continua sendo quem prova o motor.
/// </para>
/// </remarks>
internal sealed class TemplateInventory
{
    private TemplateInventory(IReadOnlyList<string> markers, IReadOnlyList<TemplateHost> hosts)
    {
        Markers = markers;
        Hosts = hosts;
    }

    /// <summary>Todo marcador de contribuição declarado, como <c>__Nome__</c>, em ordem ordinal.</summary>
    public IReadOnlyList<string> Markers { get; }

    /// <summary>Os arquivos de template que citam algum marcador, em ordem ordinal.</summary>
    public IReadOnlyList<TemplateHost> Hosts { get; }

    /// <summary>Varre os fragmentos no disco.</summary>
    public static TemplateInventory Read()
    {
        string root = RepositoryLayout.TemplatesDirectory;

        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException(
                $"O diretório de fragmentos '{root}' não existe. Sem ele, toda guarda de template " +
                "desta camada passaria sem varrer nada.");
        }

        List<string> markers = [];
        List<string> candidates = [];

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (TemplateContributions.MentionsReservedDirectory(relative))
            {
                // `__parts__/<Nome>.<ext>` — a extensão existe só para o editor colorir a sintaxe
                // e é ignorada (ADR-0011, item 1).
                markers.Add($"__{Path.GetFileNameWithoutExtension(relative)}__");
            }
            else
            {
                candidates.Add(relative);
            }
        }

        markers = [.. markers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        List<TemplateHost> hosts = [];

        foreach (string relative in candidates.Order(StringComparer.Ordinal))
        {
            string content = File.ReadAllText(Path.Combine(root, relative));

            if (markers.Any(marker => content.Contains(marker, StringComparison.Ordinal)))
            {
                hosts.Add(new TemplateHost(relative, content));
            }
        }

        return new TemplateInventory(markers, hosts);
    }
}
