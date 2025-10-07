// InfinityPathMover.cs
// ��Ŀ���ء��ޣ����ŵ� 8����·��ѭ���˶��������� Sprite �� UI Image��
// Unity 2020+ ���ݣ��� UI RectTransform��

using UnityEngine;

[DisallowMultipleComponent]
public class InfinityPathMover : MonoBehaviour
{
    public enum TargetSpace { World, Local, UIAnchored }

    [Header("Target")]
    [Tooltip("Ҫ�ƶ���Ŀ�꣬������ʹ�õ�ǰ����")]
    public Transform target;

    [Tooltip("����ռ䣺World/Local/UIAnchored(����RectTransform)")]
    public TargetSpace space = TargetSpace.Local;

    [Header("Motion")]
    [Tooltip("����ߴ�����")]
    public float size = 1f;

    [Tooltip("����/����뾶���� size ��ˣ�")]
    public Vector2 axisScale = new Vector2(2f, 1f);

    [Tooltip("�˶��ٶȣ�����/�룩")]
    public float speed = 1.5f;

    [Tooltip("��ʼ��λ�����ȣ�")]
    public float phase = 0f;

    [Tooltip("·�����ͣ�true=Bernoulli ��ķ��˹���أ�false=Lissajous(1:2)")]
    public bool useLemniscate = true;

    // ��ʼê��
    Vector3 worldStart;
    Vector3 localStart;
    Vector2 uiStart;
    RectTransform rect;
    float t;

    void Reset()
    {
        target = transform;
    }

    void Awake()
    {
        if (!target) target = transform;

        rect = target as RectTransform;
        if (space == TargetSpace.UIAnchored && rect == null)
        {
            // ��ѡ���� UI ģʽ������ RectTransform�����Զ�����Ϊ Local
            space = TargetSpace.Local;
        }

        CacheStart();
    }

    void OnEnable()
    {
        CacheStart();
        t = 0f;
    }

    void CacheStart()
    {
        if (!target) return;

        worldStart = target.position;
        localStart = target.localPosition;
        if (rect) uiStart = rect.anchoredPosition;
    }

    void Update()
    {
        if (!target) return;

        t += speed * Time.deltaTime;
        Vector2 p = useLemniscate ? LemniscateBernoulli(t + phase) : Lissajous12(t + phase);

        // �ߴ�����
        p = Vector2.Scale(p, axisScale * Mathf.Max(0.0001f, size));

        switch (space)
        {
            case TargetSpace.World:
                target.position = worldStart + (Vector3)p;
                break;
            case TargetSpace.Local:
                target.localPosition = localStart + (Vector3)p;
                break;
            case TargetSpace.UIAnchored:
                rect.anchoredPosition = uiStart + p;
                break;
        }
    }

    // ���� ·���������� ���� 
    // 1) ��ķ��˹���أ�Bernoulli������״������ѧ����ġ��ޡ�
    //    x = a * cos�� / (1 + sin^2��)
    //    y = a * sin�� * cos�� / (1 + sin^2��)
    static Vector2 LemniscateBernoulli(float theta)
    {
        float s = Mathf.Sin(theta);
        float c = Mathf.Cos(theta);
        float d = 1f + s * s;         // ��ֹ����
        float x = c / d;
        float y = (s * c) / d;
        return new Vector2(x, y) * 2f; // *2 ������������չ
    }

    // 2) Lissajous��1:2����Ҳ�ܵõ�����8��
    //    x = sin��, y = 0.5 * sin(2��)
    static Vector2 Lissajous12(float theta)
    {
        return new Vector2(Mathf.Sin(theta), 0.5f * Mathf.Sin(2f * theta));
    }

#if UNITY_EDITOR
    // ��������ӻ�·��
    void OnDrawGizmosSelected()
    {
        if (!target) target = transform;

        const int steps = 160;
        Vector3 origin =
            (space == TargetSpace.World) ? target.position :
            (space == TargetSpace.Local) ? target.parent ? target.parent.TransformPoint(target.localPosition) : target.localPosition
            : target.position;

        Vector3 prev = origin + (Vector3)(Vector2.Scale(LemniscateBernoulli(0f), axisScale * size));
        for (int i = 1; i <= steps; i++)
        {
            float th = (Mathf.PI * 2f) * i / steps;
            Vector2 p = useLemniscate ? LemniscateBernoulli(th) : Lissajous12(th);
            p = Vector2.Scale(p, axisScale * Mathf.Max(0.0001f, size));
            Vector3 now = origin + (Vector3)p;
            Gizmos.DrawLine(prev, now);
            prev = now;
        }
    }
#endif
}
