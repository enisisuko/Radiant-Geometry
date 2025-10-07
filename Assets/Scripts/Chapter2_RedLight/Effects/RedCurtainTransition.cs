using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class RedCurtainTransition : MonoBehaviour
{
    public static RedCurtainTransition Instance { get; private set; }

    [Header("外观")]
    public Color curtainColor = new Color(1f, 0f, 0f, 0f); // 起始透明红
    [Tooltip("Canvas 的排序（越大越靠前）")]
    public int sortingOrder = 32767;

    Canvas canvas;     // 我们要用/创建的 Canvas（可能在子物体上）
    Image image;       // 全屏红幕
    bool isBusy;

    // —— 对外静态入口 —— //
    public static void Go(string sceneName, float fadeIn = 0.8f, float fadeOut = 1.2f, float holdAtFull = 0f)
    {
        Ensure();
        Instance.Play(sceneName, fadeIn, fadeOut, holdAtFull);
    }

    // —— 确保实例存在（必要时动态创建） —— //
    public static void Ensure()
    {
        if (Instance) return;
        var go = new GameObject("~RedCurtainTransition(Auto)");
        Instance = go.AddComponent<RedCurtainTransition>();
        DontDestroyOnLoad(go);
        Instance.BuildOverlay();
    }

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!canvas || !image)
            BuildOverlay();
    }

    void BuildOverlay()
    {
        // 1) 尽量复用：本物体或子物体上的 Canvas
        canvas = GetComponent<Canvas>() ?? GetComponentInChildren<Canvas>(true);

        // 2) 没有就尝试在“自己”加；若失败（可能已有 Canvas 或被限制），退而在子物体创建
        if (!canvas)
        {
            canvas = gameObject.AddComponent<Canvas>();
            if (!canvas)
            {
                var cgo = new GameObject("CurtainCanvas");
                cgo.transform.SetParent(transform, false);
                canvas = cgo.AddComponent<Canvas>();
            }
        }

        // 安全保护：若还是没拿到，就直接报错返回，避免后续 NRE
        if (!canvas)
        {
            Debug.LogError("[RedCurtainTransition] 无法创建/获取 Canvas。请确认 Unity UI (UGUI) 包已启用。", this);
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        // 确保有 CanvasScaler；避免挡交互，移除 GraphicRaycaster 或禁用 RaycastTarget
        if (!canvas.TryGetComponent<CanvasScaler>(out _))
            canvas.gameObject.AddComponent<CanvasScaler>();
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster) Destroy(raycaster); // 不需要拦截点击

        // 3) 创建/复用全屏 Image（放在 Canvas 下）
        if (!image)
        {
            var imgGO = new GameObject("Curtain");
            imgGO.transform.SetParent(canvas.transform, false);
            image = imgGO.AddComponent<Image>();
        }

        image.raycastTarget = false;      // 不挡 UI 交互
        image.color = curtainColor;

        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        SetAlpha(0f);
    }

    void SetAlpha(float a)
    {
        if (!image) return;
        var c = image.color;
        c.a = Mathf.Clamp01(a);
        image.color = c;
    }

    public void Play(string sceneName, float fadeIn, float fadeOut, float holdAtFull)
    {
        if (isBusy) return;
        StartCoroutine(CoPlay(sceneName, Mathf.Max(0f, fadeIn), Mathf.Max(0f, fadeOut), Mathf.Max(0f, holdAtFull)));
    }

    IEnumerator CoPlay(string sceneName, float fadeIn, float fadeOut, float holdAtFull)
    {
        isBusy = true;

        // —— 淡入（到全红）——
        yield return Fade(0f, 1f, fadeIn);

        // —— 满红时可停留片刻 ——（使用未缩放时间，不受 timeScale 影响）
        if (holdAtFull > 0f)
            yield return new WaitForSecondsRealtime(holdAtFull);

        // —— 切换场景（异步）——
        if (!string.IsNullOrEmpty(sceneName))
        {
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;
        }

        // —— 淡出（回透明）——
        yield return Fade(1f, 0f, fadeOut);

        isBusy = false;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (!image)
            yield break;

        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float t = 0f;
        SetAlpha(from);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // 不受 Time.timeScale 影响
            float a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            SetAlpha(a);
            yield return null;
        }
        SetAlpha(to);
    }
}
