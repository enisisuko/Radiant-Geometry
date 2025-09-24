using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance { get; private set; }

    [Header("Auto")]
    public float autoFadeInOnLoad = 1.0f;   // �����³�����ĵ���ʱ��

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
        // ���Ը���
        canvas = GetComponentInChildren<Canvas>();
        img = GetComponentInChildren<Image>();

        if (!canvas)
        {
            var go = new GameObject("ScreenFadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50000; // ��֤���ϲ�
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
        // �³����Զ��Ӻڵ��루���ǰһ�������ڴ��ڣ�
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

    // ������
    public static ScreenFade Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("~ScreenFade");
        return go.AddComponent<ScreenFade>();
    }
}
