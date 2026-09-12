using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace TemplateGenerator.Api.Tests;

/// <summary>
/// Hospeda a API geradora in-process para os testes de integração.
/// </summary>
/// <remarks>
/// <para>
/// Sem container (ADR-0005) e sem banco: o gerador é stateless, então não há estado a preparar
/// nem a limpar entre testes.
/// </para>
/// <para>
/// O ponto de extensão <see cref="Configure"/> existe para os testes de limite, que precisam de
/// uma instância com outros valores de <c>Generation:Limits</c> — e precisam dela isolada, porque
/// o limitador guarda a contagem da janela por partição dentro do host.
/// </para>
/// </remarks>
public class GeneratorApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Ajustes de serviço aplicados depois do registro da aplicação, e que portanto vencem.
    /// </summary>
    /// <remarks>
    /// Configurar por serviço, e não por arquivo de configuração, é deliberado: as opções são
    /// lidas em <c>Program.cs</c> na hora de registrar, e uma fonte de configuração acrescentada
    /// pelo host de teste entraria tarde demais para ser vista lá.
    /// </remarks>
    protected virtual void Configure(IServiceCollection services)
    {
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureTestServices(Configure);
    }
}
