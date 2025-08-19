using System;
using OlegGrizzly.VirtualizedScrollview.Adapters;
using OlegGrizzly.VirtualizedScrollview.Core.Pooling;
using OlegGrizzly.VirtualizedScrollview.Core.View;
using UnityEngine;
using UnityEngine.UI;

namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IViewAdapter<T, TCell> where TCell : VirtualCell<T>
    {
        void Initialize(ScrollRect scroll, RectTransform content, ComponentPool<TCell> pool, IVirtualDataSource<T> dataSource);
        
        void SetLayout(float itemHeight, float spacing = 0f, float paddingTop = 0f, float paddingBottom = 0f);

        void SetOverscanItems(int before, int after);
        
        void SetDynamicHeightProvider(Func<int, float> getHeight);
        
        void ScrollToIndex(int index, ScrollAlign align = ScrollAlign.Start);
        
        void ScrollToStart();
        
        void ScrollToEnd();
        
        void Refresh(bool keepScrollPosition = true);
        
        (int start, int end) VisibleRange { get; }
        
        int TotalCount { get; }
        
        void Destroy();
    }
}