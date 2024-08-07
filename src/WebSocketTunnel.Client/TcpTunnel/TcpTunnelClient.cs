using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace WebSocketTunnel.Client.TcpTunnel;

public class TcpTunnelClient
{
    private readonly HubConnection Connection;
    private readonly TcpTunnelRequest Tunnel;
    private TcpTunnelResponse? _currentTunnel = null;

    public TcpTunnelClient(TcpTunnelRequest tunnel, LogLevel logLevel)
    {
        Tunnel = tunnel;

        Connection = new HubConnectionBuilder()
            .WithUrl($"{Program.PublicServerUrl}/wsstcptunnel?clientId={tunnel.ClientId}")
            .AddMessagePackProtocol()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(logLevel);
                logging.AddConsole();
            })
            .WithAutomaticReconnect()
            .Build();

        Connection.On<TcpConnection>("NewTcpConnection", async (tcpConnection) =>
        {
            try
            {
                using var localClient = new TcpClient();
                await localClient.ConnectAsync(Tunnel.Host, Tunnel.LocalPort);

                var incomingTask = StreamIncomingTcpDataAsync(localClient, tcpConnection);
                var outgoingTask = StreamOutgoingTcpDataAsync(localClient, tcpConnection);

                await Task.WhenAll(incomingTask, outgoingTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling TCP connection {ex.Message}");
            }
            finally
            {
                await Connection.InvokeAsync("CloseTcpConnectionAsync", tcpConnection);
            }
        });

        Connection.Reconnected += async connectionId =>
        {
            Console.WriteLine($"Reconnected. New ConnectionId {connectionId}");

            tunnel.PublicPort = _currentTunnel?.Port;

            _currentTunnel = await RegisterTunnelAsync(tunnel);
        };

        Connection.Closed += async (error) =>
        {
            Console.WriteLine("Connection closed... reconnecting");

            await Task.Delay(new Random().Next(0, 5) * 1000);

            if (await ConnectWithRetryAsync(Connection, CancellationToken.None))
            {
                tunnel.PublicPort = _currentTunnel?.Port;

                _currentTunnel = await RegisterTunnelAsync(tunnel);
            }
        };
    }

    public async Task ConnectAsync()
    {
        if (await ConnectWithRetryAsync(Connection, CancellationToken.None))
        {
            _currentTunnel = await RegisterTunnelAsync(Tunnel);
        }
    }

    public async Task<TcpTunnelResponse> RegisterTunnelAsync(TcpTunnelRequest tunnel)
    {
        var tunnelResponse = await Connection.InvokeAsync<TcpTunnelResponse>("RegisterTunnelAsync", tunnel);

        if (string.IsNullOrEmpty(tunnelResponse.Error))
        {
            Console.WriteLine($"Tunnel created successfully: {tunnelResponse!.TunnelUrl}");
        }
        else
        {
            Console.WriteLine($"{tunnelResponse!.Message}:{tunnelResponse.Error}");
        }

        return tunnelResponse;
    }

    private async Task StreamIncomingTcpDataAsync(TcpClient localClient, TcpConnection tcpConnection)
    {
        var incomingTcpStream = Connection.StreamAsync<byte[]>("StreamIncomingTcpDataAsync", tcpConnection);

        var localTcpStream = localClient.GetStream();

        await foreach (var chunk in incomingTcpStream)
        {
            await localTcpStream.WriteAsync(chunk);
        }
    }

    private async Task StreamOutgoingTcpDataAsync(TcpClient localClient, TcpConnection tcpConnection)
    {
        var localTcpStream = localClient.GetStream();

        await Connection.InvokeAsync("StreamOutgoingTcpDataAsync", StreamLocalTcpAsync(localTcpStream), tcpConnection);
    }

    private static async IAsyncEnumerable<byte[]> StreamLocalTcpAsync(Stream tcpStream)
    {
        var buffer = new byte[1024];
        int bytesRead;

        while ((bytesRead = await tcpStream.ReadAsync(buffer)) > 0)
        {
            if (bytesRead == buffer.Length)
            {
                yield return buffer;
            }
            else
            {
                var chunk = new byte[bytesRead];

                Array.Copy(buffer, chunk, bytesRead);

                yield return chunk;
            }
        }
    }

    private static async Task<bool> ConnectWithRetryAsync(HubConnection connection, CancellationToken token)
    {
        while (true)
        {
            try
            {
                await connection.StartAsync(token);

                Console.WriteLine($"Client connected to SignalR hub. ConnectionId: {connection.ConnectionId}");

                return true;
            }
            catch when (token.IsCancellationRequested)
            {
                return false;
            }
            catch
            {
                Console.WriteLine($"Cannot connect to WebSocket server on {Program.PublicServerUrl}");

                await Task.Delay(5000, token);
            }
        }
    }
}
