// Assets/Scripts/WORLD/InstantLightDrain.cs
using UnityEngine;
using FadedDreams.Player; // PlayerLightController

namespace FadedDreams.World
{
    /// <summary>
    /// 持续失光区：玩家进入后，只要停留在本触发器内，
    /// 每秒扣除 drainPerSecond（默认 100）光量。
    /// 死亡/重生由 PlayerLightController 内部统一处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class InstantLightDrain : MonoBehaviour
    {
        [Tooltip("只对该 Tag 生效（留空则对所有进入者生效）。")]
        [SerializeField] private string requiredTag = "Player";

        [Tooltip("每秒扣除的光量。")]
        [Min(0f)]
        [SerializeField] private float drainPerSecond = 100f;

        // 进入计数，兼容“玩家有多个碰撞体”的情况，避免出/入抖动
        private int _insideCount = 0;
        private PlayerLightController _plcCached;
        private Collider2D _col;

        private void Reset()
        {
            _col = GetComponent<Collider2D>();
            if (_col) _col.isTrigger = true; // 作为触发器使用
        }

        private void Awake()
        {
            if (!_col) _col = GetComponent<Collider2D>();
            if (_col && !_col.isTrigger) _col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other) return;
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

            var plc = other.GetComponentInParent<PlayerLightController>() ?? other.GetComponent<PlayerLightController>();
            if (plc == null) return;

            // 首次进入时缓存引用
            if (_insideCount == 0) _plcCached = plc;
            _insideCount++;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other) return;
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

            var plc = other.GetComponentInParent<PlayerLightController>() ?? other.GetComponent<PlayerLightController>();
            if (plc == null) return;

            // 只在当前缓存对象离开时计数减一
            if (_insideCount > 0 && plc == _plcCached)
            {
                _insideCount--;
                if (_insideCount <= 0)
                {
                    _insideCount = 0;
                    _plcCached = null; // 彻底离开后清空引用
                }
            }
        }

        private void Update()
        {
            if (_plcCached == null || _insideCount <= 0) return;
            if (drainPerSecond <= 0f) return;

            // 通过公共 API 持续扣能量；内部会在 <=0 时触发 onDeath + GameManager.OnPlayerDeath()
            _plcCached.AddEnergy(-drainPerSecond * Time.deltaTime);
        }

        // 可在运行时动态修改速率（例如难度曲线用）
        public void SetDrainPerSecond(float newRate)
        {
            drainPerSecond = Mathf.Max(0f, newRate);
        }
    }
}
