using System;
using OlegGrizzly.VirtualizedScrollview.Adapters;
using OlegGrizzly.VirtualizedScrollview.Core.Pooling;
using UnityEngine;
using UnityEngine.UI;

namespace OlegGrizzly.VirtualizedScrollview.Abstractions
{
    public interface IViewAdapter<T, TCell> : IDisposable where TCell : Component, IVirtualCell<T>
    {
        event Action TopLoadRequested;
        
        event Action BottomLoadRequested;
        
        void Initialize(ScrollRect scroll, RectTransform content, ComponentPool<TCell> pool, IVirtualDataSource<T> dataSource);
        
        void SetLayout(float itemHeight, float spacing = 0f, float paddingTop = 0f, float paddingBottom = 0f);

        void SetBufferItems(int before, int after);
        
        void SetDynamicHeightProvider(Func<int, float> getHeight);

        void SetLoadThresholds(float topPixels = 200f, float bottomPixels = 200f);

        void ResetLoadTriggers();
        
        void ScrollToIndex(int index, ScrollAlign align = ScrollAlign.Start);
        
        void ScrollToStart();
        
        void ScrollToEnd();
        
        void Refresh(bool keepScrollPosition = true);

        float GetItemFullHeight(int index);

        void InvalidateHeights(bool keepScrollPosition = true);
    }
}