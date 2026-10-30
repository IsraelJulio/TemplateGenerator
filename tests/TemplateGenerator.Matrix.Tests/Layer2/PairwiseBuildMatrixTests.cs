using TemplateGenerator.Generation;
using TemplateGenerator.Matrix.Tests.Layer3;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer2;

/// <summary>
/// Camada 2 completa (docs/quality/test-strategy.md): <c>dotnet build</c> de um conjunto de 9
/// combinações que cobre <strong>todos os pares de valores</strong> entre os quatro eixos, com a
/// prova de cobertura de pares registrada em código.
/// </summary>
/// <remarks>
/// <para>
/// O mínimo teórico para 2×3×3×2 é 9 (3×3, os dois eixos de três valores). Com a restrição
/// <c>identity</c> ⇒ <c>database ≠ none</c>, os pares <c>(identity, none)</c> não existem e portanto não
/// são exigidos: o conjunto de pares a cobrir é <strong>derivado de <see cref="Combinations.Valid"/></strong>,
/// nunca escrito à mão, para que a prova acompanhe o catálogo.
/// </para>
/// <para>
/// <c>NUGET_PACKAGES</c> compartilhado é obrigatório. Nada é pulado: build que falha derruba o teste
/// com a saída do comando (via <see cref="DatabaseRuntime"/>).
/// </para>
/// </remarks>
[Trait("Camada", "2")]
public sealed class PairwiseBuildMatrixTests
{
    private const string ProjectName = "Camada2.Pairwise";

    private static readonly string _sharedPackages = Path.Combine(
        Path.GetTempPath(),
        "templategenerator-layer2-nuget");

    /// <summary>
    /// O conjunto pairwise: (arquitetura, banco, autenticação, swagger). Encontrado por busca e
    /// provado por <see cref="O_conjunto_cobre_todos_os_pares_de_valores_das_combinacoes_validas"/>.
    /// </summary>
    private static readonly (string Architecture, string Database, string Authentication, bool Swagger)[] _set =
    [
        ("clean", "none", "none", false),
        ("clean", "postgresql", "none", true),
        ("clean", "sqlite", "identity", false),
        ("clean", "sqlite", "jwt", false),
        ("simple", "none", "jwt", true),
        ("simple", "none", "none", true),
        ("simple", "postgresql", "identity", true),
        ("simple", "postgresql", "jwt", false),
        ("simple", "sqlite", "none", true),
    ];

    public static TheoryData<string, string, string, bool> PairwiseCombinations
    {
        get
        {
            TheoryData<string, string, string, bool> data = [];

            foreach ((string architecture, string database, string authentication, bool swagger) in _set)
            {
                data.Add(architecture, database, authentication, swagger);
            }

            return data;
        }
    }

    [Fact]
    public void O_conjunto_cobre_todos_os_pares_de_valores_das_combinacoes_validas()
    {
        HashSet<string> required = [.. Combinations.Valid.SelectMany(Pairs)];

        HashSet<string> covered = [.. _set.Select(ToRequest).SelectMany(Pairs)];

        // Sanidade: 6 pares de eixos; sem pares exigidos a prova seria vácua.
        Assert.True(required.Count > 0, "Nenhum par exigido: Combinations.Valid está vazio?");

        string[] missing = [.. required.Except(covered).Order(StringComparer.Ordinal)];

        Assert.True(
            missing.Length == 0,
            $"O conjunto pairwise da camada 2 não cobre {missing.Length} par(es): " +
            string.Join("; ", missing));
    }

    [Fact]
    public void O_conjunto_tem_o_tamanho_minimo_e_so_combinacoes_validas_e_disponiveis()
    {
        // 9 = 3 × 3, o mínimo teórico (test-strategy.md). Crescer sem motivo encarece a camada.
        Assert.Equal(9, _set.Length);
        Assert.Equal(_set.Length, _set.Distinct().Count());

        foreach (GenerationRequest request in _set.Select(ToRequest))
        {
            Assert.Contains(Combinations.Valid, valid => Same(valid, request));

            Assert.Contains(Combinations.Available, available => Same(available, request));
        }
    }

    [Theory]
    [MemberData(nameof(PairwiseCombinations))]
    public async Task A_combinacao_do_conjunto_pairwise_compila(
        string architecture,
        string database,
        string authentication,
        bool swagger)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using DatabaseRuntime runtime = await DatabaseRuntime.ExtractAsync(
            new GenerationRequest(ProjectName, architecture, database, authentication, swagger, "net10.0"),
            cancellationToken);

        // Cache compartilhado — obrigatório na camada 2.
        runtime.Environment["NUGET_PACKAGES"] = _sharedPackages;

        await runtime.BuildAsync(cancellationToken);
    }

    private static GenerationRequest ToRequest(
        (string Architecture, string Database, string Authentication, bool Swagger) row) =>
        new(Combinations.ProjectName, row.Architecture, row.Database, row.Authentication, row.Swagger, "net10.0");

    private static bool Same(GenerationRequest a, GenerationRequest b) =>
        a.Architecture == b.Architecture &&
        a.Database == b.Database &&
        a.Authentication == b.Authentication &&
        a.Swagger == b.Swagger;

    /// <summary>Os 6 pares de valores de uma combinação, como texto estável.</summary>
    private static IEnumerable<string> Pairs(GenerationRequest request)
    {
        string[] values =
        [
            $"architecture={request.Architecture}",
            $"database={request.Database}",
            $"authentication={request.Authentication}",
            $"swagger={request.Swagger}",
        ];

        for (int i = 0; i < values.Length; i++)
        {
            for (int j = i + 1; j < values.Length; j++)
            {
                yield return $"{values[i]}|{values[j]}";
            }
        }
    }
}
