namespace simple_lan_file_transfer.Internals;

public interface ISelfDeletingObjectParent<in TChild> where TChild : ISelfDeletingObject<TChild>
{
    public void RemoveChild(TChild child);
}

public interface ISelfDeletingObject<in TSelf> where TSelf : ISelfDeletingObject<TSelf>
{
    public ISelfDeletingObjectParent<TSelf> Parent { get; }

    public void RemoveSelfFromParent() => Parent.RemoveChild((TSelf) this);
}