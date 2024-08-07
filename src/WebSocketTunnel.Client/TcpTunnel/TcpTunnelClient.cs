using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace WebSocketTunnel.Client.TcpTunnel;

public class TcpTunnelClient
{
    private readonly HubConnection Connection;
    private readonly TcpTunnelRequest Tunnel;
    private TcpTunnelResponse? _currentTunnel = null;
    private readonly ConcurrentDictionary<Guid, TcpClient> Clients = new();

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
            var localClient = new TcpClient();

            Console.WriteLine($"New TCP connection for {Tunnel.Host}:{Tunnel.LocalPort}");

            try
            {
                await localClient.ConnectAsync(Tunnel.Host, Tunnel.LocalPort);

                Clients.TryAdd(tcpConnection.RequestId, localClient);

                var receiveDataTask = Connection.InvokeAsync("SendIncomingTcpData", Tunnel.ClientId, tcpConnection);
                var forwardDataToServerTask = StreamOutgoingTcpDataAsync(localClient, Tunnel.ClientId, tcpConnection);

                await Task.WhenAny(receiveDataTask, forwardDataToServerTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling TCP connection {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"Client NewTcpConnection finally");

                //localClient.Close();
                //await Connection.InvokeAsync("CloseTcpConnectionAsync", tcpConnection);
            }
        });


        Connection.On<TcpConnection, byte[]>("ReceiveIncomingTcpData", async (tcpConnection, bytes) =>
        {
            if (!Clients.TryGetValue(tcpConnection.RequestId, out var tcpClient))
            {
                return;
            }

            Console.WriteLine($"Client Receiving data");

            await tcpClient.GetStream().WriteAsync(bytes);
        });

        Connection.On<TcpConnection>("CloseTcpConnection", (tcpConnection) =>
        {
            if (!Clients.TryRemove(tcpConnection.RequestId, out var tcpClient))
            {
                return;
            }

            tcpClient.Close();

            Console.WriteLine($"Closed TCP connection: {tcpConnection.RequestId}");
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

    private async Task StreamOutgoingTcpDataAsync(TcpClient tcpClient, Guid clientId, TcpConnection tcpConnection)
    {
        var buffer = new byte[4096];

        var stream = tcpClient.GetStream();

        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                Console.WriteLine($"Client sending data");

                await Connection.InvokeAsync("ReceiveOutgoingTcpData", clientId, tcpConnection, buffer[..bytesRead]);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in TCP connection {tcpConnection.RequestId}: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"Client CloseTcpConnection");

            await Connection.InvokeAsync("CloseTcpConnection", clientId, tcpConnection);

            tcpClient.Close();

            Clients.TryRemove(tcpConnection.RequestId, out _);
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
