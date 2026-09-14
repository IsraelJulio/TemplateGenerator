using System.Diagnostics;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Engine;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Um projeto gerado <strong>com banco</strong>, extraído em disco e levado pelos comandos do
/// README <em>na ordem em que estão escritos</em> — <c>dotnet restore</c>, <c>dotnet tool
/// restore</c> e <c>dotnet ef database update</c> — antes de a aplicação subir (ADR-0015, "O que
/// defende esta decisão, executável").
/// </summary>
/// <remarks>
/// <para>
/// É a ferramenta de camada 3 para os fragmentos de T05. Difere de <see cref="GeneratedProject"/>
/// em dois pontos que o banco impõe: a migração é aplicada por comando externo antes da subida (o
/// projeto gerado não migra sozinho, ADR-0015 decisão 2), e a aplicação sobe com o
/// <strong>diretório de trabalho do projeto de subida</strong>, como <c>dotnet run --project</c>
/// faz — sem isso, o <c>Data Source=app.db</c> relativo do SQLite apontaria para a pasta de saída
/// e não para o arquivo que o <c>dotnet ef</c> acabou de criar.
/// </para>
/// <para>
/// <strong>Nada é pulado.</strong> Se um comando do README falhar, o teste falha com a saída do
/// comando colada na mensagem. Para PostgreSQL, a indisponibilidade do serviço ou da credencial é
/// falha explícita, nunca <c>Skip</c> (ADR-0005, docs/quality/test-strategy.md).
/// </para>
/// </remarks>
internal sealed partial class DatabaseRuntime : IAsyncDisposable
{
    private readonly string _root;
    private readonly string _projectDirectory;
    private Process? _app;

    private DatabaseRuntime(string root, string projectDirectory, string assembly, int port)
    {
        _root = root;
        _projectDirectory = projectDirectory;
        Assembly = assembly;
        Port = port;
    }

    /// <summary>Caminho do <c>.dll</c> compilado do projeto Web API.</summary>
    public string Assembly { get; }

    /// <summary>Porta livre reservada para esta instância.</summary>
    public int Port { get; }

    /// <summary>Endereço base da aplicação em execução.</summary>
    public Uri BaseAddress => new($"http://127.0.0.1:{Port}/");

    /// <summary>Raiz do projeto extraído.</summary>
    public string Root => _root;

    /// <summary>
    /// Variáveis de ambiente aplicadas a <strong>todo</strong> processo — <c>dotnet ef</c>,
    /// <c>dotnet build</c> e a aplicação. É por aqui que a cadeia de conexão do PostgreSQL, com
    /// senha, chega ao processo sem tocar arquivo nenhum do projeto (o caminho que o próprio README
    /// ensina: <c>ConnectionStrings__Default</c> pelo ambiente).
    /// </summary>
    public Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);

    /// <summary>Extrai o pacote de <paramref name="request"/> e localiza o projeto Web API.</summary>
    public static async Task<DatabaseRuntime> ExtractAsync(
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "templategenerator-layer3-db",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        GenerationEngine engine = new();

        using (MemoryStream buffer = new())
        {
            await engine.WriteArchiveAsync(request, buffer, cancellationToken);

            buffer.Position = 0;

            using ZipArchive archive = new(buffer, ZipArchiveMode.Read);

            archive.ExtractToDirectory(root);
        }

        string source = Path.Combine(root, "src");

        string[] entryPoints =
        [
            .. Directory.EnumerateFiles(source, "Program.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal),
        ];

        if (entryPoints.Length != 1)
        {
            throw new InvalidOperationException(
                $"O pacote de {GeneratedPackage.Describe(request)} não trouxe exatamente um " +
                "'Program.cs' sob 'src/'. A camada 3 não sabe qual projeto subir.");
        }

        string projectDirectory = Path.GetDirectoryName(entryPoints[0])!;

        string csproj = Directory
            .EnumerateFiles(projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
            .Single();

        string projectName = Path.GetFileNameWithoutExtension(csproj);

        string assembly = Path.Combine(
            projectDirectory,
            "bin",
            "Debug",
            request.DotnetVersion,
            $"{projectName}.dll");

        return new DatabaseRuntime(root, projectDirectory, assembly, FreePort());
    }

    /// <summary>O texto do <c>README.md</c> gerado, na raiz do pacote.</summary>
    public string Readme() => File.ReadAllText(Path.Combine(_root, "README.md"));

    /// <summary>
    /// Executa os comandos do README na ordem escrita: <c>dotnet restore</c>, <c>dotnet tool
    /// restore</c> e o <c>dotnet ef database update …</c> extraído do próprio README. Cada comando
    /// que falha derruba o teste com a saída colada.
    /// </summary>
    public async Task RunReadmeMigrationStepsAsync(CancellationToken cancellationToken)
    {
        string readme = Readme();

        int restore = readme.IndexOf("dotnet restore", StringComparison.Ordinal);
        int toolRestore = readme.IndexOf("dotnet tool restore", StringComparison.Ordinal);
        int migrate = readme.IndexOf("dotnet ef database update", StringComparison.Ordinal);
        int run = readme.IndexOf("dotnet run", StringComparison.Ordinal);

        if (restore < 0 || toolRestore < 0 || migrate < 0 || run < 0)
        {
            throw new InvalidOperationException(
                "O README não traz os quatro passos esperados (restore, tool restore, ef database " +
                "update, run). Sem eles a camada 3 não tem o roteiro do banco para executar.");
        }

        // A ordem escrita É o critério: restaurar, restaurar a ferramenta, migrar, e só então subir.
        if (!(restore < toolRestore && toolRestore < migrate && migrate < run))
        {
            throw new InvalidOperationException(
                "O README não apresenta os passos na ordem exigida por ADR-0015: 'dotnet restore' " +
                "< 'dotnet tool restore' < 'dotnet ef database update' < 'dotnet run'. " +
                $"Posições: restore={restore}, tool restore={toolRestore}, migrate={migrate}, " +
                $"run={run}.");
        }

        Match command = EfDatabaseUpdate().Match(readme);

        if (!command.Success)
        {
            throw new InvalidOperationException(
                "O README não traz 'dotnet ef database update --project <x> --startup-project <y>' " +
                "com os dois caminhos. É o comando que a camada 3 executa (ADR-0015).");
        }

        await RunOrThrowAsync("dotnet", "restore --disable-build-servers", cancellationToken);
        await RunOrThrowAsync("dotnet", "tool restore", cancellationToken);
        await RunOrThrowAsync("dotnet", command.Value, cancellationToken);
    }

    /// <summary>Compila o projeto Web API (o passo que o <c>dotnet run</c> do README faria).</summary>
    public async Task BuildAsync(CancellationToken cancellationToken)
    {
        await RunOrThrowAsync(
            "dotnet",
            $"build \"{ProjectFile()}\" --nologo --disable-build-servers " +
            "-nodeReuse:false -p:UseSharedCompilation=false",
            cancellationToken);

        if (!File.Exists(Assembly))
        {
            throw new InvalidOperationException(
                $"A compilação terminou sem erro, mas '{Assembly}' não existe.");
        }
    }

    /// <summary>
    /// Sobe a aplicação com o diretório de trabalho do projeto de subida — como
    /// <c>dotnet run --project</c> — e espera <c>GET /health</c> responder. Cada chamada é um
    /// processo novo: é assim que a sobrevivência ao reinício é exercitada.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await StopAsync();

        ProcessStartInfo start = new("dotnet", $"\"{Assembly}\"")
        {
            WorkingDirectory = _projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.Environment["ASPNETCORE_URLS"] = BaseAddress.ToString().TrimEnd('/');
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        foreach ((string key, string value) in Environment)
        {
            start.Environment[key] = value;
        }

        StringBuilder log = new();

        _app = Process.Start(start)
            ?? throw new InvalidOperationException("Não foi possível iniciar a aplicação gerada.");

        _app.OutputDataReceived += (_, args) => log.AppendLine(args.Data);
        _app.ErrorDataReceived += (_, args) => log.AppendLine(args.Data);
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();

        using HttpClient client = new()
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(5),
        };

        DateTime deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            if (_app.HasExited)
            {
                throw new InvalidOperationException(
                    $"A aplicação terminou com código {_app.ExitCode} antes de responder. Saída:" +
                    System.Environment.NewLine + log);
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync("health", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException(
            $"A aplicação não respondeu em {BaseAddress}health em 60 segundos. Saída:" +
            System.Environment.NewLine + log);
    }

    /// <summary>Derruba a instância em execução, se houver.</summary>
    public async Task StopAsync()
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            if (!_app.HasExited)
            {
                _app.Kill(entireProcessTree: true);

                await _app.WaitForExitAsync();
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _app.Dispose();
            _app = null;
        }
    }

    /// <summary>Um cliente apontado para esta instância.</summary>
    public HttpClient Client() =>
        new() { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Executa um comando na raiz do pacote, com o ambiente configurado.</summary>
    public async Task<(int Exit, string Output)> RunAsync(
        string file,
        string arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(file, arguments)
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Sem servidores de build persistentes. Um nó de MSBuild ou o VBCSCompiler que sobrevive ao
        // comando herda a saída redirecionada e a mantém aberta, e aí `ReadToEndAsync` nunca vê o
        // fim — o processo do teste trava esperando um EOF que só chega quando o servidor expira.
        // Na Clean, com quatro projetos, é o que acontece. Desligá-los custa um pouco de tempo de
        // compilação e compra determinismo, que é o que a camada 2 precisa.
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        foreach ((string key, string value) in Environment)
        {
            start.Environment[key] = value;
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                $"Não foi possível executar '{file} {arguments}'. O SDK do .NET está no PATH?");

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, await output + await error);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string ProjectFile() =>
        Directory.EnumerateFiles(_projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly).Single();

    private async Task RunOrThrowAsync(
        string file,
        string arguments,
        CancellationToken cancellationToken)
    {
        (int exit, string output) = await RunAsync(file, arguments, cancellationToken);

        if (exit != 0)
        {
            throw new InvalidOperationException(
                $"`{file} {arguments}` falhou com código {exit}. Saída:" +
                System.Environment.NewLine + output);
        }
    }

    private static int FreePort()
    {
        using TcpListener listener = new(System.Net.IPAddress.Loopback, 0);

        listener.Start();

        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        listener.Stop();

        return port;
    }

    [GeneratedRegex(
        @"dotnet ef database update --project \S+ --startup-project \S+",
        RegexOptions.CultureInvariant)]
    private static partial Regex EfDatabaseUpdate();
}
