using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using TemplateGenerator.Generation;
using TemplateGenerator.Generation.Engine;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Um projeto gerado, extraído em disco, compilado e <strong>em execução</strong> — a ferramenta
/// da camada 3 (docs/quality/test-strategy.md).
/// </summary>
/// <remarks>
/// <para>
/// Aqui, ao contrário das camadas 1 e 2, roda-se <c>dotnet build</c> e o executável de verdade.
/// Isso custa tempo e exige a rede na primeira execução, para restaurar os pacotes da combinação —
/// o mesmo custo que a camada 2 já assume. É o preço de provar o que só a execução prova: em T03,
/// o corpo JSON malformado que voltava <c>500</c> em vez de <c>400</c> passou pelas camadas 1 e 2
/// sem um arrepio.
/// </para>
/// <para>
/// <strong>Nada é pulado.</strong> Se o SDK não estiver disponível, se o restore falhar ou se a
/// aplicação não subir, o teste <strong>falha</strong> com a saída do comando colada na mensagem.
/// Um <c>Skip</c> aqui apagaria a única camada que olha comportamento.
/// </para>
/// <para>
/// <strong>Fora da árvore de trabalho.</strong> A extração vai para o diretório temporário do
/// sistema e é apagada no fim (AGENTS.md, "Arquivos temporários nunca no repositório").
/// </para>
/// <para>
/// A aplicação sobe pelo assembly compilado, com <c>ASPNETCORE_URLS</c> e
/// <c>ASPNETCORE_ENVIRONMENT</c> explícitos, em vez de <c>dotnet run</c>: o perfil de
/// <c>launchSettings.json</c> fixa a porta 5100, e dois testes em paralelo — ou uma máquina com
/// algo já escutando ali — brigariam por ela. Os comandos do README, esses, são executados à mão
/// pelo papel <c>qa</c> e a saída vai para o relatório da tarefa; é a parte que uma pessoa precisa
/// fazer como uma pessoa faria.
/// </para>
/// </remarks>
internal sealed class GeneratedProject : IAsyncDisposable
{
    private readonly string _root;
    private readonly string _name;
    private Process? _app;

    private GeneratedProject(string root, string name, string assembly, int port)
    {
        _root = root;
        _name = name;
        Assembly = assembly;
        Port = port;
    }

    /// <summary>Caminho do <c>.dll</c> compilado do projeto Web API.</summary>
    public string Assembly { get; }

    /// <summary>Porta livre reservada para esta instância.</summary>
    public int Port { get; }

    /// <summary>Endereço base da aplicação em execução.</summary>
    public Uri BaseAddress => new($"http://127.0.0.1:{Port}/");

    /// <summary>Raiz do projeto extraído, para inspecionar arquivo em disco.</summary>
    public string Root => _root;

    /// <summary>
    /// Gera, extrai e compila o pacote de <paramref name="request"/>.
    /// </summary>
    public static async Task<GeneratedProject> BuildAsync(
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "templategenerator-layer3",
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

        // O projeto Web API é achado pelo `Program.cs`, e NÃO pelo nome. É a mesma regra por papel
        // de `PackageLayout`, pelo mesmo motivo: na Simples o projeto de código se chama
        // exatamente `<projectName>`, na Clean ele é `<projectName>.Api` entre quatro irmãos, e
        // reimplementar aqui a regra de nomes de generated-projects.md criaria uma segunda cópia
        // dela. Ponto de entrada há um só, nas duas arquiteturas.
        //
        // Até T04 este caminho era literal — `src/<projectName>/<projectName>.csproj` —, e por
        // isso a camada 3 simplesmente não tinha como receber um pacote de Clean.
        string? project = WebApiProject(root);

        if (project is null || !File.Exists(project))
        {
            throw new InvalidOperationException(
                $"O pacote de {request.Architecture}/{request.Database}/{request.Authentication} " +
                "não trouxe um projeto Web API com 'Program.cs' e '.csproj' ao lado, sob 'src/'. " +
                "A camada 3 não tem o que compilar.");
        }

        // Só o projeto Web API. Compilar a solução arrastaria o projeto de testes gerado e os
        // pacotes de xUnit, que não têm nada a ver com subir a aplicação — `dotnet test` do pacote
        // é comando do README e é executado à parte.
        (int exit, string output) = await RunAsync(
            "dotnet",
            $"build \"{project}\" --nologo",
            root,
            cancellationToken);

        if (exit != 0)
        {
            Directory.Delete(root, recursive: true);

            throw new InvalidOperationException(
                $"`dotnet build` do projeto gerado falhou com código {exit}. Saída:" +
                Environment.NewLine + output);
        }

        // O nome do assembly acompanha o do projeto, que é o nome da pasta — e é decisão de
        // generated-projects.md que os três sejam a mesma string. Derivar do `.csproj` encontrado,
        // em vez de reconstruir a partir de `projectName`, é o que faz isto valer nas duas
        // arquiteturas sem um `if`.
        string projectName = Path.GetFileNameWithoutExtension(project);

        string assembly = Path.Combine(
            Path.GetDirectoryName(project)!,
            "bin",
            "Debug",
            request.DotnetVersion,
            $"{projectName}.dll");

        if (!File.Exists(assembly))
        {
            Directory.Delete(root, recursive: true);

            throw new InvalidOperationException(
                $"A compilação terminou sem erro mas '{assembly}' não existe. Saída:" +
                Environment.NewLine + output);
        }

        return new GeneratedProject(root, projectName, assembly, LoopbackPort.Reserve());
    }

    /// <summary>
    /// O <c>.csproj</c> do projeto Web API extraído em <paramref name="root"/>: aquele cuja pasta
    /// tem o <c>Program.cs</c>. <c>null</c> quando não há exatamente um.
    /// </summary>
    private static string? WebApiProject(string root)
    {
        string source = Path.Combine(root, "src");

        if (!Directory.Exists(source))
        {
            return null;
        }

        string[] entryPoints =
        [
            .. Directory.EnumerateFiles(source, "Program.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal),
        ];

        if (entryPoints.Length != 1)
        {
            return null;
        }

        string directory = Path.GetDirectoryName(entryPoints[0])!;

        string[] projects =
        [
            .. Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly),
        ];

        return projects.Length == 1 ? projects[0] : null;
    }

    /// <summary>
    /// Sobe a aplicação e espera <c>GET /health</c> responder. Cada chamada é um processo novo —
    /// é assim que o reinício de RF-16 é exercitado.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await StopAsync();

        ProcessStartInfo start = new("dotnet", $"\"{Assembly}\"")
        {
            WorkingDirectory = Path.GetDirectoryName(Assembly)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.Environment["ASPNETCORE_URLS"] = BaseAddress.ToString().TrimEnd('/');
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        StringBuilder log = new();

        _app = Process.Start(start)
            ?? throw new InvalidOperationException($"Não foi possível iniciar '{_name}'.");

        _app.OutputDataReceived += (_, args) => log.AppendLine(args.Data);
        _app.ErrorDataReceived += (_, args) => log.AppendLine(args.Data);
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();

        using HttpClient client = new() { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(5) };

        DateTime deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            if (_app.HasExited)
            {
                throw new InvalidOperationException(
                    $"'{_name}' terminou com código {_app.ExitCode} antes de responder. Saída:" +
                    Environment.NewLine + log);
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
                // Ainda subindo.
            }
            catch (TaskCanceledException)
            {
                // Ainda subindo.
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException(
            $"'{_name}' não respondeu em {BaseAddress}health dentro de 60 segundos. Saída:" +
            Environment.NewLine + log);
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
            // Já terminou.
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Arquivo ainda preso pelo processo que acabou de morrer: o diretório é temporário e
            // o sistema o recolhe. Falhar aqui esconderia o resultado do teste.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }

    private static async Task<(int Exit, string Output)> RunAsync(
        string file,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(file, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                $"Não foi possível executar '{file} {arguments}'. O SDK do .NET está no PATH?");

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, await output + await error);
    }
}
