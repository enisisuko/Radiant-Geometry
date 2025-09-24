using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class RedCurtainTransition : MonoBehaviour
{
    public static RedCurtainTransition Instance { get; private set; }

    [Header("���")]
    public Color curtainColor = new Color(1f, 0f, 0f, 0f); // ��ʼ͸����
    [Tooltip("Canvas ������Խ��Խ��ǰ��")]
    public int sortingOrder = 32767;

    Canvas canvas;     // ����Ҫ��/������ Canvas���������������ϣ�
    Image image;       // ȫ����Ļ
    bool isBusy;

    // ���� ���⾲̬��� ���� //
    public static void Go(string sceneName, float fadeIn = 0.8f, float fadeOut = 1.2f, float holdAtFull = 0f)
    {
        Ensure();
        Instance.Play(sceneName, fadeIn, fadeOut, holdAtFull);
    }

    // ���� ȷ��ʵ�����ڣ���Ҫʱ��̬������ ���� //
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
        // 1) �������ã���������������ϵ� Canvas
        canvas = GetComponent<Canvas>() ?? GetComponentInChildren<Canvas>(true);

        // 2) û�оͳ����ڡ��Լ����ӣ���ʧ�ܣ��������� Canvas �����ƣ����˶��������崴��
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

        // ��ȫ������������û�õ�����ֱ�ӱ����أ�������� NRE
        if (!canvas)
        {
            Debug.LogError("[RedCurtainTransition] �޷�����/��ȡ Canvas����ȷ�� Unity UI (UGUI) �������á�", this);
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        // ȷ���� CanvasScaler�����⵲�������Ƴ� GraphicRaycaster ����� RaycastTarget
        if (!canvas.TryGetComponent<CanvasScaler>(out _))
            canvas.gameObject.AddComponent<CanvasScaler>();
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster) Destroy(raycaster); // ����Ҫ���ص��

        // 3) ����/����ȫ�� Image������ Canvas �£�
        if (!image)
        {
            var imgGO = new GameObject("Curtain");
            imgGO.transform.SetParent(canvas.transform, false);
            image = imgGO.AddComponent<Image>();
        }

        image.raycastTarget = false;      // ���� UI ����
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

        // ���� ���루��ȫ�죩����
        yield return Fade(0f, 1f, fadeIn);

        // ���� ����ʱ��ͣ��Ƭ�� ������ʹ��δ����ʱ�䣬���� timeScale Ӱ�죩
        if (holdAtFull > 0f)
            yield return new WaitForSecondsRealtime(holdAtFull);

        // ���� �л��������첽������
        if (!string.IsNullOrEmpty(sceneName))
        {
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;
        }

        // ���� ��������͸��������
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
            t += Time.unscaledDeltaTime; // ���� Time.timeScale Ӱ��
            float a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            SetAlpha(a);
            yield return null;
        }
        SetAlpha(to);
    }
}
