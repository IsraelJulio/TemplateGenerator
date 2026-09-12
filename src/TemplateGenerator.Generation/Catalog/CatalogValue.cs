using System.Text.Json.Serialization;

namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Um valor escalar do catálogo: texto ou booleano.
/// </summary>
/// <remarks>
/// <para>
/// São os dois únicos tipos que docs/architecture/http-contract.md admite em <c>default</c> e
/// nos lados <c>when</c>/<c>requires</c> de uma restrição. Modelar isso como <c>object</c>
/// resolveria a serialização, mas deixaria a validação comparando caixas sem tipo; este tipo
/// mantém a comparação por valor e falha cedo quando alguém tenta guardar outra coisa.
/// </para>
/// <para>
/// Não há operador implícito de propósito: a conversão é explícita para que o texto
/// <c>"true"</c> nunca seja confundido com o booleano <c>true</c>.
/// </para>
/// </remarks>
[JsonConverter(typeof(CatalogValueJsonConverter))]
public readonly record struct CatalogValue
{
    private readonly string? _text;
    private readonly bool _flag;

    private CatalogValue(string? text, bool flag)
    {
        _text = text;
        _flag = flag;
    }

    /// <summary>Verdadeiro quando o valor é textual.</summary>
    public bool IsText => _text is not null;

    /// <summary>O texto, ou <c>null</c> quando o valor é booleano.</summary>
    public string? Text => _text;

    /// <summary>O booleano. Só tem significado quando <see cref="IsText"/> é falso.</summary>
    public bool Flag => _flag;

    /// <summary>Cria um valor textual.</summary>
    public static CatalogValue OfText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new CatalogValue(value, false);
    }

    /// <summary>Cria um valor booleano.</summary>
    public static CatalogValue OfFlag(bool value) => new(null, value);

    /// <summary>Representação legível, usada em mensagem de erro.</summary>
    public override string ToString() => _text ?? (_flag ? "true" : "false");
}
