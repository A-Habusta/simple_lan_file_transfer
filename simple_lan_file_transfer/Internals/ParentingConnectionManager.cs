namespace simple_lan_file_transfer.Internals;

using System.Collections.Concurrent;

public abstract class ParentingConnectionManager<TChild> : IDisposable where TChild : IDisposable 
{
    protected bool Disposed;
    
    private RequestListener _requestListener;
    private readonly ConcurrentBag<TChild> _children = new();
    
    protected ParentingConnectionManager(int port)
    {
        _requestListener = new RequestListener(port, HandleNewConnectionRequestAsync);
    }

    protected abstract Task HandleNewConnectionRequestAsync(Socket socket, CancellationToken cancellationToken = default);
    protected abstract Task<TChild> StartNewConnectionAsync(IPAddress ipAddress, int port, CancellationToken cancellationToken = default);
    
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
            _requestListener.Dispose();
            DisposeOfAllChildren();
        }
        
        Disposed = true;
    }

    private void DisposeOfAllChildren()
    {
        foreach (TChild child in _children)
        {
            child.Dispose();
        }
    }
}