using System.Net.Sockets;
using System.Text;

class TcpClientApp
{
    static async Task Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: TcpClientApp <host> <port>");
            return;
        }

        string host = args[0];
        int port = int.Parse(args[1]);

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port);
            Console.WriteLine($"Connected to {host}:{port}");

            using NetworkStream stream = client.GetStream();

            var sendTask = SendDataAsync(stream);
            var receiveTask = ReceiveDataAsync(stream);

            await Task.WhenAll(sendTask, receiveTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
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