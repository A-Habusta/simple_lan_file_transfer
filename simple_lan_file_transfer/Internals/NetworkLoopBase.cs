namespace simple_lan_file_transfer.Internals;

public abstract class NetworkLoopBase : IDisposable
{
    protected bool Disposed;
    
    private CancellationTokenSource? _cancellationTokenSource;
    protected Task? LoopTask;
    
    protected abstract Task LoopAsync(CancellationToken cancellationToken);

    public void Run()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = _cancellationTokenSource.Token;
        LoopTask = Task.Run(async () => await LoopAsync(cancellationToken), cancellationToken); 
    }
    
    public void Stop()
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