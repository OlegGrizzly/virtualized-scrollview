using OlegGrizzly.VirtualizedScrollview.Abstractions;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.Example
{
    /// <summary>
    /// Простая UI-ячейка для теста пула.
    /// Совместима с вашим IPoolable: Get/Release/Destroy.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PooledBox : MonoBehaviour, IPoolable
    {
        [SerializeField] private Image _bg;
        [SerializeField] private Text _label; // можно заменить на TMP_Text, если нужен TextMeshPro

        private RectTransform _rect;
        public RectTransform Rect => _rect ? _rect : (_rect = (RectTransform)transform);

        private static int _counter;

        public void OnGetFromPool()
        {
            if (_label) _label.text = $"#{_counter++}";
            if (_bg) _bg.fillCenter = true;
            // любой небольшой визуальный эффект для отличия «только что взят из пула»
        }

        public void OnReturnToPool()
        {
            // сброс визуального состояния
            if (_label) _label.text = string.Empty;
            if (_bg) _bg.fillCenter = false;
        }

        public void OnPoolDestroy()
        {
            // если бы были ресурсы — тут бы их чистили
        }
    }
}