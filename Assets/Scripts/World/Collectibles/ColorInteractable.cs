using UnityEngine;
using FadedDreams.Player;

namespace FadedDreams.World
{
    /// <summary>
    /// Interactables that respond to player's current light color (Chapter 3+).
    /// </summary>
    public class ColorInteractable : MonoBehaviour
    {
        public enum ColorType { Red, Green, Blue }
        public ColorType type;
        public bool isHarmfulToMatch = false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var plc = other.GetComponent<PlayerLightController>();
            if (plc == null) return;

            bool match = (type == ColorType.Red && plc.mode == PlayerLightController.LightMode.Red) ||
                         (type == ColorType.Green && plc.mode == PlayerLightController.LightMode.Green) ||
                         (type == ColorType.Blue && plc.mode == PlayerLightController.LightMode.Blue);

            if (isHarmfulToMatch && match)
            {
                plc.currentEnergy = Mathf.Max(0, plc.currentEnergy - 25f);
            }
            else if (match)
            {
                // Example: open door / enable platform
                gameObject.SetActive(false);
            }
        }
    }
}
