using UnityEngine;

/// <summary>
/// 2D ��������棨�������䡢�������ѡ�߽磩
/// ʹ�÷������ҵ� Main Camera��������
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;               // ��ң������ջ��Զ��� Tag=Player Ѱ�ң�

    [Header("Soft Zone (����ռ�ߴ�)")]
    [Tooltip("���������������ĵ�ƫ�ƣ����絥λ��������(0, 1)����������������һ�㡣")]
    public Vector2 softZoneCenterOffset = Vector2.zero;
    [Tooltip("�������ߣ����絥λ����������С������ӿڳߴ硣")]
    public Vector2 softZoneSize = new Vector2(4f, 3f);

    [Header("Damping / Lookahead")]
    [Tooltip("���׷���ƽ��ʱ�䣨SmoothDamp����ԽСԽ���֡�")]
    [Range(0.01f, 1.0f)] public float smoothTime = 0.15f;
    [Tooltip("���׷���ٶȣ����絥λ/�룩��0=�����ơ�")]
    public float maxSpeed = 0f;
    [Tooltip("����Ŀ���ٶȵ�Ԥ������ϵ����������(0.5,0.2) ��ʾ��Ŀ���ٶȷ�����ǰ����")]
    public Vector2 lookaheadFactor = new Vector2(0.5f, 0.2f);
    [Tooltip("Ԥ�е�ƽ��ʱ�䣬���ⶶ����")]
    [Range(0.01f, 1.0f)] public float lookaheadSmoothTime = 0.2f;

    [Header("World Bounds (��ѡ)")]
    [Tooltip("��ѡ���� BoxCollider2D ָ�������Χ�У�������������硣")]
    public BoxCollider2D worldBounds;

    private Camera cam;

    // ���������ٶȻ��棬�������Ͳ�ƥ��
    private Vector3 moveVelocity;          // ���� Vector3.SmoothDamp�����λ�ƣ�
    private Vector2 lookaheadVelocity;     // ���� Vector2.SmoothDamp��Ԥ��ƽ����

    private Vector2 targetPrevPos;
    private Vector2 lookaheadPos;          // ƽ�����Ԥ��λ��

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam.orthographic)
        {
            Debug.LogWarning("[CameraFollow2D] ����ʹ�����������Orthographic����");
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

        // 1) �����ӿ��������䣨������ռ䣩
        float viewHalfHeight = cam.orthographicSize;
        float viewHalfWidth = viewHalfHeight * cam.aspect;

        Vector2 camCenter = new Vector2(transform.position.x, transform.position.y);

        // ���������ģ�������������ƫ�ƣ�
        Vector2 softCenter = camCenter + softZoneCenterOffset;

        // �������ߴ�
        Vector2 softHalf = softZoneSize * 0.5f;

        // 2) ���㽫Ŀ�걣�����������������λ��
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

        // 3) Ԥ�У�����Ŀ���ٶȣ�
        Vector2 targetVel = (targetPos2D - targetPrevPos) / Mathf.Max(Time.deltaTime, 1e-5f);
        targetPrevPos = targetPos2D;

        Vector2 desiredLookahead = new Vector2(
            targetVel.x * lookaheadFactor.x,
            targetVel.y * lookaheadFactor.y
        );

        // ƽ��Ԥ�У����ⶶ�� ���� ʹ�� Vector2.SmoothDamp + Vector2 ����
        lookaheadPos = Vector2.SmoothDamp(lookaheadPos, desiredLookahead, ref lookaheadVelocity, lookaheadSmoothTime);

        // 4) ���������������
        Vector2 desiredCenter = camCenter + deltaToSoft + lookaheadPos;

        // 5) Ӧ������߽�
        if (worldBounds != null)
        {
            Bounds b = worldBounds.bounds;
            float minX = b.min.x + viewHalfWidth;
            float maxX = b.max.x - viewHalfWidth;
            float minY = b.min.y + viewHalfHeight;
            float maxY = b.max.y - viewHalfHeight;

            // ���߽���ӿڻ�С��ֱ�Ӿ���
            if (minX > maxX) { float mid = (b.min.x + b.max.x) * 0.5f; minX = maxX = mid; }
            if (minY > maxY) { float mid = (b.min.y + b.max.y) * 0.5f; minY = maxY = mid; }

            desiredCenter.x = Mathf.Clamp(desiredCenter.x, minX, maxX);
            desiredCenter.y = Mathf.Clamp(desiredCenter.y, minY, maxY);
        }

        // 6) ƽ���ƶ���� ���� ʹ�� Vector3.SmoothDamp + Vector3 ����
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

        // �ӿ�
        Gizmos.color = new Color(1, 1, 1, 0.25f);
        Gizmos.DrawWireCube(new Vector3(camCenter.x, camCenter.y, 0f),
            new Vector3(viewHalfWidth * 2f, viewHalfHeight * 2f, 0f));

        // ������
        Gizmos.color = new Color(1, 1, 0, 0.9f);
        Gizmos.DrawWireCube(new Vector3(softCenter.x, softCenter.y, 0f),
            new Vector3(softHalf.x * 2f, softHalf.y * 2f, 0f));

        // ����߽�
        if (worldBounds != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.6f);
            Gizmos.DrawWireCube(worldBounds.bounds.center, worldBounds.bounds.size);
        }
    }
#endif
}
