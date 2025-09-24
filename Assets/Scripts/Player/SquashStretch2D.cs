using UnityEngine;

/// <summary>
/// 2D �α���ƣ��ɵ��ӣ���
/// �� 1���ƶ������α䡿����ˮƽ�ٶ�����΢����/ѹ����
/// �� 2���¼������α䡿������/������/���ע�����壬�ɶ�ε��Ӳ���ʱ��ص���
/// �� 3������ҡ�� Jelly Wobble���������ƶ�ʱ��������ҡ�Σ���������������ӣ���
/// ���в���/Shader���ɸĲ��ʲ���������ֱ�Ӹ� transform.localScale��
/// ʹ�ã�
/// 1) �����ű��ҵ���ң�
/// 2) �� PlayerController2D �� onJump/onDoubleJump/onLanded �����ű� Trigger*��
/// 3) �������ģʽ����ѡ useMaterial����ָ�� SpriteRenderer��
/// 4) ����ҡ�β����ڡ�Jelly Wobble����������ڡ�
/// </summary>
[RequireComponent(typeof(Transform))]
public class SquashStretch2D : MonoBehaviour
{
    [Header("Continuous by Speed")]
    public Rigidbody2D sourceBody;            // �������ٶȣ��������������
    public float maxSpeedForNormalize = 10f;  // ����ٶ�ӳ��
    [Tooltip("�ƶ�ʱ��������ǿ�ȣ�x ���� / y ѹ������0.05~0.12 ��Ȼ")]
    public float moveStretchAmount = 0.08f;

    [Header("Event Impulses (Stackable)")]
    [Tooltip("�������������졢����ѹ��")]
    public float jumpImpulse = 0.18f;
    [Tooltip("����������ǿ������")]
    public float doubleJumpImpulse = 0.22f;
    [Tooltip("��أ�����ѹ������������")]
    public float landImpulse = 0.20f;
    [Tooltip("����˥����Խ��ص�Խ�죩")]
    public float impulseDamp = 4f;

    [Header("Jelly Wobble (Additive)")]
    [Tooltip("����Ƶ�ʣ�Hz����0.8~2 ֮�����Ȼ")]
    public float wobbleFrequencyHz = 1.2f;
    [Tooltip("ҡ�����������൱�� scale �仯������")]
    public float wobbleMaxAmplitude = 0.12f;
    [Tooltip("ҡ�����ᣨԽ��Խ��ͣ������ 3~8��")]
    public float wobbleDamping = 5f;
    [Tooltip("�ٶȡ����ӳ��ϵ�����ٶ�Խ��ҡ��Խ���ױ���ҡ������")]
    public float wobbleVelocityToAmplitude = 0.08f;
    [Tooltip("����ת�����ص��¼�����Ķ���ҡ������")]
    public float wobbleEventKick = 0.08f;
    [Tooltip("�ڿ���ʱ����ҡ��ǿ�ȣ�0~1��")]
    public float airWobbleMultiplier = 0.75f;

    [Header("Material Mode (Optional)")]
    public bool useMaterial = false;
    public SpriteRenderer spriteRenderer;     // ��ʹ�ò��ʣ���ָ��
    public string deformXProp = "_DeformX";   // �����α������
    public string deformYProp = "_DeformY";   // �����α������

    Vector2 baseScale = Vector2.one;

    // �¼�����ɵ����α�����x/y ��һ��
    Vector2 impulseAccum;
    MaterialPropertyBlock mpb;

    // Jelly ҡ��״̬
    float wobblePhase;        // ��λ�����ȣ�
    float wobbleAmp;          // ��ǰ�����0~wobbleMaxAmplitude��
    float lastVX;             // ��һ֡ˮƽ�ٶ�
    bool lastGrounded = true;

    // ��ѡ��������̬������δ���룬������ֻ������ٶȲ²�ǿ�ȣ�
    public bool grounded = true;

    void Awake()
    {
        baseScale = transform.localScale;
        if (useMaterial && spriteRenderer)
            mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        // === �� 1���ƶ������α� ===
        Vector2 moveDeform = Vector2.zero;
        float vx = 0f;
        if (sourceBody)
        {
            vx = sourceBody.linearVelocity.x;
            float spd = Mathf.Abs(vx);
            float n = Mathf.Clamp01(spd / Mathf.Max(0.0001f, maxSpeedForNormalize));
            moveDeform.x += n * moveStretchAmount; // ��������
            moveDeform.y -= n * moveStretchAmount; // ����ѹ������������У�
        }

        // === �� 2���¼����壨ָ��˥���� ===
        impulseAccum = Vector2.Lerp(impulseAccum, Vector2.zero, 1f - Mathf.Exp(-impulseDamp * Time.deltaTime));

        // === �� 3������ҡ�Σ�����г�� + �ٶ������� ===
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float twoPi = Mathf.PI * 2f;
        // �����ٶȱ仯/���ٶȡ����ȡ����
        float accelX = (vx - lastVX) / dt;
        float ampTarget = Mathf.Clamp01(Mathf.Abs(vx) * wobbleVelocityToAmplitude);
        if (!grounded) ampTarget *= airWobbleMultiplier; // ������һ��
        // ��Ŀ�������£��������
        wobbleAmp = Mathf.MoveTowards(wobbleAmp, Mathf.Min(ampTarget, wobbleMaxAmplitude), 3f * dt);

        // ���⣺����⵽����ת�򡱣���һ������
        if (Mathf.Sign(vx) != Mathf.Sign(lastVX) && Mathf.Abs(vx) > 0.1f && Mathf.Abs(lastVX) > 0.1f)
            wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick, 0f, wobbleMaxAmplitude);

        // ��λ�ƽ���Ƶ�ʣ�
        wobblePhase += twoPi * wobbleFrequencyHz * dt;
        if (wobblePhase > twoPi) wobblePhase -= twoPi;

        // ���ᣨ��ֹͣ��
        wobbleAmp *= Mathf.Exp(-wobbleDamping * dt);

        // ��ҡ��ӳ��� x/y ���������ѹ�������������������˦��
        float wobbleSine = Mathf.Sin(wobblePhase);
        float wobbleX = wobbleAmp * wobbleSine;   // x �����ұ仯
        float wobbleY = -wobbleX * 0.8f;          // y ��������С��������ȱ���

        // �ϳ������α�
        float dx = moveDeform.x + impulseAccum.x + wobbleX;
        float dy = moveDeform.y + impulseAccum.y + wobbleY;

        // Ӧ��
        if (useMaterial && spriteRenderer)
        {
            spriteRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(deformXProp, dx);
            mpb.SetFloat(deformYProp, dy);
            spriteRenderer.SetPropertyBlock(mpb);
        }
        else
        {
            float sx = baseScale.x * (1f + dx);
            float sy = baseScale.y * (1f + dy);
            transform.localScale = new Vector3(sx, sy, 1f);
        }

        lastVX = vx;
        lastGrounded = grounded;
    }

    // === �¼����������ӣ� ===
    public void TriggerJump()
    {
        impulseAccum += new Vector2(-jumpImpulse, jumpImpulse);
        // ����Ҳ�� Jelly һ���������
        wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick * 0.6f, 0f, wobbleMaxAmplitude);
    }

    public void TriggerDoubleJump()
    {
        impulseAccum += new Vector2(-doubleJumpImpulse, doubleJumpImpulse);
        wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick * 0.8f, 0f, wobbleMaxAmplitude);
    }

    public void TriggerLand()
    {
        impulseAccum += new Vector2(landImpulse, -landImpulse);
        // ���ʱҡ������һ�㣨�������һ�£�
        wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick * 1.25f, 0f, wobbleMaxAmplitude);
        grounded = true;
    }
}
