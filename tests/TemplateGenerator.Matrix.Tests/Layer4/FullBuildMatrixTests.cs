using TemplateGenerator.Generation;
using TemplateGenerator.Matrix.Tests.Layer3;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer4;

/// <summary>
/// Camada 4 (docs/quality/test-strategy.md): <c>dotnet build</c> nas <strong>32</strong> combinações
/// válidas. Sob demanda — fora do ciclo normal de <c>dotnet test</c>.
/// </summary>
/// <remarks>
/// <para>
/// O build é um <c>[Theory(Explicit = true)]</c> (xUnit v3). Sem o modo explícito os 32 casos
/// <strong>não executam e não contam como aprovados</strong>. O console os lista, um por linha, como
/// <c>Skipped …A_combinacao_compila(…)</c>, mas o resumo não os conta: <c>Skipped: 0, Total: 1</c> (só
/// o teste de contagem). O outcome real, <c>NotExecuted</c>, só aparece num TRX. O sinal confiável é o
/// total: <c>Total: 1</c> com o filtro da camada 4 significa que ela <strong>não</strong> rodou;
/// <c>Total: 33</c>, que rodou. Comando que os executa:
/// </para>
/// <code>
/// dotnet test tests/TemplateGenerator.Matrix.Tests --filter "Camada=4" -- xUnit.Explicit=on
/// </code>
/// <para>
/// (<c>xUnit.Explicit=on</c> é a opção de RunSettings do adaptador VSTest; a forma
/// <c>-- --explicit on</c>, do runner nativo do xUnit, é ignorada aqui em silêncio e deixa os 32 sem
/// executar.) O número de casos é protegido por <see cref="A_camada_cobre_as_32_combinacoes_validas"/>,
/// que roda sempre.
/// </para>
/// <para>
/// Mecanismo igual ao da camada 2: <c>NUGET_PACKAGES</c> compartilhado e falha derruba o teste com a
/// saída do comando (<see cref="DatabaseRuntime"/>). PostgreSQL só compila; o build não precisa de banco.
/// </para>
/// </remarks>
[Trait("Camada", "4")]
public sealed class FullBuildMatrixTests
{
    private const string ProjectName = "Camada4.Completa";

    private const int ExpectedCombinations = 32;

    private static readonly string _sharedPackages = Path.Combine(
        Path.GetTempPath(),
        "templategenerator-layer4-nuget");

    public static TheoryData<string, string, string, bool> AllCombinations
    {
        get
        {
            TheoryData<string, string, string, bool> data = [];

            foreach (GenerationRequest request in Combinations.Valid)
            {
                data.Add(request.Architecture, request.Database, request.Authentication, request.Swagger);
            }

            return data;
        }
    }

    [Fact]
    public void A_camada_cobre_as_32_combinacoes_validas()
    {
        Assert.Equal(ExpectedCombinations, Combinations.Valid.Count);
        Assert.Equal(ExpectedCombinations, AllCombinations.Count());

        Assert.Equal(
            ExpectedCombinations,
            Combinations.Valid
                .Select(r => (r.Architecture, r.Database, r.Authentication, r.Swagger))
                .Distinct()
                .Count());
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(AllCombinations))]
    public async Task A_combinacao_compila(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using DatabaseRuntime runtime = await DatabaseRuntime.ExtractAsync(
            new GenerationRequest(ProjectName, architecture, database, authentication, swagger, "net10.0"),
            cancellationToken);

        runtime.Environment["NUGET_PACKAGES"] = _sharedPackages;

        await runtime.BuildAsync(cancellationToken);
    }
}
