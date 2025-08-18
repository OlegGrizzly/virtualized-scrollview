using OlegGrizzly.VirtualizedScrollview.Abstractions;
using OlegGrizzly.VirtualizedScrollview.Adapters;
using OlegGrizzly.VirtualizedScrollview.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.Example
{
    public class UsersView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private UserCell prefab;
        [SerializeField] private Transform parent;
        
        [SerializeField] private Button scrollToTopButton;
        [SerializeField] private Button scrollToBottomButton;
        [SerializeField] private Button scrollToIndex30Button;

        private ComponentPool<UserCell> _pool;
        private IVirtualDataSource<User> _dataSource;
        private IViewAdapter<User, UserCell> _adapter;

        private void Awake()
        {
            _pool = new ComponentPool<UserCell>(prefab, parent, preWarm: 16, capacity: 32);
            _dataSource = new VirtualDataSource<User>(user => user.Id.ToString());

            _adapter = new VerticalViewAdapter<User, UserCell>();
            _adapter.SetLayout(itemHeight: 80f, spacing: 8f, paddingTop: 8f, paddingBottom: 8f);
            _adapter.Initialize(scroll, content, _pool, _dataSource);

            for (var i = 0; i < 100; i++)
            {
                var user = new User(i, $"User {i}");
                _dataSource.Add(user);
            }

            scrollToTopButton.onClick.AddListener(() => _adapter?.ScrollToStart());
            scrollToBottomButton.onClick.AddListener(() => _adapter?.ScrollToEnd());
            scrollToIndex30Button.onClick.AddListener(() => _adapter?.ScrollToIndex(30, ScrollAlign.Start));
        }

        private void OnDestroy()
        {
            scrollToTopButton?.onClick.RemoveAllListeners();
            scrollToBottomButton?.onClick.RemoveAllListeners();
            scrollToIndex30Button?.onClick.RemoveAllListeners();

            _adapter?.Destroy();
            _pool?.Dispose();
        }
    }
}