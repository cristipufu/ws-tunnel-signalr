namespace WebSocketTunnel.Server.HttpTunnel;

public class HttpDefferedRequest : IDisposable
{
    public HttpContext? HttpContext { get; set; }

    public Guid RequestId { get; set; }

    public TaskCompletionSource? TaskCompletionSource { get; set; }

    public CancellationTokenSource? TimeoutCancellationTokenSource { get; set; }

    public CancellationTokenSource? CancellationTokenSource { get; set; }

    public CancellationTokenRegistration CancellationTokenRegistration { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            HttpContext = null;
            TimeoutCancellationTokenSource?.Dispose();
            CancellationTokenSource?.Dispose();
            CancellationTokenRegistration.Dispose();
        }
    }
}
