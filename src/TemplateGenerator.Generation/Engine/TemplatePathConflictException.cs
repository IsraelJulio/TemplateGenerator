namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Dois fragmentos escrevem o mesmo caminho no pacote.
/// </summary>
/// <remarks>
/// docs/architecture/generation-engine.md é explícito: conflito de caminho é <strong>erro</strong>,
/// não resolução silenciosa por precedência. Se <c>architecture/simple</c> e <c>auth/jwt</c>
/// escrevem os dois <c>Program.cs</c>, não existe resposta certa — a que "vence" depende da ordem
/// da composição, e a combinação passaria a gerar um projeto que ninguém revisou. O motor recusa,
/// e o tipo carrega os dois fragmentos para que a mensagem diga onde mexer.
/// </remarks>
public sealed class TemplatePathConflictException : TemplateDefectException
{
    /// <summary>Cria a exceção.</summary>
    /// <param name="path">Caminho disputado, já com os tokens substituídos.</param>
    /// <param name="firstFragment">Fragmento que reivindicou o caminho primeiro.</param>
    /// <param name="secondFragment">Fragmento que reivindicou o mesmo caminho depois.</param>
    public TemplatePathConflictException(string path, string firstFragment, string secondFragment)
        : base(
            $"Os fragmentos '{firstFragment}' e '{secondFragment}' escrevem o mesmo arquivo " +
            $"'{path}'. Conflito de caminho é defeito de template, não precedência: mova o " +
            "arquivo para um único fragmento ou quebre a parte comum em 'common' " +
            "(docs/architecture/generation-engine.md, seção \"Composição\").")
    {
        Path = path;
        FirstFragment = firstFragment;
        SecondFragment = secondFragment;
    }

    /// <summary>Caminho disputado.</summary>
    public string Path { get; }

    /// <summary>Fragmento que reivindicou o caminho primeiro.</summary>
    public string FirstFragment { get; }

    /// <summary>Fragmento que reivindicou o mesmo caminho depois.</summary>
    public string SecondFragment { get; }
}
