using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WebSocketTunnel.Client.HttpTunnel;

public class HttpTunnelClient
{
    private static readonly HttpClientHandler LocalHttpClientHandler = new()
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true,
    };
    private static readonly HttpClient ServerHttpClient = new();
    private static readonly HttpClient LocalHttpClient = new(LocalHttpClientHandler);

    private HttpTunnelResponse? _currentTunnel = null;
    private readonly HubConnection Connection;
    private readonly HttpTunnelRequest Tunnel;

    public HttpTunnelClient(HttpTunnelRequest tunnel, LogLevel logLevel)
    {
        Tunnel = tunnel;

        Connection = new HubConnectionBuilder()
            .WithUrl($"{Program.PublicServerUrl}/wsshttptunnel?clientId={tunnel.ClientId}")
            .AddMessagePackProtocol()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(logLevel);
                logging.AddConsole();
            })
            .WithAutomaticReconnect()
            .Build();

        Connection.On<HttpConnection>("NewHttpConnection", async (httpConnection) =>
        {
            Console.WriteLine($"Received http tunneling request: [{httpConnection.Method}]{httpConnection.Path}");

            await TunnelConnectionAsync(httpConnection);
        });

        Connection.Reconnected += async connectionId =>
        {
            Console.WriteLine($"Reconnected. New ConnectionId {connectionId}");

            tunnel.Subdomain = _currentTunnel?.Subdomain;

            _currentTunnel = await RegisterTunnelAsync(tunnel);
        };

        Connection.Closed += async (error) =>
        {
            Console.WriteLine("Connection closed... reconnecting");

            await Task.Delay(new Random().Next(0, 5) * 1000);

            if (await ConnectWithRetryAsync(Connection, CancellationToken.None))
            {
                tunnel.Subdomain = _currentTunnel?.Subdomain;

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

    private static async Task TunnelConnectionAsync(HttpConnection httpConnection)
    {
        var publicUrl = Program.PublicServerUrl;

        try
        {
            // Start the request to the public server
            using var publicResponse = await ServerHttpClient.GetAsync(
                $"{publicUrl}/tunnelite/request/{httpConnection.RequestId}",
                HttpCompletionOption.ResponseHeadersRead);

            publicResponse.EnsureSuccessStatusCode();

            // Prepare the request to the local server
            var localRequest = new HttpRequestMessage(new HttpMethod(httpConnection.Method), httpConnection.Path);

            // Copy headers from public response to local request
            foreach (var header in publicResponse.Headers)
            {
                if (header.Key.StartsWith("X-TR-"))
                {
                    localRequest.Headers.TryAddWithoutValidation(header.Key[5..], header.Value);
                }
            }

            // Set the content of the local request to stream the data from the public response
            localRequest.Content = new StreamContent(await publicResponse.Content.ReadAsStreamAsync());

            if (httpConnection.ContentType != null)
            {
                localRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(httpConnection.ContentType);
            }

            // Send the request to the local server and get the response
            using var localResponse = await LocalHttpClient.SendAsync(localRequest);

            // Prepare the request back to the public server
            var publicRequest = new HttpRequestMessage(HttpMethod.Post, $"{publicUrl}/tunnelite/request/{httpConnection.RequestId}");

            // Set the status code
            publicRequest.Headers.Add("X-T-Status", ((int)localResponse.StatusCode).ToString());

            // Copy headers from local response to public request
            foreach (var header in localResponse.Headers)
            {
                publicRequest.Headers.TryAddWithoutValidation($"X-TR-{header.Key}", header.Value);
            }

            // Copy content headers from local response to public request
            foreach (var header in localResponse.Content.Headers)
            {
                publicRequest.Headers.TryAddWithoutValidation($"X-TC-{header.Key}", header.Value);
            }

            // Set the content of the public request to stream from the local response
            publicRequest.Content = new StreamContent(await localResponse.Content.ReadAsStreamAsync());

            // Send the response back to the public server
            using var response = await ServerHttpClient.SendAsync(publicRequest);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error tunneling request: {ex.Message}");

            // todo complete deferred request with error
        }
    }

    private static async Task<HttpTunnelResponse> RegisterTunnelAsync(HttpTunnelRequest tunnel)
    {
        var response = await ServerHttpClient.PostAsJsonAsync($"{Program.PublicServerUrl}/tunnelite/tunnel", tunnel);

        var content = await response.Content.ReadFromJsonAsync<HttpTunnelResponse>();

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Tunnel created successfully: {content!.TunnelUrl}");
        }
        else
        {
            Console.WriteLine($"{content!.Message}:{content.Error}");
        }

        return content;
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
