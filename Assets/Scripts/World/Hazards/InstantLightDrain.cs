using FadedDreams.Player; // PlayerLightController
using UnityEngine;
using FadedDreams.Core;

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class InstantLightDrain : MonoBehaviour
    {
        [Tooltip("只对指定 Tag 生效（默认 Player）")]
        public string requiredTag = "Player";

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other) return;
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

            var plc = other.GetComponentInParent<PlayerLightController>() ?? other.GetComponent<PlayerLightController>();
            if (plc != null)
            {
                // 直接清空能量
                plc.currentEnergy = 0f;
                plc.onEnergyChanged?.Invoke();

                // 手动触发死亡逻辑（和 PlayerLightController.HandleEnergy 里一致）
                plc.onDeath?.Invoke();
                GameManager.Instance.OnPlayerDeath();
            }
        }
    }
}
