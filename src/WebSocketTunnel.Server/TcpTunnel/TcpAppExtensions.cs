namespace WebSocketTunnel.Server.TcpTunnel;

public static class TcpAppExtensions
{
    public static void UseTcpTunneling(this WebApplication app)
    {
        app.MapHub<TcpTunnelHub>("/wsstcptunnel");
    }
}
