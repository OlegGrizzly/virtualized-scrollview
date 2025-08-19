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

            RebuildCaches();
            Refresh(keepScrollPosition: false);
        }

        public void Destroy()
        {
            if (_data != null) _data.Changed -= OnDataChanged;
            if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScrollChanged);

            // снять всё видимое
            _toRemove.Clear();
            foreach (var kv in _visible) _toRemove.Add(kv.Key);
            for (int i = 0; i < _toRemove.Count; i++)
            {
                int idx = _toRemove[i];
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

        public void SetLayout(float itemHeight, float spacing = 0f, float paddingTop = 0f, float paddingBottom = 0f)
        {
            _itemHeight = Mathf.Max(0f, itemHeight);
            _spacing = Mathf.Max(0f, spacing);
            _paddingTop = Mathf.Max(0f, paddingTop);
            _paddingBottom = Mathf.Max(0f, paddingBottom);

            RebuildCaches();
            Refresh(true);
        }

        public void SetDynamicHeightProvider(Func<int, float> getHeight)
        {
            _getDynamicHeight = getHeight;
            RebuildCaches();
            Refresh(true);
        }

        public void Refresh(bool keepScrollPosition = true)
        {
            if (_scroll == null) return;

            // optionally сохранить позицию
            Vector2 savedNormPos = _scroll.normalizedPosition;

            // Обновить размер content
            UpdateContentSize();

            // Снять все ячейки и заново рассчитать видимые — самый простой безопасный путь.
            _toRemove.Clear();
            foreach (var kv in _visible) _toRemove.Add(kv.Key);
            for (int i = 0; i < _toRemove.Count; i++)
            {
                int idx = _toRemove[i];
                var cell = _visible[idx];
                SafeUnbind(cell);
                _pool.Release(cell);
                _visible.Remove(idx);
            }
            _toRemove.Clear();

            if (keepScrollPosition)
                _scroll.normalizedPosition = savedNormPos;

            UpdateVisible();
        }

        public void ScrollToIndex(int index, ScrollAlign align = ScrollAlign.Start)
        {
            if (_scroll == null || TotalCount == 0) return;
            index = Mathf.Clamp(index, 0, TotalCount - 1);

            float alignValue = align switch
            {
                ScrollAlign.Start => 0f,
                ScrollAlign.Center => 0.5f,
                ScrollAlign.End => 1f,
                _ => 0f
            };

            float targetY = GetItemTopY(index);
            float itemSize = GetItemFullHeight(index);

            float viewportHeight = _viewport.rect.height;
            float contentHeight = _content.rect.height;

            // сместить так, чтобы элемент оказался в позиции align (0 — верх вьюпорта, 1 — низ)
            float viewportTopY = targetY - alignValue * Mathf.Max(0f, viewportHeight - itemSize);
            float normalized = (contentHeight <= viewportHeight)
                ? 1f
                : 1f - Mathf.Clamp01(viewportTopY / (contentHeight - viewportHeight));

            _scroll.normalizedPosition = new Vector2(0f, normalized);
            UpdateVisible();
        }

        public void ScrollToStart() => ScrollToIndex(0, ScrollAlign.Start);
        public void ScrollToEnd() => ScrollToIndex(TotalCount - 1, ScrollAlign.End);

        // ======== ВНУТРЕННЯЯ ЛОГИКА ========

        private void OnScrollChanged(Vector2 _) => UpdateVisible();

        private void OnDataChanged(IReadOnlyList<DataChange<T>> changes)
        {
            // Простой путь: перестроить всё. Потом можно оптимизировать по типам изменений.
            RebuildCaches();
            Refresh(true);
        }

        private void RebuildCaches()
        {
            _heights.Clear();
            _prefix.Clear();

            int n = TotalCount;
            if (n <= 0) return;

            _heights.Capacity = Math.Max(_heights.Capacity, n);
            _prefix.Capacity = Math.Max(_prefix.Capacity, n + 1);

            _prefix.Add(0f);
            for (int i = 0; i < n; i++)
            {
                float h = GetItemFullHeight(i);
                _heights.Add(h);
                _prefix.Add(_prefix[i] + h);
            }
        }

        private float GetItemFullHeight(int index)
        {
            // высота ячейки + spacing (кроме последней) учитывается в кумулятивной сумме
            float core = _getDynamicHeight != null ? Mathf.Max(0f, _getDynamicHeight(index)) : _itemHeight;
            return (index < TotalCount - 1) ? core + _spacing : core;
        }

        private float GetItemsRangeHeight(int start, int endInclusive)
        {
            if (TotalCount == 0 || start > endInclusive) return 0f;
            float sum = _prefix[endInclusive + 1] - _prefix[start];
            return sum;
        }

        private float GetItemTopY(int index)
        {
            // координата от верхней кромки content вниз
            return _paddingTop + _prefix[index];
        }

        private void UpdateContentSize()
        {
            float items = (TotalCount > 0) ? _prefix[TotalCount] : 0f;
            float height = _paddingTop + items + _paddingBottom;

            var size = _content.sizeDelta;
            size.y = height;
            _content.sizeDelta = size;
        }

        private (int start, int end) ComputeVisibleIndices()
        {
            if (TotalCount <= 0) return (-1, -1);

            float viewportHeight = _viewport.rect.height;

            // верх видимой области в системе координат content
            float contentHeight = _content.rect.height;
            float normalizedY = _scroll.normalizedPosition.y; // 1 = top
            float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
            float viewportTop = (1f - normalizedY) * maxOffset;

            // ищем первый индекс, чья нижняя граница находится ниже top,
            // и последний индекс, чья верхняя граница выше bottom
            float top = viewportTop;
            float bottom = viewportTop + viewportHeight;

            int start = LowerBoundPrefix(top - _paddingTop);
            int end = UpperBoundPrefix(bottom - _paddingTop) - 1;

            start = Mathf.Clamp(start, 0, TotalCount - 1);
            end = Mathf.Clamp(end, start, TotalCount - 1);

            return (start, end);
        }

        // Бинарные поиски по префиксным суммам.
        private int LowerBoundPrefix(float value)
        {
            // минимальный i: prefix[i] >= value
            int lo = 0, hi = TotalCount;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_prefix[mid] < value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        private int UpperBoundPrefix(float value)
        {
            // минимальный i: prefix[i] > value
            int lo = 0, hi = TotalCount;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_prefix[mid] <= value) lo = mid + 1; else hi = mid;
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

            // убрать лишних
            _toRemove.Clear();
            foreach (var kv in _visible)
            {
                int idx = kv.Key;
                if (idx < needStart || idx > needEnd)
                    _toRemove.Add(idx);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                int idx = _toRemove[i];
                var cell = _visible[idx];
                SafeUnbind(cell);
                _pool.Release(cell);
                _visible.Remove(idx);
            }
            _toRemove.Clear();

            // добавить недостающих
            for (int i = needStart; i <= needEnd; i++)
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
            foreach (var kv in _visible) _toRemove.Add(kv.Key);
            for (int i = 0; i < _toRemove.Count; i++)
            {
                int idx = _toRemove[i];
                var cell = _visible[idx];
                SafeUnbind(cell);
                _pool.Release(cell);
                _visible.Remove(idx);
            }
            _toRemove.Clear();
        }

        private void PositionCell(RectTransform rect, int index)
        {
            // Анкеруем к верху (pivot.y = 1, anchorMin.y = anchorMax.y = 1) — стандарт для вертикального списка.
            float topY = GetItemTopY(index);
            float heightCore = (_getDynamicHeight != null) ? _getDynamicHeight(index) : _itemHeight;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);   // горизонтальный stretch
            rect.pivot    = new Vector2(0.5f, 1f);

            // В режиме horizontal-stretch ширина задаётся left/right offsets, а не sizeDelta.x.
            // Если префаб имел фиксированную ширину, в sizeDelta.x останется отрицательное значение
            // (например -500), что даёт визуально left/right = -250. Обнуляем смещения по X.
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);  // left = 0
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);  // right = 0

            // Высоту задаём через sizeDelta.y, а sizeDelta.x держим = 0 для корректного стретча по ширине.
            rect.sizeDelta = new Vector2(0f, heightCore);

            // y — отрицательный сдвиг вниз от верха content
            rect.anchoredPosition = new Vector2(0f, -topY);
        }

        private static void SafeUnbind(TCell cell)
        {
            try { cell.Unbind(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}