using OlegGrizzly.VirtualizedScrollview.Abstractions;
using UnityEngine;

namespace OlegGrizzly.VirtualizedScrollview.Core
{
    public class VirtualCellFactory : IVirtualCellFactory
    {
        private readonly GameObject _prefab;
        
        public VirtualCellFactory(GameObject prefab)
        {
            _prefab = prefab;
        }

        public IVirtualCell Create(Transform parent)
        {
            if (_prefab == null)
            {
                Debug.LogError("[VirtualCellFactory] Cannot Create: prefab is null.");
                return null;
            }
            
            var go = Object.Instantiate(_prefab, parent, false);
            var cell = go.GetComponent<IVirtualCell>();
            if (cell == null)
            {
                Debug.LogError("[VirtualCellFactory] Instantiated prefab has no IVirtualCell component.");
            }
            
            return cell;
        }

        public void Destroy(IVirtualCell cell)
        {
            if (cell == null) return;
            
            var go = cell.Rect != null ? cell.Rect.gameObject : (cell as Component)?.gameObject;

            if (go == null) return;
            
            Object.Destroy(go);
        }
    }
}