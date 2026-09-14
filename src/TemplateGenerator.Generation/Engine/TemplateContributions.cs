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
/// <strong>Leitura para trás</strong> (ADR-0015, decisão 1): uma contribuição pode usar um marcador
/// de contribuição alimentado <em>exclusivamente</em> por eixos estritamente anteriores na ordem de
/// seleção — <c>common &lt; architecture &lt; database &lt; auth &lt; swagger</c>. Citar o próprio
/// eixo ou um eixo posterior é defeito de template. Como a resolução é uma passada por eixo, na
/// ordem, quando o eixo <em>n</em> é resolvido todos os anteriores já estão fechados: o mecanismo
/// <strong>termina por construção</strong>, sem ponto fixo, sem detecção de ciclo e sem
/// profundidade a limitar.
/// </para>
/// <para>
/// <strong>Texto inerte</strong> (ADR-0011, item 9): o motor não avalia, não ordena por conteúdo,
/// não desduplica e não interpreta. Sem condicional, sem laço, sem expressão. É esta a fronteira
/// que impede o mecanismo de virar uma linguagem de template por acréscimo, e mexer nela exige ADR
/// própria. ADR-0015 amplia <em>o que pode ser citado</em>, e não o que o motor faz com o que leu.
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

        return parts.Count == 0 ? string.Empty : Join(parts) + "\n";
    }

    /// <summary>
    /// O valor de um marcador em <strong>qualquer outra posição</strong> — no meio de uma linha ou
    /// no caminho do arquivo: as contribuições separadas por <c>\n</c>, sem quebra final
    /// (ADR-0011, item 8).
    /// </summary>
    public string Inline(string marker) => Join(Selected(marker));

    /// <summary>
    /// A forma do item 8: as contribuições separadas por <c>\n</c>, sem quebra final.
    /// </summary>
    /// <remarks>
    /// Mora aqui porque são dois que a usam: <see cref="Inline"/>, no arquivo hospedeiro, e a
    /// leitura para trás de ADR-0015, dentro de outra contribuição. O item 6 daquela ADR diz que as
    /// duas são a mesma forma — a regra da linha vale <strong>só</strong> no hospedeiro —, e uma
    /// segunda cópia desta linha seria exatamente por onde as duas passariam a divergir.
    /// </remarks>
    private static string Join(IReadOnlyList<string> parts) => string.Join("\n", parts);

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

        IReadOnlyList<string> order = AxisOrder(request);

        // Fase 1 — o repositório inteiro: quais marcadores existem, onde, e de qual EIXO.
        Dictionary<string, Dictionary<string, TemplateFile>> byFragment = new(StringComparer.Ordinal);
        HashSet<string> markers = new(StringComparer.Ordinal);

        // Por marcador, o eixo mais TARDIO que o alimenta. É esse que decide a legalidade de uma
        // citação: "alimentado exclusivamente por eixos estritamente anteriores" (ADR-0015,
        // decisão 1) é o mesmo que "o mais tardio dos alimentadores vem antes de quem cita". E é
        // esse também que a mensagem de defeito nomeia, porque é ele o culpado.
        Dictionary<string, int> fedBy = new(StringComparer.Ordinal);

        foreach (string fragment in TemplateAxes.All)
        {
            Dictionary<string, TemplateFile> parts = new(StringComparer.Ordinal);
            int axis = RankOf(fragment, order);

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

                // A apuração é sobre o repositório inteiro, e não sobre os fragmentos
                // selecionados (ADR-0015, especificação, item 1, que repete o item 3 de ADR-0011
                // pelo mesmo motivo). Um template é legal ou ilegal por si: se `auth/jwt` passar a
                // alimentar um marcador, toda citação a ele vinda de `database/*` vira defeito em
                // TODAS as combinações, inclusive nas que não selecionam `auth/jwt`.
                if (!fedBy.TryGetValue(marker, out int latest) || axis > latest)
                {
                    fedBy[marker] = axis;
                }
            }

            byFragment[fragment] = parts;
        }

        // Fase 2 — a combinação pedida, na ordem de seleção (ADR-0011, item 5), que desde ADR-0015
        // é também ordem de DEPENDÊNCIA. Uma passada por eixo: quando o eixo `n` é resolvido, todos
        // os anteriores já estão fechados. Não há ponto fixo, não há ciclo a detectar e não há
        // profundidade a limitar — "um nível" é o que a ordem total entrega de graça.
        FrozenSet<string> known = markers.ToFrozenSet(StringComparer.Ordinal);
        Dictionary<string, List<string>> selected = new(StringComparer.Ordinal);

        foreach (string fragment in TemplateAxes.Select(request))
        {
            if (!byFragment.TryGetValue(fragment, out Dictionary<string, TemplateFile>? parts))
            {
                continue;
            }

            // O que este eixo pode ler, apurado ANTES de ele contribuir qualquer coisa: só os
            // marcadores cujo alimentador mais tardio é estritamente anterior. Um marcador do
            // próprio eixo nunca entra aqui — nem o alimentado por um fragmento irmão que esta
            // combinação não selecionou (item 3), porque dois fragmentos do mesmo eixo não têm
            // ordem entre si.
            Scope scope = Scope.For(fragment, order, fedBy, selected, known);

            foreach (KeyValuePair<string, TemplateFile> entry in parts.OrderBy(
                entry => entry.Key,
                StringComparer.Ordinal))
            {
                string origin = $"{fragment}/{entry.Value.Path}";

                // A contribuição passa pelos marcadores de VALOR — é o que permite uma
                // ProjectReference contribuída — e pelos marcadores de contribuição já fechados,
                // que para este eixo se comportam exatamente como marcadores de valor.
                string content = TemplateTokens.ApplyValues(
                    TextContent.Normalize(entry.Value.Content),
                    values,
                    scope,
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

    /// <summary>
    /// A ordem dos <strong>eixos</strong>, que é a ordem de seleção de fragmentos sem os valores:
    /// <c>common, architecture, database, auth, swagger</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ordem <strong>não é redeclarada aqui</strong>. Ela é lida de
    /// <see cref="TemplateAxes.Select"/>, que é onde está declarada (ADR-0011, item 5) e onde entra
    /// no hash. Uma segunda lista escrita neste arquivo seria uma segunda fonte de verdade para
    /// algo que ADR-0015 acabou de transformar em ordem de dependência: as duas teriam de
    /// concordar para sempre, e a divergência apareceria como um defeito de template que não é
    /// defeito de template.
    /// </para>
    /// <para>
    /// O <c>Swagger = true</c> é <strong>sonda</strong>, não a requisição: o eixo booleano só
    /// aparece na seleção quando está ligado, e a legalidade de uma citação não pode depender da
    /// combinação pedida (especificação, item 1). Os valores dos demais campos não são olhados —
    /// <see cref="TemplateAxes.Select"/> mapeia campo para diretório e nunca interpreta o valor.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string> AxisOrder(GenerationRequest request)
    {
        List<string> order = [];

        foreach (string fragment in TemplateAxes.Select(request with { Swagger = true }))
        {
            string axis = AxisOf(fragment);

            if (!order.Contains(axis, StringComparer.Ordinal))
            {
                order.Add(axis);
            }
        }

        return order;
    }

    /// <summary>
    /// O eixo de um fragmento: <c>architecture/clean</c> ⇒ <c>architecture</c>, <c>common</c> ⇒
    /// <c>common</c>.
    /// </summary>
    private static string AxisOf(string fragment)
    {
        int separator = fragment.IndexOf(TemplateAxes.Separator, StringComparison.Ordinal);

        return separator < 0 ? fragment : fragment[..separator];
    }

    /// <summary>A posição do eixo de <paramref name="fragment"/> na ordem de seleção.</summary>
    /// <exception cref="InvalidOperationException">
    /// Quando o fragmento não pertence a nenhum eixo selecionável. É defeito de programação — os
    /// dois lados saem de <see cref="TemplateAxes"/> — e precisa aparecer alto, não virar um eixo
    /// com posição inventada.
    /// </exception>
    private static int RankOf(string fragment, IReadOnlyList<string> order)
    {
        string axis = AxisOf(fragment);

        for (int rank = 0; rank < order.Count; rank++)
        {
            if (string.Equals(order[rank], axis, StringComparison.Ordinal))
            {
                return rank;
            }
        }

        throw new InvalidOperationException(
            $"O fragmento '{fragment}' está em TemplateAxes.All mas o eixo '{axis}' dele não " +
            $"aparece em TemplateAxes.Select. Ordem conhecida: {string.Join(", ", order)}.");
    }

    /// <summary>
    /// O que uma contribuição de um eixo pode ler: os marcadores de contribuição já
    /// <strong>fechados</strong> — os alimentados exclusivamente por eixos estritamente anteriores
    /// — com o valor acumulado (ADR-0015, decisão 1).
    /// </summary>
    /// <remarks>
    /// Para quem escreve template a regra cabe numa frase: <em>a contribuição de um eixo anterior
    /// comporta-se, para um eixo posterior, exatamente como um marcador de valor.</em> Há uma ordem
    /// total; lê-se para trás, nunca para a frente e nunca de lado.
    /// </remarks>
    public sealed class Scope
    {
        private readonly IReadOnlyDictionary<string, string> _closed;
        private readonly IReadOnlyDictionary<string, int> _fedBy;
        private readonly IReadOnlySet<string> _markers;
        private readonly IReadOnlyList<string> _order;
        private readonly int _rank;

        private Scope(
            string axis,
            int rank,
            IReadOnlyList<string> order,
            IReadOnlyDictionary<string, int> fedBy,
            IReadOnlyDictionary<string, string> closed,
            IReadOnlySet<string> markers)
        {
            Axis = axis;
            _rank = rank;
            _order = order;
            _fedBy = fedBy;
            _closed = closed;
            _markers = markers;
        }

        /// <summary>O eixo de quem cita.</summary>
        public string Axis { get; }

        /// <summary>Todo marcador de contribuição que o repositório declara.</summary>
        public IReadOnlyCollection<string> Markers => _markers;

        /// <summary>Diz se <paramref name="marker"/> é um marcador de contribuição conhecido.</summary>
        public bool Declares(string marker) => _markers.Contains(marker);

        /// <summary>
        /// O valor de <paramref name="marker"/> para este eixo, quando ele já está fechado.
        /// </summary>
        public bool TryRead(string marker, out string value) => _closed.TryGetValue(marker, out value!);

        /// <summary>
        /// O defeito de citar um marcador que <strong>não</strong> vem de um eixo estritamente
        /// anterior.
        /// </summary>
        public TemplateDefectException NotBackwards(string marker, string origin)
        {
            string fed = _order[_fedBy[marker]];

            string relation = string.Equals(fed, Axis, StringComparison.Ordinal)
                ? $"'{fed}' não vem antes de '{Axis}': é o próprio eixo de quem cita, e dois " +
                  "fragmentos do mesmo eixo não têm ordem entre si — vale mesmo entre fragmentos " +
                  "que nunca são selecionados juntos"
                : $"'{fed}' não vem antes de '{Axis}': é '{Axis}' que vem antes de '{fed}', e a " +
                  "leitura vale só para trás";

            return new TemplateDefectException(
                $"A contribuição '{origin}', do eixo '{Axis}', usa o marcador de contribuição " +
                $"'{marker}', que é alimentado pelo eixo '{fed}'. Uma contribuição só pode usar " +
                "marcador alimentado exclusivamente por eixos estritamente anteriores na ordem de " +
                $"seleção ({string.Join(" < ", _order)}), e {relation} " +
                "(ADR-0015, decisão 1; substitui a regra 6 de ADR-0011).");
        }

        /// <summary>
        /// O escopo de <paramref name="fragment"/>, com o que os eixos anteriores acumularam até
        /// aqui.
        /// </summary>
        /// <remarks>
        /// O valor é congelado no momento da chamada, e é por isso que ela acontece <em>antes</em>
        /// de o fragmento contribuir: o que entra aqui é acumulado de eixo estritamente anterior,
        /// que a partir deste ponto não muda mais. Um marcador anterior que nenhum fragmento
        /// selecionado alimentou entra com a <strong>string vazia</strong> — a regra 3 de ADR-0011
        /// vale igual aqui dentro.
        /// </remarks>
        internal static Scope For(
            string fragment,
            IReadOnlyList<string> order,
            IReadOnlyDictionary<string, int> fedBy,
            IReadOnlyDictionary<string, List<string>> accumulated,
            IReadOnlySet<string> markers)
        {
            int rank = RankOf(fragment, order);
            Dictionary<string, string> closed = new(StringComparer.Ordinal);

            foreach (KeyValuePair<string, int> entry in fedBy)
            {
                if (entry.Value >= rank)
                {
                    continue;
                }

                // Item 6: aqui dentro vale a regra 8 — substituição literal, `\n` entre
                // contribuições. A regra 7 (marcador sozinho na coluna 0 consome a linha) vale só
                // no arquivo hospedeiro: o texto da contribuição já foi normalizado e aparado, e
                // uma segunda regra de linha ali só produziria surpresa.
                closed[entry.Key] = accumulated.TryGetValue(entry.Key, out List<string>? parts)
                    ? Join(parts)
                    : string.Empty;
            }

            return new Scope(AxisOf(fragment), rank, order, fedBy, closed, markers);
        }
    }
}
