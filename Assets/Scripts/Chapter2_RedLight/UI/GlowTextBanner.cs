using UnityEngine;
using TMPro;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 屏幕顶部/中央的一行发光文字控制器。
    /// 放在 HUD Canvas 下，绑定 TextMeshProUGUI 与 CanvasGroup。
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

        // 当前“拥有者”（进入的触发器），用 token 避免多个区间重叠时相互抢夺显示
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
            ownerToken = token; // 把所有权交给最新进入的触发器
            if (label != null) label.text = text ?? string.Empty;

            float duration = Mathf.Max(0f, fadeIn ?? defaultFadeIn);
            StartFadeTo(1f, duration);
        }

        /// <summary>
        /// 只有当调用方 token 与当前拥有者一致时才会隐藏；否则忽略（避免别的区误关）
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
                t += Time.unscaledDeltaTime; // 用 unscaled 保证暂停菜单不影响淡入淡出
                canvasGroup.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            canvasGroup.alpha = target;
        }
    }
}
