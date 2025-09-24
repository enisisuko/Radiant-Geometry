using UnityEngine;
using FadedDreams.UI;

namespace FadedDreams.World
{
    /// <summary>
    /// 玩家进入范围时在屏幕显示一行发光文本；离开时淡出。
    /// 在场景中放一个带 Collider2D(isTrigger) 的物体，挂此脚本，设置 message 即可。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GlowTextZone : MonoBehaviour
    {
        [TextArea(1, 3)]
        public string message = "Light will guide you.";

        [Header("Fade (seconds)")]
        [Min(0f)] public float fadeIn = 0.35f;
        [Min(0f)] public float fadeOut = 0.35f;

        [Header("Player Filter")]
        public string playerTag = "Player";

        // 用实例 ID 作为 token，避免与别的触发器互相干扰
        private string token;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void Awake()
        {
            token = GetInstanceID().ToString();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (GlowTextBanner.Instance == null) return;

            GlowTextBanner.Instance.Show(message, token, fadeIn);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (GlowTextBanner.Instance == null) return;

            GlowTextBanner.Instance.Hide(token, fadeOut);
        }
    }
}
