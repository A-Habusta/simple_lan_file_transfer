namespace simple_lan_file_transfer.Internals;

public abstract class ParentingConnectionManager<TChild> : IDisposable where TChild : IDisposable
{
    protected bool Disposed;
    
    private RequestListener _requestListener;
    
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
            DisposeOfAllChildren();
            
        }
        
        Disposed = true;
    }
    
    private void DisposeOfAllChildren()
    {
        _requestListener?.Dispose();
        _requestListener = null;
    }
}