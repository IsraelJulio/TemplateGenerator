using System.ComponentModel.DataAnnotations;

namespace TemplateGenerator.Api.Generation;

/// <summary>
/// Os dois limites exigidos por RNF-04, configuráveis sem recompilar.
/// </summary>
/// <remarks>
/// <para>
/// Ficam na seção <c>Generation:Limits</c> da configuração. Os valores default estão escritos
/// aqui, e não só no <c>appsettings.json</c>: uma instalação que apague a seção continua com
/// limite, em vez de ficar sem nenhum.
/// </para>
/// <para>
/// São dois limites diferentes, de propósito. <see cref="MaxConcurrentGenerations"/> protege a
/// máquina — quantas gerações cabem ao mesmo tempo, independentemente de quem pediu.
/// <see cref="RequestsPerWindow"/> protege contra uma origem sozinha ocupar a fila inteira.
/// Um sem o outro deixa um dos dois buracos aberto.
/// </para>
/// </remarks>
public sealed class GenerationLimits
{
    /// <summary>Caminho da seção de configuração.</summary>
    public const string SectionPath = "Generation:Limits";

    /// <summary>Quantas gerações podem estar em curso ao mesmo tempo, no processo inteiro.</summary>
    /// <remarks>
    /// Sem fila: o excedente é recusado na hora com <c>429</c>. Enfileirar faria o cliente esperar
    /// sem saber por quê, e um download que demora é indistinguível de um servidor travado.
    /// </remarks>
    [Range(1, 1024)]
    public int MaxConcurrentGenerations { get; set; } = 4;

    /// <summary>Requisições de geração aceitas por origem dentro de uma janela.</summary>
    [Range(1, 100_000)]
    public int RequestsPerWindow { get; set; } = 30;

    /// <summary>Tamanho da janela, em segundos.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Valor de <c>Retry-After</c> quando o limitador não sabe dizer quanto falta — o caso do
    /// limite de concorrência, que depende de outra requisição terminar.
    /// </summary>
    [Range(1, 3600)]
    public int RetryAfterSeconds { get; set; } = 5;

    /// <summary>A janela como <see cref="TimeSpan"/>.</summary>
    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    /// <summary>O <c>Retry-After</c> default como <see cref="TimeSpan"/>.</summary>
    public TimeSpan RetryAfter => TimeSpan.FromSeconds(RetryAfterSeconds);
}
