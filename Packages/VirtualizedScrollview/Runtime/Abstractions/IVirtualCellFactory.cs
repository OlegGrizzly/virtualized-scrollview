using UnityEngine;

namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IVirtualCellFactory
    {
        IVirtualCell Create(Transform parent);
        
        void Destroy(IVirtualCell cell);
    }
}