using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Catalog;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer1;

/// <summary>
/// Camada 1 do eixo <c>authentication</c> <strong>inteiro</strong>: o que vale para
/// <em>qualquer</em> valor que exija token, e não para um valor em particular — o CRUD protegido
/// (RF-14), a saúde sempre pública (RF-12) e o README mandando executar os comandos que de fato
/// funcionam na combinação (RF-21).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Por que este arquivo nasceu em T07.</strong> As duas afirmações abaixo moravam em
/// <see cref="IdentityFragmentMatrixTests"/> e calculavam o esperado como
/// <c>authentication == "identity"</c>. Enquanto o Identity era a única autenticação escrita, a
/// condição certa e a escrita coincidiam; com <c>auth/jwt</c> escrito elas se separaram, e as 24
/// invocações de <c>jwt</c> caíram. O defeito não era o número: era o teorema estar escrito sobre
/// <em>um valor do eixo</em> quando o que RF-14 diz é sobre <strong>haver autenticação</strong>.
/// </para>
/// <para>
/// Corrigir a condição sem mudar de casa deixaria, em um arquivo chamado "fragmento do Identity",
/// dois testes que não falam de Identity — e o próximo valor de autenticação seria escrito sem que
/// ninguém pensasse em procurá-los ali. Eles estão aqui, com o nome do que afirmam;
/// <see cref="IdentityFragmentMatrixTests"/> fica com o que só vale para o Identity, e
/// <see cref="JwtFragmentMatrixTests"/> com o que só vale para o JWT.
/// </para>
/// <para>
/// <strong>"Se e somente se", sempre.</strong> A metade negativa é a que pega o defeito mais
/// provável de um eixo com três valores: um fragmento vazando para a combinação que não o escolheu.
/// Um teste que só olhasse as combinações autenticadas deixaria passar um <c>RequireAuthorization</c>
/// em <c>auth/none</c> — e RF-20 é exatamente sobre isso.
/// </para>
/// <para>
/// <strong>Assert de sanidade, obrigatório</strong> (ADR-0008, ADR-0011, ADR-0015): se a matriz
/// deixasse de produzir combinação autenticada — ou deixasse de produzir combinação sem
/// autenticação —, os teoremas desta classe passariam por vacuidade. Um verificador que para de
/// verificar em silêncio é pior que nenhum.
/// </para>
/// </remarks>
public sealed class AuthenticationMatrixTests
{
    /// <summary>
    /// O único valor do eixo que <strong>não</strong> exige token.
    /// </summary>
    /// <remarks>
    /// A condição é escrita como "não é <c>none</c>", e não como uma lista dos valores que exigem
    /// token, de propósito: uma lista precisaria ser editada a cada valor novo do eixo, e o valor
    /// novo entraria em produção com os dois teoremas abaixo calados a respeito dele. Do jeito que
    /// está, um <c>auth/oauth2</c> escrito amanhã já nasce cobrado.
    /// </remarks>
    private const string None = "none";

    public static TheoryData<string, string, string, bool> AvailableCombinations =>
        GenerationMatrixTests.AvailableCombinations;

    [Fact]
    public void A_matriz_produz_combinacao_autenticada_e_combinacao_sem_autenticacao()
    {
        // Sanidade dos dois lados de cada "se e somente se" abaixo. E mais: que a matriz produza
        // MAIS DE UM valor autenticado, porque é isso que distingue este arquivo de uma cópia do
        // de Identity — se sobrasse um valor só, o teorema voltaria a ser sobre aquele valor.
        string[] authenticated =
        [
            .. Combinations.Available
                .Select(request => request.Authentication)
                .Where(value => !string.Equals(value, None, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            authenticated.Length >= 2,
            "A matriz produziu " + authenticated.Length + " valor(es) de autenticação com " +
            "fragmento escrito (" + string.Join(", ", authenticated) + "). Com menos de dois, " +
            "'o CRUD exige token se e somente se há autenticação' não se distingue de 'se e " +
            "somente se authentication = <aquele valor>', que é a formulação que T07 derrubou.");

        Assert.Contains(
            Combinations.Available,
            request => string.Equals(request.Authentication, None, StringComparison.Ordinal));

        // E o catálogo não pode ter valor de autenticação sem combinação disponível: se um deles
        // apagasse, as teorias abaixo continuariam verdes com menos casos.
        Assert.Equal(
            TemplateCatalog.Current.Fields[CatalogFields.Authentication].Values!
                .Select(option => option.Value)
                .Order(StringComparer.Ordinal),
            Combinations.Available
                .Select(request => request.Authentication)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task O_CRUD_exige_token_se_e_somente_se_ha_autenticacao_e_a_saude_nunca_exige(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // RF-14 e RF-12, lidos no código gerado: o grupo `/items` ganha `RequireAuthorization`
        // quando há autenticação — qualquer que seja ela — e não o ganha sem; `/health` nunca o
        // ganha, em combinação nenhuma.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        bool expected = RequiresToken(authentication);

        Assert.True(
            ItemEndpoints(package).Contains("items.RequireAuthorization();", StringComparison.Ordinal)
                == expected,
            $"{GeneratedPackage.Describe(package.Request)}: o CRUD " +
            $"{(expected ? "não exige" : "exige")} token, e authentication = {authentication} " +
            "(RF-14).");

        string health = package.Read(package.Paths.Single(path =>
            path.EndsWith("/HealthEndpoints.cs", StringComparison.Ordinal)));

        Assert.DoesNotContain("RequireAuthorization", health, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AvailableCombinations))]
    public async Task Todo_comando_de_CRUD_do_README_leva_token_se_e_somente_se_ha_autenticacao(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        // RF-21 na metade que quase escapou em T06: não basta o README **acrescentar** as frases
        // certas sobre a autenticação; os comandos que ele manda copiar precisam ser os comandos
        // que funcionam naquela combinação. A primeira versão de T06 tratou o `requests.http` e
        // esqueceu a seção "Testar o CRUD" do README, que continuava ensinando cinco chamadas sem
        // `Authorization` — todas `401` — e uma tabela prometendo `200` e `201` para elas. O
        // documento se contradizia, e a metade errada era a copiável.
        //
        // Um teste que só conferisse frases PRESENTES passa verde em cima desse defeito. Este
        // confere o inverso, que é o que o pega: nenhum comando de CRUD pode estar **sem** token
        // onde o token é obrigatório, e nenhum pode estar **com** token onde não há autenticação.
        GeneratedPackage package = await GeneratedPackage.GenerateAsync(
            Request(architecture, database, authentication, swagger),
            TestContext.Current.CancellationToken);

        bool expected = RequiresToken(authentication);
        string crud = CrudSection(package.Read("README.md"));

        // "Contém `curl`", e não "começa com `curl`": no bloco PowerShell o corpo da requisição vai
        // pelo pipe (`'{…}' | curl.exe …`, ver ReadmeShellMatrixTests), então a linha do POST começa
        // pelo JSON. Um filtro de prefixo deixaria justamente essa linha de fora — e ela é uma das
        // que precisam do token.
        string[] calls =
        [
            .. crud
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line =>
                    line.Contains("curl", StringComparison.Ordinal) &&
                    line.Contains("/items", StringComparison.Ordinal)),
        ];

        // Sanidade: a seção tem cinco chamadas em Bash e duas em PowerShell. Se o recorte deixasse
        // de achá-las — heading renomeado, bloco movido —, o laço abaixo passaria sem olhar nada, e
        // este teste voltaria a ser a falsa segurança que ele existe para tirar.
        Assert.True(
            calls.Length >= 7,
            $"{GeneratedPackage.Describe(package.Request)}: a seção de CRUD do README tem só " +
            $"{calls.Length} chamada(s) a '/items'. O recorte da seção provavelmente quebrou, e " +
            "a afirmação abaixo passaria por vacuidade.");

        foreach (string call in calls)
        {
            Assert.True(
                call.Contains("Authorization: Bearer", StringComparison.Ordinal) == expected,
                $"{GeneratedPackage.Describe(package.Request)}: o README manda executar " +
                $"`{call}`, que {(expected ? "responde 401 — falta o cabeçalho 'Authorization'" : "leva um cabeçalho 'Authorization' que esta combinação não tem")}. " +
                "Todo comando do README tem de rodar como está (RF-21).");
        }

        // E a tabela de respostas: com autenticação, a promessa de `200`/`201` só vale com o token,
        // e a seção precisa dizer sob que condição ela deixa de valer. Sem autenticação não existe
        // `401` a mencionar ali.
        Assert.True(
            crud.Contains("`401`", StringComparison.Ordinal) == expected,
            $"{GeneratedPackage.Describe(package.Request)}: a seção de CRUD do README " +
            $"{(expected ? "não diz" : "diz")} o que acontece sem token, e authentication = " +
            $"{authentication}.");
    }

    /// <summary>Se o valor de <c>authentication</c> faz o CRUD exigir token.</summary>
    private static bool RequiresToken(string authentication) =>
        !string.Equals(authentication, None, StringComparison.Ordinal);

    private static GenerationRequest Request(
        string architecture,
        string database,
        string authentication,
        bool swagger) =>
        new(Combinations.ProjectName, architecture, database, authentication, swagger, "net10.0");

    /// <summary>
    /// A seção "Testar o CRUD" do README, do próprio título até o título seguinte de mesmo nível.
    /// </summary>
    /// <remarks>
    /// O recorte existe para a afirmação não escorregar para as outras seções: a de autenticação
    /// cita `401` de propósito e mostra chamadas sem token para demonstrar a recusa, e misturar as
    /// duas faria o teste cobrar a coisa errada de cada uma.
    /// </remarks>
    private static string CrudSection(string readme)
    {
        const string Heading = "## Testar o CRUD de";

        int start = readme.IndexOf(Heading, StringComparison.Ordinal);

        Assert.True(start >= 0, $"O README não tem a seção '{Heading} …'.");

        int next = readme.IndexOf("\n## ", start + Heading.Length, StringComparison.Ordinal);

        return next < 0 ? readme[start..] : readme[start..next];
    }

    private static string ItemEndpoints(GeneratedPackage package) =>
        package.Read(package.Paths.Single(path =>
            path.EndsWith("/ItemEndpoints.cs", StringComparison.Ordinal)));
}
