using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OlegGrizzly.VirtualizedScrollview.Controllers
{
    [RequireComponent(typeof(ScrollRect))]
    public class VerticalVirtualizedScrollView : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private RectTransform content; // assign ScrollRect.content
        [SerializeField] private GameObject cellPrefab; // prefab with FriendCell component (defined below)

        [Header("Initial Fake Data")]
        [SerializeField] private int initialCount = 10;

        // Data source (generic) — replace with your own implementation if already in project
        private VirtualDataSource _ds;

        // Maintain a 1:1 list of instantiated cells by index
        private readonly List<FriendCell> _cells = new List<FriendCell>();

        private ScrollRect _scroll;
        private System.Random _rng;

        // highlight helpers
        private readonly Dictionary<GameObject, Coroutine> _flashRoutines = new Dictionary<GameObject, Coroutine>();
        [Header("Highlighting")] 
        [SerializeField] private float highlightDuration = 0.35f;
        [SerializeField] private Color insertColor = new Color(0.2f, 0.8f, 0.2f, 0.45f);   // green
        [SerializeField] private Color removeColor = new Color(0.9f, 0.2f, 0.2f, 0.45f);   // red
        [SerializeField] private Color moveColor   = new Color(1.0f, 0.8f, 0.2f, 0.45f);   // yellow
        [SerializeField] private Color updateColor = new Color(0.2f, 0.6f, 1.0f, 0.45f);   // blue

        #region Minimal VirtualDataSource implementation (inner for demo)
        // If you already have OlegGrizzly.VirtualizedScrollview.Core.VirtualDataSource<T> in your project,
        // delete this inner class and use that one. The API surface used here is tiny.

        private enum ChangeKind { Insert, Remove, Move, Update }
        private readonly struct DataChange
        {
            public readonly ChangeKind Kind; public readonly int Index; public readonly int Count; public readonly int From; public readonly int To;
            public DataChange(ChangeKind kind, int index, int count){Kind=kind;Index=index;Count=count;From=-1;To=-1;}
            public DataChange(int from, int to, int count){Kind=ChangeKind.Move;From=from;To=to;Count=count;Index=to;}
        }

        private sealed class VirtualDataSource
        {
            private readonly List<Friend> _list = new List<Friend>();
            private readonly Dictionary<string,int> _indexById = new Dictionary<string,int>();
            public event Action<IReadOnlyList<DataChange>> Changed;
            public int Count => _list.Count;
            public Friend this[int index] => _list[index];
            private string Key(Friend f) => f.Id;

            public void Add(Friend f) => Insert(_list.Count, f);
            public void Insert(int index, Friend f)
            {
                var id = Key(f) ?? throw new Exception("Id null");
                if (_indexById.ContainsKey(id)) throw new Exception($"Duplicate id {id}");
                _list.Insert(index, f); ReindexFrom(index);
                Emit(new DataChange(ChangeKind.Insert,index,1));
            }
            public void RemoveAt(int index, int count=1)
            {
                count=Mathf.Min(count, _list.Count-index); if (count<=0) return;
                _list.RemoveRange(index,count); ReindexFrom(index);
                Emit(new DataChange(ChangeKind.Remove,index,count));
            }
            public void UpdateAt(int index, Friend f)
            {
                if (_list[index].Id != f.Id) throw new Exception("Update must preserve Id");
                _list[index]=f; Emit(new DataChange(ChangeKind.Update,index,1));
            }
            public void Move(int from, int to, int count=1)
            {
                if (count<=0||from==to) return; var block=_list.GetRange(from,count); _list.RemoveRange(from,count); if(to>from) to-=count; _list.InsertRange(to,block); ReindexFrom(Math.Min(from,to));
                Emit(new DataChange(from,to,count));
            }
            public void Clear(){ if(_list.Count==0) return; var n=_list.Count; _list.Clear(); _indexById.Clear(); Emit(new DataChange(ChangeKind.Remove,0,n)); }

            public void SetItems(IEnumerable<Friend> items)
            {
                // Simple diff for demo: remove all then insert all (still checks events order in UI). Replace with smart diff if needed.
                var target = new List<Friend>(items);
                Clear();
                for(int i=0;i<target.Count;i++) Insert(i, target[i]);
            }

            private void ReindexFrom(int start){ _indexById.Clear(); for(int i=0;i<_list.Count;i++) _indexById[_list[i].Id]=i; }
            private void Emit(DataChange c){ _tmp.Clear(); _tmp.Add(c); Changed?.Invoke(_tmp); }
            private readonly List<DataChange> _tmp = new List<DataChange>(1);
        }
        #endregion

        private void Awake()
        {
            _scroll = GetComponent<ScrollRect>();
            if (content == null) content = _scroll.content;
            _rng = new System.Random(Environment.TickCount);

            _ds = new VirtualDataSource();
            _ds.Changed += OnDataSourceChanged;
        }

        private void Start()
        {
            // seed fake data
            for (int i = 0; i < initialCount; i++)
                _ds.Add(MakeFriend());
        }

        // ============ Buttons you can wire in Inspector =================
        public void Btn_Add() => _ds.Add(MakeFriend());
        public void Btn_Insert0() => _ds.Insert(0, MakeFriend());
        public void Btn_RemoveLast(){ if(_ds.Count>0) _ds.RemoveAt(_ds.Count-1,1);}        
        public void Btn_Clear() => _ds.Clear();
        public void Btn_MoveRandom()
        {
            if (_ds.Count < 2) return;
            int from = _rng.Next(0, _ds.Count);
            int count = Math.Min(1 + _rng.Next(0,2), _ds.Count-from);
            int to = _rng.Next(0, _ds.Count - count + 1);
            _ds.Move(from,to,count);
        }
        public void Btn_UpdateRandom()
        {
            if (_ds.Count == 0) return; int i=_rng.Next(0,_ds.Count);
            var f = _ds[i]; f.Name += "*"; f.Age += 1; _ds.UpdateAt(i,f);
        }
        public void Btn_ResetSet()
        {
            var n = _rng.Next(5, 15);
            var list = new List<Friend>(n);
            for (int i=0;i<n;i++) list.Add(MakeFriend());
            _ds.SetItems(list);
        }

        // ============ Apply changes to simple visual list =================
        private void OnDataSourceChanged(IReadOnlyList<DataChange> batch)
        {
            foreach (var ch in batch)
            {
                switch (ch.Kind)
                {
                    case ChangeKind.Insert: ApplyInsert(ch.Index, ch.Count); Debug.Log($"INSERT at {ch.Index} x{ch.Count}"); break;
                    case ChangeKind.Remove: ApplyRemove(ch.Index, ch.Count); Debug.Log($"REMOVE at {ch.Index} x{ch.Count}"); break;
                    case ChangeKind.Move:   ApplyMove(ch.From, ch.To, ch.Count); Debug.Log($"MOVE {ch.From}->{ch.To} x{ch.Count}"); break;
                    case ChangeKind.Update: ApplyUpdate(ch.Index, ch.Count); Debug.Log($"UPDATE at {ch.Index} x{ch.Count}"); break;
                }
            }
            RefreshAllPositionsAndSize();
        }

        private void ApplyInsert(int index, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(cellPrefab, content, false);
                var cell = go.GetComponent<FriendCell>();
                if (cell == null) cell = go.AddComponent<FriendCell>();
                _cells.Insert(index + i, cell);
                FlashCell(cell, insertColor);
            }
            // Rebind inserted and everything after
            for (int i = index; i < _cells.Count; i++) Bind(i);
        }

        private void ApplyRemove(int index, int count)
        {
            count = Mathf.Min(count, _cells.Count - index);
            for (int i = 0; i < count; i++)
            {
                var cell = _cells[index];
                _cells.RemoveAt(index);
                if (cell) FlashCell(cell, removeColor, andDestroy: true);
            }
            for (int i = index; i < _cells.Count; i++) Bind(i);
        }

        private void ApplyMove(int from, int to, int count)
        {
            // Move cells in parallel with data
            var block = _cells.GetRange(from, count);
            _cells.RemoveRange(from, count);
            if (to > from) to -= count;
            _cells.InsertRange(to, block);
            foreach (var c in block)
                FlashCell(c, moveColor);
            for (int i = Math.Min(from, to); i < _cells.Count; i++) Bind(i);
        }

        private void ApplyUpdate(int index, int count)
        {
            int end = Mathf.Min(index + count, _cells.Count);
            for (int i = index; i < end; i++) 
            {
                Bind(i);
                FlashCell(_cells[i], updateColor);
            }
        }

        private void Bind(int index)
        {
            if (index < 0 || index >= _cells.Count) return;
            var cell = _cells[index];
            var f = _ds[index];
            cell.Set(f, index);
            cell.transform.SetSiblingIndex(index);
        }

        private void RefreshAllPositionsAndSize()
        {
            // Simple vertical layout: rely on a VerticalLayoutGroup if present,
            // otherwise just ensure sibling indices are correct and content height matches children count.
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].transform.SetSiblingIndex(i);

            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                // No layout group: adjust size to fit children height if they have LayoutElement
                float total = 0f;
                foreach (var c in _cells)
                {
                    var le = c.GetComponent<LayoutElement>();
                    total += le ? le.preferredHeight : 60f; // default height
                }
                content.sizeDelta = new Vector2(content.sizeDelta.x, total);
            }
        }

        private void FlashCell(FriendCell cell, Color color, bool andDestroy = false)
        {
            if (cell == null) return;
            var go = cell.gameObject;
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.raycastTarget = false; // do not block clicks

            if (_flashRoutines.TryGetValue(go, out var running) && running != null)
            {
                StopCoroutine(running);
                _flashRoutines.Remove(go);
            }
            _flashRoutines[go] = StartCoroutine(CoFlash(img, color, highlightDuration, andDestroy ? go : null));
        }

        private System.Collections.IEnumerator CoFlash(Image img, Color flash, float dur, GameObject destroyAfter)
        {
            var start = Time.unscaledTime;
            var baseColor = img.color;
            // bring to flash color instantly
            img.color = flash;
            while (Time.unscaledTime - start < dur)
            {
                float t = (Time.unscaledTime - start) / dur;
                img.color = Color.Lerp(flash, baseColor, t);
                yield return null;
            }
            img.color = baseColor;
            if (destroyAfter != null)
            {
                Destroy(destroyAfter);
            }
        }

        private Friend MakeFriend()
        {
            // random but stable-looking id
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            string[] names = { "Alice","Bob","Carla","Dima","Eva","Fred","Gina","Hugo","Ilya","Judy" };
            return new Friend
            {
                Id = id,
                Name = names[_rng.Next(0, names.Length)] + _rng.Next(0,100),
                Age = _rng.Next(18, 60)
            };
        }
    }
}