using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// O lado <strong>backend</strong> do critério de aceite 7 de T09: o corpo de
/// <c>GET /api/template-options</c> é igual, estrutural e completamente, ao snapshot versionado em
/// <c>src/web/src/app/testing/template-options.snapshot.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Por que um snapshot, e por que neste caminho.</strong> Em T01 a fixture do frontend
/// divergiu do payload real em três pontos — <c>type</c> ausente, <c>when</c> escalar,
/// <c>description</c> ausente — e nenhum teste caiu: os leitores toleram as duas formas, então
/// a fixture errada e o payload certo passavam pelos mesmos testes. A deriva só apareceu em
/// conferência manual. O snapshot fecha o laço pelos dois lados: <strong>aqui</strong> ele é
/// cobrado contra o payload de verdade, servido pela Api in-process; <strong>no frontend</strong> a
/// fixture deriva do mesmo arquivo, sem cópia. O arquivo mora em <c>src/app/testing/</c> porque é
/// de lá que o frontend o importa — é o mesmo arranjo de <c>ZipStructureContractTests</c>, com o
/// arquivo do lado de quem o consome.
/// </para>
/// <para>
/// <strong>Igualdade, não contenção.</strong> A comparação é a de
/// <see cref="JsonStructuralComparer"/>: chave a mais ou a menos, tipo JSON trocado e array fora de
/// ordem derrubam o teste. Só a ordem das chaves de objeto e a formatação são ignoradas.
/// </para>
/// <para>
/// <strong>Atualizar é um ato explícito.</strong> Por padrão este teste <em>nunca</em> escreve no
/// arquivo. Com <c>UPDATE_SNAPSHOTS=1</c> ele regrava o snapshot a partir do payload e
/// <strong>falha mesmo assim</strong>: uma suíte verde nunca pode ter sido produzida por uma
/// regravação, senão quem rodasse com a variável esquecida no ambiente aprovaria qualquer mudança
/// de payload sem ver. A regravação aparece no <c>git diff</c>, que é onde ela tem de ser julgada.
/// </para>
/// </remarks>
public sealed class TemplateOptionsSnapshotTests : IClassFixture<GeneratorApiFactory>
{
    /// <summary>Caminho do snapshot, relativo à raiz. Fixo: o frontend o importa daqui.</summary>
    public const string SnapshotPath = "src/web/src/app/testing/template-options.snapshot.json";

    /// <summary>A variável de ambiente que autoriza regravar o snapshot.</summary>
    public const string UpdateVariable = "UPDATE_SNAPSHOTS";

    /// <summary>
    /// A formatação gravada: indentação de 2 espaços, LF e o <em>encoder</em> relaxado da Api, para
    /// que "Autenticação" fique legível no arquivo e não vire <c>ç</c>. A formatação não entra
    /// na comparação; ela existe para que o diff de uma regravação mostre só o que mudou.
    /// </summary>
    private static readonly JsonSerializerOptions _snapshotFormat = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
    };

    private static readonly string _updateHowTo =
        $"Se a mudança do payload é intencional, regrave o snapshot de propósito:{Environment.NewLine}" +
        $"  {UpdateVariable}=1 dotnet test tests/TemplateGenerator.Api.Tests " +
        $"--filter FullyQualifiedName~{nameof(TemplateOptionsSnapshotTests)}{Environment.NewLine}" +
        $"O teste regrava o arquivo e falha de propósito. Revise o git diff de '{SnapshotPath}', " +
        $"rode de novo sem a variável e confira que o frontend (a fixture derivada do snapshot) " +
        $"acompanhou.";

    private readonly GeneratorApiFactory _factory;

    public TemplateOptionsSnapshotTests(GeneratorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task O_payload_de_template_options_e_exatamente_o_snapshot_versionado()
    {
        string body = await GetCatalogBodyAsync();

        using JsonDocument payload = JsonDocument.Parse(body);

        // O payload também passa pela sanidade: regravar o snapshot a partir de um payload vazio
        // produziria um par vazio-contra-vazio que passaria daí em diante.
        AssertNotVacuous(payload.RootElement, "o payload de GET /api/template-options");

        string file = RepositoryLayout.Resolve(SnapshotPath);

        if (string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal))
        {
            string formatted = JsonSerializer.Serialize(payload.RootElement, _snapshotFormat) + "\n";

            await File.WriteAllTextAsync(
                file,
                formatted,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                TestContext.Current.CancellationToken);

            Assert.Fail(
                $"{UpdateVariable}=1: '{SnapshotPath}' foi regravado a partir do payload atual. " +
                "Este teste falha de propósito para que nenhuma regravação passe despercebida. " +
                $"Revise o git diff do arquivo e rode de novo sem {UpdateVariable}.");
        }

        Assert.True(
            File.Exists(file),
            $"'{SnapshotPath}' não existe. Ele é o snapshot versionado do payload de " +
            $"GET /api/template-options (critério 7 de T09).{Environment.NewLine}{_updateHowTo}");

        string text = await File.ReadAllTextAsync(file, Encoding.UTF8, TestContext.Current.CancellationToken);

        Assert.False(
            string.IsNullOrWhiteSpace(text),
            $"'{SnapshotPath}' está vazio. Um snapshot vazio não protege nada.{Environment.NewLine}{_updateHowTo}");

        using JsonDocument snapshot = ParseSnapshot(text);

        AssertNotVacuous(snapshot.RootElement, $"'{SnapshotPath}'");

        JsonDivergence? divergence = JsonStructuralComparer.FirstDivergence(snapshot.RootElement, payload.RootElement);

        Assert.True(
            divergence is null,
            $"O payload de GET /api/template-options não corresponde mais a '{SnapshotPath}'." +
            $"{Environment.NewLine}{divergence}{Environment.NewLine}{Environment.NewLine}{_updateHowTo}");
    }

    /// <summary>
    /// A comparação morde nos quatro pontos que o critério nomeia — e nos que viriam junto —, cada
    /// um aplicado a uma cópia do payload real, sem tocar no arquivo.
    /// </summary>
    /// <remarks>
    /// Existe para que a prova de que o teste principal morde não seja só um registro de execução
    /// manual: se alguém afrouxar <see cref="JsonStructuralComparer"/> para "contém", isto cai.
    /// </remarks>
    [Theory]
    [InlineData("description-sumido", "$.fields.database.values[1].description", "o payload tem esta chave e o snapshot não")]
    [InlineData("when-escalar", "$.constraints[0].when.authentication", "o snapshot tem texto e o payload tem array")]
    [InlineData("type-ausente", "$.fields.swagger.type", "o payload tem esta chave e o snapshot não")]
    [InlineData("chave-a-mais-no-snapshot", "$.fields.architecture.novo", "o snapshot tem esta chave e o payload não")]
    [InlineData("booleano-virou-texto", "$.fields.swagger.default", "o snapshot tem texto e o payload tem booleano true")]
    [InlineData("array-fora-de-ordem", "$.fields.database.values[0].value", "o texto é outro")]
    public async Task A_comparacao_aponta_o_caminho_da_primeira_divergencia(
        string mutation,
        string expectedPath,
        string expectedReason)
    {
        string body = await GetCatalogBodyAsync();

        using JsonDocument payload = JsonDocument.Parse(body);
        JsonNode altered = JsonNode.Parse(body)!;

        // Cada mutação é aplicada ao lado "snapshot": é o arquivo que envelhece, e o payload é a
        // verdade. Por isso "campo novo no payload" é o mesmo caso que "description sumido": o
        // payload tem uma chave que o snapshot não tem. O inverso é "chave a mais no snapshot".
        switch (mutation)
        {
            case "description-sumido":
                altered["fields"]!["database"]!["values"]![1]!.AsObject().Remove("description");
                break;
            case "when-escalar":
                altered["constraints"]![0]!["when"]!["authentication"] = "identity";
                break;
            case "type-ausente":
                altered["fields"]!["swagger"]!.AsObject().Remove("type");
                break;
            case "chave-a-mais-no-snapshot":
                altered["fields"]!["architecture"]!["novo"] = "x";
                break;
            case "booleano-virou-texto":
                altered["fields"]!["swagger"]!["default"] = "true";
                break;
            case "array-fora-de-ordem":
                JsonArray values = altered["fields"]!["database"]!["values"]!.AsArray();
                JsonNode first = values[0]!.DeepClone();
                values.RemoveAt(0);
                values.Add(first);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Mutação desconhecida.");
        }

        using JsonDocument snapshot = JsonDocument.Parse(altered.ToJsonString());

        JsonDivergence? divergence = JsonStructuralComparer.FirstDivergence(snapshot.RootElement, payload.RootElement);

        Assert.NotNull(divergence);
        Assert.Equal(expectedPath, divergence.Path);
        Assert.Equal(expectedReason, divergence.Reason);
    }

    [Fact]
    public async Task A_comparacao_ignora_a_ordem_das_chaves_de_objeto_e_a_formatacao()
    {
        string body = await GetCatalogBodyAsync();

        using JsonDocument payload = JsonDocument.Parse(body);
        JsonObject reordered = new();

        // As mesmas chaves de topo, em ordem inversa, e serializadas compactas: nada disso é
        // divergência.
        foreach (KeyValuePair<string, JsonNode?> property in JsonNode.Parse(body)!.AsObject().Reverse().ToList())
        {
            reordered[property.Key] = property.Value?.DeepClone();
        }

        using JsonDocument snapshot = JsonDocument.Parse(reordered.ToJsonString());

        Assert.Null(JsonStructuralComparer.FirstDivergence(snapshot.RootElement, payload.RootElement));
    }

    /// <summary>
    /// A sanidade exigida pelo critério, pela mesma razão de ADR-0008 e ADR-0011: um documento que
    /// não é objeto, ou que não tem <c>fields</c> com conteúdo, faria a comparação passar sem olhar
    /// nada que importe.
    /// </summary>
    private static void AssertNotVacuous(JsonElement root, string what)
    {
        Assert.True(
            root.ValueKind == JsonValueKind.Object,
            $"{what} não é um objeto JSON (é {root.ValueKind}).{Environment.NewLine}{_updateHowTo}");

        Assert.True(
            root.TryGetProperty("fields", out JsonElement fields) &&
            fields.ValueKind == JsonValueKind.Object &&
            fields.EnumerateObject().Any(),
            $"{what} não tem 'fields', ou 'fields' está vazio. Sem campos, a comparação passaria " +
            $"por vacuidade.{Environment.NewLine}{_updateHowTo}");
    }

    private static JsonDocument ParseSnapshot(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            Assert.Fail(
                $"'{SnapshotPath}' não é JSON válido: {exception.Message}{Environment.NewLine}{_updateHowTo}");
            throw;
        }
    }

    private async Task<string> GetCatalogBodyAsync()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/template-options", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
