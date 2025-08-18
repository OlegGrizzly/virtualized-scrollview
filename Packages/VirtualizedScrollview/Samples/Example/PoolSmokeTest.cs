using System.Collections.Generic;
using OlegGrizzly.VirtualizedScrollview.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Samples.Example
{
    public sealed class PoolSmokeTest : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private RectTransform parent;
        [SerializeField] private PooledBox prefab;

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarm = 16;
        [SerializeField, Min(0)] private int maxPooled = 32;

        [Header("Spawn")]
        [SerializeField, Min(0)] private int initialSpawn = 8;
        [SerializeField, Min(0)] private int spawnBatch = 5;
        [SerializeField, Min(0)] private int releaseBatch = 5;

        [Header("Growth test")]
        [SerializeField] private bool forceGrowth; // периодически спауним больше, чем релизим — чтобы вынудить Instantiate
        [SerializeField, Min(0)] private int growthSpike = 10; // сколько ДОПОЛНИТЕЛЬНО спаунить в спайк
        [SerializeField, Min(1)] private int spikeEvery = 4;   // каждые N циклов
        private int _churnCounter;

        [Header("Auto churn")]
        [SerializeField] private bool autoChurn;
        [SerializeField, Min(0f)] private float churnInterval = 0.5f;

        private ComponentPool<PooledBox> _pool;
        private readonly List<PooledBox> _active = new();

        private float _t;

        private void Awake()
        {
            if (!parent) Debug.LogError("PoolSmokeTest: parent is not set");
            if (!prefab) Debug.LogError("PoolSmokeTest: prefab is not set");

            _pool = new ComponentPool<PooledBox>(prefab, parent, prewarm, maxPooled);
        }

        private void Start()
        {
            Spawn(initialSpawn);
        }

        private void Update()
        {
            if (autoChurn)
            {
                _t += Time.unscaledDeltaTime;
                if (_t >= churnInterval)
                {
                    _t = 0f;
                    Churn();
                }
            }

            // хоткеи для ручного теста
            if (Input.GetKeyDown(KeyCode.Alpha1)) Spawn(spawnBatch);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ReleaseSome(releaseBatch);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ReleaseAll();
            if (Input.GetKeyDown(KeyCode.Alpha4)) ClearAndDestroy();
        }

        private void Spawn(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var box = _pool.Get();
                _active.Add(box);

                // Разбрасываем по родителю (UI): задаём anchoredPosition
                var rt = box.GetComponent<RectTransform>();
                if (rt)
                {
                    var w = parent.rect.width;
                    var h = parent.rect.height;
                    var x = Random.Range(-w * 0.5f, w * 0.5f);
                    var y = Random.Range(-h * 0.5f, h * 0.5f);
                    rt.anchoredPosition = new Vector2(x, y);
                    rt.sizeDelta = new Vector2(120, 40);
                }
            }
        }

        private void ReleaseSome(int count)
        {
            // снимаем с конца — быстрее
            for (int i = 0; i < count && _active.Count > 0; i++)
            {
                var last = _active[^1];
                _active.RemoveAt(_active.Count - 1);
                _pool.Release(last);
            }
        }

        private void ReleaseAll()
        {
            _pool.ReleaseAll();
            _active.Clear();
        }

        private void ClearAndDestroy()
        {
            _pool.ClearAndDestroy();
            _active.Clear();
        }

        private void Churn()
        {
            _churnCounter++;
            var extra = (forceGrowth && (_churnCounter % spikeEvery == 0)) ? growthSpike : 0;

            ReleaseSome(releaseBatch);
            Spawn(spawnBatch + extra);
        }
    }
}