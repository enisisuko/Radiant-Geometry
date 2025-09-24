using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class FadeScreen : MonoBehaviour
{
    public static FadeScreen Instance { get; private set; }

    [Header("Fade")]
    [Range(0f, 5f)] public float defaultDuration = 0.8f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    CanvasGroup cg;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureCanvas();
        SetAlpha(0f);
    }

    void EnsureCanvas()
    {
        var go = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // 始终最上层

        var blocker = new GameObject("Blocker", typeof(Image));
        blocker.transform.SetParent(go.transform, false);
        var img = blocker.GetComponent<Image>();
        img.color = Color.black;
        var rt = blocker.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;   // 渐变期间阻塞点击
        cg.interactable = false;
    }

    public Coroutine FadeOut(float duration = -1f) => StartCoroutine(FadeTo(1f, duration));
    public Coroutine FadeIn(float duration = -1f) => StartCoroutine(FadeTo(0f, duration));

    IEnumerator FadeTo(float target, float duration)
    {
        if (duration < 0f) duration = defaultDuration;
        float start = cg.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = t / duration;
            cg.alpha = Mathf.Lerp(start, target, curve.Evaluate(u));
            yield return null;
        }
        cg.alpha = target;
    }

    public void SetAlpha(float a) => cg.alpha = Mathf.Clamp01(a);
}
