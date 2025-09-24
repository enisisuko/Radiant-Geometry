// Scripts/World/PhotoGate.cs
using UnityEngine;

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    public class PhotoGate : MonoBehaviour
    {
        public enum OpenMode
        {
            LightOpens, // 亮 → 开，暗 → 关
            DarkOpens   // 暗 → 开，亮 → 关
        }

        [Header("Sensor")]
        public LightIrradianceSensor sensor;

        [Header("Open Mode")]
        public OpenMode openMode = OpenMode.DarkOpens;

        [Header("Threshold (with hysteresis, in % of fullIntensity)")]
        [Tooltip(">= CloseThreshold → 触发“亮端”动作，<= OpenThreshold → 触发“暗端”动作")]
        [Range(0f, 100f)] public float closeThreshold = 30f; // “亮端”阈值
        [Range(0f, 100f)] public float openThreshold = 25f;  // “暗端”阈值

        [Header("Effect")]
        [Tooltip("门的碰撞体：Open=true 时禁用（可通过）")]
        public Collider2D gateCollider;
        public Animator animator;
        public string animParam = "Open";

        bool _open = true;

        void Reset()
        {
            gateCollider = GetComponent<Collider2D>();
            animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (!sensor) return;

            // 把当前照度换算到 0..100 的相对值（相对 sensor.fullIntensity）
            float pct = sensor.IrradianceRaw / Mathf.Max(0.0001f, sensor.fullIntensity) * 100f;

            bool wantOpen = _open;

            // 判断逻辑：先根据阈值进入“亮端/暗端”的布尔，再根据模式决定开或关
            bool isBrightSide = pct >= closeThreshold;
            bool isDarkSide = pct <= openThreshold;

            switch (openMode)
            {
                case OpenMode.LightOpens:
                    // 亮→开，暗→关（有迟滞，避免抖动）
                    if (isBrightSide) wantOpen = true;
                    else if (isDarkSide) wantOpen = false;
                    break;

                case OpenMode.DarkOpens:
                    // 暗→开，亮→关（你最初的需求）
                    if (isBrightSide) wantOpen = false;
                    else if (isDarkSide) wantOpen = true;
                    break;
            }

            if (wantOpen != _open)
            {
                _open = wantOpen;
                Apply();
            }
        }

        void Apply()
        {
            if (gateCollider) gateCollider.enabled = !_open; // Open=true → 不阻挡
            if (animator && !string.IsNullOrEmpty(animParam))
                animator.SetBool(animParam, _open);
        }
    }
}
