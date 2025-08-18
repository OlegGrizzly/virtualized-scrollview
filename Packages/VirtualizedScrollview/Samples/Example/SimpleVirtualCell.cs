using OlegGrizzly.VirtualizedScrollview.Abstractions;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.Example
{
    public class SimpleVirtualCell : MonoBehaviour, IVirtualCell
    {
        [SerializeField] private Text label;

        public RectTransform Rect => (RectTransform) transform;
        
        public void Bind(int dataIndex, object data)
        {
            label.text = $"{dataIndex}: {data}";
        }

        public void Unbind()
        {
            label.text = "";
        }
    }
}