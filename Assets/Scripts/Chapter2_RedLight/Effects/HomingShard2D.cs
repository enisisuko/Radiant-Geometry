using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HomingShard2D : MonoBehaviour
{
    [Header("Ŀ��")]
    [Tooltip("Ŀ�ꡣ��Ϊ�գ����� Start �� Tag=Player �Զ�����")]
    public Transform target;
    [Tooltip("�����Զ�ʶ�� Player �� Tag")]
    public string playerTag = "Player";

    [Header("�˶�")]
    [Tooltip("���ٶȷ�Χ���ɷ�������ֵ��Ҳ���ڴ˸��ǣ�")]
    public Vector2 initialVelocity = new Vector2(8f, 0f);
    [Tooltip("׷����Ӧϵ����Խ��Խ��ذ��ٶȷ�������Ŀ�꣩")]
    [Min(0f)] public float angularResponsiveness = 6f;
    [Tooltip("�� 0 ��ʼ�ڸ�ʱ�����𲽴ﵽ��׷��Ȩ��")]
    [Min(0.01f)] public float timeToFullHoming = 1.25f;
    [Tooltip("׷��Ȩ�����ߣ�����=0..1 �Ĺ�һ������������=0..1 ��Ȩ�أ�")]
    public AnimationCurve homingOverLife = AnimationCurve.EaseInOut(0, 0.1f, 1, 1f);
    [Tooltip("�Ƿ��ó�������ٶȷ���")]
    public bool faceVelocity = true;
    [Tooltip("�������ʱ�������ⳡ��������")]
    [Min(0.1f)] public float lifeTime = 10f;

    Rigidbody2D rb;
    Collider2D col;
    float age;
    float speedMagnitude; // ά�֣����£��㶨���ʣ���Ҫ�ı䷽��

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        if (col) col.isTrigger = true;
    }

    void Start()
    {
        if (!target)
        {
            var p = GameObject.FindGameObjectWithTag(string.IsNullOrEmpty(playerTag) ? "Player" : playerTag);
            if (p) target = p.transform;
        }

        // ��ʼ���ٶ�
        rb.linearVelocity = initialVelocity;
        speedMagnitude = initialVelocity.magnitude;
        if (speedMagnitude < 0.01f) speedMagnitude = 0.01f;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifeTime) Destroy(gameObject);

        if (faceVelocity && rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            float ang = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(ang, Vector3.forward);
        }
    }

    void FixedUpdate()
    {
        if (!rb) return;
        Vector2 v = rb.linearVelocity;

        if (target)
        {
            // ��һ��ʱ�䣬��Ȩ�أ��𽥴� 0 �ƽ� 1��
            float t01 = Mathf.Clamp01(age / Mathf.Max(0.0001f, timeToFullHoming));
            float weight01 = Mathf.Clamp01(homingOverLife.Evaluate(t01));

            // ϣ�����ٶȷ���ָ��Ŀ��
            Vector2 toTarget = ((Vector2)target.position - rb.position);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 desired = toTarget.normalized * speedMagnitude;

                // ָ��ƽ���ѵ�ǰ�ٶ��� desired ������Ҫ�ı䷽��
                float k = angularResponsiveness * weight01;
                float lerp = 1f - Mathf.Exp(-k * Time.fixedDeltaTime);
                rb.linearVelocity = Vector2.Lerp(v, desired, lerp);
            }
        }
        else
        {
            // û��Ŀ��ͱ��ֵ�ǰ�ٶ�
            rb.linearVelocity = v;
        }
    }

    /// <summary>�������������ɺ������趨������Ŀ�ꡣ</summary>
    public void Launch(Vector2 velocity, Transform tgt)
    {
        initialVelocity = velocity;
        target = tgt ? tgt : target;
        if (rb)
        {
            rb.linearVelocity = velocity;
            speedMagnitude = Mathf.Max(0.01f, velocity.magnitude);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other) return;

        // ײ�� Player����������ײ�壩�������Լ�
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            Destroy(gameObject);
            return;
        }

        // �� Player �ڸ��㣨�����ں���� Collider �Ľ�ɫ��
        var root = other.attachedRigidbody ? other.attachedRigidbody.transform.root : other.transform.root;
        if (root && !string.IsNullOrEmpty(playerTag) && root.CompareTag(playerTag))
        {
            Destroy(gameObject);
        }
    }
}
