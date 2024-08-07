using System.Collections.Concurrent;
using System.Net.Sockets;

#nullable disable
namespace WebSocketTunnel.Server.TcpTunnel;

public class TcpTunnelStore
{
    // clientId, connectionId
    public ConcurrentDictionary<Guid, string> Connections = new();

    // clientId, TcpListener
    public ConcurrentDictionary<Guid, ListenerTask> Listeners = new();

    // requestId, TcpClient
    public ConcurrentDictionary<Guid, TcpClient> Clients = new();
}

public class ListenerTask : IDisposable
{
    public TcpListener TcpListener { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; set; }

    public Task AcceptConnectionsTask { get; set; }

    public void Dispose()
    {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        TcpListener?.Dispose();
    }
}

public class TcpTunnelRequest
{
    public int LocalPort { get; set; }
    public int? PublicPort { get; set; }
    public string Host { get; set; }
    public Guid ClientId { get; set; }
    public string LocalUrl { get; set; }
}

public class TcpTunnelResponse
{
    public string TunnelUrl { get; set; }
    public int Port { get; set; }
    public string Error { get; set; }
    public string Message { get; set; }
}
