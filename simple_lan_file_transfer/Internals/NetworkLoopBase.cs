namespace simple_lan_file_transfer.Internals;

public abstract class NetworkLoopBase
{
    private CancellationTokenSource? _cancellationTokenSource;
    private Task _loopTask;
    
    protected abstract void Loop(CancellationToken cancellationToken);

    public void Run()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        
        _loopTask = Task.Run(() => Loop(_cancellationTokenSource.Token));
        _loopTask.ContinueWith(_ => CancellationTokenSourceDispose());
    }
    
    public void Stop()
    {
        if (_cancellationTokenSource?.IsCancellationRequested == false)
        {
            _cancellationTokenSource.Cancel();
        }
    }
    
    private void CancellationTokenSourceDispose()
    {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null; 
    }
}