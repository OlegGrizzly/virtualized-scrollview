using System.Threading.Tasks;
using UnityEngine;

namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IVirtualCell<in T>
    {
        RectTransform Rect { get; }
        
        Task Bind(T item, int index);
        
        void Unbind();
    }
}