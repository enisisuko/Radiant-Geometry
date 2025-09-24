using FadedDreams.Player; // PlayerLightController
using UnityEngine;
using FadedDreams.Core;

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class InstantLightDrain : MonoBehaviour
    {
        [Tooltip("ֻ��ָ�� Tag ��Ч��Ĭ�� Player��")]
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
                // ֱ���������
                plc.currentEnergy = 0f;
                plc.onEnergyChanged?.Invoke();

                // �ֶ����������߼����� PlayerLightController.HandleEnergy ��һ�£�
                plc.onDeath?.Invoke();
                GameManager.Instance.OnPlayerDeath();
            }
        }
    }
}
