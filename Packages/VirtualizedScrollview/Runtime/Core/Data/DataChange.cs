using System.Collections.Generic;

namespace OlegGrizzly.VirtualizedScrollview.Core.Data
{
    public readonly struct DataChange<T>
    {
        public readonly ChangeKind Kind;
        public readonly int Index;
        public readonly int Count;
        public readonly int FromIndex;
        public readonly int ToIndex;
        public readonly IReadOnlyList<T> Items;

        public DataChange(ChangeKind kind, int index, int count, IReadOnlyList<T> items = null)
        {
            Kind = kind;
            Index = index;
            Count = count;
            Items = items;
            FromIndex = -1;
            ToIndex = -1;
        }
        
        private DataChange(ChangeKind kind, int fromIndex, int toIndex, int count)
        {
            Kind = kind;
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Index = toIndex;
            Count = count;
            Items = null;
        }

        public static DataChange<T> MakeMove(int fromIndex, int toIndex, int count = 1)
        {
            return new DataChange<T>(ChangeKind.Move, fromIndex, toIndex, count);
        }
    }
}