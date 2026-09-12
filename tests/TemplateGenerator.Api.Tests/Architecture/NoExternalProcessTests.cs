using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Api.Tests.Architecture;

/// <summary>
/// RNF-01, provado por inspeção de metadados: a plataforma <strong>não consegue</strong> executar
/// processo externo, porque não existe referência a um.
/// </summary>
/// <remarks>
/// <para>
/// "Não executa comandos, não restaura pacotes e não compila durante a requisição" é uma
/// afirmação difícil de testar por comportamento: um teste de tempo ou de efeito colateral prova
/// que <em>aquela</em> requisição não chamou nada, não que nenhuma chama. A tabela de
/// <c>TypeRef</c> do assembly, em compensação, lista todo tipo externo que o código referencia —
/// e <c>Process.Start</c> exige um <c>TypeRef</c> para <c>System.Diagnostics.Process</c>.
/// </para>
/// <para>
/// Mora em Api.Tests por um motivo simples: é o único projeto de teste que referencia os dois
/// assemblies da plataforma.
/// </para>
/// </remarks>
public sealed class NoExternalProcessTests
{
    /// <summary>
    /// Os tipos que existem para rodar outro programa. <c>dotnet restore</c>, <c>dotnet build</c>
    /// e <c>dotnet new</c> passam por um deles.
    /// </summary>
    private static readonly string[] _forbiddenTypes =
    [
        "System.Diagnostics.Process",
        "System.Diagnostics.ProcessStartInfo",
    ];

    public static TheoryData<string> PlatformAssemblies =>
    [
        typeof(IGenerationEngine).Assembly.Location,
        typeof(Program).Assembly.Location,
    ];

    [Theory]
    [MemberData(nameof(PlatformAssemblies))]
    public void Assembly_da_plataforma_nao_referencia_execucao_de_processo(string assemblyPath)
    {
        Assert.True(File.Exists(assemblyPath), $"Assembly não encontrado em '{assemblyPath}'.");

        string[] referenced = [.. ReadTypeReferences(assemblyPath)];

        // Sanidade: um assembly sempre referencia System.Object. Leitura vazia significaria um
        // teste que não olhou nada e passaria sozinho para sempre.
        Assert.Contains("System.Object", referenced);

        string[] violations =
        [
            .. referenced
                .Where(name => _forbiddenTypes.Contains(name, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            violations.Length == 0,
            $"'{Path.GetFileName(assemblyPath)}' referencia tipo de execução de processo: " +
            string.Join(", ", violations) + ". " +
            "A geração não executa comandos, não restaura pacotes e não compila (RNF-01); " +
            "ela lê recurso embutido, substitui marcador e escreve entrada de ZIP.");
    }

    [Fact]
    public void A_biblioteca_de_geracao_nao_referencia_o_sistema_de_arquivos()
    {
        // A outra metade de RNF-01/RNF-04: os fragmentos são recurso embutido, então uma geração
        // não abre, não cria e não apaga arquivo nenhum. Sem arquivo temporário e sem diretório
        // compartilhado entre requisições não é uma promessa — é a ausência da referência.
        //
        // Contar arquivos em %TEMP% antes e depois seria a versão comportamental deste teste, e
        // seria instável: qualquer outro processo da máquina escrevendo lá o faria falhar.
        string[] filesystemTypes =
        [
            "System.IO.File",
            "System.IO.FileInfo",
            "System.IO.FileStream",
            "System.IO.Directory",
            "System.IO.DirectoryInfo",
            "System.IO.Path",
        ];

        string[] referenced = [.. ReadTypeReferences(typeof(IGenerationEngine).Assembly.Location)];

        string[] violations =
        [
            .. referenced
                .Where(name => filesystemTypes.Contains(name, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            violations.Length == 0,
            "TemplateGenerator.Generation toca o sistema de arquivos: " +
            string.Join(", ", violations) + ". " +
            "Os templates são recurso embutido; a geração lê da memória e escreve no destino.");
    }

    private static IEnumerable<string> ReadTypeReferences(string assemblyPath)
    {
        using FileStream file = File.OpenRead(assemblyPath);
        using PEReader pe = new(file);

        MetadataReader metadata = pe.GetMetadataReader();

        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            TypeReference reference = metadata.GetTypeReference(handle);

            string name = metadata.GetString(reference.Name);
            string ns = metadata.GetString(reference.Namespace);

            yield return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }
    }
}
