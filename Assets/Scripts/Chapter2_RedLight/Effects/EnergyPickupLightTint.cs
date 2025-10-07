using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.World
{
    /// <summary>
    /// 依据 EnergyPickup.energyColor，把 Light2D 染成对应颜色（红/绿）。
    /// 可选：轻微呼吸闪烁以提示“可拾取”。
    /// </summary>
    [DisallowMultipleComponent]
    public class EnergyPickupLightTint : MonoBehaviour
    {
        [Header("Refs")]
        public EnergyPickup pickup;        // 若为空，自动在父级/本体查找
        public Light2D light2D;            // 若为空，自动在子物体查找

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

            // 持续沿用正确颜色（若运行时改 energyColor 也能跟上）
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
