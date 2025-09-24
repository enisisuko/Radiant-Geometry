using UnityEngine;
using FadedDreams.Player; // RedLightController

namespace FadedDreams.World.Mechanics
{
    [RequireComponent(typeof(Collider2D))]
    public class DarkHeatBlock : MonoBehaviour
    {
        [Header("Config")]
        public bool isTrigger = true;
        [Tooltip("ÿ��۳��ĺ�⣨=������")]
        public float drainPerSecond = 50f;

        private void Reset()
        {
            var c = GetComponent<Collider2D>();
            c.isTrigger = isTrigger;
        }

        // ��������ͣ���ڼ������
        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isTrigger) return;
            ApplyDrain(other);
        }

        // ʵ����ײ��ͣ���ڼ������
        private void OnCollisionStay2D(Collision2D col)
        {
            if (isTrigger) return;
            ApplyDrain(col.collider);
        }

        private void ApplyDrain(Component comp)
        {
            var red = comp.GetComponentInParent<RedLightController>();
            if (!red) return;
            red.TryConsume(drainPerSecond * Time.deltaTime);
        }
    }
}
