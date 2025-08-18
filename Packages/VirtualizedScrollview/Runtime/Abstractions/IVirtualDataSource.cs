using System;
using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Core;

namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IVirtualDataSource<T>
    {
        event Action<IReadOnlyList<DataChange<T>>> Changed;
        
        int Count { get; }
        
        T this[int index] { get; }

        string GetId(int index);
        
        bool TryGetIndexById(string id, out int index);
        
        void SetItems(IEnumerable<T> items, bool detectMoves = true, bool detectUpdates = true);
        
        void Add(T item);
        
        void Insert(int index, T item);
        
        void RemoveAt(int index, int count = 1);
        
        bool RemoveById(string id);
        
        void UpdateAt(int index, T newItem);
        
        bool UpdateById(string id, T newItem);
        
        void Move(int fromIndex, int toIndex, int count = 1);
        
        void Sort(IComparer<T> comparer);
        
        void Clear();
    }
}