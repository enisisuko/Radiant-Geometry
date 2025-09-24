using UnityEngine;
using UnityEngine.Events;
using FadedDreams.Player; // RedLightController

namespace FadedDreams.World.Mechanics
{
    public class HeatPlatform : MonoBehaviour
    {
        public RedLightController red;  // 与本体同节点/或外部拖拽
        public float threshold = 50f;

        public enum Mode { Once, Repeat }
        public Mode mode = Mode.Repeat;

        [Tooltip("Repeat 模式触发后立刻扣除的红光")]
        public float repeatCost = 20f;
        [Tooltip("触发冷却，避免每帧触发")]
        public float triggerCooldown = 0.3f;

        public UnityEvent onTriggered;

        private float _cd;

        private void Reset() => red = GetComponent<RedLightController>();

        private void Update()
        {
            if (!red) return;
            _cd -= Time.deltaTime; if (_cd > 0f) return;

            if (red.Current >= threshold)
            {
                onTriggered.Invoke();
                if (mode == Mode.Once) { Destroy(this); return; }
                if (repeatCost > 0f) red.TryConsume(repeatCost); // 用 Red 的消费接口:contentReference[oaicite:6]{index=6}
                _cd = triggerCooldown;
            }
        }
    }
}
