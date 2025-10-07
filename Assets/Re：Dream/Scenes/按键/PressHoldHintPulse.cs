// PressHoldHintPulse.cs
// 正常 -> 0.5s 缩到 0.7 并震颤 -> 2s 后停震 -> 0.5s 复原 -> 1s 间隔 -> 循环
using UnityEngine;

[DisallowMultipleComponent]
public class PressHoldHintPulse : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("要播放动画的目标，不填默认当前物体")]
    public Transform target;

    [Header("Timings (秒)")]
    public float shrinkDuration = 0.5f;
    public float shakeDuration = 2.0f;
    public float restoreDuration = 0.5f;
    public float repeatDelay = 1.0f;

    [Header("Scale")]
    [Tooltip("正常大小（一般为1,1,1）")]
    public Vector3 normalScale = Vector3.one;
    [Tooltip("缩小时的大小（例如 0.7）")]
    [Min(0f)] public float shrinkScale = 0.7f;
    public AnimationCurve easeIn = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve easeOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Shake 摇晃/震颤")]
    [Tooltip("位置抖动幅度（世界或本地：使用 localPosition）")]
    public Vector2 posAmplitude = new Vector2(0.1f, 0.1f);
    [Tooltip("Z 轴旋转抖动幅度（度）")]
    public float rotAmplitude = 9f;
    [Tooltip("抖动频率（越大越快）")]
    public float frequency = 18f;
    [Tooltip("使用非真实时间（忽略 Time.timeScale）")]
    public bool unscaledTime = false;

    // 运行时
    RectTransform rect;
    Vector3 baseLocalPos;
    Quaternion baseLocalRot;
    Vector3 baseScale;
    bool shaking;
    float seedX, seedY, seedR;
    Coroutine loopCo;

    void Reset()
    {
        target = transform;
    }

    void Awake()
    {
        if (!target) target = transform;
        rect = target as RectTransform;

        baseLocalPos = target.localPosition;
        baseLocalRot = target.localRotation;
        baseScale = normalScale == Vector3.zero ? target.localScale : normalScale;

        // 随机相位，避免多个实例完全同步
        seedX = Random.value * 10f;
        seedY = Random.value * 10f;
        seedR = Random.value * 10f;
    }

    void OnEnable()
    {
        StopAll();
        loopCo = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        StopAll();
        // 还原
        target.localPosition = baseLocalPos;
        target.localRotation = baseLocalRot;
        target.localScale = baseScale;
    }

    void StopAll()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = null;
        shaking = false;
    }

    System.Collections.IEnumerator Loop()
    {
        var waitFrame = new WaitForEndOfFrame();

        while (true)
        {
            // 1) 缩放到目标
            yield return TweenScale(baseScale, baseScale * shrinkScale, shrinkDuration, easeIn);

            // 2) 震颤阶段
            shaking = true;
            float t = 0f;
            while (t < shakeDuration)
            {
                float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                t += dt;

                ApplyShake(t);
                yield return waitFrame;
            }
            shaking = false;
            // 停止震颤时立刻恢复基础位姿（保持缩小后的scale）
            target.localPosition = baseLocalPos;
            target.localRotation = baseLocalRot;

            // 3) 回到正常大小
            yield return TweenScale(target.localScale, baseScale, restoreDuration, easeOut);

            // 4) 间隔
            float delay = repeatDelay;
            while (delay > 0f)
            {
                float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                delay -= dt;
                yield return waitFrame;
            }
        }
    }

    System.Collections.IEnumerator TweenScale(Vector3 from, Vector3 to, float dur, AnimationCurve curve)
    {
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        while (t < dur)
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / dur);
            float e = curve != null ? curve.Evaluate(k) : k;
            target.localScale = Vector3.LerpUnclamped(from, to, e);

            if (shaking) ApplyShake((unscaledTime ? Time.unscaledTime : Time.time));
            yield return null;
        }
        target.localScale = to;
    }

    void ApplyShake(float timeSec)
    {
        float t = (unscaledTime ? Time.unscaledTime : Time.time);

        // 使用 Perlin 噪声生成平滑抖动
        float nx = (Mathf.PerlinNoise(seedX, t * frequency) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(seedY, t * frequency) - 0.5f) * 2f;
        float nr = (Mathf.PerlinNoise(seedR, t * frequency) - 0.5f) * 2f;

        Vector3 offset = new Vector3(nx * posAmplitude.x, ny * posAmplitude.y, 0f);
        target.localPosition = baseLocalPos + offset;
        target.localRotation = Quaternion.Euler(0f, 0f, nr * rotAmplitude);
    }

#if UNITY_EDITOR
    // 在编辑器中实时预览调整 normalScale
    void OnValidate()
    {
        if (!target) target = transform;
        if (Application.isPlaying) return;
        target.localScale = normalScale == Vector3.zero ? Vector3.one : normalScale;
    }
#endif
}
