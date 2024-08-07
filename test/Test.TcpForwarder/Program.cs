using System.Net;
using System.Net.Sockets;

class Program
{
    static async Task Main(string[] args)
    {
        const int listenPort = 5000;
        const int sqlServerPort = 1433; // SQL Server default port
        const string sqlServerHost = "localhost";

        var listener = new TcpListener(IPAddress.Any, listenPort);
        listener.Start();
        Console.WriteLine($"Listening on port {listenPort}. Forwarding to {sqlServerHost}:{sqlServerPort}");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client, sqlServerHost, sqlServerPort);
        }
    }

    static async Task HandleClientAsync(TcpClient client, string sqlServerHost, int sqlServerPort)
    {
        Console.WriteLine("New client connected");
        using var sqlServer = new TcpClient();
        try
        {
            await sqlServer.ConnectAsync(sqlServerHost, sqlServerPort);

            using var clientStream = client.GetStream();
            using var serverStream = sqlServer.GetStream();

            var task1 = ForwardDataAsync(clientStream, serverStream, "ClientToServer");
            var task2 = ForwardDataAsync(serverStream, clientStream, "ServerToClient");

            await Task.WhenAny(task1, task2);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            client.Close();
            sqlServer.Close();
            Console.WriteLine("Client disconnected");
        }
    }

    static async Task ForwardDataAsync(NetworkStream source, NetworkStream destination, string message)
    {
        var buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer)) > 0)
        {
            Console.WriteLine($"{message} reading and sending");

            await destination.WriteAsync(buffer, 0, bytesRead);
            await destination.FlushAsync();
        }

        Console.WriteLine($"{message} done reading");
    }
}