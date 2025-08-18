namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IPoolable
    {
        void OnGetFromPool();
        
        void OnReturnToPool();
        
        void OnPoolDestroy();
    }
}