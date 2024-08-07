using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using WebSocketTunnel.Client.TcpTunnel;
using WebSocketTunnel.Client.HttpTunnel;

namespace WebSocketTunnel.Client;

public class Program
{
    private static readonly Guid ClientId = Guid.NewGuid();
    //public static readonly string PublicServerUrl = "https://tunnelite.com";
    public static readonly string PublicServerUrl = "https://localhost:7193";

    public static async Task Main(string[] args)
    {
        var localUrlArgument = new Argument<string>("localUrl", "The local URL to tunnel to.");
        var logLevelOption = new Option<LogLevel>(
            "--log",
            () => LogLevel.Warning,
            "The logging level (e.g., Trace, Debug, Information, Warning, Error, Critical)");

        var rootCommand = new RootCommand
        {
            localUrlArgument,
            logLevelOption
        };

        rootCommand.Description = "CLI tool to create a tunnel to a local server.";

        rootCommand.SetHandler(async (string localUrl, LogLevel logLevel) =>
        {
            if (string.IsNullOrWhiteSpace(localUrl))
            {
                Console.WriteLine("Error: Local URL is required.");
                return;
            }

            Uri uri;
            try
            {
                uri = new Uri(localUrl);
            }
            catch (UriFormatException)
            {
                Console.WriteLine("Error: Invalid URL format.");
                return;
            }

            var scheme = uri.Scheme.ToLowerInvariant();

            switch (scheme)
            {
                case "tcp":

                    var tcpTunnel = new TcpTunnelRequest
                    {
                        ClientId = ClientId,
                        LocalUrl = localUrl,
                        Host = uri.Host,
                        LocalPort = uri.Port,
                    };

                    var tcpTunnelClient = new TcpTunnelClient(tcpTunnel, logLevel);

                    await tcpTunnelClient.ConnectAsync();

                    break;

                case "http":
                case "https":

                    var httpTunnel = new HttpTunnelRequest
                    {
                        ClientId = ClientId,
                        LocalUrl = localUrl,
                    };

                    var httpTunnelClient = new HttpTunnelClient(httpTunnel, logLevel);

                    await httpTunnelClient.ConnectAsync();

                    break;

                default:

                    Console.WriteLine("Error: Unsupported protocol. Use tcp:// or http(s)://");

                    return;
            }

        }, localUrlArgument, logLevelOption);

        await rootCommand.InvokeAsync(args);

        Console.ReadLine();
    }
}
