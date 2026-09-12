namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Defeito no conjunto de templates: caminho inválido, marcador inexistente, colisão de arquivo.
/// </summary>
/// <remarks>
/// <para>
/// É defeito de <em>servidor</em>, não entrada inválida. Entrada inválida vira
/// <c>ProblemDetails</c> 400 antes de o motor ser chamado
/// (<see cref="Validation.GenerationRequestValidator"/>); isto aqui só acontece quando o
/// repositório de templates está errado, e o lugar de descobrir isso é o teste da camada 1, não
/// a requisição de alguém.
/// </para>
/// <para>
/// Toda verificação que lança este tipo roda <strong>antes</strong> de o primeiro byte do ZIP ser
/// escrito (RNF-03): o plano de geração é montado inteiro na memória e só então empacotado.
/// </para>
/// </remarks>
public class TemplateDefectException : InvalidOperationException
{
    /// <summary>Cria a exceção com uma mensagem.</summary>
    public TemplateDefectException(string message)
        : base(message)
    {
    }

    /// <summary>Cria a exceção com uma mensagem e uma causa.</summary>
    public TemplateDefectException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
