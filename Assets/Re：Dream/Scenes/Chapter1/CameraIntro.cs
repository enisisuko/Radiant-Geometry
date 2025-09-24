using UnityEngine;

public class CameraIntro : MonoBehaviour
{
    [Header("位移设置")]
    [Tooltip("从原位向镜头前方推进的距离（越大越“近”）")]
    public float nearDistance = 3f;
    [Tooltip("拉回到原位所需时长（秒）")]
    public float moveDuration = 2.0f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("可选：视角动画")]
    public bool animateFOV = true;
    public float startFOV = 35f;
    public float endFOV = 60f;

    [Header("黑幕淡出")]
    [Tooltip("全屏 CanvasGroup（起始 Alpha=1）")]
    public CanvasGroup fadeGroup;
    [Tooltip("黑幕从 1 淡到 0 的时长（秒）")]
    public float fadeDuration = 1.2f;

    [Header("其他")]
    public float delayBeforeStart = 0f;
    public bool useUnscaledTime = true;

    Vector3 _originalPos;
    Quaternion _originalRot;
    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _originalPos = transform.position;
        _originalRot = transform.rotation;
    }

    void OnEnable()
    {
        // 先把相机推近到“起始位置”
        Vector3 forward = _originalRot * Vector3.forward;
        Vector3 startPos = _originalPos + forward * nearDistance; // 从更“近”的位置开始
        transform.SetPositionAndRotation(startPos, _originalRot);

        if (animateFOV && _cam != null) _cam.fieldOfView = startFOV;

        // 确保黑幕一开始是全黑
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true; // 可挡住误触；结束后会关
        }

        StopAllCoroutines();
        StartCoroutine(PlayIntro());
    }

    System.Collections.IEnumerator PlayIntro()
    {
        if (delayBeforeStart > 0f)
        {
            float d = 0f;
            while (d < delayBeforeStart)
            {
                d += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }

        float t = 0f;
        while (t < Mathf.Max(moveDuration, 0.0001f))
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            float u = Mathf.Clamp01(t / moveDuration);
            float k = ease.Evaluate(u);

            // 位移回到原位
            transform.position = Vector3.LerpUnclamped(
                _originalPos + (_originalRot * Vector3.forward) * nearDistance,
                _originalPos,
                k
            );

            // FOV 变化（可选）
            if (animateFOV && _cam != null)
                _cam.fieldOfView = Mathf.Lerp(startFOV, endFOV, k);

            // 黑幕淡出：与镜头动画并行
            if (fadeGroup != null && fadeDuration > 0f)
            {
                float f = Mathf.Clamp01(t / fadeDuration);
                fadeGroup.alpha = 1f - f;
            }

            yield return null;
        }

        // 兜底对齐最终状态
        transform.SetPositionAndRotation(_originalPos, _originalRot);
        if (animateFOV && _cam != null) _cam.fieldOfView = endFOV;

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }
    }
}
