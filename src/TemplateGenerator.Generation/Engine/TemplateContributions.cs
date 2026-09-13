using System.Collections.Frozen;

namespace TemplateGenerator.Generation.Engine;

/// <summary>
/// Os marcadores de <strong>contribuição</strong> (ADR-0011): texto que um eixo entrega a um
/// arquivo de outro eixo, sem que nenhum caminho do ZIP passe a ter dois donos.
/// </summary>
/// <remarks>
/// <para>
/// O problema que isto resolve: o <c>.csproj</c> precisa variar por <c>swagger</c>, por
/// <c>database</c> e por <c>authentication</c> ao mesmo tempo, e a união de arquivos não tem
/// resposta para isso — um arquivo, um dono. A decisão foi dar o arquivo à arquitetura e deixar os
/// outros eixos contribuírem <em>texto</em>, de modo que o <c>.csproj</c> gerado continue literal e
/// a camada 1 continue conseguindo afirmar RF-20 e RNF-06 lendo um XML.
/// </para>
/// <para>
/// Um arquivo em <c>__parts__/&lt;Nome&gt;.&lt;ext&gt;</c>, na raiz do fragmento, não vira entrada
/// do ZIP: o conteúdo dele passa a ser uma das contribuições do marcador <c>__&lt;Nome&gt;__</c>.
/// </para>
/// <para>
/// <strong>Texto inerte</strong> (ADR-0011, item 9): o motor não avalia, não ordena por conteúdo,
/// não desduplica e não interpreta. Sem condicional, sem laço, sem expressão. É esta a fronteira
/// que impede o mecanismo de virar uma linguagem de template por acréscimo, e mexer nela exige ADR
/// própria.
/// </para>
/// </remarks>
public sealed class TemplateContributions
{
    /// <summary>Diretório reservado, na raiz do fragmento.</summary>
    public const string Directory = "__parts__";

    private readonly FrozenSet<string> _markers;
    private readonly FrozenDictionary<string, IReadOnlyList<string>> _selected;

    private TemplateContributions(
        FrozenSet<string> markers,
        FrozenDictionary<string, IReadOnlyList<string>> selected)
    {
        _markers = markers;
        _selected = selected;
    }

    /// <summary>
    /// Todo marcador de contribuição que o repositório declara, em qualquer fragmento.
    /// </summary>
    public IReadOnlyCollection<string> Markers => _markers;

    /// <summary>Diz se <paramref name="marker"/> é um marcador de contribuição conhecido.</summary>
    public bool Declares(string marker) => _markers.Contains(marker);

    /// <summary>
    /// As contribuições selecionadas para <paramref name="marker"/>, na ordem de seleção dos
    /// fragmentos.
    /// </summary>
    public IReadOnlyList<string> Selected(string marker) =>
        _selected.TryGetValue(marker, out IReadOnlyList<string>? parts) ? parts : [];

    /// <summary>
    /// O valor de um marcador <strong>sozinho na linha, na coluna 0</strong>: as contribuições, uma
    /// por linha, com a quebra final — ou a string vazia (ADR-0011, item 7).
    /// </summary>
    /// <remarks>
    /// Devolver string vazia é o que faz a linha inteira sumir quando ninguém contribui, em vez de
    /// deixar um <c>&lt;ItemGroup&gt;</c> vazio ou uma linha em branco órfã. Quem consome esta
    /// propriedade é quem consumiu a quebra de linha do template.
    /// </remarks>
    public string Block(string marker)
    {
        IReadOnlyList<string> parts = Selected(marker);

        return parts.Count == 0 ? string.Empty : string.Join("\n", parts) + "\n";
    }

    /// <summary>
    /// O valor de um marcador em <strong>qualquer outra posição</strong> — no meio de uma linha ou
    /// no caminho do arquivo: as contribuições separadas por <c>\n</c>, sem quebra final
    /// (ADR-0011, item 8).
    /// </summary>
    public string Inline(string marker) => string.Join("\n", Selected(marker));

    /// <summary>
    /// Diz se o conteúdo de um arquivo de contribuição <strong>contribui alguma coisa</strong>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É o teste de "não vazio" do motor, exposto de um lugar só (ADR-0012, regra R1.1). Quem
    /// pergunta são dois: a composição, em <see cref="Resolve"/>, que descarta a contribuição vazia
    /// para não inserir linha em branco no arquivo gerado; e a derivação de disponibilidade, em
    /// <see cref="TemplateAvailability"/>, que precisa saber se o fragmento contribui.
    /// </para>
    /// <para>
    /// Duas noções de vazio dariam duas respostas para a mesma pergunta — "este fragmento
    /// contribui?" — e a divergência apareceria como uma opção marcada disponível que gera arquivo
    /// com buraco dentro. Por isso a definição é uma função, e não uma linha repetida.
    /// </para>
    /// </remarks>
    public static bool Contributes(string content) => Trimmed(content).Length != 0;

    /// <summary>
    /// A forma final do texto de uma contribuição: normalizado (ADR-0003, item 4) e sem as quebras
    /// finais.
    /// </summary>
    /// <remarks>
    /// A quebra final é do empacotamento, não da contribuição: quem insere decide se ela termina em
    /// nova linha (<see cref="Block"/>) ou se é colada em uma (<see cref="Inline"/>).
    /// </remarks>
    private static string Trimmed(string content) => TextContent.Normalize(content).TrimEnd('\n');

    /// <summary>Diz se algum segmento de <paramref name="path"/> é o diretório reservado.</summary>
    /// <remarks>
    /// Roda para todo arquivo de todo fragmento, em toda geração. O teste de substring é a peneira
    /// barata — a esmagadora maioria dos caminhos não menciona <c>__parts__</c> e sai daqui sem
    /// alocar nada; só quem passa por ela paga a divisão em segmentos, que é o que distingue o
    /// diretório reservado de um arquivo que por acaso se chame <c>__parts__.md</c>.
    /// </remarks>
    public static bool MentionsReservedDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!path.Contains(Directory, StringComparison.Ordinal))
        {
            return false;
        }

        return path
            .Split(ArchivePath.Separator)
            .Any(segment => segment.Equals(Directory, StringComparison.Ordinal));
    }

    /// <summary>
    /// Lê as contribuições do repositório inteiro e resolve as da combinação pedida.
    /// </summary>
    /// <param name="source">Origem dos fragmentos.</param>
    /// <param name="request">Configuração já validada.</param>
    /// <param name="values">Marcadores de valor, de <see cref="TemplateTokens.For"/>.</param>
    /// <remarks>
    /// <para>
    /// A varredura é sobre <strong>todos</strong> os fragmentos, não só os selecionados
    /// (ADR-0011, item 3). É isso que permite as duas coisas ao mesmo tempo: uma combinação em que
    /// ninguém contribui recebe string vazia, e um <c>__ApiPackgeReferences__</c> digitado errado
    /// continua sendo erro. Sem a varredura completa, as duas situações seriam indistinguíveis e a
    /// proteção contra nome errado se perderia.
    /// </para>
    /// <para>
    /// A consequência é deliberada: um <c>__parts__</c> defeituoso em <c>auth/jwt</c> derruba
    /// <em>toda</em> combinação, inclusive as que não selecionam <c>auth/jwt</c>. Um defeito de
    /// template precisa aparecer na camada 1 inteira, não só na combinação que o usa.
    /// </para>
    /// </remarks>
    public static TemplateContributions Resolve(
        ITemplateSource source,
        GenerationRequest request,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(values);

        // Fase 1 — o repositório inteiro: quais marcadores existem e onde.
        Dictionary<string, Dictionary<string, TemplateFile>> byFragment = new(StringComparer.Ordinal);
        HashSet<string> markers = new(StringComparer.Ordinal);

        foreach (string fragment in TemplateAxes.All)
        {
            Dictionary<string, TemplateFile> parts = new(StringComparer.Ordinal);

            foreach (TemplateFile file in source.Read(fragment))
            {
                if (!MentionsReservedDirectory(file.Path))
                {
                    continue;
                }

                string marker = MarkerOf(fragment, file.Path, values);

                if (parts.TryGetValue(marker, out TemplateFile? first))
                {
                    throw new TemplateDefectException(
                        $"O fragmento '{fragment}' declara duas vezes a contribuição " +
                        $"'{marker}': '{first.Path}' e '{file.Path}'. A ordem entre as duas seria " +
                        "indefinida, então no máximo um arquivo por marcador por fragmento " +
                        "(ADR-0011, item 2).");
                }

                parts[marker] = file;
                markers.Add(marker);
            }

            byFragment[fragment] = parts;
        }

        // Fase 2 — a combinação pedida, na ordem de seleção (ADR-0011, item 5).
        FrozenSet<string> known = markers.ToFrozenSet(StringComparer.Ordinal);
        Dictionary<string, List<string>> selected = new(StringComparer.Ordinal);

        foreach (string fragment in TemplateAxes.Select(request))
        {
            if (!byFragment.TryGetValue(fragment, out Dictionary<string, TemplateFile>? parts))
            {
                continue;
            }

            foreach (KeyValuePair<string, TemplateFile> entry in parts.OrderBy(
                entry => entry.Key,
                StringComparer.Ordinal))
            {
                string origin = $"{fragment}/{entry.Value.Path}";

                // Item 6: a contribuição passa pelos marcadores de VALOR antes de entrar — é o que
                // permite uma ProjectReference contribuída. Um marcador de contribuição aqui dentro
                // cai como desconhecido, e é assim que "não há aninhamento" se sustenta sem
                // nenhuma regra de recursão.
                string content = TemplateTokens.ApplyValues(
                    TextContent.Normalize(entry.Value.Content),
                    values,
                    known,
                    origin);

                content = Trimmed(content);

                if (content.Length == 0)
                {
                    // Arquivo de contribuição vazio contribui nada. A alternativa seria inserir uma
                    // linha em branco no arquivo gerado, que é exatamente o que o item 7 existe
                    // para evitar no caso de zero contribuições.
                    continue;
                }

                if (!selected.TryGetValue(entry.Key, out List<string>? accumulated))
                {
                    accumulated = [];
                    selected[entry.Key] = accumulated;
                }

                accumulated.Add(content);
            }
        }

        return new TemplateContributions(
            known,
            selected.ToFrozenDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<string>)entry.Value,
                StringComparer.Ordinal));
    }

    /// <summary>
    /// Valida o caminho de um arquivo de contribuição e devolve o marcador que ele alimenta.
    /// </summary>
    /// <exception cref="TemplateDefectException">
    /// Quando o caminho ou o nome não seguem ADR-0011, itens 1, 2 e 4.
    /// </exception>
    private static string MarkerOf(
        string fragment,
        string path,
        IReadOnlyDictionary<string, string> values)
    {
        string[] segments = path.Split(ArchivePath.Separator);

        // Item 1: exatamente dois segmentos, o primeiro sendo o diretório reservado. Sem isso,
        // 'docs/__parts__/x' seria ambíguo entre arquivo do pacote e contribuição.
        if (segments.Length != 2 || !segments[0].Equals(Directory, StringComparison.Ordinal))
        {
            throw new TemplateDefectException(
                $"O template '{fragment}/{path}' usa '{Directory}' fora do lugar. " +
                $"Contribuição é exatamente '{Directory}/<Nome>.<ext>', dois segmentos, na raiz " +
                "do fragmento; em qualquer outra posição o caminho seria ambíguo entre arquivo do " +
                "pacote e contribuição (ADR-0011, item 1).");
        }

        // A extensão existe para o editor colorir a sintaxe e é ignorada.
        string file = segments[1];
        int dot = file.IndexOf('.', StringComparison.Ordinal);
        string name = dot < 0 ? file : file[..dot];

        // Item 2: o nome vira o marcador, e por isso obedece à mesma forma dele.
        if (!TemplateTokens.IsValidName(name))
        {
            throw new TemplateDefectException(
                $"O template '{fragment}/{path}' não nomeia um marcador válido. " +
                $"'{name}' precisa casar [A-Za-z][A-Za-z0-9]*, porque é dele que sai " +
                "'__<Nome>__' (ADR-0011, item 2).");
        }

        string marker = $"{TemplateTokens.Delimiter}{name}{TemplateTokens.Delimiter}";

        // Item 4: os dois conjuntos são disjuntos. Um '__parts__/ProjectName.xml' deixaria o mesmo
        // marcador com duas fontes de valor, e qual delas venceria seria detalhe de implementação.
        if (values.ContainsKey(marker))
        {
            throw new TemplateDefectException(
                $"O template '{fragment}/{path}' declara a contribuição '{marker}', que já é um " +
                "marcador de valor. Os dois conjuntos são disjuntos (ADR-0011, item 4). " +
                $"Marcadores de valor: {string.Join(", ", values.Keys.Order(StringComparer.Ordinal))}.");
        }

        return marker;
    }
}
