using UnityEngine;
using UnityEngine.Events;
using FadedDreams.Player; // RedLightController

namespace FadedDreams.World.Mechanics
{
    public class HeatPlatform : MonoBehaviour
    {
        public RedLightController red;  // �뱾��ͬ�ڵ�/���ⲿ��ק
        public float threshold = 50f;

        public enum Mode { Once, Repeat }
        public Mode mode = Mode.Repeat;

        [Tooltip("Repeat ģʽ���������̿۳��ĺ��")]
        public float repeatCost = 20f;
        [Tooltip("������ȴ������ÿ֡����")]
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
                if (repeatCost > 0f) red.TryConsume(repeatCost); // �� Red �����ѽӿ�:contentReference[oaicite:6]{index=6}
                _cd = triggerCooldown;
            }
        }
    }
}
