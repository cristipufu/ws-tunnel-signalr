using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using WebSocketTunnel.Client.TcpTunnel;
using WebSocketTunnel.Client.HttpTunnel;

namespace WebSocketTunnel.Client;

public class Program
{
    private static readonly Guid ClientId = Guid.NewGuid();

    public static async Task Main(string[] args)
    {
        var localUrlArgument = new Argument<string>("localUrl", "The local URL to tunnel to.");

        var logLevelOption = new Option<LogLevel>(
            "--log",
            () => LogLevel.Warning,
            "The logging level (e.g., Trace, Debug, Information, Warning, Error, Critical)");

        var publicUrlOption = new Option<string>(
            "--publicUrl",
            () => "https://tunnelite.com",
            "The public server URL.");

        var rootCommand = new RootCommand
        {
            localUrlArgument,
            publicUrlOption,
            logLevelOption
        };

        rootCommand.Description = "CLI tool to create a tunnel to a local server.";

        rootCommand.SetHandler(async (string localUrl, string publicUrl, LogLevel logLevel) =>
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

            publicUrl = publicUrl.TrimEnd(['/']);

            switch (scheme)
            {
                case "tcp":

                    var tcpTunnel = new TcpTunnelRequest
                    {
                        ClientId = ClientId,
                        LocalUrl = localUrl,
                        PublicUrl = publicUrl,
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
                        PublicUrl = publicUrl,
                    };

                    var httpTunnelClient = new HttpTunnelClient(httpTunnel, logLevel);

                    await httpTunnelClient.ConnectAsync();

                    break;

                default:

                    Console.WriteLine("Error: Unsupported protocol. Use tcp:// or http(s)://");

                    return;
            }

        }, localUrlArgument, publicUrlOption, logLevelOption);

        await rootCommand.InvokeAsync(args);

        Console.ReadLine();
    }
}
