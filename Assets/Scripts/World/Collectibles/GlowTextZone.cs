using UnityEngine;
using FadedDreams.UI;

namespace FadedDreams.World
{
    /// <summary>
    /// ��ҽ��뷶Χʱ����Ļ��ʾһ�з����ı����뿪ʱ������
    /// �ڳ����з�һ���� Collider2D(isTrigger) �����壬�Ҵ˽ű������� message ���ɡ�
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

        // ��ʵ�� ID ��Ϊ token���������Ĵ������������
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
