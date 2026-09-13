namespace TemplateGenerator.Generation.Catalog;

/// <summary>
/// Um par <c>(campo, valor)</c> do catálogo que <strong>não gera projeto</strong>, e a razão.
/// É um item do membro de topo <c>unavailable</c> de <c>GET /api/template-options</c>
/// (ADR-0012).
/// </summary>
/// <remarks>
/// <para>
/// É <strong>dado</strong>, como as restrições já são: a tela desabilita o que vier aqui e mostra
/// a razão, casando <see cref="Field"/> e <see cref="Value"/> contra o que o próprio catálogo lhe
/// entregou. Nenhum valor de opção é codificado do outro lado (RF-02 continua literal).
/// </para>
/// <para>
/// <strong>Por que fora de <c>values</c></strong>, que seria o lugar óbvio: o eixo booleano não
/// <em>tem</em> <c>values</c>. Se a disponibilidade morasse dentro de cada valor, <c>swagger</c>
/// ficaria sem onde dizê-la, e no dia em que <c>swagger/enabled</c> esvaziasse não haveria como
/// desabilitar o interruptor. Um formato que não consegue exprimir um dos campos está errado no
/// desenho, não no caso raro.
/// </para>
/// <para>
/// <strong>Por que só os indisponíveis</strong>, e não um <c>available</c> por valor: o estado
/// final do produto é <c>"unavailable": []</c> — uma linha que se lê como "está tudo
/// implementado" — em vez de um <c>true</c> repetido em nove lugares para sempre.
/// </para>
/// </remarks>
/// <param name="Field">A chave do campo, como aparece em <c>fields</c>.</param>
/// <param name="Value">
/// O valor, <strong>no tipo do campo</strong>: texto no campo de escolha, booleano no interruptor.
/// </param>
/// <param name="Reason">
/// A razão, em português. É uma frase só, igual para todas e derivada — ver
/// <c>TemplateAvailability.UnavailableReason</c>.
/// </param>
public sealed record UnavailableOption(string Field, CatalogValue Value, string Reason);
