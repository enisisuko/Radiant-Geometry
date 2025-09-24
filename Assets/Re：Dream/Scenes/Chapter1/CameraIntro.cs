using UnityEngine;

public class CameraIntro : MonoBehaviour
{
    [Header("λ������")]
    [Tooltip("��ԭλ��ͷǰ���ƽ��ľ��루Խ��Խ��������")]
    public float nearDistance = 3f;
    [Tooltip("���ص�ԭλ����ʱ�����룩")]
    public float moveDuration = 2.0f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("��ѡ���ӽǶ���")]
    public bool animateFOV = true;
    public float startFOV = 35f;
    public float endFOV = 60f;

    [Header("��Ļ����")]
    [Tooltip("ȫ�� CanvasGroup����ʼ Alpha=1��")]
    public CanvasGroup fadeGroup;
    [Tooltip("��Ļ�� 1 ���� 0 ��ʱ�����룩")]
    public float fadeDuration = 1.2f;

    [Header("����")]
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
        // �Ȱ�����ƽ�������ʼλ�á�
        Vector3 forward = _originalRot * Vector3.forward;
        Vector3 startPos = _originalPos + forward * nearDistance; // �Ӹ���������λ�ÿ�ʼ
        transform.SetPositionAndRotation(startPos, _originalRot);

        if (animateFOV && _cam != null) _cam.fieldOfView = startFOV;

        // ȷ����Ļһ��ʼ��ȫ��
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true; // �ɵ�ס�󴥣���������
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

            // λ�ƻص�ԭλ
            transform.position = Vector3.LerpUnclamped(
                _originalPos + (_originalRot * Vector3.forward) * nearDistance,
                _originalPos,
                k
            );

            // FOV �仯����ѡ��
            if (animateFOV && _cam != null)
                _cam.fieldOfView = Mathf.Lerp(startFOV, endFOV, k);

            // ��Ļ�������뾵ͷ��������
            if (fadeGroup != null && fadeDuration > 0f)
            {
                float f = Mathf.Clamp01(t / fadeDuration);
                fadeGroup.alpha = 1f - f;
            }

            yield return null;
        }

        // ���׶�������״̬
        transform.SetPositionAndRotation(_originalPos, _originalRot);
        if (animateFOV && _cam != null) _cam.fieldOfView = endFOV;

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }
    }
}
