using UnityEngine;

namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IVirtualCell<in T>
    {
        RectTransform Rect { get; }
        
        void Bind(T item, int index);
        
        void Unbind();
    }
}