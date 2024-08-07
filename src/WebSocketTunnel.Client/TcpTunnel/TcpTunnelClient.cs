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
            .WithUrl($"{Tunnel.PublicUrl}/wsstcptunnel?clientId={tunnel.ClientId}")
            .AddMessagePackProtocol()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(logLevel);
                logging.AddConsole();
            })
            .WithAutomaticReconnect()
            .Build();

        Connection.On<TcpConnection>("NewTcpConnection", (tcpConnection) =>
        {
            Console.WriteLine($"New TCP Connection {tcpConnection.RequestId}");

            _ = HandleNewTcpConnectionAsync(tcpConnection);

            return Task.CompletedTask;
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

    private async Task HandleNewTcpConnectionAsync(TcpConnection tcpConnection)
    {
        var localClient = new TcpClient();

        try
        {
            await localClient.ConnectAsync(Tunnel.Host, Tunnel.LocalPort);

            var incomingTask = StreamIncomingAsync(localClient, tcpConnection);
            var outgoingTask = StreamOutgoingAsync(localClient, tcpConnection);

            await Task.WhenAny(incomingTask, outgoingTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling TCP connection {ex.Message}");
        }
        finally
        {
            localClient.Close();

            Console.WriteLine($"TCP Connection {tcpConnection.RequestId} done.");
        }
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

    private async Task StreamIncomingAsync(TcpClient localClient, TcpConnection tcpConnection)
    {
        var incomingTcpStream = Connection.StreamAsync<byte[]>("StreamIncomingAsync", tcpConnection);

        var localTcpStream = localClient.GetStream();

        await foreach (var chunk in incomingTcpStream)
        {
            await localTcpStream.WriteAsync(chunk);
        }

        Console.WriteLine($"Writing data to TCP connection {tcpConnection.RequestId} finished.");
    }

    private async Task StreamOutgoingAsync(TcpClient localClient, TcpConnection tcpConnection)
    {
        await Connection.InvokeAsync("StreamOutgoingAsync", StreamLocalTcpAsync(localClient, tcpConnection), tcpConnection);
    }

    private static async IAsyncEnumerable<byte[]> StreamLocalTcpAsync(TcpClient localClient, TcpConnection tcpConnection)
    {
        var tcpStream = localClient.GetStream();

        const int chunkSize = 16 * 1024;

        var buffer = new byte[chunkSize];
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

        Console.WriteLine($"Reading data from TCP connection {tcpConnection.RequestId} finished.");

        localClient.Close();
    }

    private async Task<bool> ConnectWithRetryAsync(HubConnection connection, CancellationToken token)
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
                Console.WriteLine($"Cannot connect to WebSocket server on {Tunnel.PublicUrl}");

                await Task.Delay(5000, token);
            }
        }
    }
}
