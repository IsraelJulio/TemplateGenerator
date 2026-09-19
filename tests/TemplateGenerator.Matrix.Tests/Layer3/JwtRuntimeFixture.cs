using System.Net.Http.Headers;
using TemplateGenerator.Generation;
using Xunit;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Uma combinação com <c>auth/jwt</c> pronta para ser exercitada: o emissor OIDC in-process no ar,
/// o pacote extraído, migrado quando há banco, compilado e <strong>em execução</strong> com
/// <c>Jwt:Authority</c> e <c>Jwt:Audience</c> apontando para o emissor.
/// </summary>
/// <remarks>
/// <para>
/// É <c>IClassFixture</c>, e não montagem por teste, porque <em>subir</em> é caro — restore, build
/// e o processo da aplicação — e os seis testes de <see cref="JwtRuntimeTests"/> falam da
/// <strong>mesma</strong> aplicação. Repetir a montagem seis vezes multiplicaria por seis o custo
/// da camada, que é justamente a que precisa continuar sendo executável em toda tarefa.
/// </para>
/// <para>
/// <strong>A configuração entra pelo ambiente</strong>, com os nomes que o README ensina
/// (<c>Jwt__Authority</c> e <c>Jwt__Audience</c>) — nenhum arquivo do projeto gerado é editado, que
/// é a regra da camada 3: os comandos são executados como uma pessoa os executaria.
/// </para>
/// <para>
/// <strong>Sem banco também passa por aqui.</strong> Os passos de migração do README só existem
/// quando há banco; a combinação <c>simple + none + jwt</c> pula essa etapa e faz exatamente o
/// resto. É o critério de aceite 5 de T07 — "funciona com qualquer banco, inclusive
/// <c>none</c>" — montado de modo que a diferença entre os dois casos seja <em>uma linha</em>, e
/// não dois caminhos de código que podem divergir.
/// </para>
/// <para>
/// <strong>Nada é pulado</strong>: qualquer passo que falhe derruba a fixture, e com ela todos os
/// testes da classe, com a saída do comando na mensagem.
/// </para>
/// </remarks>
public abstract class JwtRuntimeFixture : IAsyncLifetime
{
    /// <summary>O valor de <c>database</c> que dispensa migração.</summary>
    private const string SemBanco = "none";

    private DatabaseRuntime? _runtime;
    private InProcessOidcIssuer? _issuer;

    /// <summary>A combinação desta fixture.</summary>
    protected abstract GenerationRequest Request { get; }

    /// <summary>O emissor OIDC de teste no ar, com as chaves desta execução.</summary>
    public InProcessOidcIssuer Issuer => _issuer
        ?? throw new InvalidOperationException("O emissor OIDC de teste não foi iniciado.");

    /// <summary>Se esta combinação pediu Swagger.</summary>
    public bool Swagger => Request.Swagger;

    /// <summary>Descrição curta da combinação, para mensagem de falha.</summary>
    public string Descricao => GeneratedPackage.Describe(Request);

    /// <summary>
    /// Um cliente apontado para a aplicação em execução, com o <c>Authorization: Bearer</c> de
    /// <paramref name="token"/> quando houver token.
    /// </summary>
    public HttpClient Client(string? token = null)
    {
        HttpClient client = (_runtime
            ?? throw new InvalidOperationException("A aplicação gerada não foi iniciada.")).Client();

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token);
        }

        return client;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Sem token de cancelamento do teste aqui, de propósito: a fixture é construída fora do
        // ciclo de vida de um teste em particular, e os passos abaixo já falham por conta própria
        // — `StartAsync` tem prazo de 60 segundos, e cada comando externo devolve o código de saída.
        CancellationToken cancellationToken = CancellationToken.None;

        _issuer = await InProcessOidcIssuer.StartAsync(cancellationToken);

        _runtime = await DatabaseRuntime.ExtractAsync(Request, cancellationToken);

        // Exatamente as duas variáveis que a seção "Autenticação" do README manda definir.
        _runtime.Environment["Jwt__Authority"] = _issuer.Authority;
        _runtime.Environment["Jwt__Audience"] = InProcessOidcIssuer.Audience;

        if (!string.Equals(Request.Database, SemBanco, StringComparison.Ordinal))
        {
            await _runtime.RunReadmeMigrationStepsAsync(cancellationToken);
        }

        await _runtime.BuildAsync(cancellationToken);
        await _runtime.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_runtime is not null)
        {
            await _runtime.DisposeAsync();
        }

        if (_issuer is not null)
        {
            await _issuer.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
