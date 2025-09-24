using UnityEngine;
using UnityEngine.UI;
using FadedDreams.Player;

namespace FadedDreams.UI
{
    public class RedLightHUD : MonoBehaviour
    {
        public RedLightController red;
        public Slider slider;
        public Gradient colorByPercent;
        public Image fill;

        private void Awake()
        {
            if (!red) red = FindObjectOfType<RedLightController>();
        }

        private void OnEnable()
        {
            if (!red) return;
            slider.maxValue = red.Max;
            slider.value = red.Current;     // 统一从控制器读
            red.onChanged.AddListener(OnRedChanged);
            UpdateColor();
        }

        private void OnDisable()
        {
            if (red) red.onChanged.RemoveListener(OnRedChanged);
        }

        private void OnRedChanged(float cur, float max)
        {
            slider.maxValue = max;
            slider.value = cur;
            UpdateColor();
        }

        private void UpdateColor()
        {
            if (!fill || !red) return;
            fill.color = colorByPercent.Evaluate(red.Percent01);
        }
    }
}
