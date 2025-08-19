using System;
using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using OlegGrizzly.VirtualizedScrollview.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OlegGrizzly.VirtualizedScrollview.Adapters
{
    public sealed class VerticalViewAdapter<T, TCell> : IViewAdapter<T, TCell> where TCell : VirtualCell<T>
    {
        private ScrollRect _scroll;
        private RectTransform _viewport;
        private RectTransform _content;
        private ComponentPool<TCell> _pool;
        private IVirtualDataSource<T> _data;

        private readonly Dictionary<int, TCell> _visible = new();
        private readonly List<int> _toRemove = new(32);
        
        private float _itemHeight = 100f;
        private float _spacing;
        private float _paddingTop;
        private float _paddingBottom;
        
        private Func<int, float> _getDynamicHeight;

        private readonly List<float> _heights = new();
        private readonly List<float> _prefix = new();
        
        private int _overscanBeforeItems; 
        private int _overscanAfterItems;  
        
        private int _lastCount;

        public (int start, int end) VisibleRange { get; private set; } = (-1, -1);
        
        public int TotalCount => _data?.Count ?? 0;

        public void Initialize(ScrollRect scroll, RectTransform content, ComponentPool<TCell> pool, IVirtualDataSource<T> dataSource)
        {
            _scroll = scroll ? scroll : throw new ArgumentNullException(nameof(scroll));
            _viewport = _scroll.viewport ? _scroll.viewport : _scroll.GetComponent<RectTransform>();
            _content = content ? content : throw new ArgumentNullException(nameof(content));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _data = dataSource ?? throw new ArgumentNullException(nameof(dataSource));

            if (_scroll.horizontal)
            {
                _scroll.horizontal = false;
            }
            
            _scroll.onValueChanged.AddListener(OnScrollChanged);
            _data.Changed += OnDataChanged;

            _lastCount = TotalCount;

            RebuildCaches();
            Refresh(keepScrollPosition: false);
        }

        public void SetLayout(float itemHeight, float spacing = 0f, float paddingTop = 0f, float paddingBottom = 0f)
        {
            _itemHeight = Mathf.Max(0f, itemHeight);
            _spacing = Mathf.Max(0f, spacing);
            _paddingTop = Mathf.Max(0f, paddingTop);
            _paddingBottom = Mathf.Max(0f, paddingBottom);

            RebuildCaches();
            Refresh();
        }

        public void SetDynamicHeightProvider(Func<int, float> getHeight)
        {
            _getDynamicHeight = getHeight;
            
            RebuildCaches();
            Refresh();
        }
        
        public void SetOverscanItems(int before, int after)
        {
            _overscanBeforeItems = Mathf.Max(0, before);
            _overscanAfterItems  = Mathf.Max(0, after);
            
            UpdateVisible();
        }

        public void Refresh(bool keepScrollPosition = true)
        {
            if (_scroll == null) return;
            
            var savedNormPos = _scroll.normalizedPosition;
            
            UpdateContentSize();
            
            _toRemove.Clear();
            
            foreach (var kv in _visible)
            {
                _toRemove.Add(kv.Key);
            }
            
            foreach (var idx in _toRemove)
            {
                var cell = _visible[idx];
                SafeUnbind(cell);
                
                _pool.Release(cell);
                _visible.Remove(idx);
            }
            
            _toRemove.Clear();

            _scroll.normalizedPosition = keepScrollPosition ? savedNormPos : new Vector2(0f, 1f);

            UpdateVisible();
        }

        public void ScrollToIndex(int index, ScrollAlign align = ScrollAlign.Start)
        {
            if (_scroll == null || TotalCount == 0) return;
            
            index = Mathf.Clamp(index, 0, TotalCount - 1);

            var alignValue = align switch
            {
                ScrollAlign.Start => 0f,
                ScrollAlign.Center => 0.5f,
                ScrollAlign.End => 1f,
                _ => 0f
            };

            var targetY = GetItemTopY(index);
            var itemSize = GetItemFullHeight(index);

            var viewportHeight = _viewport.rect.height;
            var contentHeight = _content.rect.height;
            
            var viewportTopY = targetY - alignValue * Mathf.Max(0f, viewportHeight - itemSize);
            var normalized = contentHeight <= viewportHeight ? 1f : 1f - Mathf.Clamp01(viewportTopY / (contentHeight - viewportHeight));

            _scroll.normalizedPosition = new Vector2(0f, normalized);
            
            UpdateVisible();
        }

        public void ScrollToStart() => ScrollToIndex(0);
        
        public void ScrollToEnd() => ScrollToIndex(TotalCount - 1, ScrollAlign.End);

        private void OnScrollChanged(Vector2 _) => UpdateVisible();

        private void OnDataChanged(IReadOnlyList<DataChange<T>> changes)
        {
            var hadItemsBefore = _lastCount > 0;
            
            RebuildCaches();
            Refresh(keepScrollPosition: hadItemsBefore);
            
            _lastCount = TotalCount;
        }

        private void RebuildCaches()
        {
            _heights.Clear();
            _prefix.Clear();

            var n = TotalCount;
            if (n <= 0) return;

            _heights.Capacity = Math.Max(_heights.Capacity, n);
            _prefix.Capacity = Math.Max(_prefix.Capacity, n + 1);

            _prefix.Add(0f);
            
            for (var i = 0; i < n; i++)
            {
                var h = GetItemFullHeight(i);
                _heights.Add(h);
                _prefix.Add(_prefix[i] + h);
            }
        }

        private float GetItemFullHeight(int index)
        {
            var core = _getDynamicHeight != null ? Mathf.Max(0f, _getDynamicHeight(index)) : _itemHeight;
            
            return (index < TotalCount - 1) ? core + _spacing : core;
        }

        private float GetItemsRangeHeight(int start, int endInclusive)
        {
            if (TotalCount == 0 || start > endInclusive) return 0f;
            
            var sum = _prefix[endInclusive + 1] - _prefix[start];
            
            return sum;
        }

        private float GetItemTopY(int index) => _paddingTop + _prefix[index];

        private void UpdateContentSize()
        {
            var items = TotalCount > 0 ? _prefix[TotalCount] : 0f;
            var height = _paddingTop + items + _paddingBottom;

            var size = _content.sizeDelta;
            size.y = height;
            
            _content.sizeDelta = size;
        }

        private (int start, int end) ComputeVisibleIndices()
        {
            if (TotalCount <= 0) return (-1, -1);
            
            EnsureLayoutReady();

            var viewportHeight = _viewport.rect.height;
            
            var contentHeight = _content.rect.height;
            var normalizedY = _scroll.normalizedPosition.y;
            var maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
            var viewportTop = (1f - normalizedY) * maxOffset;
            
            var top = viewportTop;
            var bottom = viewportTop + viewportHeight;
            
            if (viewportHeight <= 1f)
            {
                var avgItem = (_heights.Count > 0) ? (_prefix[_heights.Count] / _heights.Count) : Mathf.Max(1f, _itemHeight);
                var guessViewport = Mathf.Max(avgItem, avgItem * 8f);

                var startGuess = 0;
                var itemsToFill = Mathf.Clamp(Mathf.CeilToInt(guessViewport / Mathf.Max(1f, avgItem)), 1, TotalCount);
                var endGuess = Mathf.Min(TotalCount - 1, itemsToFill - 1);
                
                startGuess = Mathf.Clamp(startGuess - _overscanBeforeItems, 0, Math.Max(0, TotalCount - 1));
                endGuess = Mathf.Clamp(endGuess   + _overscanAfterItems,  startGuess, TotalCount - 1);

                return (startGuess, endGuess);
            }

            var start = LowerBoundPrefix(top - _paddingTop);
            var end = UpperBoundPrefix(bottom - _paddingTop) - 1;
            
            start = Mathf.Clamp(start - _overscanBeforeItems, 0, Math.Max(0, TotalCount - 1));
            end = Mathf.Clamp(end + _overscanAfterItems, start, TotalCount - 1);

            return (start, end);
        }

        private void EnsureLayoutReady()
        {
            try
            {
                Canvas.ForceUpdateCanvases();
                
                if (_viewport) LayoutRebuilder.ForceRebuildLayoutImmediate(_viewport);
                if (_content) LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        
        private int LowerBoundPrefix(float value)
        {
            int lo = 0, hi = TotalCount;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (_prefix[mid] < value)
                {
                    lo = mid + 1;
                } 
                else
                {
                    hi = mid;
                }
            }
            
            return lo;
        }

        private int UpperBoundPrefix(float value)
        {
            int lo = 0, hi = TotalCount;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (_prefix[mid] <= value)
                {
                    lo = mid + 1;
                } 
                else
                {
                    hi = mid;
                }
            }
            
            return lo;
        }

        private void UpdateVisible()
        {
            if (TotalCount <= 0)
            {
                HideAll();
                VisibleRange = (-1, -1);
                return;
            }

            var (needStart, needEnd) = ComputeVisibleIndices();
            
            _toRemove.Clear();
            
            foreach (var kv in _visible)
            {
                var idx = kv.Key;
                if (idx < needStart || idx > needEnd)
                {
                    _toRemove.Add(idx);
                }
            }
            
            foreach (var idx in _toRemove)
            {
                var cell = _visible[idx];
                SafeUnbind(cell);
                
                _pool.Release(cell);
                _visible.Remove(idx);
            }
            
            _toRemove.Clear();
            
            for (var i = needStart; i <= needEnd; i++)
            {
                if (_visible.ContainsKey(i)) continue;

                var cell = _pool.Get();
                PositionCell(cell.Rect, i);
                
                var item = _data[i];
                cell.Bind(item, i);
                
                _visible.Add(i, cell);
            }

            VisibleRange = (needStart, needEnd);
        }

        private void HideAll()
        {
            _toRemove.Clear();
            
            foreach (var kv in _visible)
            {
                _toRemove.Add(kv.Key);
            }
            
            foreach (var idx in _toRemove)
            {
                var cell = _visible[idx];
                SafeUnbind(cell);
                
                _pool.Release(cell);
                _visible.Remove(idx);
            }
            
            _toRemove.Clear();
        }

        private void PositionCell(RectTransform rect, int index)
        {
            var topY = GetItemTopY(index);
            var heightCore = _getDynamicHeight?.Invoke(index) ?? _itemHeight;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);  
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
            rect.sizeDelta = new Vector2(0f, heightCore);
            rect.anchoredPosition = new Vector2(0f, -topY);
        }

        private static void SafeUnbind(TCell cell)
        {
            try
            {
                cell.Unbind();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        
        public void Destroy()
        {
            if (_data != null) _data.Changed -= OnDataChanged;
            if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScrollChanged);
            
            _toRemove.Clear();
            
            foreach (var kv in _visible)
            {
                _toRemove.Add(kv.Key);
            }
            
            foreach (var idx in _toRemove)
            {
                var cell = _visible[idx];
                SafeUnbind(cell);
                
                _pool.Release(cell);
                _visible.Remove(idx);
            }
            
            _toRemove.Clear();

            _scroll = null;
            _viewport = null;
            _content = null;
            _pool = null;
            _data = null;
            _heights.Clear();
            _prefix.Clear();
            VisibleRange = (-1, -1);
        }
    }
}