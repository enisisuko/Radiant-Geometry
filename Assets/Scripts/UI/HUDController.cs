using UnityEngine;
using UnityEngine.UI;
using FadedDreams.Player;

namespace FadedDreams.UI
{
    public class HUDController : MonoBehaviour
    {
        public Slider energyBar;
        public TMPro.TextMeshProUGUI modeLabel;
        public PlayerLightController player;

        private void Start()
        {
            if (player != null)
            {
                player.onEnergyChanged.AddListener(Refresh);
                player.onModeChanged.AddListener(RefreshMode);
            }
            Refresh();
            RefreshMode();
        }

        void Refresh()
        {
            if (player == null || energyBar == null) return;
            energyBar.value = player.currentEnergy / player.maxEnergy;
        }

        void RefreshMode()
        {
            if (player == null || modeLabel == null) return;
            modeLabel.text = player.mode.ToString();
        }
    }
}
