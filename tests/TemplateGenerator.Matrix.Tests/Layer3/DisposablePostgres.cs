using System.Diagnostics;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Um database PostgreSQL <strong>descartável</strong> no serviço nativo: nome único, criado no
/// início do teste e derrubado no fim, sem container e sem banco compartilhado entre execuções
/// (ADR-0005, docs/quality/test-strategy.md, "PostgreSQL nos testes").
/// </summary>
/// <remarks>
/// <para>
/// <strong>A credencial vem do ambiente, pela convenção do libpq</strong> — <c>PGHOST</c>,
/// <c>PGPORT</c>, <c>PGUSER</c> e <c>PGPASSWORD</c> —, que é a mesma que o cliente <c>psql</c> lê
/// nativamente e a forma legítima de o teste se autenticar sem senha escrita em arquivo nenhum do
/// repositório. Não há adivinhação de senha: se <c>PGPASSWORD</c> não estiver definida, o teste
/// <strong>falha com mensagem explícita</strong> dizendo o que definir (critério 6 de T05,
/// ADR-0005) — nunca é pulado, porque um teste pulado some da cobertura sem ninguém perceber.
/// </para>
/// <para>
/// A gerência do database (criar, derrubar) usa o cliente <c>psql</c> do próprio servidor nativo.
/// Ele é localizado no <c>PATH</c> ou na instalação padrão do PostgreSQL 18; não estando em lugar
/// nenhum, o teste também falha explicitamente.
/// </para>
/// </remarks>
internal sealed class DisposablePostgres : IAsyncDisposable
{
    private readonly string _psql;
    private readonly string _host;
    private readonly string _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _database;

    private DisposablePostgres(
        string psql,
        string host,
        string port,
        string user,
        string password,
        string database)
    {
        _psql = psql;
        _host = host;
        _port = port;
        _user = user;
        _password = password;
        _database = database;
    }

    /// <summary>O nome do database descartável, único por execução.</summary>
    public string Database => _database;

    /// <summary>
    /// A cadeia de conexão ADO.NET para a aplicação e o <c>dotnet ef</c>, apontando para o database
    /// descartável — a mesma chave <c>ConnectionStrings:Default</c> que o projeto gerado lê.
    /// </summary>
    public string ConnectionString =>
        $"Host={_host};Port={_port};Database={_database};Username={_user};Password={_password}";

    /// <summary>
    /// Cria o database descartável. Falha com mensagem explícita, e nunca pula, quando o
    /// <c>psql</c>, a credencial ou o próprio serviço não estão disponíveis.
    /// </summary>
    public static async Task<DisposablePostgres> CreateAsync(CancellationToken cancellationToken)
    {
        string psql = LocatePsqlOrThrow();

        string host = EnvOr("PGHOST", "localhost");
        string port = EnvOr("PGPORT", "5432");
        string user = EnvOr("PGUSER", "postgres");
        string? password = System.Environment.GetEnvironmentVariable("PGPASSWORD");

        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "O teste de PostgreSQL não tem credencial para se autenticar no serviço nativo. " +
                "Defina a variável de ambiente 'PGPASSWORD' (e, se diferirem do padrão, 'PGHOST', " +
                "'PGPORT' e 'PGUSER') com a senha de um usuário que possa criar banco (CREATEDB), " +
                "e rode a suíte de novo. Este teste FALHA de propósito em vez de pular: um teste de " +
                "PostgreSQL pulado some da cobertura sem ninguém perceber (ADR-0005; critério 6 de " +
                "T05). Adivinhar a senha não é o caminho.");
        }

        string database = "tg_matrix_" + Guid.NewGuid().ToString("N")[..12];

        DisposablePostgres instance = new(psql, host, port, user, password, database);

        (int exit, string output) = await instance.PsqlAsync(
            maintenanceDatabase: "postgres",
            sql: $"CREATE DATABASE \"{database}\";",
            cancellationToken);

        if (exit != 0)
        {
            throw new InvalidOperationException(
                $"Não foi possível criar o database descartável '{database}' no serviço PostgreSQL " +
                $"em {host}:{port} como '{user}'. Confira se o serviço 'postgresql-x64-18' está em " +
                "execução e se a credencial de 'PGPASSWORD' está correta e tem permissão de " +
                "CREATEDB. Saída do psql:" + System.Environment.NewLine + output);
        }

        return instance;
    }

    public async ValueTask DisposeAsync()
    {
        // WITH (FORCE) derruba as conexões abertas antes de apagar — PostgreSQL 13+; o serviço aqui
        // é o 18. Sem isso, uma conexão ainda viva da aplicação recém-parada impediria o DROP.
        await PsqlAsync(
            maintenanceDatabase: "postgres",
            sql: $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE);",
            CancellationToken.None);
    }

    private async Task<(int Exit, string Output)> PsqlAsync(
        string maintenanceDatabase,
        string sql,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(_psql)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("--host=" + _host);
        start.ArgumentList.Add("--port=" + _port);
        start.ArgumentList.Add("--username=" + _user);
        start.ArgumentList.Add("--dbname=" + maintenanceDatabase);
        start.ArgumentList.Add("--no-password");
        start.ArgumentList.Add("--set=ON_ERROR_STOP=1");
        start.ArgumentList.Add("--command=" + sql);

        start.Environment["PGPASSWORD"] = _password;

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException($"Não foi possível executar '{_psql}'.");

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, await output + await error);
    }

    private static string EnvOr(string name, string fallback)
    {
        string? value = System.Environment.GetEnvironmentVariable(name);

        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static string LocatePsqlOrThrow()
    {
        string executable = OperatingSystem.IsWindows() ? "psql.exe" : "psql";

        // 1. No PATH.
        string? path = System.Environment.GetEnvironmentVariable("PATH");

        if (path is not null)
        {
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory.Trim(), executable);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        // 2. Instalação padrão do PostgreSQL no Windows (ADR-0005 fixa o 18; irmãos servem se o
        //    ambiente evoluir).
        if (OperatingSystem.IsWindows())
        {
            foreach (string programFiles in (string[])
            [
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
                @"C:\Program Files\PostgreSQL",
            ])
            {
                string basePath = programFiles.EndsWith("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                    ? programFiles
                    : Path.Combine(programFiles, "PostgreSQL");

                if (!Directory.Exists(basePath))
                {
                    continue;
                }

                string[] found =
                [
                    .. Directory.EnumerateFiles(basePath, "psql.exe", SearchOption.AllDirectories)
                        .OrderByDescending(file => file, StringComparer.OrdinalIgnoreCase),
                ];

                if (found.Length > 0)
                {
                    return found[0];
                }
            }
        }

        throw new InvalidOperationException(
            "O cliente 'psql' não foi encontrado — nem no PATH, nem na instalação padrão do " +
            "PostgreSQL. O teste de PostgreSQL precisa dele para criar e derrubar o database " +
            "descartável. Instale o PostgreSQL (o serviço nativo do ADR-0005) ou acrescente o " +
            "diretório 'bin' dele ao PATH. Este teste FALHA em vez de pular (ADR-0005).");
    }
}
