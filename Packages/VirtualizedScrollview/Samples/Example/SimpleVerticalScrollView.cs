using UnityEngine;

namespace Samples.Example
{
    public class SimpleVerticalScrollView : MonoBehaviour
    {
        [SerializeField] private float itemHeight = 60f;
        [SerializeField] private float spacing = 4f;
    }
}

public class BasicVerticalDemo : MonoBehaviour
{
    /*[SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private SimpleCellFactory _factory;
    [SerializeField] private ArrayDataSource _data;
    [SerializeField] private VerticalScrollRectAdapter _adapter;

    [SerializeField] private float _itemHeight = 60f;
    [SerializeField] private float _spacing = 4f;

    private void Start()
    {
        var items = Enumerable.Range(0, 50000).Select(i => $"Item {i}");
        _data.SetItems(items);
        _adapter.Initialize(_scrollRect, _data, _factory, _itemHeight, _spacing, preloadExtra:2);
    }

    // Примеры кнопок UI:
    public void JumpToStart() => _adapter.ScrollToStart();
    public void JumpToEnd()   => _adapter.ScrollToEnd();
    public void JumpTo10000() => _adapter.ScrollToIndex(10000);*/
}