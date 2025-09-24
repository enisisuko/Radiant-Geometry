using UnityEngine;

namespace FadedDreams.Player
{
    /// <summary>
    /// 统一的受伤入口：玩家受伤即扣“当前模式”的能量。
    /// 请调用 TakeDamage(dmg)。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealthLight : MonoBehaviour
    {
        [Tooltip("受伤时可选的最小间隔，避免一帧多次命中叠伤")]
        public float hurtIFrame = 0.05f;

        private float _lastHurtTime = -999f;
        private PlayerColorModeController _mode;

        private void Awake()
        {
            _mode = GetComponent<PlayerColorModeController>();
            if (!_mode) _mode = GetComponentInParent<PlayerColorModeController>();
        }

        public void TakeDamage(float amount)
        {
            if (Time.time - _lastHurtTime < hurtIFrame) return;
            _lastHurtTime = Time.time;

            if (!_mode) return;
            // 扣“当前模式”的能量值
            _mode.SpendEnergy(_mode.Mode, Mathf.Max(0f, amount));
            // 如果你需要受伤屏幕特效/音效，可以在这里钩
        }
    }
}
