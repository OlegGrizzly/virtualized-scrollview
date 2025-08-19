using System;
using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using OlegGrizzly.VirtualizedScrollview.Core.Data;

namespace OlegGrizzly.VirtualizedScrollview.Core.Sources
{
    public class VirtualDataSource<T> : IVirtualDataSource<T>
    {
        private readonly List<Entry> _list = new();
        private readonly Dictionary<string, int> _indexById = new();
        private readonly Func<T, string> _keySelector;
        private readonly IEqualityComparer<T> _itemComparer;
        private readonly List<DataChange<T>> _cachePool = new(1);

        public event Action<IReadOnlyList<DataChange<T>>> Changed;

        public VirtualDataSource(Func<T, string> keySelector, IEqualityComparer<T> itemComparer = null)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _itemComparer = itemComparer ?? EqualityComparer<T>.Default;
        }

        public int Count => _list.Count;

        public T this[int index] => _list[index].Item;

        public string GetId(int index) => _list[index].Id;

        public bool TryGetIndexById(string id, out int index) => _indexById.TryGetValue(id, out index);
        
        public void Add(T item) => Insert(_list.Count, item);
        
        public void Insert(int index, T item)
        {
            if (index < 0 || index > _list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be in range [0..{_list.Count}].");
            }
            
            var id = EnsureUniqueId(item);
            _list.Insert(index, new Entry(id, item));
            
            ReindexFrom(index);
            Emit(new DataChange<T>(ChangeKind.Insert, index, 1, new[] { item }));
        }
        
        public void RemoveAt(int index, int count = 1)
        {
            if (count <= 0) return;
            if (index < 0 || index > _list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            
            var max = Math.Min(count, _list.Count - index);
            if (max <= 0) return;

            for (var i = 0; i < max; i++)
            {
                _indexById.Remove(_list[index].Id);
                _list.RemoveAt(index);
            }
            
            ReindexFrom(index);
            Emit(new DataChange<T>(ChangeKind.Remove, index, max));
        }
        
        public bool RemoveById(string id)
        {
            if (!_indexById.TryGetValue(id, out var index)) return false;
            
            RemoveAt(index);
            
            return true;
        }
        
        public void UpdateAt(int index, T newItem)
        {
            if (index < 0 || index >= _list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            
            var old = _list[index];
            var newId = _keySelector(newItem);
            if (newId != old.Id)
            {
                throw new InvalidOperationException("UpdateAt must preserve the item's stable id.");
            }

            _list[index] = new Entry(newId, newItem);
            
            Emit(new DataChange<T>(ChangeKind.Update, index, 1, new[] { newItem }));
        }

        public bool UpdateById(string id, T newItem)
        {
            if (!_indexById.TryGetValue(id, out var index)) return false;
            UpdateAt(index, newItem);
            
            return true;
        }
        
        public void Move(int fromIndex, int toIndex, int count = 1)
        {
            if (count <= 0 || fromIndex == toIndex) return;

            var n = _list.Count;
            if (fromIndex < 0 || fromIndex >= n) throw new ArgumentOutOfRangeException(nameof(fromIndex));
            if (count > n - fromIndex) throw new ArgumentOutOfRangeException(nameof(count));
            if (toIndex < 0 || toIndex > n) throw new ArgumentOutOfRangeException(nameof(toIndex));
            
            if (toIndex >= fromIndex && toIndex < fromIndex + count) return;
            
            var block = _list.GetRange(fromIndex, count);
            _list.RemoveRange(fromIndex, count);
            
            var newCount = n - count;
            var finalTo = toIndex;
            if (finalTo > newCount) finalTo = newCount;
            
            _list.InsertRange(finalTo, block);

            ReindexFrom(Math.Min(fromIndex, finalTo));
            Emit(DataChange<T>.MakeMove(fromIndex, finalTo, count));
        }
        
        public void Sort(IComparer<T> comparer)
        {
            comparer ??= Comparer<T>.Default;
           
            var target = new List<Entry>(_list);
            target.Sort((a, b) => comparer.Compare(a.Item, b.Item));
            
            ApplyOrder(target);
        }
        
        public void Clear()
        {
            if (_list.Count == 0) return;
            
            var removedCount = _list.Count;
            
            _list.Clear();
            _indexById.Clear();
            
            Emit(new DataChange<T>(ChangeKind.Remove, 0, removedCount));
        }
        
        public void SetItems(IEnumerable<T> items, bool detectMoves = true, bool detectUpdates = true)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            
            var target = new List<Entry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in items)
            {
                var id = _keySelector(it) ?? throw new InvalidOperationException("Key selector returned null.");
                if (!seen.Add(id))
                {
                    throw new InvalidOperationException($"Duplicate id detected in SetItems: '{id}'.");
                }
                
                target.Add(new Entry(id, it));
            }

            var changes = new List<DataChange<T>>();
            
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in target)
            {
                present.Add(e.Id);
            }

            var i = _list.Count - 1;
            int runCount = 0, runEnd = -1;
            
            while (i >= 0)
            {
                if (!present.Contains(_list[i].Id))
                {
                    if (runEnd == -1) runEnd = i;
                    
                    runCount++;
                }
                else if (runCount > 0)
                {
                    var start = i + 1;
                    _list.RemoveRange(start, runCount);
                    
                    changes.Add(new DataChange<T>(ChangeKind.Remove, start, runCount));
                    
                    runCount = 0; 
                    runEnd = -1;
                }
                
                i--;
            }
            
            if (runCount > 0)
            {
                var start = 0;
                _list.RemoveRange(start, runCount);
                
                changes.Add(new DataChange<T>(ChangeKind.Remove, start, runCount));
            }
            
            ReindexAll();
            
            for (var t = 0; t < target.Count; t++)
            {
                if (t < _list.Count && _list[t].Id == target[t].Id)
                {
                    if (detectUpdates && !_itemComparer.Equals(_list[t].Item, target[t].Item))
                    {
                        _list[t] = target[t];
                        changes.Add(new DataChange<T>(ChangeKind.Update, t, 1, new[] { target[t].Item }));
                    }
                    
                    continue;
                }
                
                if (_indexById.TryGetValue(target[t].Id, out var curIdx))
                {
                    if (detectMoves)
                    {
                        var entry = _list[curIdx];
                        _list.RemoveAt(curIdx);
                        
                        if (t > curIdx) t--;
                        _list.Insert(t, entry);
                        
                        ReindexFrom(Math.Min(curIdx, t));
                        
                        changes.Add(DataChange<T>.MakeMove(curIdx, t));
                        
                        if (detectUpdates && !_itemComparer.Equals(_list[t].Item, target[t].Item))
                        {
                            _list[t] = target[t];
                            changes.Add(new DataChange<T>(ChangeKind.Update, t, 1, new[] { target[t].Item }));
                        }
                        
                        continue;
                    }
                }
                
                _list.Insert(t, target[t]);
                
                ReindexFrom(t);
                
                changes.Add(new DataChange<T>(ChangeKind.Insert, t, 1, new[] { target[t].Item }));
            }
            
            if (_list.Count > target.Count)
            {
                var extra = _list.Count - target.Count;
                var start = target.Count;
                _list.RemoveRange(start, extra);
                
                ReindexFrom(start);
                
                changes.Add(new DataChange<T>(ChangeKind.Remove, start, extra));
            }
            
            if (detectUpdates && _list.Count == target.Count)
            {
                for (var k = 0; k < _list.Count; k++)
                {
                    if (_list[k].Id == target[k].Id && !_itemComparer.Equals(_list[k].Item, target[k].Item))
                    {
                        _list[k] = target[k];
                        
                        changes.Add(new DataChange<T>(ChangeKind.Update, k, 1, new[] { target[k].Item }));
                    }
                }
            }

            if (changes.Count > 0)
            {
                Emit(changes);
            }
        }
        
        private struct Entry
        {
            public readonly string Id;
            public readonly T Item;

            public Entry(string id, T item)
            {
                Id = id; 
                Item = item;
            }
        }

        private string EnsureUniqueId(T item)
        {
            var id = _keySelector(item) ?? throw new InvalidOperationException("Key selector returned null.");
            if (_indexById.ContainsKey(id))
            {
                throw new InvalidOperationException($"Item with id '{id}' already exists.");
            }
            
            return id;
        }

        private void ReindexAll()
        {
            _indexById.Clear();
            
            for (var i = 0; i < _list.Count; i++)
            {
                _indexById[_list[i].Id] = i;
            }
        }

        private void ReindexFrom(int start)
        {
            if (start < 0) start = 0;
            
            for (var i = start; i < _list.Count; i++)
            {
                _indexById[_list[i].Id] = i;
            }
        }

        private void Emit(DataChange<T> change)
        {
            Changed?.Invoke(_cacheList(change));
            
            _cachePool.Clear();
        }

        private void Emit(List<DataChange<T>> changes)
        {
            if (changes.Count == 0) return;
            
            Changed?.Invoke(changes);
            
            _cachePool.Clear();
        }
        
        private IReadOnlyList<DataChange<T>> _cacheList(DataChange<T> c)
        {
            _cachePool.Clear();
            _cachePool.Add(c);
            
            return _cachePool;
        }

        private void ApplyOrder(List<Entry> target)
        {
            SetItems(EnumerateItems(target), detectMoves: true, detectUpdates: false);

            IEnumerable<T> EnumerateItems(List<Entry> entries)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    yield return entries[i].Item;
                }
            }
        }
    }
}