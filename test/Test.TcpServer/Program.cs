using System.Net;
using System.Net.Sockets;
using System.Text;

class TcpServerApp
{
    static async Task Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: TcpServerApp <port>");
            return;
        }

        int port = int.Parse(args[0]);
        var listener = new TcpListener(IPAddress.Any, port);

        try
        {
            listener.Start();
            Console.WriteLine($"Server listening on port {port}");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            listener.Stop();
        }
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");

        using NetworkStream stream = client.GetStream();

        var sendDataTask = SendDataAsync(stream);
        var receiveDataTask = ReceiveDataAsync(stream);

        await Task.WhenAll(sendDataTask, receiveDataTask);

        client.Close();
        Console.WriteLine($"Client disconnected: {client.Client.RemoteEndPoint}");
    }

    static async Task SendDataAsync(NetworkStream stream)
    {
        var random = new Random();
        while (true)
        {
            string message = GenerateRandomString(random, 10);
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(buffer);
            Console.WriteLine($"Sent: {message}");
            await Task.Delay(1000);
        }
    }

    static async Task ReceiveDataAsync(NetworkStream stream)
    {
        var buffer = new byte[1024];
        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;
            Console.WriteLine($"Received: {Encoding.UTF8.GetString(buffer, 0, bytesRead)}");
        }
    }

    static string GenerateRandomString(Random random, int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}