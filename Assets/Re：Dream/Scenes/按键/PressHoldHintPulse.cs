// PressHoldHintPulse.cs
// ���� -> 0.5s ���� 0.7 ����� -> 2s ��ͣ�� -> 0.5s ��ԭ -> 1s ��� -> ѭ��
using UnityEngine;

[DisallowMultipleComponent]
public class PressHoldHintPulse : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Ҫ���Ŷ�����Ŀ�꣬����Ĭ�ϵ�ǰ����")]
    public Transform target;

    [Header("Timings (��)")]
    public float shrinkDuration = 0.5f;
    public float shakeDuration = 2.0f;
    public float restoreDuration = 0.5f;
    public float repeatDelay = 1.0f;

    [Header("Scale")]
    [Tooltip("������С��һ��Ϊ1,1,1��")]
    public Vector3 normalScale = Vector3.one;
    [Tooltip("��Сʱ�Ĵ�С������ 0.7��")]
    [Min(0f)] public float shrinkScale = 0.7f;
    public AnimationCurve easeIn = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve easeOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Shake ҡ��/���")]
    [Tooltip("λ�ö������ȣ�����򱾵أ�ʹ�� localPosition��")]
    public Vector2 posAmplitude = new Vector2(0.1f, 0.1f);
    [Tooltip("Z ����ת�������ȣ��ȣ�")]
    public float rotAmplitude = 9f;
    [Tooltip("����Ƶ�ʣ�Խ��Խ�죩")]
    public float frequency = 18f;
    [Tooltip("ʹ�÷���ʵʱ�䣨���� Time.timeScale��")]
    public bool unscaledTime = false;

    // ����ʱ
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

        // �����λ��������ʵ����ȫͬ��
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
        // ��ԭ
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
            // 1) ���ŵ�Ŀ��
            yield return TweenScale(baseScale, baseScale * shrinkScale, shrinkDuration, easeIn);

            // 2) ����׶�
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
            // ֹͣ���ʱ���ָ̻�����λ�ˣ�������С���scale��
            target.localPosition = baseLocalPos;
            target.localRotation = baseLocalRot;

            // 3) �ص�������С
            yield return TweenScale(target.localScale, baseScale, restoreDuration, easeOut);

            // 4) ���
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

        // ʹ�� Perlin ��������ƽ������
        float nx = (Mathf.PerlinNoise(seedX, t * frequency) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(seedY, t * frequency) - 0.5f) * 2f;
        float nr = (Mathf.PerlinNoise(seedR, t * frequency) - 0.5f) * 2f;

        Vector3 offset = new Vector3(nx * posAmplitude.x, ny * posAmplitude.y, 0f);
        target.localPosition = baseLocalPos + offset;
        target.localRotation = Quaternion.Euler(0f, 0f, nr * rotAmplitude);
    }

#if UNITY_EDITOR
    // �ڱ༭����ʵʱԤ������ normalScale
    void OnValidate()
    {
        if (!target) target = transform;
        if (Application.isPlaying) return;
        target.localScale = normalScale == Vector3.zero ? Vector3.one : normalScale;
    }
#endif
}
