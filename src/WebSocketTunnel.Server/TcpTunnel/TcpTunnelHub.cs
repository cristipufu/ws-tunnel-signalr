using Microsoft.AspNetCore.SignalR;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;

namespace WebSocketTunnel.Server.TcpTunnel;

public class TcpTunnelHub(TcpTunnelStore tunnelStore, IHubContext<TcpTunnelHub> hubContext, ILogger<TcpTunnelHub> logger) : Hub
{
    private readonly TcpTunnelStore _tunnelStore = tunnelStore;
    private readonly IHubContext<TcpTunnelHub> _hubContext = hubContext;
    private readonly ILogger _logger = logger;

    public override Task OnConnectedAsync()
    {
        var clientId = Context.GetHttpContext()!.Request.Query["clientId"].ToString();

        _tunnelStore.Connections.AddOrUpdate(Guid.Parse(clientId), Context.ConnectionId, (key, oldValue) => Context.ConnectionId);

        return base.OnConnectedAsync();
    }

    public Task<TcpTunnelResponse> RegisterTunnelAsync(TcpTunnelRequest payload)
    {
        var response = new TcpTunnelResponse();

        try
        {
            var listenerTask = new ListenerTask
            {
                TcpListener = new TcpListener(IPAddress.Any, payload.PublicPort ?? 0),
                CancellationTokenSource = new CancellationTokenSource(),
            };

            listenerTask.TcpListener.Start();

            payload.PublicPort = ((IPEndPoint)listenerTask.TcpListener.LocalEndpoint).Port;

            listenerTask.AcceptConnectionsTask = AcceptConnectionsAsync(listenerTask.TcpListener, payload.ClientId, listenerTask.CancellationTokenSource.Token);

            _tunnelStore.Listeners.AddOrUpdate(payload.ClientId, listenerTask, (key, oldValue) => listenerTask);

            var httpContext = Context.GetHttpContext();
            var tunnelUrl = $"tcp://{httpContext!.Request.Host.Host}:{payload.PublicPort}";

            response.Port = payload.PublicPort ?? 0;
            response.TunnelUrl = tunnelUrl;
        }
        catch (Exception ex)
        {
            response.Message = "An error occurred while creating the tunnel";
            response.Error = ex.Message;
        }

        return Task.FromResult(response);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var clientIdQuery = Context.GetHttpContext()!.Request.Query["clientId"].ToString();

        var clientId = Guid.Parse(clientIdQuery);

        _tunnelStore.Connections.Remove(clientId, out var _);

        if (_tunnelStore.Listeners.TryRemove(clientId, out var listener))
        {
            listener?.Dispose();
        }
        
        return base.OnDisconnectedAsync(exception);
    }

    public Task CloseTcpConnectionAsync(TcpConnection tcpConnection)
    {
        if (!_tunnelStore.Clients.TryGetValue(tcpConnection.RequestId, out var tcpClient) || tcpClient == null)
        {
            return Task.CompletedTask;
        }

        tcpClient.Close();
        tcpClient.Dispose();

        return Task.CompletedTask;
    }

    private async Task AcceptConnectionsAsync(TcpListener listener, Guid clientId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);

                if (_tunnelStore.Connections.TryGetValue(clientId, out var connectionId))
                {
                    var tcpConnection = new TcpConnection
                    {
                        RequestId = Guid.NewGuid(),
                    };

                    _tunnelStore.Clients.TryAdd(tcpConnection.RequestId, tcpClient);

                    await _hubContext.Clients.Client(connectionId).SendAsync("NewTcpConnection", tcpConnection, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Task was cancelled, clean up
        }
        catch (ObjectDisposedException)
        {
            // Listener has been closed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }

    public async IAsyncEnumerable<byte[]> StreamIncomingTcpDataAsync(TcpConnection tcpConnection, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_tunnelStore.Clients.TryGetValue(tcpConnection.RequestId, out var tcpClient) || tcpClient == null)
        {
            yield break;
        }

        const int chunkSize = 1024;

        var buffer = new byte[chunkSize];
        int bytesRead;

        var stream = tcpClient.GetStream();

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
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

    public async Task StreamOutgoingTcpDataAsync(TcpConnection tcpConnection, IAsyncEnumerable<byte[]> stream)
    {
        if (!_tunnelStore.Clients.TryGetValue(tcpConnection.RequestId, out var tcpClient) || tcpClient == null)
        {
            return;
        }

        try
        {
            var tcpStream = tcpClient.GetStream();

            await foreach (var chunk in stream)
            {
                await tcpStream.WriteAsync(chunk);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);

            throw;
        }
    }
}
