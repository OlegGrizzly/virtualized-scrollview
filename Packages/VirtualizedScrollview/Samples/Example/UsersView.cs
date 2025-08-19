using System;
using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using OlegGrizzly.VirtualizedScrollview.Adapters;
using OlegGrizzly.VirtualizedScrollview.Core;
using OlegGrizzly.VirtualizedScrollview.Core.Pooling;
using OlegGrizzly.VirtualizedScrollview.Core.Sources;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Samples.Example
{
    public class UsersView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private UserCell prefab;
        [SerializeField] private Transform parent;
        
        [Header("SCROLL UI")]
        [SerializeField] private Button scrollToTopButton;
        [SerializeField] private Button scrollToBottomButton;
        [SerializeField] private Button scrollToIndex30Button;

        [Header("CRUD UI (index-based)")] 
        [SerializeField] private InputField idInput;
        [SerializeField] private InputField nameInput;
        [SerializeField] private Button addButton;
        [SerializeField] private Button updateButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button sortButton;
        [SerializeField] private Button clearButton;

        [Header("INSERT UI")]
        [SerializeField] private InputField insertIndexInput;
        [SerializeField] private InputField insertNameInput;
        [SerializeField] private Button insertButton;

        [Header("MOVE UI")]
        [SerializeField] private InputField moveFromInput;
        [SerializeField] private InputField moveToInput;
        [SerializeField] private InputField moveCountInput;
        [SerializeField] private Button moveButton;

        [Header("SCROLL TO INDEX UI")]
        [SerializeField] private InputField scrollIndexInput;
        [SerializeField] private Button scrollToIndexButton;

        [Header("Layout Settings")]
        [Min(0f)] [SerializeField] private float itemHeight = 150f;
        [Min(0f)] [SerializeField] private float spacing = 20f;
        [Min(0f)] [SerializeField] private float paddingTop = 0f;
        [Min(0f)] [SerializeField] private float paddingBottom = 0f;

        [Header("Overscan (items)")]
        [Min(0)] [SerializeField] private int overscanBefore = 0;
        [Min(0)] [SerializeField] private int overscanAfter  = 0;

        private ComponentPool<UserCell> _pool;
        private IVirtualDataSource<User> _dataSource;
        private IViewAdapter<User, UserCell> _adapter;
        private bool _sortAsc = true;

        private void Awake()
        {
            _pool = new ComponentPool<UserCell>(prefab, parent, preWarm: 16, capacity: 32);
            // Ключ не используется в CRUD-операциях (работаем по индексах). Если у VirtualDataSource есть конструктор без ключа — используйте его.
            _dataSource = new VirtualDataSource<User>(user => user.Id.ToString());

            _adapter = new VerticalViewAdapter<User, UserCell>();
            _adapter.Initialize(scroll, content, _pool, _dataSource);
            _adapter.SetLayout(itemHeight, spacing, paddingTop, paddingBottom);
            _adapter.SetOverscanItems(overscanBefore, overscanAfter);

            var initial = new List<User>(100);
            for (var i = 0; i < 100; i++)
            {
                // Id используется только как поле модели; все операции делаем по index
                initial.Add(new User(i, $"User {i}"));
            }
            _dataSource.SetItems(initial);

            scrollToTopButton.onClick.AddListener(() => _adapter?.ScrollToStart());
            scrollToBottomButton.onClick.AddListener(() => _adapter?.ScrollToEnd());
            scrollToIndex30Button.onClick.AddListener(() => _adapter?.ScrollToIndex(30, ScrollAlign.Start));

            addButton.onClick.AddListener(OnAddClicked);
            updateButton.onClick.AddListener(OnUpdateClicked);
            deleteButton.onClick.AddListener(OnDeleteClicked);
            
            sortButton.onClick.AddListener(OnSortClicked);
            clearButton.onClick.AddListener(OnClearClicked);

            insertButton.onClick.AddListener(OnInsertClicked);
            moveButton.onClick.AddListener(OnMoveClicked);
            scrollToIndexButton.onClick.AddListener(OnScrollToIndexClicked);
        }

        private void OnValidate()
        {
            if (_adapter != null)
            {
                _adapter.SetLayout(itemHeight, spacing, paddingTop, paddingBottom);
                _adapter.SetOverscanItems(overscanBefore, overscanAfter);
            }
        }

        private void OnSortClicked()
        {
            _sortAsc = !_sortAsc;
            SortByName(_sortAsc);

            var label = sortButton?.GetComponentInChildren<Text>();
            if (label != null)
                label.text = _sortAsc ? "Sort Name ASC" : "Sort Name DESC";
        }
        
        private static readonly IComparer<User> NameAscComparer = Comparer<User>.Create(
            (a, b) => string.Compare(a?.Name, b?.Name, StringComparison.Ordinal));

        private static readonly IComparer<User> NameDescComparer = Comparer<User>.Create(
            (a, b) => string.Compare(b?.Name, a?.Name, StringComparison.Ordinal));
        private void SortByName(bool asc)
        {
            var comparer = asc ? NameAscComparer : NameDescComparer;
            _dataSource.Sort(comparer);
        }

        private bool TryParseIndex(out int index)
        {
            index = 0;
            return idInput && int.TryParse(idInput.text, out index);
        }

        private static bool TryParse(InputField input, out int value)
        {
            value = 0;
            return input && int.TryParse(input.text, out value);
        }

        private int ClampIndex(int index, bool allowEnd = false)
        {
            return Mathf.Clamp(index, 0, allowEnd ? _dataSource.Count : Mathf.Max(0, _dataSource.Count - 1));
        }

        private void OnAddClicked()
        {
            if (!TryParseIndex(out var index)) return;
            var name = nameInput ? nameInput.text : $"User {index}";

            // Сформируем новый список, вставляя по индексу (или добавим в конец, если индекс больше Count)
            var newList = new List<User>(_dataSource.Count + 1);
            for (int i = 0; i < _dataSource.Count; i++)
                newList.Add(_dataSource[i]);

            index = Mathf.Clamp(index, 0, newList.Count);
            newList.Insert(index, new User(index, name));

            // Перенумерация Id поля модели необязательна, но для наглядности можно синхронизировать с индексом
            for (int i = 0; i < newList.Count; i++)
                newList[i] = new User(i, newList[i].Name);

            _dataSource.SetItems(newList);

            Debug.LogWarning(_dataSource.Count);
        }

        private void OnUpdateClicked()
        {
            if (!TryParseIndex(out var index)) return;
            if (index < 0 || index >= _dataSource.Count) return;

            var name = nameInput ? nameInput.text : $"User {index}";
            var updated = new User(index, name);
            _dataSource.UpdateAt(index, updated);

            Debug.LogWarning(_dataSource.Count);
        }

        private void OnDeleteClicked()
        {
            if (!TryParseIndex(out var index)) return;
            if (index < 0 || index >= _dataSource.Count) return;

            _dataSource.RemoveAt(index);

            // (опционально) Перенумеруем Id для наглядности соответствию индексу
            var list = new List<User>(_dataSource.Count);
            for (int i = 0; i < _dataSource.Count; i++)
            {
                var u = _dataSource[i];
                list.Add(new User(i, u.Name));
            }
            _dataSource.SetItems(list);

            Debug.LogWarning(_dataSource.Count);
        }

        private void OnInsertClicked()
        {
            if (!TryParse(insertIndexInput, out var index)) return;
            var name = insertNameInput && !string.IsNullOrEmpty(insertNameInput.text)
                ? insertNameInput.text
                : $"User {index}";

            // Собираем новый список и вставляем по индексу (с допуском вставки в конец)
            var list = new List<User>(_dataSource.Count + 1);
            for (int i = 0; i < _dataSource.Count; i++)
                list.Add(_dataSource[i]);

            index = Mathf.Clamp(index, 0, list.Count);
            list.Insert(index, new User(index, name));

            // Опционально: синхронизируем Id с индексом для наглядности
            for (int i = 0; i < list.Count; i++)
                list[i] = new User(i, list[i].Name);

            _dataSource.SetItems(list);
            Debug.LogWarning($"Inserted at {index}. Count={_dataSource.Count}");
        }

        private void OnMoveClicked()
        {
            if (!TryParse(moveFromInput, out var from)) return;
            if (!TryParse(moveToInput, out var to)) return;
            if (!TryParse(moveCountInput, out var count)) count = 1;
            count = Mathf.Max(1, count);

            if (_dataSource.Count == 0) return;
            from = ClampIndex(from);
            to = ClampIndex(to);

            // Кламп по count, чтобы не выходить за границы
            count = Mathf.Min(count, _dataSource.Count - from);
            if (count <= 0) return;

            _dataSource.Move(from, to, count);

            // Опционально: синхронизируем Id с индексом
            var list = new List<User>(_dataSource.Count);
            for (int i = 0; i < _dataSource.Count; i++)
            {
                var u = _dataSource[i];
                list.Add(new User(i, u.Name));
            }
            _dataSource.SetItems(list);

            Debug.LogWarning($"Moved {count} item(s) from {from} to {to}. Count={_dataSource.Count}");
        }

        private void OnScrollToIndexClicked()
        {
            if (!TryParse(scrollIndexInput, out var index)) return;
            if (_adapter == null) return;

            index = ClampIndex(index);
            _adapter.ScrollToIndex(index, ScrollAlign.Start);
        }

        private void OnClearClicked()
        {
            _dataSource.Clear();
            Debug.LogWarning("Data source cleared");
        }

        private void OnDestroy()
        {
            scrollToTopButton?.onClick.RemoveAllListeners();
            scrollToBottomButton?.onClick.RemoveAllListeners();
            scrollToIndex30Button?.onClick.RemoveAllListeners();

            addButton?.onClick.RemoveAllListeners();
            updateButton?.onClick.RemoveAllListeners();
            deleteButton?.onClick.RemoveAllListeners();
            clearButton?.onClick.RemoveAllListeners();

            insertButton?.onClick.RemoveAllListeners();
            moveButton?.onClick.RemoveAllListeners();
            scrollToIndexButton?.onClick.RemoveAllListeners();

            _adapter?.Destroy();
            _pool?.Dispose();
        }
    }
}