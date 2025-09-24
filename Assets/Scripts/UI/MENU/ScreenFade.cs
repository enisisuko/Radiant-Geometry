using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance { get; private set; }

    [Header("Auto")]
    public float autoFadeInOnLoad = 1.0f;   // 进入新场景后的淡入时长

    Canvas canvas;
    Image img;

    void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureCanvasImage();
        SetAlpha(0f);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void EnsureCanvasImage()
    {
        // 尝试复用
        canvas = GetComponentInChildren<Canvas>();
        img = GetComponentInChildren<Image>();

        if (!canvas)
        {
            var go = new GameObject("ScreenFadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50000; // 保证最上层
        }
        if (!img)
        {
            var go = new GameObject("Black", typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            img = go.GetComponent<Image>();
            img.color = Color.black;
            var r = img.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 新场景自动从黑淡入（如果前一刻正处于纯黑）
        if (img && img.color.a > 0.99f && autoFadeInOnLoad > 0f)
        {
            StartCoroutine(FadeIn(autoFadeInOnLoad));
        }
    }

    void SetAlpha(float a)
    {
        if (!img) return;
        var c = img.color; c.a = Mathf.Clamp01(a); img.color = c;
    }

    public IEnumerator FadeOut(float dur)
    {
        EnsureCanvasImage();
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }
        SetAlpha(1f);
    }

    public IEnumerator FadeIn(float dur)
    {
        EnsureCanvasImage();
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.SmoothStep(1f, 0f, t / dur));
            yield return null;
        }
        SetAlpha(0f);
    }

    // 简便入口
    public static ScreenFade Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("~ScreenFade");
        return go.AddComponent<ScreenFade>();
    }
}
