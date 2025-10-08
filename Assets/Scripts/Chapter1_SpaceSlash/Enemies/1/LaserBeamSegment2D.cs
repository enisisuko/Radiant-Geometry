// LaserBeamSegment2D.cs — 通用2D激光段（可被空间斩切开：更炫酷的电影化演出版）
// 变更点（相对你现有版本）：
// 1) 命中“空间斩”后新增【冲击弹宽+白闪+震屏+短暂时间凝滞】→【爆裂光团/冲击波/火花 VFX】→【高频电光闪烁】→【线宽与透明度同速收束】整套演出。
// 2) 提供可选的：命中点传入（SetSlicedHit）、摄像机冲击（Cinemachine 或自定义事件）、命中音效、2D Light 脉冲（有URP则启用）。
// 3) 所有时序在被切后均走“不受Time.timeScale影响”的Unscaled计时，保证HitStop不拖慢演出。
// 4) 沿用你的伤害、扫屏、追踪、撞地特效等原逻辑；被切后自动停更这些逻辑，交给演出协程收尾。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using FadedDreams.Enemies;
// 若你的项目无URP/Light2D，本脚本会自动降级；无需引入命名空间。
// using UnityEngine.Rendering.Universal; // 不强依赖，运行时反射添加 Light2D

using Random = UnityEngine.Random;

public class LaserBeamSegment2D : MonoBehaviour, IDamageable
{
    [Header("Runtime (read-only)")]
    public Vector3 startPos;
    public Vector3 endPos;
    public Color baseColor = Color.white;
    public float thickness = 0.1f;
    public float chargeSeconds = 0.5f;
    public float lethalSeconds = 0.2f;
    public float lifeSeconds = 1.0f;
    public bool sweeping = false;
    public Vector2 velocity = Vector2.zero;

    [Header("Damage / Effects")]
    [Tooltip("命中玩家时扣除的能量（continuousDrain=false时为一次性；true时为每秒）。")]
    public float energyDamage = 10f;
    [Tooltip("命中玩家时施加的上挑力度（冲量）。0为关闭。")]
    public float knockupImpulse = 0f;
    [Tooltip("是否持续每秒扣能，否则为一次性。")]
    public bool continuousDrain = false;

    [Header("Charge / Lethal presentation")]
    public bool useChargeColorLerp = false;
    public Color chargeStartColor = Color.white;
    public Color chargeEndColor = Color.red;
    [Tooltip("进入致命期后，是否在短时间内变粗到倍数。")]
    public bool thickenOnLethal = false;
    public float thickenMul = 2f;
    public float thickenLerpSeconds = 0.1f;
    [Tooltip("寿命末尾的缓慢消逝秒数（线宽和透明度同时衰减）。")]
    public float fadeOutSeconds = 0.3f;

    [Header("Homing (optional)")]
    public bool homing = false;
    public Transform homingOrigin;
    public Transform homingTarget;
    public float homingFollowLerp = 5f;
    [Tooltip("Homing 时，最大延展长度。")]
    public float maxLength = 30f;

    [Header("Impact VFX")]
    public GameObject vfxHitGround;
    public LayerMask groundMask = -1;

    [Header("Slice Settings (for Space Slash)")]
    [Tooltip("仅在致命期可被切开（true），或任何时刻都可切（false）。")]
    public bool sliceOnlyWhenLethal = true;
    [Tooltip("可选：自动把激光放到该图层（需与空间斩的 enemyMask 一致）。空字符串则不变更。")]
    public string enemyLayerName = "Enemy";
    public bool autoAssignLayer = true;

    [Header("Sliced Death Presentation (Baseline)")]
    [Tooltip("被空间斩切中后：闪烁时长（秒）。")]
    public float slicedBlinkSeconds = 0.2f;
    [Tooltip("被空间斩切中后：闪烁频率（次/秒）。")]
    public float slicedBlinkFrequency = 24f;
    [Tooltip("被空间斩切中后：随后渐隐时长（秒）。")]
    public float slicedFadeSeconds = 0.35f;

    // ========================== 新增：电影化演出参数 ==========================
    [Header("Cinematic Boost — Hit Impact")]
    [Tooltip("命中瞬间的“弹宽”倍数（极短时间内线宽冲击放大）。")]
    public float slicedWidthPunchMul = 3f;
    [Tooltip("弹宽持续时间（秒），建议 0.04~0.08。")]
    public float slicedWidthPunchSeconds = 0.06f;
    [Tooltip("命中瞬间额外白闪强度（0=无，建议0.5~1.5）。")]
    public float slicedWhiteFlashAdd = 1.0f;

    [Header("Cinematic Boost — Camera & Time")]
    [Tooltip("是否启用短暂时间凝滞（HitStop）。")]
    public bool enableHitStop = true;
    [Tooltip("凝滞时Time.timeScale目标值（越小越停滞）。")]
    public float hitStopTimeScale = 0.05f;
    [Tooltip("凝滞时长（秒，使用Unscaled计时）。")]
    public float hitStopDuration = 0.08f;
    [Tooltip("是否尝试触发 CinemachineImpulseSource 或自定义震屏事件。")]
    public bool enableCameraImpulse = true;
    [Tooltip("找不到 Cinemachine 时，触发本事件（可挂你的 CameraShake/ScreenKick 脚本）。")]
    public UnityEvent onSlicedCameraKick;

    [Header("Cinematic Boost — VFX & SFX")]
    [Tooltip("命中点的爆裂光团（粒子/精灵皆可）。")]
    public GameObject vfxSlicedBurstPrefab;
    [Tooltip("从命中点扩散的冲击波（Scale随时间放大）。")]
    public GameObject vfxSlicedShockwavePrefab;
    [Tooltip("沿激光线体冒出的电火花（会在多处生成）。")]
    public GameObject vfxSlicedSparksPrefab;
    [Tooltip("可选：生成一个短命Light2D脉冲（若工程有URP）。")]
    public bool spawnLight2DPulse = true;
    [Tooltip("Light2D 脉冲最大半径。")]
    public float light2DMaxRadius = 4f;
    [Tooltip("Light2D 脉冲存活（秒）。")]
    public float light2DLife = 0.15f;

    [Tooltip("命中音效。")]
    public AudioClip sfxSlicedImpact;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Cinematic Boost — Sparks Along Beam")]
    [Tooltip("沿线随机落点的火花数量。")]
    public int sparksCountOnBeam = 6;
    [Tooltip("火花沿线的纵向抖动（世界单位）。")]
    public float sparksJitter = 0.2f;

    // ========================== 内部状态 ==========================
    private LineRenderer _lr;
    private float _t;
    private float _lethalStartTime;
    private float _lethalEndTime;
    private float _killTime;
    private float _initialThickness;
    private bool _madeLethalThicken;
    private Material _mat;
    private readonly HashSet<Collider2D> _hitOnce = new HashSet<Collider2D>();
    private float _impactCooldown; // 限制地面特效频率

    private EdgeCollider2D _edge;
    public bool IsDead { get; private set; }
    private bool _slicedDying;
    private float _slicedBaseAlpha;
    private float _slicedBaseWidth;

    // 命中点（可由外部在斩击触发前调用 SetSlicedHit 传入；否则用中点）
    private bool _hasSlicedHit;
    private Vector3 _slicedHitPos;
    private Vector3 _slicedHitNormal = Vector3.up;

    // —— 对外辅助：空间斩可在调用 TakeDamage 前设置命中点（更精准的VFX定位）
    public void SetSlicedHit(Vector3 pos, Vector3 normal)
    {
        _hasSlicedHit = true;
        _slicedHitPos = pos;
        _slicedHitNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
    }

    public void Initialize(Vector3 a, Vector3 b, Color color, float thickness, float chargeSeconds, float lethalSeconds, float lifeSeconds, bool sweeping, Vector2 velocity)
    {
        this.startPos = a;
        this.endPos = b;
        this.baseColor = color;
        this.thickness = thickness;
        this.chargeSeconds = chargeSeconds;
        this.lethalSeconds = lethalSeconds;
        this.lifeSeconds = lifeSeconds;
        this.sweeping = sweeping;
        this.velocity = velocity;

        if (autoAssignLayer && !string.IsNullOrEmpty(enemyLayerName))
        {
            int l = LayerMask.NameToLayer(enemyLayerName);
            if (l >= 0) gameObject.layer = l;
        }

        _lr = GetComponent<LineRenderer>();
        if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
        _lr.positionCount = 2;
        _lr.numCapVertices = 6;
        _lr.numCornerVertices = 2;
        _lr.useWorldSpace = true;
        _lr.sortingOrder = 5000;
        _initialThickness = thickness;

        _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", color);
        _lr.material = _mat;
        _lr.startWidth = _lr.endWidth = thickness;

        _edge = GetComponent<EdgeCollider2D>();
        if (_edge == null) _edge = gameObject.AddComponent<EdgeCollider2D>();
        _edge.isTrigger = true;
        // Unity 6.2: EdgeCollider2D不支持compositeOperation，移除usedByComposite设置
        // _edge.usedByComposite = false;  // 已废弃
        _edge.edgeRadius = Mathf.Max(0.01f, thickness * 0.5f);

        transform.position = Vector3.zero;

        _t = 0f;
        _lethalStartTime = chargeSeconds;
        _lethalEndTime = (lethalSeconds >= 999f) ? float.PositiveInfinity : (chargeSeconds + lethalSeconds);
        _killTime = lifeSeconds;

        UpdateVisual(0f, true);
        UpdateEdgeColliderPoints();
        UpdateEdgeColliderEnable();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;

        if (!_slicedDying)
        {
            if (sweeping)
            {
                Vector3 v = new Vector3(velocity.x, velocity.y, 0f);
                startPos += v * dt;
                endPos += v * dt;
            }
            if (homing && homingOrigin && homingTarget)
            {
                Vector3 o = homingOrigin.position;
                Vector3 tgt = Vector3.Lerp(GetLineEnd(), homingTarget.position, dt * homingFollowLerp);
                Vector3 dir = (tgt - o);
                float len = Mathf.Min(maxLength, Mathf.Max(0.1f, dir.magnitude));
                dir = dir.normalized;
                startPos = o;
                endPos = o + dir * len;
            }
        }

        UpdateVisual(dt, false);
        if (!_slicedDying) SpawnGroundImpactIfAny(dt);

        bool lethal = (_t >= _lethalStartTime) && (_t < _lethalEndTime);
        if (lethal && !_slicedDying) ApplyHitbox(dt);

        UpdateEdgeColliderPoints();
        UpdateEdgeColliderEnable();

        if (!_slicedDying && _t >= _killTime) Destroy(gameObject);
    }

    private void UpdateVisual(float dt, bool force)
    {
        _lr.SetPosition(0, startPos);
        _lr.SetPosition(1, endPos);

        if (_slicedDying) return;

        if (useChargeColorLerp && _t < chargeSeconds)
        {
            float u = Mathf.Clamp01(_t / Mathf.Max(0.0001f, chargeSeconds));
            Color c = Color.Lerp(chargeStartColor, chargeEndColor, u);
            SetColor(c);
        }
        else
        {
            SetColor(baseColor);
        }

        if (thickenOnLethal && !_madeLethalThicken && _t >= _lethalStartTime)
        {
            _madeLethalThicken = true;
            StartCoroutine(CoLerpThickness(_initialThickness, _initialThickness * Mathf.Max(1f, thickenMul), thickenLerpSeconds));
        }

        if (fadeOutSeconds > 0f)
        {
            float remain = Mathf.Max(0f, _killTime - _t);
            if (remain <= fadeOutSeconds)
            {
                float u = 1f - Mathf.Clamp01(remain / fadeOutSeconds);
                float width = Mathf.Lerp(_lr.startWidth, 0f, u);
                _lr.startWidth = _lr.endWidth = width;

                Color c = GetColor();
                c.a = Mathf.Lerp(c.a, 0f, u);
                SetColor(c);
            }
        }
    }

    private System.Collections.IEnumerator CoLerpThickness(float from, float to, float seconds)
    {
        seconds = Mathf.Max(0.0001f, seconds);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / seconds);
            float w = Mathf.Lerp(from, to, u);
            _lr.startWidth = _lr.endWidth = w;
            yield return null;
        }
        _lr.startWidth = _lr.endWidth = to;
    }

    private void ApplyHitbox(float dt)
    {
        Vector2 a = startPos;
        Vector2 b = endPos;
        Vector2 dir = (b - a).normalized;
        float len = Vector2.Distance(a, b);
        float radius = Mathf.Max(0.01f, thickness * 0.5f);

        var hits = Physics2D.CircleCastAll(a, radius, dir, len);
        foreach (var h in hits)
        {
            var col = h.collider;
            if (!col || !col.enabled) continue;

            bool isPlayer = col.CompareTag("Player");
            if (isPlayer)
            {
                if (knockupImpulse > 0f)
                {
                    var rb = col.attachedRigidbody;
                    if (rb != null) rb.AddForce(Vector2.up * knockupImpulse, ForceMode2D.Impulse);
                }

                if (continuousDrain)
                {
                    TryApplyEnergy(col.gameObject, energyDamage * dt);
                }
                else
                {
                    if (!_hitOnce.Contains(col))
                    {
                        _hitOnce.Add(col);
                        TryApplyEnergy(col.gameObject, energyDamage);
                    }
                }
            }
        }
    }

    private void TryApplyEnergy(GameObject target, float amount)
    {
        var plc = target.GetComponent<FadedDreams.Player.PlayerLightController>();
        if (plc != null) { plc.AddEnergy(-Mathf.Abs(amount)); return; }

        var dmg = target.GetComponent("IDamageable");
        if (dmg != null)
        {
            var m = dmg.GetType().GetMethod("TakeDamage");
            if (m != null) m.Invoke(dmg, new object[] { Mathf.Abs(amount) });
        }
    }

    private void SpawnGroundImpactIfAny(float dt)
    {
        _impactCooldown -= dt;
        if (_impactCooldown > 0f) return;
        if (!vfxHitGround) return;

        Vector2 a = startPos;
        Vector2 b = endPos;
        Vector2 dir = (b - a).normalized;
        float len = Vector2.Distance(a, b);

        var hit = Physics2D.Raycast(a, dir, len, groundMask);
        if (hit.collider != null)
        {
            Instantiate(vfxHitGround, hit.point, Quaternion.identity);
            _impactCooldown = 0.08f;
        }
    }

    private void UpdateEdgeColliderPoints()
    {
        if (_edge == null) return;
        var p0 = transform.InverseTransformPoint(startPos);
        var p1 = transform.InverseTransformPoint(endPos);
        _edge.points = new Vector2[] { p0, p1 };
        _edge.edgeRadius = Mathf.Max(0.01f, thickness * 0.5f);
    }

    private void UpdateEdgeColliderEnable()
    {
        if (_edge == null) return;

        if (_slicedDying)
        {
            _edge.enabled = false;
            return;
        }

        if (!sliceOnlyWhenLethal)
        {
            _edge.enabled = true;
            return;
        }
        bool lethal = (_t >= _lethalStartTime) && (_t < _lethalEndTime);
        _edge.enabled = lethal;
    }

    public Color GetColor()
    {
        if (_mat && _mat.HasProperty("_BaseColor")) return _mat.GetColor("_BaseColor");
        return baseColor;
    }

    public void SetColor(Color c)
    {
        if (_mat && _mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
        baseColor = c;
    }

    private Vector3 GetLineEnd() => endPos;

    // ========================== 被空间斩命中：主流程 ==========================
    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        IsDead = true;

        _slicedDying = true;
        sweeping = false;
        homing = false;
        vfxHitGround = null;
        _lethalStartTime = float.PositiveInfinity;
        _lethalEndTime = float.NegativeInfinity;
        _killTime = float.PositiveInfinity;
        if (_edge) _edge.enabled = false;

        _slicedBaseAlpha = GetColor().a;
        _slicedBaseWidth = Mathf.Max(0.0001f, _lr.startWidth);

        // 命中点：若外部没传，则用中点
        if (!_hasSlicedHit) _slicedHitPos = 0.5f * (startPos + endPos);

        // 触发电影化链路
        StartCoroutine(CoCinematicSlicedSequence());
    }

    // 电影化链路：Unscaled计时
    private System.Collections.IEnumerator CoCinematicSlicedSequence()
    {
        // === 0) 摄像机 + 时间凝滞 ===
        if (enableCameraImpulse) TryCameraImpulseOrEvent(_slicedHitPos);
        if (enableHitStop) StartCoroutine(CoHitStop(hitStopTimeScale, hitStopDuration));

        // === 1) 音效、VFX瞬发（爆裂/冲击波/沿线火花/Light2D） ===
        TryPlaySFX(sfxSlicedImpact, _sfxPos: _slicedHitPos);
        if (vfxSlicedBurstPrefab) Instantiate(vfxSlicedBurstPrefab, _slicedHitPos, Quaternion.identity);
        if (vfxSlicedShockwavePrefab) Instantiate(vfxSlicedShockwavePrefab, _slicedHitPos, Quaternion.identity);
        if (vfxSlicedSparksPrefab && sparksCountOnBeam > 0) SpawnSparksAlongBeam();

        if (spawnLight2DPulse) TrySpawnLight2DPulse(_slicedHitPos, light2DMaxRadius, light2DLife);

        // === 2) 弹宽 + 白闪（极短） ===
        yield return CoWidthPunchAndWhiteFlashUnscaled();

        // === 3) 高频电光闪烁（你的基础闪烁），随后进入渐隐收束并销毁 ===
        yield return CoSlicedBlinkThenFade_Unscaled();
        Destroy(gameObject);
    }

    // 命中瞬间：线宽冲击&白闪（Unscaled）
    private System.Collections.IEnumerator CoWidthPunchAndWhiteFlashUnscaled()
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, slicedWidthPunchSeconds);
        float fromW = _slicedBaseWidth;
        float toW = _slicedBaseWidth * Mathf.Max(1f, slicedWidthPunchMul);

        var c0 = GetColor();
        float baseA = c0.a;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float w = Mathf.Lerp(fromW, toW, EaseOutExpo(u));
            _lr.startWidth = _lr.endWidth = w;

            // 白闪（提升亮度/Alpha）
            Color c = c0;
            float add = slicedWhiteFlashAdd * (1f - u);
            c.a = Mathf.Clamp01(baseA + add);
            SetColor(c);

            yield return null;
        }

        // 复原到基础（后续闪烁接管）
        _lr.startWidth = _lr.endWidth = _slicedBaseWidth;
        SetColor(new Color(c0.r, c0.g, c0.b, baseA));
    }

    // 你的“闪烁→渐隐”版，但用Unscaled计时，保证HitStop下节奏不拖慢
    private System.Collections.IEnumerator CoSlicedBlinkThenFade_Unscaled()
    {
        // 1) 高频闪烁
        float t = 0f;
        Color c = GetColor();
        float baseA = _slicedBaseAlpha;
        while (t < slicedBlinkSeconds)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Abs(Mathf.Sin(t * Mathf.PI * slicedBlinkFrequency));
            float a = Mathf.Lerp(0.15f, baseA, s);
            c.a = a;
            SetColor(c);
            _lr.startWidth = _lr.endWidth = _slicedBaseWidth;
            yield return null;
        }

        // 2) 渐隐收束
        t = 0f;
        float dur = Mathf.Max(0.0001f, slicedFadeSeconds);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float a = Mathf.Lerp(baseA, 0f, u);
            float w = Mathf.Lerp(_slicedBaseWidth, 0f, u);
            c.a = a;
            SetColor(c);
            _lr.startWidth = _lr.endWidth = w;
            yield return null;
        }
    }

    // —— 辅助：时间凝滞（全局），注意短时且用Realtime恢复
    private System.Collections.IEnumerator CoHitStop(float toScale, float duration)
    {
        float prev = Time.timeScale;
        Time.timeScale = Mathf.Clamp(toScale, 0.001f, 1f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
        Time.timeScale = prev;
    }

    // —— 辅助：尝试 CinemachineImpulseSource → 否则触发事件
    private void TryCameraImpulseOrEvent(Vector3 pos)
    {
        // A) 尝试 CinemachineImpulseSource.GenerateImpulse()
        var type = Type.GetType("Cinemachine.CinemachineImpulseSource, Cinemachine");
        if (type != null)
        {
            var cmp = GetComponent(type);
            if (cmp == null) cmp = gameObject.AddComponent(type);
            var m = type.GetMethod("GenerateImpulse", new Type[] { typeof(Vector3) });
            if (m != null) { m.Invoke(cmp, new object[] { (Vector3)(Vector3.up * 1f) }); return; }
            m = type.GetMethod("GenerateImpulse");
            if (m != null) { m.Invoke(cmp, null); return; }
        }
        // B) 触发自定义事件（把你的摄像机震动脚本挂到这个事件上）
        if (onSlicedCameraKick != null) onSlicedCameraKick.Invoke();
    }

    // —— 辅助：SFX
    private void TryPlaySFX(AudioClip clip, Vector3 _sfxPos)
    {
        if (!clip) return;
        AudioSource.PlayClipAtPoint(clip, _sfxPos, sfxVolume);
    }

    // —— 辅助：沿线撒火花
    private void SpawnSparksAlongBeam()
    {
        if (!vfxSlicedSparksPrefab || sparksCountOnBeam <= 0) return;
        Vector3 a = startPos;
        Vector3 b = endPos;
        for (int i = 0; i < sparksCountOnBeam; i++)
        {
            float t = Random.Range(0f, 1f);
            Vector3 p = Vector3.Lerp(a, b, t);
            p += new Vector3(0f, Random.Range(-sparksJitter, sparksJitter), 0f);
            Instantiate(vfxSlicedSparksPrefab, p, Quaternion.identity);
        }
    }

    // —— 辅助：若有URP，反射添加 Light2D 临时脉冲；无则跳过
    private void TrySpawnLight2DPulse(Vector3 pos, float radius, float life)
    {
        var t = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
        if (t == null) return; // 无URP或未引入运行时
        var go = new GameObject("Light2D_Pulse");
        go.transform.position = pos;
        var comp = go.AddComponent(t);
        // 尝试设置类型、强度、半径
        var lightTypeProp = t.GetProperty("lightType");
        var intensityProp = t.GetProperty("intensity");
        var pointLightOuterRadiusProp = t.GetProperty("pointLightOuterRadius");
        var colorProp = t.GetProperty("color");

        if (lightTypeProp != null) lightTypeProp.SetValue(comp, Enum.ToObject(lightTypeProp.PropertyType, 0), null); // 0: Point
        if (intensityProp != null) intensityProp.SetValue(comp, 2.0f, null);
        if (pointLightOuterRadiusProp != null) pointLightOuterRadiusProp.SetValue(comp, 0.1f, null);
        if (colorProp != null) colorProp.SetValue(comp, Color.white, null);

        // 脉冲协程（Unscaled）
        StartCoroutine(CoLight2DPulseScale(go, t, radius, life));
    }

    private System.Collections.IEnumerator CoLight2DPulseScale(GameObject lightGO, Type lightType, float maxR, float life)
    {
        float tAcc = 0f;
        var comp = lightGO.GetComponent(lightType);
        var rProp = lightType.GetProperty("pointLightOuterRadius");
        var iProp = lightType.GetProperty("intensity");
        float baseI = 2f;

        while (tAcc < life)
        {
            tAcc += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(tAcc / life);
            float r = Mathf.Lerp(0.1f, maxR, EaseOutExpo(u));
            if (rProp != null) rProp.SetValue(comp, r, null);
            if (iProp != null) iProp.SetValue(comp, baseI * (1f - u), null);
            yield return null;
        }
        Destroy(lightGO);
    }

    // —— 缓动
    private float EaseOutExpo(float x) => (x >= 1f) ? 1f : 1f - Mathf.Pow(2f, -10f * x);

    // —— 公共方法
    public void Setup(float thickness, Color color)
    {
        this.thickness = thickness;
        this.baseColor = color;
        SetColor(color);
        SetThickness(thickness);
    }


    public void SetThickness(float thickness)
    {
        this.thickness = thickness;
        // 更新渲染器宽度
        LineRenderer lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.startWidth = thickness;
            lr.endWidth = thickness;
        }
    }

    public float GetThickness()
    {
        return thickness;
    }
}
