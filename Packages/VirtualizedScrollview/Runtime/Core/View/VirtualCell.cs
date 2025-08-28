using System.Threading.Tasks;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using UnityEngine;

namespace OlegGrizzly.VirtualizedScrollview.Core.View
{
    public abstract class VirtualCell<T> : MonoBehaviour, IVirtualCell<T>, IPoolable
    {
        [SerializeField] private RectTransform rect;
        
        public RectTransform Rect => rect != null ? rect : rect = transform as RectTransform;
        
        public int Index { get; private set; } = -1;
        
        protected T Data { get; private set; }

        public async Task Bind(T item, int index)
        {
            Data = item;
            Index = index;
            
            await OnBound(item, index);
        }

        public void Unbind()
        {
            OnUnbound();
            
            Data = default;
            Index = -1;
        }
        
        protected abstract Task OnBound(T item, int index);
        
        protected virtual void OnUnbound() { }
        
        public virtual void OnGetFromPool() { }

        public virtual void OnReturnToPool() { }

        public virtual void OnPoolDestroy() { }
    }
}