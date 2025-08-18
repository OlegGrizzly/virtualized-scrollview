using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using UnityEngine;

namespace OlegGrizzly.VirtualizedScrollview.Core
{
    public class VirtualCellPool
    {
        private readonly Stack<IVirtualCell> _pool = new();
        private readonly HashSet<IVirtualCell> _inPool = new();
        private readonly IVirtualCellFactory _factory;
        private readonly Transform _parent;

        public VirtualCellPool(IVirtualCellFactory factory, Transform parent)
        {
            _factory = factory; 
            _parent = parent;
        }

        public IVirtualCell Get()
        {
            var view = _pool.Count > 0 ? _pool.Pop() : _factory.Create(_parent);
            _inPool.Remove(view);
            
            if (view.Rect.transform.parent != _parent)
            {
                view.Rect.SetParent(_parent, false);
            }
            
            view.Rect.gameObject.SetActive(true);

            return view;
        }

        public void Release(IVirtualCell view)
        {
            if (view == null || _inPool.Contains(view)) return; 

            view.Unbind();
            view.Rect.gameObject.SetActive(false);

            _pool.Push(view);
            _inPool.Add(view);
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var v = _pool.Pop();
                _inPool.Remove(v);
                _factory.Destroy(v);
            }
            
            _inPool.Clear();
        }
    }
}