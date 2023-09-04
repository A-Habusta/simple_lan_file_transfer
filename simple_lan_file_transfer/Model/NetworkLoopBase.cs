namespace simple_lan_file_transfer.Models;

public abstract class NetworkLoopBase : IDisposable
{
    protected bool Disposed;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _loopTask;

    protected abstract Task LoopAsync(CancellationToken cancellationToken);

    public void RunLoop()
    {
        if (Disposed) throw new ObjectDisposedException(nameof(NetworkLoopBase));

        _cancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = _cancellationTokenSource.Token;
        _loopTask = Task.Run(async () => await LoopAsync(cancellationToken), cancellationToken);
        _loopTask.ContinueWith(_ => CancellationTokenSourceDispose(), CancellationToken.None);
    }

    public void StopLoop()
    {
        _cancellationTokenSource?.Cancel();
    }

    public void Dispose()
    {
        if (Disposed) return;

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;

        if (disposing)
        {
            CancellationTokenSourceDispose();
        }

        Disposed = true;
    }

    private void CancellationTokenSourceDispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}