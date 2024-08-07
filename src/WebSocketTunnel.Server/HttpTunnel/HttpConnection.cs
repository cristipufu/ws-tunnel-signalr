#nullable disable
namespace WebSocketTunnel.Server.HttpTunnel;

public class HttpConnection
{
    public Guid RequestId { get; set; }

    public string Method { get; set; }

    public string ContentType { get; set; }

    public string Path { get; set; }
}
