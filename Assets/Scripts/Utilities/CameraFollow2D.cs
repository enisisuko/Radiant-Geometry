using UnityEngine;

/// <summary>
/// 2D 摄像机跟随（带软区间、阻尼与可选边界）
/// 使用方法：挂到 Main Camera（正交）
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;               // 玩家（若留空会自动按 Tag=Player 寻找）

    [Header("Soft Zone (世界空间尺寸)")]
    [Tooltip("软区间相对相机中心的偏移（世界单位）。例如(0, 1)让软区间整体上移一点。")]
    public Vector2 softZoneCenterOffset = Vector2.zero;
    [Tooltip("软区间宽高（世界单位）。建议略小于相机视口尺寸。")]
    public Vector2 softZoneSize = new Vector2(4f, 3f);

    [Header("Damping / Lookahead")]
    [Tooltip("相机追随的平滑时间（SmoothDamp）。越小越跟手。")]
    [Range(0.01f, 1.0f)] public float smoothTime = 0.15f;
    [Tooltip("最大追随速度（世界单位/秒），0=不限制。")]
    public float maxSpeed = 0f;
    [Tooltip("基于目标速度的预判量（系数）。例如(0.5,0.2) 表示向目标速度方向提前看。")]
    public Vector2 lookaheadFactor = new Vector2(0.5f, 0.2f);
    [Tooltip("预判的平滑时间，避免抖动。")]
    [Range(0.01f, 1.0f)] public float lookaheadSmoothTime = 0.2f;

    [Header("World Bounds (可选)")]
    [Tooltip("可选：用 BoxCollider2D 指定世界包围盒，限制相机不出界。")]
    public BoxCollider2D worldBounds;

    private Camera cam;

    // 分离两套速度缓存，避免类型不匹配
    private Vector3 moveVelocity;          // 用于 Vector3.SmoothDamp（相机位移）
    private Vector2 lookaheadVelocity;     // 用于 Vector2.SmoothDamp（预判平滑）

    private Vector2 targetPrevPos;
    private Vector2 lookaheadPos;          // 平滑后的预判位移

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam.orthographic)
        {
            Debug.LogWarning("[CameraFollow2D] 建议使用正交相机（Orthographic）。");
        }
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) target = player.transform;
        }
        if (target != null) targetPrevPos = target.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1) 计算视口与软区间（在世界空间）
        float viewHalfHeight = cam.orthographicSize;
        float viewHalfWidth = viewHalfHeight * cam.aspect;

        Vector2 camCenter = new Vector2(transform.position.x, transform.position.y);

        // 软区间中心（相对相机中心有偏移）
        Vector2 softCenter = camCenter + softZoneCenterOffset;

        // 软区间半尺寸
        Vector2 softHalf = softZoneSize * 0.5f;

        // 2) 计算将目标保持在软区间内所需的位移
        Vector2 targetPos2D = (Vector2)target.position;
        Vector2 deltaToSoft = Vector2.zero;

        if (targetPos2D.x < softCenter.x - softHalf.x)
            deltaToSoft.x = targetPos2D.x - (softCenter.x - softHalf.x);
        else if (targetPos2D.x > softCenter.x + softHalf.x)
            deltaToSoft.x = targetPos2D.x - (softCenter.x + softHalf.x);

        if (targetPos2D.y < softCenter.y - softHalf.y)
            deltaToSoft.y = targetPos2D.y - (softCenter.y - softHalf.y);
        else if (targetPos2D.y > softCenter.y + softHalf.y)
            deltaToSoft.y = targetPos2D.y - (softCenter.y + softHalf.y);

        // 3) 预判（基于目标速度）
        Vector2 targetVel = (targetPos2D - targetPrevPos) / Mathf.Max(Time.deltaTime, 1e-5f);
        targetPrevPos = targetPos2D;

        Vector2 desiredLookahead = new Vector2(
            targetVel.x * lookaheadFactor.x,
            targetVel.y * lookaheadFactor.y
        );

        // 平滑预判，避免抖动 —— 使用 Vector2.SmoothDamp + Vector2 缓存
        lookaheadPos = Vector2.SmoothDamp(lookaheadPos, desiredLookahead, ref lookaheadVelocity, lookaheadSmoothTime);

        // 4) 期望的新相机中心
        Vector2 desiredCenter = camCenter + deltaToSoft + lookaheadPos;

        // 5) 应用世界边界
        if (worldBounds != null)
        {
            Bounds b = worldBounds.bounds;
            float minX = b.min.x + viewHalfWidth;
            float maxX = b.max.x - viewHalfWidth;
            float minY = b.min.y + viewHalfHeight;
            float maxY = b.max.y - viewHalfHeight;

            // 若边界比视口还小，直接居中
            if (minX > maxX) { float mid = (b.min.x + b.max.x) * 0.5f; minX = maxX = mid; }
            if (minY > maxY) { float mid = (b.min.y + b.max.y) * 0.5f; minY = maxY = mid; }

            desiredCenter.x = Mathf.Clamp(desiredCenter.x, minX, maxX);
            desiredCenter.y = Mathf.Clamp(desiredCenter.y, minY, maxY);
        }

        // 6) 平滑移动相机 —— 使用 Vector3.SmoothDamp + Vector3 缓存
        Vector3 current = transform.position;
        Vector3 target3 = new Vector3(desiredCenter.x, desiredCenter.y, current.z);

        if (maxSpeed > 0f)
            transform.position = Vector3.SmoothDamp(current, target3, ref moveVelocity, smoothTime, maxSpeed);
        else
            transform.position = Vector3.SmoothDamp(current, target3, ref moveVelocity, smoothTime);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        float viewHalfHeight = cam.orthographicSize;
        float viewHalfWidth = viewHalfHeight * cam.aspect;

        Vector2 camCenter = new Vector2(transform.position.x, transform.position.y);
        Vector2 softCenter = camCenter + softZoneCenterOffset;
        Vector2 softHalf = softZoneSize * 0.5f;

        // 视口
        Gizmos.color = new Color(1, 1, 1, 0.25f);
        Gizmos.DrawWireCube(new Vector3(camCenter.x, camCenter.y, 0f),
            new Vector3(viewHalfWidth * 2f, viewHalfHeight * 2f, 0f));

        // 软区间
        Gizmos.color = new Color(1, 1, 0, 0.9f);
        Gizmos.DrawWireCube(new Vector3(softCenter.x, softCenter.y, 0f),
            new Vector3(softHalf.x * 2f, softHalf.y * 2f, 0f));

        // 世界边界
        if (worldBounds != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.6f);
            Gizmos.DrawWireCube(worldBounds.bounds.center, worldBounds.bounds.size);
        }
    }
#endif
}
