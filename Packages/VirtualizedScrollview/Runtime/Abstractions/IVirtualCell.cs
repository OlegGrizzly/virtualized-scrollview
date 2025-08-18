using UnityEngine;

namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IVirtualCell
    {
        RectTransform Rect { get; }
        
        void Bind(int dataIndex, object data);
        
        void Unbind();
    }
}