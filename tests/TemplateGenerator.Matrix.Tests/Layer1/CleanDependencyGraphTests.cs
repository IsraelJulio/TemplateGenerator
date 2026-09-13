using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1, item "referências de projeto respeitam o diagrama"
/// (docs/quality/test-strategy.md) — o critério de aceite 4 de T04. A especificação é
/// docs/architecture/generated-projects.md, seção <em>"O conjunto de arestas, para o teste do
/// critério 4"</em>, e este arquivo a segue literalmente.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Igualdade de conjunto, e não "não contém".</strong> Um pacote que <em>perdesse</em> uma
/// referência legítima precisa falhar tanto quanto um que <em>ganhasse</em> uma indevida: sem a
/// aresta <c>Api → Infrastructure</c> o adaptador nunca é composto e a aplicação sobe sem
/// persistência — um defeito tão real quanto uma seta a mais, e invisível para qualquer
/// verificação que só procure o proibido.
/// </para>
/// <para><strong>As quatro permitidas, todas usadas:</strong></para>
/// <list type="bullet">
///   <item><description><c>Api → Application</c></description></item>
///   <item><description><c>Api → Infrastructure</c></description></item>
///   <item><description><c>Application → Domain</c></description></item>
///   <item><description><c>Infrastructure → Domain</c></description></item>
/// </list>
/// <para><strong>O que a igualdade proíbe, e por que cada uma importa:</strong></para>
/// <list type="bullet">
///   <item><description>
///   <c>Domain</c> → qualquer coisa. <c>Domain</c> tem <strong>zero</strong>
///   <c>ProjectReference</c>: é o critério de aceite 2 e a afirmação mais forte do conjunto.
///   </description></item>
///   <item><description>
///   <c>Application → Infrastructure</c> e <c>Application → Api</c> — o caso de uso não conhece
///   adaptador nem transporte.
///   </description></item>
///   <item><description>
///   <c>Infrastructure → Application</c> e <c>Infrastructure → Api</c> — o adaptador não chama
///   caso de uso. É a proibição que a decisão de pôr a porta em <c>Domain</c> comprou, e é o
///   motivo de ela ter sido escolhida: com a porta em <c>Application</c> esta aresta seria
///   <em>permitida</em>, e "o adaptador não chama o caso de uso" viraria convenção sem
///   verificador.
///   </description></item>
///   <item><description>
///   <strong><c>Api → Domain</c> declarada.</strong> Não está no diagrama, logo não pode ser
///   declarada — e <strong>isto não impede o <c>Api</c> de usar tipos do <c>Domain</c></strong>:
///   referência de projeto é transitiva por padrão no SDK moderno, e
///   <c>Api → Application → Domain</c> já entrega os tipos ao <c>Program.cs</c>. Declarar a aresta
///   não mudaria nada em compilação e faria o <c>.csproj</c> afirmar uma dependência que o
///   diagrama nega. Ela é proibida como <strong>declaração</strong>, não como uso; se algum
///   template um dia precisar dela declarada, isso é mudança de diagrama, não exceção de teste.
///   </description></item>
/// </list>
/// <para>
/// <strong>O projeto de testes fica fora do diagrama, de propósito.</strong> Ele não é camada:
/// pode referenciar qualquer projeto de produção, e a regra que vale é a <em>inversa</em> —
/// <strong>nada pode referenciar o projeto de testes</strong>. Afirmar <c>Tests → Application</c>
/// congelaria o acidente de hoje e quebraria no primeiro teste que precisasse de outro projeto.
/// Por isso as arestas <em>saindo</em> de <c>Tests</c> não entram na comparação, e uma aresta
/// <em>chegando</em> nele é violação em qualquer direção.
/// </para>
/// <para>
/// <strong>A comparação é por papel, não por nome de arquivo</strong> (<see cref="PackageLayout"/>):
/// a regra de nomes mora em generated-projects.md e não é reimplementada aqui.
/// </para>
/// <para>
/// <strong>Na arquitetura Simples não há diagrama a verificar</strong>: há um projeto de código e
/// um de testes. Por isso o recorte é <see cref="Combinations.Clean"/> — e ele é a interseção da
/// Clean com as combinações <em>disponíveis</em>, porque uma combinação recusada não produz
/// <c>.csproj</c> nenhum e "nenhuma aresta indevida" seria verdade por vacuidade.
/// </para>
/// </remarks>
public sealed class CleanDependencyGraphTests
{
    /// <summary>Exatamente as quatro arestas do diagrama, e nada mais.</summary>
    private static readonly (string From, string To)[] _allowedEdges =
    [
        (PackageLayout.ApiRole, PackageLayout.ApplicationRole),
        (PackageLayout.ApiRole, PackageLayout.InfrastructureRole),
        (PackageLayout.ApplicationRole, PackageLayout.DomainRole),
        (PackageLayout.InfrastructureRole, PackageLayout.DomainRole),
    ];

    /// <summary>Os quatro projetos que todo pacote da Clean traz, por papel.</summary>
    private static readonly string[] _cleanRoles =
    [
        PackageLayout.ApiRole,
        PackageLayout.ApplicationRole,
        PackageLayout.DomainRole,
        PackageLayout.InfrastructureRole,
    ];

    public static TheoryData<string, string, string, bool> CleanCombinations
    {
        get
        {
            TheoryData<string, string, string, bool> data = [];

            foreach (GenerationRequest request in Combinations.Clean)
            {
                data.Add(
                    request.Architecture,
                    request.Database,
                    request.Authentication,
                    request.Swagger);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CleanCombinations))]
    public async Task O_conjunto_de_arestas_e_exatamente_o_diagrama(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string description = GeneratedPackage.Describe(package.Request);

        AssertShapeOfCleanPackage(package, description);

        HashSet<(string From, string To)> actual = ProductionEdges(package);

        string[] forbidden =
        [
            .. actual.Except(_allowedEdges).Select(Describe).Order(StringComparer.Ordinal),
        ];

        string[] missing =
        [
            .. _allowedEdges.Except(actual).Select(Describe).Order(StringComparer.Ordinal),
        ];

        Assert.True(
            forbidden.Length == 0,
            $"{description}: referência arquitetural indevida — {string.Join(", ", forbidden)}. " +
            "O diagrama de generated-projects.md tem quatro arestas e só elas: " +
            $"{string.Join(", ", _allowedEdges.Select(Describe))}. Se 'Api → Domain' aparece " +
            "aqui, note que ela é proibida como DECLARAÇÃO e não como uso: os tipos de Domain " +
            "chegam ao Program.cs por transitividade, via Application.");

        Assert.True(
            missing.Length == 0,
            $"{description}: falta a aresta {string.Join(", ", missing)}. A comparação é por " +
            "IGUALDADE de conjunto: perder uma referência legítima é tão defeito quanto ganhar " +
            "uma indevida — sem 'Api → Infrastructure', por exemplo, o adaptador nunca é composto " +
            "e a aplicação sobe sem persistência.");
    }

    [Theory]
    [MemberData(nameof(CleanCombinations))]
    public async Task O_dominio_nao_referencia_ninguem(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério de aceite 2, afirmado sozinho e não só como consequência da igualdade acima:
        // é a regra que o `.csproj` do Domain existe para cumprir, e a mensagem de falha precisa
        // dizer isso com essas palavras para quem a encontrar.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string domain = Assert.Single(
            PackageLayout.SourceProjects(package),
            path => PackageLayout.RoleOf(path).Equals(
                PackageLayout.DomainRole,
                StringComparison.Ordinal));

        IReadOnlyList<string> references = PackageLayout.ProjectReferences(package, domain);

        Assert.True(
            references.Count == 0,
            $"{GeneratedPackage.Describe(package.Request)}: '{domain}' declara " +
            $"{string.Join(", ", references)}. O DOMÍNIO NÃO REFERENCIA NINGUÉM — nem projeto, " +
            "nem pacote de infraestrutura (generated-projects.md, 'Clean Architecture'; critério " +
            "de aceite 2 de T04).");
    }

    [Theory]
    [MemberData(nameof(CleanCombinations))]
    public async Task Nenhum_projeto_referencia_o_projeto_de_testes(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // A regra INVERSA, que é a única que vale para o projeto de testes. Ele fica fora do
        // diagrama: pode referenciar qualquer projeto de produção — e afirmar quais congelaria o
        // acidente de hoje —, mas nada pode referenciá-lo. Um projeto de produção que dependesse
        // do de testes arrastaria xUnit para dentro do pacote de quem recebe.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        IReadOnlyList<string> testProjects = PackageLayout.TestProjects(package);

        Assert.NotEmpty(testProjects);

        foreach (string project in PackageLayout.ProjectFiles(package))
        {
            string[] toTests =
            [
                .. PackageLayout.ProjectReferences(package, project)
                    .Where(reference => testProjects.Contains(reference, StringComparer.Ordinal)),
            ];

            Assert.True(
                toTests.Length == 0,
                $"{GeneratedPackage.Describe(package.Request)}: '{project}' referencia " +
                $"{string.Join(", ", toTests)}. Nada pode referenciar o projeto de testes " +
                "(generated-projects.md, 'O projeto de testes fica fora do diagrama').");
        }
    }

    [Fact]
    public async Task A_varredura_encontrou_pacote_de_clean_com_os_quatro_projetos_e_arestas()
    {
        // Assert de sanidade, obrigatório — e mais necessário agora que ADR-0012 faz a maioria das
        // combinações recusar: "nenhuma aresta indevida" é verdade por vacuidade num pacote que
        // não existe. Falha se não houver pacote de `clean`, se faltar um dos quatro `.csproj`, ou
        // se o conjunto de arestas lido da matriz inteira vier vazio.
        Assert.True(
            Combinations.Clean.Count > 0,
            "Nenhuma combinação de 'clean' está disponível. Enquanto isso for verdade, todo teste " +
            "desta classe passa sem abrir um único '.csproj' — o diagrama deixa de ser " +
            "verificado e nada fica vermelho.");

        int edges = 0;

        foreach (GenerationRequest request in Combinations.Clean)
        {
            GeneratedPackage package = await GeneratedPackage.GenerateAsync(
                request,
                TestContext.Current.CancellationToken);

            AssertShapeOfCleanPackage(package, GeneratedPackage.Describe(request));

            edges += ProductionEdges(package).Count;
        }

        Assert.True(
            edges > 0,
            "A matriz inteira de 'clean' não produziu uma única 'ProjectReference' entre projetos " +
            "de produção. Ou os '.csproj' pararam de declará-las, ou a leitura de arestas quebrou " +
            "— nos dois casos, a igualdade de conjunto passaria a comparar vazio com vazio.");
    }

    /// <summary>
    /// Todo pacote de Clean tem os quatro projetos de produção, um por papel, e um projeto de
    /// testes. É a pré-condição das três afirmações acima.
    /// </summary>
    private static void AssertShapeOfCleanPackage(GeneratedPackage package, string description)
    {
        string[] roles =
        [
            .. PackageLayout.SourceProjects(package)
                .Select(PackageLayout.RoleOf)
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            roles.SequenceEqual(_cleanRoles.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            $"{description}: os projetos sob 'src/' têm os papéis [{string.Join(", ", roles)}] e " +
            $"a Clean exige exatamente [{string.Join(", ", _cleanRoles)}], um de cada. O pacote " +
            $"traz: {string.Join(", ", PackageLayout.ProjectFiles(package))}");

        Assert.Single(PackageLayout.TestProjects(package));
    }

    /// <summary>
    /// As arestas declaradas <strong>entre projetos de produção</strong>, por papel. As que saem
    /// do projeto de testes ficam de fora: ele não é camada do diagrama.
    /// </summary>
    private static HashSet<(string From, string To)> ProductionEdges(GeneratedPackage package)
    {
        HashSet<(string From, string To)> edges = [];

        foreach (string project in PackageLayout.SourceProjects(package))
        {
            foreach (string reference in PackageLayout.ProjectReferences(package, project))
            {
                edges.Add((PackageLayout.RoleOf(project), PackageLayout.RoleOf(reference)));
            }
        }

        return edges;
    }

    private static string Describe((string From, string To) edge) => $"{edge.From} → {edge.To}";

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");
}
