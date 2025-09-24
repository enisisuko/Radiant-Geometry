using UnityEngine;

/// <summary>
/// 2D 形变控制（可叠加）：
/// 层 1【移动连续形变】：按水平速度做轻微拉伸/压缩；
/// 层 2【事件脉冲形变】：起跳/二段跳/落地注入脉冲，可多次叠加并随时间回弹；
/// 层 3【果冻摇晃 Jelly Wobble】：左右移动时触发弹性摇晃（可与上面两层叠加）。
/// 如有材质/Shader，可改材质参数；否则直接改 transform.localScale。
/// 使用：
/// 1) 将本脚本挂到玩家；
/// 2) 绑定 PlayerController2D 的 onJump/onDoubleJump/onLanded 到本脚本 Trigger*；
/// 3) 若需材质模式，勾选 useMaterial，并指定 SpriteRenderer；
/// 4) 果冻摇晃参数在“Jelly Wobble”分组里调节。
/// </summary>
[RequireComponent(typeof(Transform))]
public class SquashStretch2D : MonoBehaviour
{
    [Header("Continuous by Speed")]
    public Rigidbody2D sourceBody;            // 用来读速度，建议拖玩家自身
    public float maxSpeedForNormalize = 10f;  // 最大速度映射
    [Tooltip("移动时基础拉伸强度（x 拉伸 / y 压缩），0.05~0.12 自然")]
    public float moveStretchAmount = 0.08f;

    [Header("Event Impulses (Stackable)")]
    [Tooltip("起跳：纵向拉伸、横向压缩")]
    public float jumpImpulse = 0.18f;
    [Tooltip("二段跳：略强于起跳")]
    public float doubleJumpImpulse = 0.22f;
    [Tooltip("落地：纵向压缩、横向拉伸")]
    public float landImpulse = 0.20f;
    [Tooltip("脉冲衰减（越大回弹越快）")]
    public float impulseDamp = 4f;

    [Header("Jelly Wobble (Additive)")]
    [Tooltip("基础频率（Hz），0.8~2 之间较自然")]
    public float wobbleFrequencyHz = 1.2f;
    [Tooltip("摇晃最大振幅（相当于 scale 变化比例）")]
    public float wobbleMaxAmplitude = 0.12f;
    [Tooltip("摇晃阻尼（越大越快停，建议 3~8）")]
    public float wobbleDamping = 5f;
    [Tooltip("速度→振幅映射系数，速度越大，摇晃越容易被“摇起来”")]
    public float wobbleVelocityToAmplitude = 0.08f;
    [Tooltip("左右转向或落地等事件给予的额外摇晃脉冲")]
    public float wobbleEventKick = 0.08f;
    [Tooltip("在空中时降低摇晃强度（0~1）")]
    public float airWobbleMultiplier = 0.75f;

    [Header("Material Mode (Optional)")]
    public bool useMaterial = false;
    public SpriteRenderer spriteRenderer;     // 若使用材质，请指定
    public string deformXProp = "_DeformX";   // 横向形变参数名
    public string deformYProp = "_DeformY";   // 纵向形变参数名

    Vector2 baseScale = Vector2.one;

    // 事件脉冲可叠加形变量（x/y 各一）
    Vector2 impulseAccum;
    MaterialPropertyBlock mpb;

    // Jelly 摇晃状态
    float wobblePhase;        // 相位（弧度）
    float wobbleAmp;          // 当前振幅（0~wobbleMaxAmplitude）
    float lastVX;             // 上一帧水平速度
    bool lastGrounded = true;

    // 可选：读地面态（若你未接入，这个标记只会基于速度猜测强度）
    public bool grounded = true;

    void Awake()
    {
        baseScale = transform.localScale;
        if (useMaterial && spriteRenderer)
            mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        // === 层 1：移动连续形变 ===
        Vector2 moveDeform = Vector2.zero;
        float vx = 0f;
        if (sourceBody)
        {
            vx = sourceBody.linearVelocity.x;
            float spd = Mathf.Abs(vx);
            float n = Mathf.Clamp01(spd / Mathf.Max(0.0001f, maxSpeedForNormalize));
            moveDeform.x += n * moveStretchAmount; // 横向拉伸
            moveDeform.y -= n * moveStretchAmount; // 纵向压缩（保持体积感）
        }

        // === 层 2：事件脉冲（指数衰减） ===
        impulseAccum = Vector2.Lerp(impulseAccum, Vector2.zero, 1f - Mathf.Exp(-impulseDamp * Time.deltaTime));

        // === 层 3：果冻摇晃（阻尼谐振 + 速度驱动） ===
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float twoPi = Mathf.PI * 2f;
        // 根据速度变化/加速度“加热”振幅
        float accelX = (vx - lastVX) / dt;
        float ampTarget = Mathf.Clamp01(Mathf.Abs(vx) * wobbleVelocityToAmplitude);
        if (!grounded) ampTarget *= airWobbleMultiplier; // 空中弱一点
        // 向目标振幅靠拢（缓慢）
        wobbleAmp = Mathf.MoveTowards(wobbleAmp, Mathf.Min(ampTarget, wobbleMaxAmplitude), 3f * dt);

        // 额外：若检测到“急转向”，加一脚脉冲
        if (Mathf.Sign(vx) != Mathf.Sign(lastVX) && Mathf.Abs(vx) > 0.1f && Mathf.Abs(lastVX) > 0.1f)
            wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick, 0f, wobbleMaxAmplitude);

        // 相位推进（频率）
        wobblePhase += twoPi * wobbleFrequencyHz * dt;
        if (wobblePhase > twoPi) wobblePhase -= twoPi;

        // 阻尼（逐渐停止）
        wobbleAmp *= Mathf.Exp(-wobbleDamping * dt);

        // 把摇晃映射成 x/y 互逆的拉伸压缩（看起来像果冻左右甩）
        float wobbleSine = Mathf.Sin(wobblePhase);
        float wobbleX = wobbleAmp * wobbleSine;   // x 随正弦变化
        float wobbleY = -wobbleX * 0.8f;          // y 反向且略小，避免过度变形

        // 合成三层形变
        float dx = moveDeform.x + impulseAccum.x + wobbleX;
        float dy = moveDeform.y + impulseAccum.y + wobbleY;

        // 应用
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

    // === 事件触发（叠加） ===
    public void TriggerJump()
    {
        impulseAccum += new Vector2(-jumpImpulse, jumpImpulse);
        // 跳起也给 Jelly 一点额外能量
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
        // 落地时摇晃显著一点（像果冻抖一下）
        wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick * 1.25f, 0f, wobbleMaxAmplitude);
        grounded = true;
    }
}
