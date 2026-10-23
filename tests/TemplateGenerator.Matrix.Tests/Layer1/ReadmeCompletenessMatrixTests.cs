using System.Text.RegularExpressions;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 do README como <strong>documento completo</strong>: cada combinação disponível cobre os
/// sete assuntos que RF-21 e o critério 3 de T08 enumeram — pré-requisitos, instalação,
/// configuração, migrações, execução, autenticação e teste do CRUD —, na ordem em que uma pessoa
/// precisa deles.
/// </summary>
/// <remarks>
/// <para>
/// Até T08 o README era cobrado por pedaços, cada um pelo eixo que o escreveu: o passo de banco em
/// T05, o de Identity em T06, o de JWT em T07. Nada afirmava o documento <em>inteiro</em>, e uma
/// seção que sumisse de uma combinação que nenhum desses testes olhasse passaria sem ruído. Esta
/// classe é essa afirmação, nas 32 combinações disponíveis.
/// </para>
/// <para>
/// <strong>A ordem é parte da prova.</strong> A camada 3 executa os comandos na ordem escrita
/// (generated-projects.md, "README gerado"), e o README com "Executar" antes de "Banco de dados"
/// mandaria subir a aplicação sem migração. Os títulos são, portanto, conferidos como sequência, e
/// não como conjunto.
/// </para>
/// <para>
/// <strong>O que esta classe não afirma:</strong> que os comandos <em>funcionam</em>. Isso é a
/// camada 3, e a execução manual do papel <c>qa</c> registrada no relatório de T08.
/// </para>
/// </remarks>
public sealed partial class ReadmeCompletenessMatrixTests
{
    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    /// <summary>
    /// Os títulos de nível 2 que todo README gerado tem, na ordem. "Banco de dados" é a seção de
    /// configuração e migração — ou a que diz, em <c>database = none</c>, que não há nenhuma das
    /// duas; "Autenticação" idem para <c>authentication = none</c>. Uma combinação sem algo a
    /// configurar diz isso com essas palavras, em vez de omitir a seção.
    /// </summary>
    private static readonly string[] _sections =
    [
        "## Pré-requisitos",
        "## Estrutura",
        "## Instalação",
        "## Banco de dados",
        "## Autenticação",
        "## Executar",
        "## Verificar a saúde",
        "## Testar o CRUD de `Item`",
        "## Testes automatizados",
        "## Próximos passos",
    ];

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_README_cobre_os_sete_assuntos_de_RF_21_na_ordem_de_execucao(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");
        string combination = $"{architecture}/{database}/{authentication}/swagger={swagger}";

        string[] headings = [.. Heading().Matches(readme).Select(match => match.Value.TrimEnd())];

        // Com autenticação, "Usar a autenticação" entra depois de "Verificar a saúde": é onde ficam
        // as chamadas HTTP de cadastro, login e 401, que precisam da aplicação no ar. O Swagger
        // entra entre "Testes automatizados" e "Próximos passos", e só quando marcado.
        List<string> expected = [.. _sections];

        if (authentication != "none")
        {
            expected.Insert(expected.IndexOf("## Testar o CRUD de `Item`"), "## Usar a autenticação");
        }

        if (swagger)
        {
            expected.Insert(expected.IndexOf("## Próximos passos"), "## Swagger");
        }

        Assert.True(
            expected.SequenceEqual(headings, StringComparer.Ordinal),
            $"{combination}: os títulos do README não são os esperados, na ordem esperada." +
            $"{Environment.NewLine}Esperado: {string.Join(" | ", expected)}" +
            $"{Environment.NewLine}Obtido:   {string.Join(" | ", headings)}");
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Nenhuma_chamada_a_aplicacao_aparece_antes_de_Executar(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // O defeito que esta guarda fecha, achado pelo papel `qa` em T08 ao executar os READMEs do
        // zero: em identity e jwt, os `curl` de cadastro, login e 401 ficavam em "## Autenticação",
        // ANTES de "## Executar". Executados na ordem escrita — que é como a camada 3 e uma pessoa
        // os executam — davam `curl: (7) Failed to connect`. O texto avisava "suba-a primeiro",
        // mas um README que precisa de aviso para ser lido fora de ordem está na ordem errada
        // (generated-projects.md, "README gerado"). A regra, dita como teste: o endereço da
        // aplicação não aparece antes do passo que a sobe.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");

        int run = readme.IndexOf("## Executar", StringComparison.Ordinal);
        int firstCall = readme.IndexOf("localhost:5100", StringComparison.Ordinal);

        Assert.True(run >= 0, "O README não tem a seção '## Executar'.");
        Assert.True(
            firstCall > run,
            $"{architecture}/{database}/{authentication}/swagger={swagger}: 'localhost:5100' aparece " +
            $"antes de '## Executar' — na linha {readme[..Math.Max(firstCall, 0)].Count(c => c == '\n') + 1}. " +
            "Um comando que chama a aplicação antes do passo que a sobe falha quando o README é " +
            "executado na ordem escrita.");
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_README_manda_migrar_exatamente_os_DbContext_que_o_pacote_tem(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // "Migrações" de RF-21, contado: zero comandos sem banco, um com banco, dois com Identity
        // (AppDbContext e AppIdentityDbContext, ADR de T06). Um a menos é uma tabela que não
        // existe na primeira chamada; um a mais é um comando que falha copiado do README.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");

        string[] contexts = [.. EfDatabaseUpdate().Matches(readme).Select(match => match.Groups["context"].Value)];

        string[] expected = database switch
        {
            "none" => [],
            _ when authentication == "identity" => ["AppDbContext", "AppIdentityDbContext"],
            _ => ["AppDbContext"],
        };

        Assert.Equal(expected, contexts);

        // `dotnet tool restore` vem antes do primeiro `dotnet ef`, e só existe com banco — o
        // manifesto de ferramenta só existe com banco (generated-projects.md).
        int toolRestore = readme.IndexOf("dotnet tool restore", StringComparison.Ordinal);

        if (expected.Length == 0)
        {
            Assert.Equal(-1, toolRestore);
        }
        else
        {
            Assert.InRange(
                toolRestore,
                0,
                readme.IndexOf("dotnet ef database update", StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task A_secao_Swagger_diz_o_que_a_interface_faz_com_a_autenticacao_escolhida(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // T08 abriu a subseção para dizer que a interface não tinha botão *Authorize*; T08b
        // declarou o esquema bearer no documento e a subseção passou a ensinar o botão. Numa
        // combinação autenticada, o *Try it out* do CRUD responde 401 até a pessoa autorizar — e
        // um README que não dissesse como deixaria a pessoa achando que o token está errado. Sem
        // autenticação não há o que avisar, e o aviso não pode aparecer.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");

        const string subsection = "### Swagger e autenticação";

        bool expected = swagger && authentication != "none";

        Assert.Equal(expected, readme.Contains(subsection, StringComparison.Ordinal));

        if (!swagger)
        {
            Assert.DoesNotContain("/openapi/v1.json", readme, StringComparison.Ordinal);
            Assert.DoesNotContain("/swagger", readme, StringComparison.Ordinal);

            return;
        }

        Assert.Contains("http://localhost:5100/openapi/v1.json", readme, StringComparison.Ordinal);
        Assert.Contains("http://localhost:5100/swagger", readme, StringComparison.Ordinal);

        if (expected)
        {
            int section = readme.IndexOf("## Swagger", StringComparison.Ordinal);
            int next = readme.IndexOf("## Próximos passos", StringComparison.Ordinal);

            Assert.InRange(readme.IndexOf(subsection, StringComparison.Ordinal), section, next);

            string swaggerSection = readme[section..next];

            // T08b: o documento declara o esquema bearer e a interface TEM o botão. A subseção
            // diz isso, diz o que colar — só o token, sem o prefixo — e diz os dois desfechos do
            // *Try it out*: `200` depois de autorizar, `401` sem. A frase de T08 ("não tem o
            // botão") não pode sobreviver: com a guarda da camada 3 invertida, ela seria falsa.
            Assert.Contains("a interface tem o botão *Authorize*", swaggerSection, StringComparison.Ordinal);
            Assert.Contains("cole **só o token**", swaggerSection, StringComparison.Ordinal);
            Assert.Contains("`200`", swaggerSection, StringComparison.Ordinal);
            Assert.Contains("`401`", swaggerSection, StringComparison.Ordinal);
            Assert.DoesNotContain("não tem o botão", readme, StringComparison.Ordinal);

            // De onde vem o token, que é o que muda entre as duas autenticações: no Identity, o
            // `accessToken` do login da própria aplicação, e as rotas `/auth/manage/...` também
            // têm cadeado; no JWT, o provedor externo, porque a aplicação não emite token.
            switch (authentication)
            {
                case "identity":
                    Assert.Contains("`accessToken`", swaggerSection, StringComparison.Ordinal);
                    Assert.Contains("`/auth/manage/...`", swaggerSection, StringComparison.Ordinal);
                    break;
                case "jwt":
                    Assert.Contains("**seu** provedor", swaggerSection, StringComparison.Ordinal);
                    break;
                default:
                    Assert.Fail(
                        $"authentication = {authentication} tem subseção Swagger + autenticação, e " +
                        "este teste não sabe de onde vem o token dela. Diga aqui.");
                    break;
            }
        }
    }

    [Fact]
    public void A_matriz_tem_identity_com_e_sem_Swagger()
    {
        // Sanidade do teste abaixo: ele só vale alguma coisa se a matriz tiver Identity nos dois
        // estados de Swagger. Sem Swagger, a seção de uso da autenticação é a ÚNICA menção a
        // `/auth/manage/...` no README — é o caso que o reviewer de T08b achou sem guarda.
        GenerationRequest[] identity =
            [.. Combinations.Available.Where(request => request.Authentication == "identity")];

        Assert.Contains(identity, request => request.Swagger);
        Assert.Contains(identity, request => !request.Swagger);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Com_Identity_o_README_cita_as_rotas_de_gestao_da_conta_como_protegidas(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Critério 6 de T08b, na metade que não depende do Swagger: o parágrafo de
        // `auth/identity/__parts__/ReadmeAuthUsage.md` que diz que `/auth/manage/...` exige token.
        // Até a rodada 1 da revisão de T08b ele só era cobrado dentro da subseção do Swagger —
        // apagá-lo deixava a suíte inteira verde nas quatro combinações Identity sem Swagger.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");

        int usage = readme.IndexOf("## Usar a autenticação", StringComparison.Ordinal);
        int crud = readme.IndexOf("## Testar o CRUD de `Item`", StringComparison.Ordinal);

        if (authentication != "identity")
        {
            Assert.DoesNotContain("/auth/manage/", readme, StringComparison.Ordinal);

            return;
        }

        Assert.True(usage >= 0 && crud > usage, "Com Identity, o README tem de ter '## Usar a autenticação' antes de '## Testar o CRUD de `Item`'.");

        string section = readme[usage..crud];

        Assert.Contains("`/auth/manage/...`", section, StringComparison.Ordinal);
        Assert.Contains("POST /auth/manage/2fa", section, StringComparison.Ordinal);
        Assert.Contains("curl -i http://localhost:5100/auth/manage/info -H 'Authorization: Bearer COLE_O_ACCESS_TOKEN_AQUI'", section, StringComparison.Ordinal);
        Assert.Matches(@"/auth/manage/[\s\S]*respondem `401` sem token", section);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_README_nao_tem_linha_em_branco_dupla(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // Regra 7 de ADR-0011 vista pelo resultado: contribuição vazia não deixa buraco. O README é
        // o arquivo com mais pontos de contribuição do pacote — seis eixos de texto —, e é o lugar
        // onde um buraco aparece primeiro para quem lê.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        string readme = package.Read("README.md");

        Assert.DoesNotContain("\n\n\n", readme, StringComparison.Ordinal);
        Assert.EndsWith("\n", readme, StringComparison.Ordinal);
        Assert.False(readme.EndsWith("\n\n", StringComparison.Ordinal), "O README termina com linha em branco.");
    }

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    // Título de nível 2 no início da linha. Os blocos de código do README não têm linha começando
    // por "## ", então não há falso positivo a filtrar.
    [GeneratedRegex(@"^## .+$", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"dotnet ef database update --project \S+ --startup-project \S+ --context (?<context>\w+)")]
    private static partial Regex EfDatabaseUpdate();
}
