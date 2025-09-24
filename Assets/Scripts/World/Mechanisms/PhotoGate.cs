// Scripts/World/PhotoGate.cs
using UnityEngine;

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    public class PhotoGate : MonoBehaviour
    {
        public enum OpenMode
        {
            LightOpens, // �� �� ������ �� ��
            DarkOpens   // �� �� ������ �� ��
        }

        [Header("Sensor")]
        public LightIrradianceSensor sensor;

        [Header("Open Mode")]
        public OpenMode openMode = OpenMode.DarkOpens;

        [Header("Threshold (with hysteresis, in % of fullIntensity)")]
        [Tooltip(">= CloseThreshold �� ���������ˡ�������<= OpenThreshold �� ���������ˡ�����")]
        [Range(0f, 100f)] public float closeThreshold = 30f; // �����ˡ���ֵ
        [Range(0f, 100f)] public float openThreshold = 25f;  // �����ˡ���ֵ

        [Header("Effect")]
        [Tooltip("�ŵ���ײ�壺Open=true ʱ���ã���ͨ����")]
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

            // �ѵ�ǰ�նȻ��㵽 0..100 �����ֵ����� sensor.fullIntensity��
            float pct = sensor.IrradianceRaw / Mathf.Max(0.0001f, sensor.fullIntensity) * 100f;

            bool wantOpen = _open;

            // �ж��߼����ȸ�����ֵ���롰����/���ˡ��Ĳ������ٸ���ģʽ���������
            bool isBrightSide = pct >= closeThreshold;
            bool isDarkSide = pct <= openThreshold;

            switch (openMode)
            {
                case OpenMode.LightOpens:
                    // �������������أ��г��ͣ����ⶶ����
                    if (isBrightSide) wantOpen = true;
                    else if (isDarkSide) wantOpen = false;
                    break;

                case OpenMode.DarkOpens:
                    // �������������أ������������
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
            if (gateCollider) gateCollider.enabled = !_open; // Open=true �� ���赲
            if (animator && !string.IsNullOrEmpty(animParam))
                animator.SetBool(animParam, _open);
        }
    }
}
