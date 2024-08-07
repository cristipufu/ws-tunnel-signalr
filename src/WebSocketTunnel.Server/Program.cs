using WebSocketTunnel.Server;
using WebSocketTunnel.Server.HttpTunnel;
using WebSocketTunnel.Server.TcpTunnel;

var builder = WebApplication.CreateBuilder(args);

builder.AddHttpTunneling();

builder.AddTcpTunneling();

builder.AddAzureSignalR();

var app = builder.Build();

app.UseStaticFiles();

app.UseFavicon();

app.UseHttpsRedirection();

app.UseHttpTunneling();

app.UseTcpTunneling();

app.Run();