using UnityEngine;

/// <summary>
/// 2D 挤压拉伸（果冻）系统 - 增强版
/// 【1】移动时挤压变形：根据水平速度进行微拉伸/压缩
/// 【2】事件冲击变形：跳跃/落地/受伤等事件，可多次叠加，会随时间衰减
/// 【3】果冻摇晃 Jelly Wobble：移动时产生摇晃，速度越快摇晃越明显
/// 【4】顶端晃动 Top Wobble：左右移动时顶端会晃动，像果冻一样更灵动
/// 支持材质/Shader，可改材质属性，否则直接改 transform.localScale
/// 使用：
/// 1) 挂脚本到角色上；
/// 2) 在 PlayerController2D 的 onJump/onDoubleJump/onLanded 调用 Trigger*；
/// 3) 材质模式可选 useMaterial 并指定 SpriteRenderer；
/// 4) 摇晃参数在"Jelly Wobble"和"Top Wobble"中调整。
/// </summary>
[RequireComponent(typeof(Transform))]
public class SquashStretch2D : MonoBehaviour
{
    [Header("Continuous by Speed")]
    public Rigidbody2D sourceBody;            // 获取速度，建议拖入
    public float maxSpeedForNormalize = 10f;  // 最大速度映射
    [Tooltip("移动时拉伸强度（x 拉伸 / y 压缩），0.05~0.12 较自然")]
    public float moveStretchAmount = 0.08f;

    [Header("Event Impulses (Stackable)")]
    [Tooltip("跳跃时：向上拉伸、向下压缩")]
    public float jumpImpulse = 0.18f;
    [Tooltip("二段跳：更强拉伸")]
    public float doubleJumpImpulse = 0.22f;
    [Tooltip("落地：向下压缩，向上拉伸")]
    public float landImpulse = 0.20f;
    [Tooltip("冲击衰减（越大衰减越快）")]
    public float impulseDamp = 4f;

    [Header("Jelly Wobble (Additive)")]
    [Tooltip("摇晃频率（Hz），0.8~2 之间较自然")]
    public float wobbleFrequencyHz = 1.2f;
    [Tooltip("摇晃最大幅度，相当于 scale 变化的百分比")]
    public float wobbleMaxAmplitude = 0.12f;
    [Tooltip("摇晃衰减（越大越快速停），建议 3~8")]
    public float wobbleDamping = 5f;
    [Tooltip("速度→幅度映射系数，速度越快摇晃越容易被激发")]
    public float wobbleVelocityToAmplitude = 0.08f;
    [Tooltip("方向转变事件踢，增加方向改变时的额外摇晃")]
    public float wobbleEventKick = 0.08f;
    [Tooltip("在空中时摇晃强度（0~1）")]
    public float airWobbleMultiplier = 0.75f;

    [Header("Top Wobble (顶端晃动)")]
    [Tooltip("是否启用顶端晃动效果")]
    public bool enableTopWobble = true;
    [Tooltip("顶端晃动频率（Hz），比整体摇晃稍快")]
    public float topWobbleFrequencyHz = 2.5f;
    [Tooltip("顶端晃动最大幅度")]
    public float topWobbleMaxAmplitude = 0.15f;
    [Tooltip("顶端晃动衰减速度")]
    public float topWobbleDamping = 6f;
    [Tooltip("移动速度对顶端晃动的影响系数")]
    public float topWobbleVelocityInfluence = 0.12f;
    [Tooltip("方向改变时顶端晃动的额外强度")]
    public float topWobbleDirectionKick = 0.2f;
    [Tooltip("顶端晃动的相位偏移（创造更自然的摆动）")]
    public float topWobblePhaseOffset = 0.3f;
    [Tooltip("顶端晃动的左右摆动强度")]
    public float topWobbleSideSway = 0.8f;

    [Header("Material Mode (Optional)")]
    public bool useMaterial = false;
    public SpriteRenderer spriteRenderer;     // 如使用材质，需指定
    public string deformXProp = "_DeformX";   // 材质变形属性
    public string deformYProp = "_DeformY";   // 材质变形属性
    public string topWobbleXProp = "_TopWobbleX"; // 顶端晃动X属性
    public string topWobbleYProp = "_TopWobbleY"; // 顶端晃动Y属性

    Vector2 baseScale = Vector2.one;

    // 事件冲击累积的变形（x/y 独立）
    Vector2 impulseAccum;
    MaterialPropertyBlock mpb;

    // Jelly 摇晃状态
    float wobblePhase;        // 相位（弧度）
    float wobbleAmp;          // 当前幅度（0~wobbleMaxAmplitude）
    float lastVX;             // 上一帧水平速度
    bool lastGrounded = true;

    // 顶端晃动状态
    float topWobblePhase;     // 顶端晃动相位
    float topWobbleAmp;       // 顶端晃动幅度
    float topWobbleX;         // 顶端晃动X分量
    float topWobbleY;         // 顶端晃动Y分量
    float lastTopWobbleVX;    // 上一帧用于顶端晃动的速度

    // 可选：地面状态（如未接入，系统只会根据速度猜测强度）
    public bool grounded = true;

    void Awake()
    {
        baseScale = transform.localScale;
        if (useMaterial && spriteRenderer)
            mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        // === 【1】移动时挤压变形 ===
        Vector2 moveDeform = Vector2.zero;
        float vx = 0f;
        if (sourceBody)
        {
            vx = sourceBody.linearVelocity.x;
            float spd = Mathf.Abs(vx);
            float n = Mathf.Clamp01(spd / Mathf.Max(0.0001f, maxSpeedForNormalize));
            moveDeform.x += n * moveStretchAmount; // 水平拉伸
            moveDeform.y -= n * moveStretchAmount; // 垂直压缩（保持体积）
        }

        // === 【2】事件冲击（指数衰减） ===
        impulseAccum = Vector2.Lerp(impulseAccum, Vector2.zero, 1f - Mathf.Exp(-impulseDamp * Time.deltaTime));

        // === 【3】果冻摇晃（正弦波 + 速度激发） ===
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float twoPi = Mathf.PI * 2f;
        
        // 计算速度变化/加速度，激发摇晃
        float accelX = (vx - lastVX) / dt;
        float ampTarget = Mathf.Clamp01(Mathf.Abs(vx) * wobbleVelocityToAmplitude);
        if (!grounded) ampTarget *= airWobbleMultiplier; // 空中弱一点
        // 向目标幅度，平滑过渡
        wobbleAmp = Mathf.MoveTowards(wobbleAmp, Mathf.Min(ampTarget, wobbleMaxAmplitude), 3f * dt);

        // 检测：检测到"方向转变"，给一个冲击
        if (Mathf.Sign(vx) != Mathf.Sign(lastVX) && Mathf.Abs(vx) > 0.1f && Mathf.Abs(lastVX) > 0.1f)
            wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick, 0f, wobbleMaxAmplitude);

        // 相位推进频率
        wobblePhase += twoPi * wobbleFrequencyHz * dt;
        if (wobblePhase > twoPi) wobblePhase -= twoPi;

        // 衰减（逐渐停止）
        wobbleAmp *= Mathf.Exp(-wobbleDamping * dt);

        // 摇晃映射到 x/y：水平拉伸压缩，垂直相反，创造果冻甩动
        float wobbleSine = Mathf.Sin(wobblePhase);
        float wobbleX = wobbleAmp * wobbleSine;   // x 水平变化
        float wobbleY = -wobbleX * 0.8f;          // y 垂直相反，保持体积感

        // === 【4】顶端晃动（新增） ===
        if (enableTopWobble)
        {
            UpdateTopWobble(vx, dt, twoPi);
        }
        else
        {
            topWobbleX = 0f;
            topWobbleY = 0f;
        }

        // 合成所有变形
        float dx = moveDeform.x + impulseAccum.x + wobbleX + topWobbleX;
        float dy = moveDeform.y + impulseAccum.y + wobbleY + topWobbleY;

        // 应用
        if (useMaterial && spriteRenderer)
        {
            spriteRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(deformXProp, dx);
            mpb.SetFloat(deformYProp, dy);
            mpb.SetFloat(topWobbleXProp, topWobbleX);
            mpb.SetFloat(topWobbleYProp, topWobbleY);
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

    void UpdateTopWobble(float vx, float dt, float twoPi)
    {
        // 计算顶端晃动的目标幅度
        float topAmpTarget = Mathf.Clamp01(Mathf.Abs(vx) * topWobbleVelocityInfluence);
        if (!grounded) topAmpTarget *= airWobbleMultiplier;

        // 检测方向改变，给顶端晃动一个冲击
        if (Mathf.Sign(vx) != Mathf.Sign(lastTopWobbleVX) && Mathf.Abs(vx) > 0.1f && Mathf.Abs(lastTopWobbleVX) > 0.1f)
        {
            topWobbleAmp = Mathf.Clamp(topWobbleAmp + topWobbleDirectionKick, 0f, topWobbleMaxAmplitude);
        }

        // 向目标幅度平滑过渡
        topWobbleAmp = Mathf.MoveTowards(topWobbleAmp, Mathf.Min(topAmpTarget, topWobbleMaxAmplitude), 4f * dt);

        // 相位推进（比整体摇晃稍快）
        topWobblePhase += twoPi * topWobbleFrequencyHz * dt;
        if (topWobblePhase > twoPi) topWobblePhase -= twoPi;

        // 衰减
        topWobbleAmp *= Mathf.Exp(-topWobbleDamping * dt);

        // 计算顶端晃动的X和Y分量
        float topWobbleSine = Mathf.Sin(topWobblePhase + topWobblePhaseOffset);
        float topWobbleCosine = Mathf.Cos(topWobblePhase + topWobblePhaseOffset);
        
        // X分量：左右摆动，跟随移动方向
        topWobbleX = topWobbleAmp * topWobbleSine * topWobbleSideSway * Mathf.Sign(vx);
        
        // Y分量：上下摆动，创造顶端晃动的感觉
        topWobbleY = topWobbleAmp * topWobbleCosine * 0.6f;

        lastTopWobbleVX = vx;
    }

    // === 事件触发（可叠加） ===
    public void TriggerJump()
    {
        impulseAccum += new Vector2(-jumpImpulse, jumpImpulse);
        // 跳跃也会激发 Jelly 摇晃
        wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick * 0.6f, 0f, wobbleMaxAmplitude);
        // 跳跃也会激发顶端晃动
        if (enableTopWobble)
        {
            topWobbleAmp = Mathf.Clamp(topWobbleAmp + topWobbleDirectionKick * 0.5f, 0f, topWobbleMaxAmplitude);
        }
    }

    public void TriggerDoubleJump()
    {
        impulseAccum += new Vector2(-doubleJumpImpulse, doubleJumpImpulse);
        wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick * 0.8f, 0f, wobbleMaxAmplitude);
        if (enableTopWobble)
        {
            topWobbleAmp = Mathf.Clamp(topWobbleAmp + topWobbleDirectionKick * 0.7f, 0f, topWobbleMaxAmplitude);
        }
    }

    public void TriggerLand()
    {
        impulseAccum += new Vector2(landImpulse, -landImpulse);
        // 落地时摇晃强一点（模拟冲击）
        wobbleAmp = Mathf.Clamp(wobbleAmp + wobbleEventKick * 1.25f, 0f, wobbleMaxAmplitude);
        if (enableTopWobble)
        {
            topWobbleAmp = Mathf.Clamp(topWobbleAmp + topWobbleDirectionKick * 1.0f, 0f, topWobbleMaxAmplitude);
        }
        grounded = true;
    }

    // === 新增：手动触发顶端晃动 ===
    public void TriggerTopWobble(float intensity = 1f)
    {
        if (enableTopWobble)
        {
            topWobbleAmp = Mathf.Clamp(topWobbleAmp + topWobbleDirectionKick * intensity, 0f, topWobbleMaxAmplitude);
        }
    }

    // === 新增：设置顶端晃动参数 ===
    public void SetTopWobbleEnabled(bool enabled)
    {
        enableTopWobble = enabled;
        if (!enabled)
        {
            topWobbleAmp = 0f;
            topWobbleX = 0f;
            topWobbleY = 0f;
        }
    }
}