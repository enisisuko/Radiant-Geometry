using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.World
{
    /// <summary>
    /// ���� EnergyPickup.energyColor���� Light2D Ⱦ�ɶ�Ӧ��ɫ����/�̣���
    /// ��ѡ����΢������˸����ʾ����ʰȡ����
    /// </summary>
    [DisallowMultipleComponent]
    public class EnergyPickupLightTint : MonoBehaviour
    {
        [Header("Refs")]
        public EnergyPickup pickup;        // ��Ϊ�գ��Զ��ڸ���/�������
        public Light2D light2D;            // ��Ϊ�գ��Զ������������

        [Header("Colors")]
        public Color redEnergyLight = new Color(1f, 0.3f, 0.3f, 1f);
        public Color greenEnergyLight = new Color(0.3f, 1f, 0.3f, 1f);

        [Header("Breath")]
        public bool breathe = true;
        public float baseIntensity = 1.0f;
        public float breatheAmplitude = 0.15f;
        public float breatheSpeed = 2.4f;

        private void Reset()
        {
            pickup = GetComponentInParent<EnergyPickup>();
            if (!pickup) pickup = GetComponent<EnergyPickup>();
            if (!light2D) light2D = GetComponentInChildren<Light2D>(true);
        }

        private void Awake()
        {
            if (!pickup) pickup = GetComponentInParent<EnergyPickup>() ?? GetComponent<EnergyPickup>();
            if (!light2D) light2D = GetComponentInChildren<Light2D>(true);
            ApplyColor();
        }

        private void Update()
        {
            if (!light2D) return;

            // ����������ȷ��ɫ��������ʱ�� energyColor Ҳ�ܸ��ϣ�
            ApplyColor();

            if (breathe)
            {
                float t = Time.time * breatheSpeed;
                light2D.intensity = baseIntensity + Mathf.Sin(t) * breatheAmplitude;
            }
        }

        private void ApplyColor()
        {
            if (!pickup || !light2D) return;
            light2D.color = (pickup.energyColor == FadedDreams.Player.ColorMode.Red)
                ? redEnergyLight
                : greenEnergyLight;
        }
    }
}
