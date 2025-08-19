using System;
using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using OlegGrizzly.VirtualizedScrollview.Adapters;
using OlegGrizzly.VirtualizedScrollview.Core;
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

        [Header("CRUD UI")] 
        [SerializeField] private InputField idInput;
        [SerializeField] private InputField nameInput;
        [SerializeField] private Button addButton;
        [SerializeField] private Button updateButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button sortButton;

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
        private readonly HashSet<int> _usedIds = new();
        private bool _sortAsc = true;

        private void Awake()
        {
            _pool = new ComponentPool<UserCell>(prefab, parent, preWarm: 16, capacity: 32);
            _dataSource = new VirtualDataSource<User>(user => user.Id.ToString());

            _adapter = new VerticalViewAdapter<User, UserCell>();
            _adapter.Initialize(scroll, content, _pool, _dataSource);
            _adapter.SetLayout(itemHeight, spacing, paddingTop, paddingBottom);
            _adapter.SetOverscanItems(overscanBefore, overscanAfter);

            var initial = new List<User>(100);
            for (var i = 0; i < 100; i++)
            {
                var id = GenerateUid();
                initial.Add(new User(id, $"User {i}"));
            }
            _dataSource.SetItems(initial);

            scrollToTopButton.onClick.AddListener(() => _adapter?.ScrollToStart());
            scrollToBottomButton.onClick.AddListener(() => _adapter?.ScrollToEnd());
            scrollToIndex30Button.onClick.AddListener(() => _adapter?.ScrollToIndex(30, ScrollAlign.Start));

            addButton.onClick.AddListener(OnAddClicked);
            updateButton.onClick.AddListener(OnUpdateClicked);
            deleteButton.onClick.AddListener(OnDeleteClicked);
            
            sortButton.onClick.AddListener(OnSortClicked);
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

        private int GenerateUid()
        {
            int uid;
            int guard = 0;
            do
            {
                uid = Random.Range(100000, 999999);
            } while (_usedIds.Contains(uid) && ++guard < 1000);
            _usedIds.Add(uid);
            return uid;
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

        private bool TryParseId(out int id)
        {
            id = 0;
            return idInput && int.TryParse(idInput.text, out id);
        }

        private void OnAddClicked()
        {
            if (!TryParseId(out var id)) return;
            var name = nameInput ? nameInput.text : $"User {id}";
            
            if (_dataSource.TryGetIndexById(id.ToString(), out var index))
            {
                var updated = new User(id, name);
                _dataSource.UpdateAt(index, updated);
            }
            else
            {
                var list = new List<User>(_dataSource.Count + 1);
                for (int i = 0; i < _dataSource.Count; i++)
                    list.Add(_dataSource[i]);
                list.Add(new User(id, name));
                _dataSource.SetItems(list);
            }
            
            Debug.LogWarning(_dataSource.Count);
        }

        private void OnUpdateClicked()
        {
            if (!TryParseId(out var id)) return;
            var name = nameInput ? nameInput.text : $"User {id}";
            
            var updated = new User(id, name);
            _dataSource.UpdateById(id.ToString(), updated);
            
            Debug.LogWarning(_dataSource.Count);
        }

        private void OnDeleteClicked()
        {
            if (!TryParseId(out var id)) return;
            if (_dataSource.TryGetIndexById(id.ToString(), out var index))
            {
                _dataSource.RemoveAt(index);
                _usedIds.Remove(id);
            }
            
            Debug.LogWarning(_dataSource.Count);
        }

        private void OnDestroy()
        {
            scrollToTopButton?.onClick.RemoveAllListeners();
            scrollToBottomButton?.onClick.RemoveAllListeners();
            scrollToIndex30Button?.onClick.RemoveAllListeners();

            addButton?.onClick.RemoveAllListeners();
            updateButton?.onClick.RemoveAllListeners();
            deleteButton?.onClick.RemoveAllListeners();

            _adapter?.Destroy();
            _pool?.Dispose();
        }
    }
}