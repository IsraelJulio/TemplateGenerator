using System.Collections.Frozen;
using TemplateGenerator.Generation.Catalog;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Quais valores do catálogo têm template — <strong>derivado dos fragmentos</strong>, nunca
/// escrito à mão (ADR-0012, decisão 2).
/// </summary>
/// <remarks>
/// <para>
/// Este tipo não recebe lista nenhuma, não tem constante de valor e não conhece <c>simple</c>,
/// <c>clean</c>, <c>sqlite</c> nem qualquer outro. As duas únicas fontes são o <strong>catálogo</strong>
/// (quais valores existem) e a <strong>origem</strong> (quais fragmentos têm conteúdo). É isso que
/// o torna diferente de um comentário: quando alguém escrever <c>architecture/clean/...</c>, a
/// Clean acende sozinha — nenhuma edição de catálogo, nenhuma linha de C#, nenhuma chance de
/// alguém esquecer.
/// </para>
/// <para><strong>As regras</strong>, e cada uma existe porque a ausência dela quebra algo:</para>
/// <list type="bullet">
///   <item>
///     <description>
///     <strong>R1 — contribuir arquivo.</strong> Um valor está disponível se, e somente se, o
///     fragmento dele tiver pelo menos um arquivo que seja <em>(a)</em> entrada de ZIP — caminho
///     fora de <c>__parts__/</c> — <em>ou (b)</em> uma contribuição <c>__parts__/</c> de conteúdo
///     não vazio. A alínea (b) não é enfeite: <c>database/none</c>, <c>auth/none</c> e
///     <c>swagger/enabled</c> <strong>só</strong> têm <c>__parts__/</c>, e sem ela a derivação
///     declararia indisponível tudo que hoje funciona.
///     </description>
///   </item>
///   <item>
///     <description>
///     <strong>R1.1 — "não vazio" é o mesmo do motor.</strong> O teste é
///     <see cref="TemplateContributions.Contributes(string)"/>, exposto de um lugar só. Duas
///     noções de vazio seriam duas respostas para "este fragmento contribui?".
///     </description>
///   </item>
///   <item>
///     <description>
///     <strong>R1.2 — <c>.gitkeep</c> não conta</strong>, e isso já é verdade sem código aqui: o
///     <c>.csproj</c> o exclui do <c>EmbeddedResource</c>, então um fragmento que só tenha
///     <c>.gitkeep</c> chega com <strong>zero</strong> arquivos. Não há segunda exclusão.
///     </description>
///   </item>
///   <item>
///     <description>
///     <strong>R2 — valor sem fragmento está sempre disponível.</strong> Ver
///     <see cref="TemplateAxes.FragmentFor"/>: quando a escolha não implica fragmento nenhum, não
///     há ausência que se possa confundir com template incompleto.
///     </description>
///   </item>
///   <item>
///     <description>
///     <strong>R3 — combinação.</strong> Uma combinação está disponível quando todos os valores
///     que ela seleciona estão disponíveis — <see cref="UnavailableFields"/>. <c>common</c> não é
///     valor de campo e não entra nesta conta.
///     </description>
///   </item>
/// </list>
/// <para>
/// <strong>Uma instância, calculada uma vez</strong>: templates são recursos embutidos e imutáveis
/// e o gerador é <em>stateless</em>. <see cref="Current"/> é a de produção, registrada como
/// singleton; o construtor que aceita uma origem arbitrária existe para o teste compor um
/// repositório mínimo, como <see cref="GenerationEngine"/> já faz.
/// </para>
/// </remarks>
public sealed class TemplateAvailability
{
    /// <summary>
    /// A razão, única e derivada, de um valor não estar disponível.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uma frase só, igual para todas. Um motivo específico por opção — "A Clean Architecture entra
    /// em uma etapa seguinte" — teria de ser escrito à mão em algum lugar, que é exatamente a
    /// segunda fonte de verdade que ADR-0012 existe para não criar, e ficaria órfão no dia em que o
    /// valor acendesse. A especificidade já está na tela <strong>pela posição</strong>: a frase
    /// aparece colada ao rótulo da opção, que o catálogo já manda.
    /// </para>
    /// <para>
    /// É a mesma frase que o <c>501</c> devolve em <c>errors</c>: a pessoa lê as mesmas palavras
    /// tendo aprendido pela tela ou pela recusa.
    /// </para>
    /// </remarks>
    public const string UnavailableReason = "O template desta opção ainda não foi escrito.";

    private readonly IReadOnlyList<string> _fields;
    private readonly FrozenSet<(string Field, CatalogValue Value)> _unavailable;

    /// <summary>
    /// Deriva a disponibilidade de <paramref name="catalog"/> sobre <paramref name="source"/>.
    /// </summary>
    public TemplateAvailability(TemplateOptionsCatalog catalog, ITemplateSource source)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(source);

        List<UnavailableOption> unavailable = [];

        // A ordem é a do catálogo — dos campos, e dentro de cada campo a dos valores. Mesma
        // disciplina da regra 1 de serialização do contrato: estável e declarada.
        foreach (KeyValuePair<string, TemplateField> entry in catalog.Fields)
        {
            foreach (CatalogValue value in ValuesOf(entry.Value))
            {
                if (!HasTemplate(source, TemplateAxes.FragmentFor(entry.Key, value)))
                {
                    unavailable.Add(new UnavailableOption(entry.Key, value, UnavailableReason));
                }
            }
        }

        _fields = [.. catalog.Fields.Keys];
        _unavailable = unavailable
            .Select(option => (option.Field, option.Value))
            .ToFrozenSet();

        Unavailable = unavailable;
    }

    private TemplateAvailability()
    {
        _fields = [];
        _unavailable = FrozenSet<(string, CatalogValue)>.Empty;
        Unavailable = [];
    }

    /// <summary>A disponibilidade de produção: o catálogo vigente sobre os templates embutidos.</summary>
    public static TemplateAvailability Current { get; } =
        new(TemplateCatalog.Current, EmbeddedTemplateSource.Default);

    /// <summary>
    /// Nada indisponível.
    /// </summary>
    /// <remarks>
    /// Existe para o teste cujo assunto é o <strong>mecanismo</strong> — composição, marcador,
    /// caminho, conflito —, que monta um repositório mínimo e não deve ser recusado por ele ser
    /// mínimo. Produção usa <see cref="Current"/>, e é ela que <see cref="GenerationEngine"/>
    /// constrói sozinho: não há caminho em que a Api chegue aqui.
    /// </remarks>
    public static TemplateAvailability Unrestricted { get; } = new();

    /// <summary>
    /// Os pares <c>(campo, valor)</c> indisponíveis, na ordem do catálogo. Lista vazia significa
    /// "está tudo implementado".
    /// </summary>
    public IReadOnlyList<UnavailableOption> Unavailable { get; }

    /// <summary>
    /// Diz se o valor <paramref name="value"/> do campo <paramref name="field"/> gera projeto.
    /// </summary>
    /// <remarks>
    /// Um par que o catálogo não declara responde <c>true</c>, e isso é deliberado: quem não
    /// pertence ao catálogo é assunto da <strong>validação</strong>, que responde <c>400</c> e roda
    /// antes. Dizer "o template desta opção ainda não foi escrito" para um valor que não existe
    /// inverteria a culpa (ADR-0012, item 4, razão 3).
    /// </remarks>
    public bool IsAvailable(string field, CatalogValue value) =>
        !_unavailable.Contains((field, value));

    /// <summary>
    /// R3 — os campos de <paramref name="request"/> cujo valor escolhido está indisponível, na
    /// ordem dos campos no catálogo. Lista vazia significa combinação disponível.
    /// </summary>
    public IReadOnlyList<string> UnavailableFields(GenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> fields = [];

        foreach (string field in _fields)
        {
            CatalogValue? value = request.ValueOf(field);

            if (value is not null && !IsAvailable(field, value.Value))
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    /// <summary>
    /// Os valores possíveis de um campo, no tipo dele.
    /// </summary>
    /// <remarks>
    /// O interruptor é enumerado nas duas posições, ligado primeiro: é a única que pode ter
    /// fragmento, e a desligada cai sempre em R2. Enumerar as duas — em vez de só a ligada — é o
    /// que faz o teste de R2 ter o que afirmar.
    /// </remarks>
    private static IEnumerable<CatalogValue> ValuesOf(TemplateField field)
    {
        if (field.IsToggle)
        {
            return [CatalogValue.OfFlag(true), CatalogValue.OfFlag(false)];
        }

        return field.Values!.Select(option => CatalogValue.OfText(option.Value));
    }

    /// <summary>R1 e R2: o fragmento contribui alguma coisa?</summary>
    private static bool HasTemplate(ITemplateSource source, string? fragment)
    {
        // R2: sem fragmento não há ausência que se possa confundir com template incompleto.
        if (fragment is null)
        {
            return true;
        }

        return source.Read(fragment).Any(Contributes);
    }

    /// <summary>R1, alíneas (a) e (b).</summary>
    private static bool Contributes(TemplateFile file) =>
        // (a) entrada de ZIP: um arquivo fora de `__parts__/` já é conteúdo do pacote.
        !TemplateContributions.MentionsReservedDirectory(file.Path)

        // (b) contribuição de conteúdo não vazio, pelo mesmo teste que o motor usa (R1.1).
        || TemplateContributions.Contributes(file.Content);
}
