using System.Net;
using System.Net.Sockets;

namespace TemplateGenerator.Matrix.Tests.Layer3;

/// <summary>
/// Reserva uma porta de <c>loopback</c> para um processo <strong>filho</strong> — a aplicação
/// gerada, que a camada 3 sobe com <c>ASPNETCORE_URLS</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aqui a corrida não tem como ser eliminada, só estreitada — e é importante dizer qual é
/// a diferença.</strong> Para um servidor que roda <em>neste</em> processo não se escolhe porta:
/// dá-se <c>:0</c> ao Kestrel e lê-se o endereço bindado depois, sem nunca soltar o socket (é o
/// que <c>InProcessOidcIssuer</c> faz). Para um processo filho isso não existe: quem binda é a
/// aplicação gerada, depois de iniciada, e o número precisa ser decidido <em>antes</em> de ela
/// subir, para entrar na variável de ambiente. Entre decidir e ela bindar há janela, e ela é do
/// desenho, não do descuido.
/// </para>
/// <para>
/// O que dá para tirar é a parte da corrida que <strong>a própria suíte cria</strong>, que é a que
/// mais acontece: a camada 3 executa várias combinações em paralelo, e duas reservas simultâneas
/// podiam receber o mesmo número — porque nenhuma das duas o estava segurando no instante em que a
/// outra perguntou. Duas mudanças fecham isso:
/// </para>
/// <list type="number">
///   <item><description>
///   <strong>Nenhuma porta é entregue duas vezes</strong> nesta execução. A lista do que já saiu
///   é do processo inteiro, e a reserva é serializada — só a escolha do número, que leva
///   microssegundos, e não a subida da aplicação.
///   </description></item>
///   <item><description>
///   <strong>A sondagem é exclusiva</strong> (<c>ExclusiveAddressUse</c>). Sem isso, no Windows, o
///   socket de sondagem aceitaria compartilhar endereço com outro que tenha pedido reuso, e a
///   porta "livre" que ele devolvesse poderia já estar ocupada.
///   </description></item>
/// </list>
/// <para>
/// O resto da janela — outro programa da máquina pegar a porta nesse intervalo — continua
/// existindo, e <strong>falha alto</strong>: a aplicação gerada não sobe, e
/// <see cref="DatabaseRuntime.StartAsync"/> derruba o teste com a saída do processo colada, em que
/// está escrito que o endereço já estava em uso. É ruído raro e legível, não um teste verde por
/// engano.
/// </para>
/// </remarks>
internal static class LoopbackPort
{
    private static readonly HashSet<int> _entregues = [];
    private static readonly Lock _portao = new();

    /// <summary>Uma porta livre de <c>loopback</c>, nunca repetida nesta execução.</summary>
    public static int Reserve()
    {
        lock (_portao)
        {
            for (int tentativa = 0; tentativa < 100; tentativa++)
            {
                using TcpListener listener = new(IPAddress.Loopback, 0);

                // Antes do `Start`: depois dele o setter recusa. Exclusivo para que o sistema não
                // devolva uma porta que outro socket já tenha com reuso.
                listener.ExclusiveAddressUse = true;

                listener.Start();

                int port = ((IPEndPoint)listener.LocalEndpoint).Port;

                listener.Stop();

                if (_entregues.Add(port))
                {
                    return port;
                }
            }

            throw new InvalidOperationException(
                $"Cem sondagens seguidas devolveram portas que esta execução já tinha entregue " +
                $"({_entregues.Count} até agora). Ou a faixa efêmera acabou, ou a sondagem parou " +
                "de pedir porta nova — nos dois casos, subir mais uma aplicação daria conflito de " +
                "endereço em vez de resultado de teste.");
        }
    }
}
