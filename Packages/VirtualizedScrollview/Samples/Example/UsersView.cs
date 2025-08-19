using System;
using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Abstractions;
using OlegGrizzly.VirtualizedScrollview.Adapters;
using OlegGrizzly.VirtualizedScrollview.Core.Pooling;
using OlegGrizzly.VirtualizedScrollview.Core.Sources;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.Example
{
    public class UsersView : MonoBehaviour
    {
        [Header("Scroll View")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private UserCell prefab;
        [SerializeField] private Transform parent;
        
        [Header("Layout")]
        [SerializeField] private bool usePrefabHeight = true;
        [SerializeField] private float itemHeight = 100f;
        [SerializeField] private float spacing = 4f;
        [SerializeField] private float paddingTop = 8f;
        [SerializeField] private float paddingBottom = 8f;
        [SerializeField] private int bufferBefore = 2;
        [SerializeField] private int bufferAfter = 2;
        [SerializeField] private int preWarm = 8;

        [Header("Search")]
        [SerializeField] private InputField searchInputField;
        [SerializeField] private Button searchButton;
        [SerializeField] private InputField filterInputField;
        [SerializeField] private Button filterButton;
        [SerializeField] private Button resetButton;
        
        [Header("Scroll")]
        [SerializeField] private InputField scrollInputField;
        [SerializeField] private Button scrollButton;
        [SerializeField] private Button scrollStartButton;
        [SerializeField] private Button scrollEndButton;
        
        [Header("CRUD")]
        [SerializeField] private Button addFirstButton;
        [SerializeField] private Button addLastButton;
        [SerializeField] private InputField updateIndexInputField;
        [SerializeField] private InputField updateNameInputField;
        [SerializeField] private InputField updateAgeInputField;
        [SerializeField] private Button updateButton;
        [SerializeField] private InputField deleteInputField;
        [SerializeField] private Button deleteButton;
        
        [Header("Insert")]
        [SerializeField] private InputField insertIndexInputField;
        [SerializeField] private InputField insertNameInputField;
        [SerializeField] private InputField insertAgeInputField;
        [SerializeField] private Button insertButton;
        
        [Header("Move")]
        [SerializeField] private InputField moveFromInputField;
        [SerializeField] private InputField moveToInputField;
        [SerializeField] private InputField moveCountInputField;
        [SerializeField] private Button moveButton;
        
        [Header("Sort")]
        [SerializeField] private Button sortIdButton;
        [SerializeField] private Button sortNameButton;
        [SerializeField] private Button sortAgeButton;
        
        [Header("Clear")]
        [SerializeField] private Text countText;
        [SerializeField] private Button clearAllButton;

        private IVirtualDataSource<User> _data;
        private IViewAdapter<User, UserCell> _adapter;
        private ComponentPool<UserCell> _pool;
        private readonly List<User> _initialUsers = new();
        private int _nextId = 1;
        
        private bool _sortIdAsc = true;
        private bool _sortNameAsc = true;
        private bool _sortAgeAsc = true;

        private void Awake()
        {
            _data = new VirtualDataSource<User>(u => u.Id.ToString());
            
            _pool = new ComponentPool<UserCell>(prefab, parent, preWarm: preWarm);
            _adapter = new VerticalViewAdapter<User, UserCell>();
            _adapter.Initialize(scroll, content, _pool, _data);
            
            var finalItemHeight = usePrefabHeight && prefab && prefab.Rect ? prefab.Rect.rect.height : itemHeight;
            _adapter.SetLayout(finalItemHeight, spacing: spacing, paddingTop: paddingTop, paddingBottom: paddingBottom);
            _adapter.SetBufferItems(before: bufferBefore, after: bufferAfter);

            HookupUI();
            
            Seed(1000);
            UpdateCountText();
        }

        #region UI wiring
        private void HookupUI()
        {
            if (searchButton) searchButton.onClick.AddListener(OnSearchClicked);
            
            if (filterButton) filterButton.onClick.AddListener(OnFilterClicked);
            
            if (resetButton) resetButton.onClick.AddListener(ResetSearchAndFilter);

            if (scrollButton) scrollButton.onClick.AddListener(OnScrollToIndexClicked);
            if (scrollStartButton) scrollStartButton.onClick.AddListener(() => _adapter.ScrollToStart());
            if (scrollEndButton) scrollEndButton.onClick.AddListener(() => _adapter.ScrollToEnd());

            if (addFirstButton) addFirstButton.onClick.AddListener(AddFirst);
            if (addLastButton) addLastButton.onClick.AddListener(AddLast);
            if (updateButton) updateButton.onClick.AddListener(UpdateAtIndex);
            if (deleteButton) deleteButton.onClick.AddListener(DeleteAtIndex);

            if (insertButton) insertButton.onClick.AddListener(InsertAtIndex);

            if (moveButton) moveButton.onClick.AddListener(MoveRange);
            
            if (sortIdButton) sortIdButton.onClick.AddListener(SortById);
            if (sortNameButton) sortNameButton.onClick.AddListener(SortByName);
            if (sortAgeButton) sortAgeButton.onClick.AddListener(SortByAge);

            if (clearAllButton) clearAllButton.onClick.AddListener(() => { _data.Clear(); UpdateCountText(); });
        }
        #endregion

        #region Seed & helpers
        private void Seed(int count)
        {
            _initialUsers.Clear();
            for (var i = 0; i < count; i++)
            {
                _initialUsers.Add(MakeUser());
            }
            
            _data.SetItems(_initialUsers);
        }

        private User MakeUser(string userName = null, int? age = null)
        {
            var id = _nextId++;
            var n = string.IsNullOrEmpty(userName) ? $"User {id:000}" : userName;
            var a = age ?? UnityEngine.Random.Range(18, 65);
            return new User(id, n, a);
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0) return -1;
            return Mathf.Clamp(index, 0, count - 1);
        }

        private static bool TryParseInt(InputField field, out int value)
        {
            value = 0;
            return field && int.TryParse(field.text, out value);
        }
        #endregion

        #region Search / Scroll
        private void OnSearchClicked()
        {
            if (!searchInputField) return;
            
            var query = searchInputField.text;
            _data.ApplySearch(query, u => $"{u.Id} {u.Name} {u.Age}");
            _adapter.ScrollToStart();
            
            UpdateCountText();
        }

        private void OnFilterClicked()
        {
            var value = filterInputField ? filterInputField.text : null;
            _data.ApplyFilter(value, u => u.Name);
            _adapter.ScrollToStart();
            
            UpdateCountText();
        }
        
        private void ResetSearchAndFilter()
        {
            _data.ResetSearchAndFilter();
            _adapter.ScrollToStart();

            searchInputField.text = "";
            filterInputField.text = "";
            
            UpdateCountText();
        }

        private void OnScrollToIndexClicked()
        {
            if (!TryParseInt(scrollInputField, out var idx)) return;
            idx = ClampIndex(idx, _data.Count);
            if (idx >= 0) _adapter.ScrollToIndex(idx);
        }
        #endregion

        #region CRUD
        private void AddFirst()
        {
            _data.Insert(0, MakeUser());
            UpdateCountText();
        }

        private void AddLast()
        {
            _data.Add(MakeUser());
            UpdateCountText();
        }

        private void UpdateAtIndex()
        {
            if (!TryParseInt(updateIndexInputField, out var idx)) return;
            if (idx < 0 || idx >= _data.Count) return;

            var cur = _data[idx];
            var newName = string.IsNullOrEmpty(updateNameInputField?.text) ? cur.Name : updateNameInputField.text;
            var newAge = cur.Age;
            if (int.TryParse(updateAgeInputField?.text, out var a)) newAge = a;

            var updated = new User(cur.Id, newName, newAge);
            _data.UpdateAt(idx, updated);
            UpdateCountText();
        }

        private void DeleteAtIndex()
        {
            if (!TryParseInt(deleteInputField, out var idx)) return;
            if (idx < 0 || idx >= _data.Count) return;
            _data.RemoveAt(idx);
            UpdateCountText();
        }
        #endregion

        #region Insert / Move
        private void InsertAtIndex()
        {
            if (!TryParseInt(insertIndexInputField, out var idx)) return;
            idx = Mathf.Clamp(idx, 0, _data.Count);
            var userName = string.IsNullOrEmpty(insertNameInputField?.text) ? null : insertNameInputField.text;
            int? userAge = null;
            if (int.TryParse(insertAgeInputField?.text, out var parsedAge))
            {
                userAge = parsedAge;
            }
            _data.Insert(idx, MakeUser(userName, userAge));
            UpdateCountText();
        }

        private void MoveRange()
        {
            if (!TryParseInt(moveFromInputField, out var from)) return;
            if (!TryParseInt(moveToInputField, out var to)) return;

            var count = 1;
            if (TryParseInt(moveCountInputField, out var parsed))
            {
                count = Mathf.Max(1, parsed);
            }

            _data.Move(from, to, count);
            UpdateCountText();
        }
        #endregion

        #region Sort
        private void SortById()
        {
            var asc = _sortIdAsc;
            _data.Sort(Comparer<User>.Create((a, b) => asc ? a.Id.CompareTo(b.Id) : b.Id.CompareTo(a.Id)));
            _sortIdAsc = !asc;
            UpdateCountText();
        }

        private void SortByName()
        {
            var asc = _sortNameAsc;
            _data.Sort(Comparer<User>.Create((a, b) => asc ? string.Compare(a.Name, b.Name, StringComparison.Ordinal) : string.Compare(b.Name, a.Name, StringComparison.Ordinal)));
            _sortNameAsc = !asc;
            UpdateCountText();
        }

        private void SortByAge()
        {
            var asc = _sortAgeAsc;
            _data.Sort(Comparer<User>.Create((a, b) => asc ? a.Age.CompareTo(b.Age) : b.Age.CompareTo(a.Age)));
            _sortAgeAsc = !asc;
            UpdateCountText();
        }
        #endregion
        
        private void UpdateCountText()
        {
            if (countText)
            {
                countText.text = $"Total: {_data.Count}";
            }
        }
        
        private void OnDestroy()
        {
            _adapter?.Destroy();
            _pool?.Dispose();
        }
    }
}