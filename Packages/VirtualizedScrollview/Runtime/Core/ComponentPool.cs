using System;
using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace OlegGrizzly.VirtualizedScrollview.Core
{
    public sealed class ComponentPool<T> : IDisposable where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _stack = new();
        private readonly HashSet<T> _inUse = new();
        private readonly int _capacity;
        private readonly Dictionary<T, IPoolable> _poolables = new();
        
        public ComponentPool(T prefab, Transform activeParent, int preWarm = 0, int capacity = int.MaxValue)
        {
            _prefab = prefab ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = activeParent ? activeParent : throw new ArgumentNullException(nameof(activeParent));
            _capacity = Mathf.Max(0, capacity);

            if (preWarm > 0)
            {
                PreWarm(preWarm);
            }
        }

        private void PreWarm(int count)
        {
            var toCreate = Mathf.Min(count, Mathf.Max(0, _capacity - _stack.Count));
            for (var i = 0; i < toCreate; i++)
            {
                var inst = Object.Instantiate(_prefab, _parent, false);
                inst.gameObject.SetActive(false);
                
                if (inst.TryGetComponent<IPoolable>(out var poolable))
                {
                    _poolables[inst] = poolable;
                }
                
                _stack.Push(inst);
            }
        }

        public T Get()
        {
            var inst = _stack.Count > 0 ? _stack.Pop() : CreateNew();
            
            inst.transform.SetParent(_parent, false);
            if (!inst.gameObject.activeSelf)
            {
                inst.gameObject.SetActive(true);
            }

            if (_poolables.TryGetValue(inst, out var poolable))
            {
                try
                {
                    poolable.OnGetFromPool();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            _inUse.Add(inst);
            
            return inst;
        }

        public void Release(T inst)
        {
            if (!inst) return;
            if (!_inUse.Remove(inst)) return;

            inst.gameObject.SetActive(false);
            inst.transform.SetParent(_parent, false);

            if (_poolables.TryGetValue(inst, out var poolable))
            {
                try
                {
                    poolable.OnReturnToPool();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            _stack.Push(inst);
            
            var currentlyAlive = _inUse.Count + _stack.Count;
            if (currentlyAlive > _capacity)
            {
                var needToRemove = currentlyAlive - _capacity;                 
                var keepPooled   = Mathf.Max(0, _stack.Count - needToRemove); 
                
                CullExcess(keepPooled);
            }
        }
        
        public void ReleaseAll()
        {
            var temp = ListPool<T>.Get();
            try
            {
                temp.AddRange(_inUse);
                foreach (var inst in temp)
                {
                    Release(inst);
                }
            }
            finally
            {
                ListPool<T>.Release(temp);
            }
        }
        
        public void ClearAndDestroy()
        {
            foreach (var inst in _inUse)
            {
                if (_poolables.TryGetValue(inst, out var poolable))
                {
                    try { poolable.OnPoolDestroy(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                if (inst)
                {
                    Object.Destroy(inst.gameObject);
                }
                _poolables.Remove(inst);
            }
            
            _inUse.Clear();
            
            while (_stack.Count > 0)
            {
                var inst = _stack.Pop();
                if (_poolables.TryGetValue(inst, out var poolable))
                {
                    try { poolable.OnPoolDestroy(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                _poolables.Remove(inst);
                if (inst)
                {
                    Object.Destroy(inst.gameObject);
                }
            }
            _poolables.Clear();
        }

        private T CreateNew()
        {
            var inst = Object.Instantiate(_prefab, _parent, false);
            inst.gameObject.SetActive(false);

            if (inst.TryGetComponent<IPoolable>(out var poolable))
            {
                _poolables[inst] = poolable;
            }
            
            return inst;
        }
        
        private void CullExcess(int keepPooled)
        {
            keepPooled = Mathf.Max(0, keepPooled);
            while (_stack.Count > keepPooled)
            {
                var inst = _stack.Pop();
                if (_poolables.TryGetValue(inst, out var poolable))
                {
                    try { poolable.OnPoolDestroy(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                _poolables.Remove(inst);
                
                if (inst)
                {
                    Object.Destroy(inst.gameObject);
                }
            }
        }

        public void Dispose()
        {
            ClearAndDestroy();
        }
    }
}