#nullable disable
namespace WebSocketTunnel.Client.TcpTunnel;

public class TcpTunnelRequest
{
    public int LocalPort { get; set; }
    public int? PublicPort { get; set; }
    public string Host { get; set; }
    public Guid ClientId { get; set; }
    public string LocalUrl { get; set; }
    public string PublicUrl { get; set; }
}
