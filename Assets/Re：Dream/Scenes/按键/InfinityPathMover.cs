// InfinityPathMover.cs
// 将目标沿“∞（横着的 8）”路径循环运动，可用于 Sprite 或 UI Image。
// Unity 2020+ 兼容（含 UI RectTransform）

using UnityEngine;

[DisallowMultipleComponent]
public class InfinityPathMover : MonoBehaviour
{
    public enum TargetSpace { World, Local, UIAnchored }

    [Header("Target")]
    [Tooltip("要移动的目标，不填则使用当前物体")]
    public Transform target;

    [Tooltip("坐标空间：World/Local/UIAnchored(用于RectTransform)")]
    public TargetSpace space = TargetSpace.Local;

    [Header("Motion")]
    [Tooltip("整体尺寸缩放")]
    public float size = 1f;

    [Tooltip("横向/纵向半径（与 size 相乘）")]
    public Vector2 axisScale = new Vector2(2f, 1f);

    [Tooltip("运动速度（弧度/秒）")]
    public float speed = 1.5f;

    [Tooltip("初始相位（弧度）")]
    public float phase = 0f;

    [Tooltip("路径类型：true=Bernoulli 伦姆尼斯盖特，false=Lissajous(1:2)")]
    public bool useLemniscate = true;

    // 初始锚点
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
            // 若选择了 UI 模式但不是 RectTransform，则自动降级为 Local
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

        // 尺寸缩放
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

    // —— 路径参数方程 —— 
    // 1) 伦姆尼斯盖特（Bernoulli），形状更像数学意义的“∞”
    //    x = a * cosθ / (1 + sin^2θ)
    //    y = a * sinθ * cosθ / (1 + sin^2θ)
    static Vector2 LemniscateBernoulli(float theta)
    {
        float s = Mathf.Sin(theta);
        float c = Mathf.Cos(theta);
        float d = 1f + s * s;         // 防止除零
        float x = c / d;
        float y = (s * c) / d;
        return new Vector2(x, y) * 2f; // *2 让整体横向更舒展
    }

    // 2) Lissajous（1:2），也能得到横向“8”
    //    x = sinθ, y = 0.5 * sin(2θ)
    static Vector2 Lissajous12(float theta)
    {
        return new Vector2(Mathf.Sin(theta), 0.5f * Mathf.Sin(2f * theta));
    }

#if UNITY_EDITOR
    // 场景里可视化路径
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
