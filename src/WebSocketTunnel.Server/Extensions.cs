using WebSocketTunnel.Server.HttpTunnel;
using WebSocketTunnel.Server.TcpTunnel;

namespace WebSocketTunnel.Server;

public static class Extensions
{
    public static void AddHttpTunneling(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<HttpTunnelStore>();
        builder.Services.AddSingleton<HttpRequestsQueue>();
    }

    public static void AddTcpTunneling(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<TcpTunnelStore>();
    }

    public static void AddAzureSignalR(this WebApplicationBuilder builder)
    {
        var signalRConnectionString = builder.Configuration.GetConnectionString("AzureSignalR");

        var signalRBuilder = builder.Services.AddSignalR(hubOptions =>
        {
            hubOptions.EnableDetailedErrors = true;
        }).AddMessagePackProtocol();

        if (!string.IsNullOrEmpty(signalRConnectionString))
        {
            signalRBuilder.AddAzureSignalR(opt =>
            {
                opt.ConnectionString = signalRConnectionString;
            });
        }
    }

    public static void UseFavicon(this WebApplication app)
    {
        app.MapGet("/favicon.ico", async context =>
        {
            context.Response.ContentType = "image/x-icon";
            await context.Response.SendFileAsync("wwwroot/favicon.ico");
        });
    }
}
