// RedLightController.cs
using UnityEngine;
using UnityEngine.Events;

namespace FadedDreams.Player
{
    [DisallowMultipleComponent]
    public class RedLightController : MonoBehaviour
    {
        [Header("Config")]
        public float maxRed = 100f;
        public float startRed = 100f;
        [Tooltip("红光为 0 时是否仍允许使用“散射照明”")]
        public bool allowScatterWhenEmpty = true;

        [Header("Runtime State")]
        [SerializeField] private float current;
        public UnityEvent<float, float> onChanged;   // (current, max)
        public UnityEvent onDepleted;                // 归零（>0→=0）
        public UnityEvent onRelit;                   // 复燃（=0→>0）

        public float Current => current;
        public float Max => maxRed;
        public float Percent01 => maxRed > 0f ? current / maxRed : 0f;
        public bool IsEmpty => current <= 0.01f;

        public bool CanScatter => allowScatterWhenEmpty || !IsEmpty;
        public bool CanConverge => !IsEmpty;

        private void OnEnable()
        {
            current = Mathf.Clamp(startRed, 0, maxRed);
            if (onChanged == null) onChanged = new UnityEvent<float, float>();
            if (onDepleted == null) onDepleted = new UnityEvent();
            if (onRelit == null) onRelit = new UnityEvent();
            onChanged.Invoke(current, maxRed);
        }

        public void Set(float value)
        {
            float prev = current;
            current = Mathf.Clamp(value, 0, maxRed);
            onChanged?.Invoke(current, maxRed);
            if (prev > 0f && current <= 0f) onDepleted?.Invoke();
            if (prev <= 0f && current > 0f) onRelit?.Invoke();
        }

        public void Add(float amount)
        {
            if (Mathf.Approximately(amount, 0f)) return;
            Set(current + amount);
        }

        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (current < amount) { Set(0f); return false; }
            Set(current - amount);
            return true;
        }

        // 敌人命中时调用
        public void OnHitByDarkSprite()
        {
            TryConsume(25f);
        }
    }
}
