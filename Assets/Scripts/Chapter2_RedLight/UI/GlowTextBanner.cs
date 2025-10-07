using UnityEngine;
using TMPro;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// ��Ļ����/�����һ�з������ֿ�������
    /// ���� HUD Canvas �£��� TextMeshProUGUI �� CanvasGroup��
    /// </summary>
    public class GlowTextBanner : MonoBehaviour
    {
        public static GlowTextBanner Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI label;

        [Header("Defaults")]
        [SerializeField, Min(0f)] private float defaultFadeIn = 0.35f;
        [SerializeField, Min(0f)] private float defaultFadeOut = 0.35f;

        // ��ǰ��ӵ���ߡ�������Ĵ����������� token �����������ص�ʱ�໥������ʾ
        private string ownerToken = null;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        public void Show(string text, string token, float? fadeIn = null)
        {
            ownerToken = token; // ������Ȩ�������½���Ĵ�����
            if (label != null) label.text = text ?? string.Empty;

            float duration = Mathf.Max(0f, fadeIn ?? defaultFadeIn);
            StartFadeTo(1f, duration);
        }

        /// <summary>
        /// ֻ�е����÷� token �뵱ǰӵ����һ��ʱ�Ż����أ�������ԣ�����������أ�
        /// </summary>
        public void Hide(string token, float? fadeOut = null)
        {
            if (ownerToken != token) return;
            ownerToken = null;

            float duration = Mathf.Max(0f, fadeOut ?? defaultFadeOut);
            StartFadeTo(0f, duration);
        }

        public void ForceHide(float? fadeOut = null)
        {
            ownerToken = null;
            float duration = Mathf.Max(0f, fadeOut ?? defaultFadeOut);
            StartFadeTo(0f, duration);
        }

        private void StartFadeTo(float target, float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeTo(target, duration));
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (canvasGroup == null) yield break;

            float start = canvasGroup.alpha;
            if (Mathf.Approximately(duration, 0f))
            {
                canvasGroup.alpha = target;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // �� unscaled ��֤��ͣ�˵���Ӱ�쵭�뵭��
                canvasGroup.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            canvasGroup.alpha = target;
        }
    }
}
