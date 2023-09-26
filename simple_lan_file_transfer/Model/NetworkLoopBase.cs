namespace simple_lan_file_transfer.Models;

/// <summary>
/// Abstract class used for running an endless network loop in a task.
/// </summary>
public abstract class NetworkLoopBase : IDisposable
{
    protected bool Disposed;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _loopTask;
    // int to allow for Interlocked operations
    private int _loopTaskRunning = False;

    private const int True = 1;
    private const int False = 0;

    protected abstract Task LoopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts running the loop specified in the <see cref="LoopAsync"/> method.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the object is disposed</exception>
    public void RunLoop()
    {
        if (Disposed) throw new ObjectDisposedException(nameof(NetworkLoopBase));

        var loopTaskRunning = Interlocked.CompareExchange(ref _loopTaskRunning, True, False);
        if (loopTaskRunning == True) return;

        _cancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = _cancellationTokenSource.Token;
        _loopTask = Task.Run(async () => await LoopAsync(cancellationToken), cancellationToken);
        _loopTask.ContinueWith(_ => CancellationTokenSourceDispose(), CancellationToken.None);
    }

    public void StopLoop()
    {
        _cancellationTokenSource?.Cancel();
        Interlocked.Exchange(ref _loopTaskRunning, False);
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