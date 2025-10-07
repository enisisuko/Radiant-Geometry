
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.Player
{
    /// <summary>
    /// 近战（红）—4段攻击版
    /// ① 下劈（顺时针弧）→ ② 上挑（逆向弧）→ ③ 三次小刺（短突进×3）→ ④ 超级大刺（长突进）
    /// 手感要点：方向平滑、收招窗口（earlyComboOpen）、命中0.3s慢动作、拖尾重置避免错位。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerMeleeLaser : MonoBehaviour
    {
        [Header("Layers / Damage")]
        public LayerMask hitMask;
        public float baseDamage = 18f;
        public float launcherDamageMul = 0.85f;
        public float thrustDamageMul = 1.05f;
        [Tooltip("命中采样圆半径（更大=更容易命中）")]
        public float hitRadius = 0.28f;
        [Tooltip("普通横斩击退力度")]
        public float knockbackForce = 6.2f;
        public float knockupOnLaunch = 8.0f;
        public float bigCleaveAoERadius = 2.6f;
        public float bigCleaveAoEDamageMul = 0.30f;
        public bool useMassToDetectLarge = true;
        public float largeMassThreshold = 3.5f;
        public float largeBoundsSize = 2.0f;

        [Header("Combo / Input")]
        public float comboWindow = 1.15f;
        public float earlyComboOpen = 0.18f;
        public float inputBuffer = 0.25f;
        public KeyCode inputKey = KeyCode.Mouse0;

        [Header("Anchors")]
        public Transform bladeOrigin;
        public FadedDreams.Player.CompanionBlade companion;

        [Header("Geometry")]
        [Range(60f, 170f)] public float sectorAngle = 135f;
        [Tooltip("近战横斩半径（有效攻击距离）")]
        public float sectorRadius = 4.2f;
        public float sampleStep = 0.20f;

        [Header("Timings (每段≈1.0s)")]
        public float cleaveWindup = 0.22f;
        public float cleaveSwing = 0.36f;
        public float cleaveRecover = 0.42f;
        public float launchWindup = 0.22f;
        public float launchSwing = 0.36f;
        public float launchRecover = 0.42f;
        public float smallThrustWindup = 0.18f;
        public float smallThrustDuration = 0.22f;
        public float smallThrustGap = 0.10f;
        public float smallThrustRecover = 0.28f;
        public float megaWindup = 0.22f;
        public float megaDuration = 0.42f;
        public float megaRecover = 0.46f;

        [Header("Thrust / 机动与无敌")]
        public float thrustDistance = 9.5f;
        public float thrustIFRames = 0.11f;
        public float softLockAngle = 28f;
        public float softLockRadius = 18f;
        public float smallThrustDistance = 4.5f;
        public float megaThrustDistance = 12.5f;

        [Header("Feel / 手感")]
        public AnimationCurve swingEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("随阶段的线宽曲线（0~1）")]
        public AnimationCurve widthByPhase = AnimationCurve.Linear(0, 0.12f, 1, 0.26f);
        public float lrCoreAlpha = 0.95f;
        [Tooltip("蓄势预览时的强度上限（透明度/宽度倍率）")]
        public float previewIntensityMax = 0.6f;

        [Header("Color (auto by Mode)")]
        public Color redCore = new Color(1.0f, 0.25f, 0.20f, 1f);
        public Color redGlow = new Color(1.0f, 0.45f, 0.10f, 0.9f);
        public Color greenCore = new Color(0.70f, 1.00f, 0.80f, 1f);
        public Color greenGlow = new Color(0.20f, 1.00f, 0.60f, 0.9f);
        private Color coreColor, glowColor;

        public Transform tipTrail;
        public bool autoEnableTrail = true;
        [Tooltip("收招后刀光淡出时长（观赏性）")] public float lingerBladeFade = 0.25f;

        [Header("Root Motion（步进）")]
        public float cleaveStepForward = 0.55f;
        public float launchStepForward = 0.48f;
        public float smallThrustStep = 0.35f;
        public float recoilBack = 0.22f;

        [Header("HitStop / SlowMo")]
        public bool enableHitStop = true;
        [Tooltip("命中时的慢动作时间比例（越小越慢）")]
        public float hitStopTimeScale = 0.12f;
        [Tooltip("全局慢动作固定持续时间（秒）")]
        public float globalSlowDuration = 0.22f;

        [Header("Aim / 方向手感")]
        public float aimSmooth = 16f;

        [Header("Ground Check (for Air Plunge)")]
        public Transform groundCheck;
        public float groundCheckRadius = 0.12f;
        public LayerMask groundMask;
        public float airPlungeGravityMul = 1.8f;
        public float airPlungeDownImpulse = -8.0f;

        [Header("Sorting")]
        public int sortingOrderCore = 20;
        public int sortingOrderGlow1 = 19;
        public int sortingOrderGlow2 = 18;

        // 可在 Inspector 里拖入你的粒子特效（例如命名 BladeVFX）
        [Header("VFX")]
        public ParticleSystem bladeVFX;

        [Header("VFX One-Shot / 长度绑定")]
        [Tooltip("只在挥砍/突进真正开始那一刻一次性喷发，而不是持续调发射率")]
        public bool vfxOneShotOnSwing = true;
        [Tooltip("一次性喷发粒子数（你说的50）")]
        public int vfxOneShotCount = 50;

        [Tooltip("粒子长度与光剑长度的映射：用于改粒子速度/拉伸比例")]
        public float saberLenToParticleSpeed = 0.6f;   // 速度映射系数
        public float saberLenToRendererScale = 0.12f;  // 拉伸比例映射系数（Stretched Billboard）

        [Header("与玩家高度绑定")]
        public Transform playerRoot;            // 可不填，自动用 transform.root
        public float heightBindMultiplier = 1f; // 若你觉得太长/太短可微调

        [Header("出现时的发光闪光")]
        public float appearGlowBoost = 2.0f;    // 出现瞬间相对当前发光的提升倍数
        public float appearGlowDuration = 0.15f;// 闪光渐回时间


        [Header("Hit VFX & Sweep")]
        public GameObject meleeHitVFX;   // 近战命中时的瞬时小特效（打到什么都播）
        public float meleeHitVFXLife = 1.0f;

        [Tooltip("横斩的整片扇形命中冷却（同一段挥砍内，以此频率最多结算一次）")]
        public float sweepCooldown = 0.3f;

        // Runtime
        private static readonly Collider2D[] _hitsBuffer = new Collider2D[64];
        private LineRenderer _lrCore, _lrGlow1, _lrGlow2;
        private TrailRenderer _trail;
        private PlayerColorModeController _mode;
        private Rigidbody2D _rb;
        private Camera _cam;
        private float _nextSweepAllowed = -999f;

        // Camera shake (reflection-based, no hard dependency)
        private static System.Reflection.MethodInfo _shakeMI;
        private static void TryShake(float duration, float strength, float frequency)
        {
            if (_shakeMI == null)
            {
                var t = System.Type.GetType("CameraShake2D") ?? System.Type.GetType("FadedDreams.Enemies.CameraShake2D");
                if (t != null) _shakeMI = t.GetMethod("Shake", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            }
            if (_shakeMI != null)
            {
                try { _shakeMI.Invoke(null, new object[] { duration, strength, frequency }); } catch { }
            }
        }

        private int _comboStep = 0;
        private float _lastComboTime = -999f;
        private bool _bufferedInput;
        private float _bufferUntil;
        private bool _busy;

        private readonly HashSetFaded _hitSet = new();
        private readonly HashSetFaded _thrustHitSet = new(); // per-thrust

        // ==== 全局慢动作并发管理（确保必恢复） ====
        private static int _slowRefCount = 0;
        private static float _savedTimeScale = 1f;
        private static float _savedFixedDelta = 0.02f;

        private static void SlowMoEnter(float slowScale)
        {
            if (_slowRefCount == 0)
            {
                _savedTimeScale = Time.timeScale;
                _savedFixedDelta = Time.fixedDeltaTime;
            }
            _slowRefCount++;
            Time.timeScale = Mathf.Clamp(slowScale, 0.05f, 1f);
            Time.fixedDeltaTime = _savedFixedDelta * Time.timeScale;
        }

        private static void SlowMoExit()
        {
            _slowRefCount = Mathf.Max(0, _slowRefCount - 1);
            if (_slowRefCount == 0)
            {
                Time.timeScale = _savedTimeScale;
                Time.fixedDeltaTime = _savedFixedDelta;
            }
        }

        // 命中触发的0.3秒全局慢动作
        private IEnumerator CoGlobalSlowMo(float seconds, float slowScale)
        {
            SlowMoEnter(slowScale);
            yield return new WaitForSecondsRealtime(seconds);
            SlowMoExit();
        }

        // aim smoothing
        private Vector2 _smoothedAim = Vector2.right;
        // 在 pos 处重新开始一条干净的拖尾
        private void TrailBeginAt(Vector3 pos)
        {
            if (_trail == null) return;
            _trail.Clear();
            tipTrail.position = pos;
            _trail.enabled = true;
            _trail.emitting = true;
        }
        private void TrailStop()
        {
            if (_trail == null) return;
            _trail.emitting = false;
            _trail.enabled = false;
        }

        private void Awake()
        {
            _mode = GetComponentInParent<PlayerColorModeController>();
            _rb = GetComponentInParent<Rigidbody2D>();
            _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

            _lrCore = GetComponent<LineRenderer>();
            SetupLR(_lrCore, sortingOrderCore);
            _lrCore.enabled = false;

            _lrGlow1 = CreateChildLR("_Glow1", sortingOrderGlow1);
            _lrGlow2 = CreateChildLR("_Glow2", sortingOrderGlow2);

            if (tipTrail) _trail = tipTrail.GetComponent<TrailRenderer>();

            // 缓存核心线材质（用于出现时发光闪光）
            if (_lrCore && _lrCore.material)
            {
                _coreMat = _lrCore.material;
                _emissiveId = Shader.PropertyToID("_EmissionColor");   // URP/HDRP 常用
                _baseColorId = Shader.PropertyToID("_BaseColor");       // URP Lit
                _colorId = Shader.PropertyToID("_Color");           // 标准/默认
            }



        }


        private Material _coreMat;
        private int _emissiveId, _baseColorId, _colorId;
        private Coroutine _glowFlashCo;


        private void OnEnable()
        {
            if (_mode == null) _mode = GetComponentInParent<PlayerColorModeController>();
            if (_mode != null)
            {
                _mode.OnModeChanged.AddListener(OnModeChanged);
                OnModeChanged(_mode.Mode);
            }
        }

        private void OnDisable()
        {
            if (_mode != null) _mode.OnModeChanged.RemoveListener(OnModeChanged);
        }

        private void OnModeChanged(ColorMode m)
        {
            if (m == ColorMode.Red) { coreColor = redCore; glowColor = redGlow; }
            else { coreColor = greenCore; glowColor = greenGlow; }
        }

        private void Update()
        {
            if (_mode == null || _mode.Mode != ColorMode.Red) return;

            // 方向平滑
            Vector2 aimNow = AimDirRaw();
            _smoothedAim = Vector2.Lerp(_smoothedAim, aimNow, 1f - Mathf.Exp(-aimSmooth * Time.deltaTime));

            if (Input.GetKeyDown(inputKey))
            {
                _bufferedInput = true;
                _bufferUntil = Time.unscaledTime + inputBuffer;
            }

            if (_busy) return;

            if (_bufferedInput && Time.unscaledTime <= _bufferUntil)
            {
                _bufferedInput = false;
                HandleComboInput();
            }
        }


        // 依据光剑当前长度，绑定粒子“长度表现”（速度 + Stretch渲染拉伸）
        private void UpdateParticleLengthBinding(float bladeLength)
        {
            var vfx = GetBladeVFX();
            if (!vfx) return;

            var rend = vfx.GetComponent<ParticleSystemRenderer>();
            if (rend && rend.renderMode == ParticleSystemRenderMode.Stretch)
            {
                rend.lengthScale = Mathf.Max(0.01f, bladeLength * saberLenToRendererScale);
            }

            var main = vfx.main;
            main.startSpeed = Mathf.Max(0.01f, bladeLength * saberLenToParticleSpeed);
        }

        private void HandleComboInput()
        {
            if (!_mode.TrySpendAttackCost()) return;

            float now = Time.unscaledTime;
            bool withinCombo = (now - _lastComboTime) <= comboWindow;
            if (!withinCombo) _comboStep = 0;
            _comboStep = Mathf.Clamp(_comboStep + 1, 1, 4);
            _lastComboTime = now;

            switch (_comboStep)
            {
                case 1: StartCoroutine(CoDownCleave(+1)); break;   // 下劈：顺时针（+1）
                case 2: StartCoroutine(CoDownCleave(-1)); break;   // 上挑：反向（-1）
                case 3: StartCoroutine(CoTripleStab()); break;     // 三次小刺
                case 4: StartCoroutine(CoMegaThrust()); break;     // 超级大刺
            }
        }

        // ①/② 弧斩：dirSign = +1 顺时针（下劈感），-1 逆时针（上挑感）
        // 扫描“整片弧形扇面”的一次性命中：半径向 + 角度向覆盖采样，保证扫过区域都有判定。
        // 命中即：伤害结算 + 可选击退/击飞 + 播放近战命中特效（meleeHitVFX）；同一挥砍段内对同一目标只结算一次（_hitSet）。
        private bool DoSectorHits(Vector3 origin, Vector2 centerDir, float radius, float damage, bool knockback = true, bool applyLaunch = false)
        {
            bool hitAny = false;

            if (radius <= 0.01f) return false;
            if (hitRadius <= 0.005f) hitRadius = 0.01f;

            float half = Mathf.Max(1f, sectorAngle * 0.5f);
            float dr = Mathf.Max(sampleStep, hitRadius * 0.6f); // 半径向步长：不小于命中圆半径的 0.6 倍，防穿孔

            const float ANGLE_STEP_MIN_DEG = 2.0f;   // 角度最细步长（度）
            const float ANGLE_STEP_MAX_DEG = 14.0f;  // 角度最粗步长（度）

            Vector2 nCenter = centerDir.sqrMagnitude > 0.0001f ? centerDir.normalized : Vector2.right;

            // 从内到外扫整片扇面
            for (float r = dr; r <= radius + 1e-3f; r += dr)
            {
                // 依据当前圆周长度估算角度步长：Δθ(rad) ≈ hitRadius / r，并限制在最小/最大范围
                float stepDeg = Mathf.Rad2Deg * Mathf.Clamp(hitRadius / Mathf.Max(0.05f, r),
                                                            ANGLE_STEP_MIN_DEG * Mathf.Deg2Rad,
                                                            ANGLE_STEP_MAX_DEG * Mathf.Deg2Rad);

                for (float a = -half; a <= half + 0.001f; a += stepDeg)
                {
                    Vector2 dir = Rotate(nCenter, a);
                    Vector3 p = origin + (Vector3)(dir * r);

                    int cnt = Physics2D.OverlapCircleNonAlloc(p, hitRadius, _hitsBuffer, hitMask);
                    for (int i = 0; i < cnt; i++)
                    {
                        var col = _hitsBuffer[i];
                        if (!col) continue;

                        var dmg = col.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                        if (dmg == null || dmg.IsDead) continue;

                        if (_hitSet.Contains(dmg)) continue; // 本段只结算一次
                        _hitSet.Add(dmg);

                        dmg.TakeDamage(damage);
                        hitAny = true;

                        // 命中就播一枚小特效（无论目标类型）
                        SpawnMeleeHitVFX(p, dir);

                        var rb = col.attachedRigidbody ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();
                        if (rb)
                        {
                            if (applyLaunch)
                            {
                                var v = rb.linearVelocity;
                                v.y = Mathf.Max(v.y, knockupOnLaunch);
                                rb.linearVelocity = v;
                            }
                            if (knockback)
                            {
                                rb.AddForce(dir.normalized * knockbackForce, ForceMode2D.Impulse);
                            }
                        }
                    }
                }
            }

            return hitAny;
        }

        // dirSign = +1 顺时针（下劈感），-1 逆时针（上挑感）
        private IEnumerator CoDownCleave(int dirSign)
        {
            _busy = true;
            if (companion) companion.AttachTo(bladeOrigin ? bladeOrigin : transform);
            if (tipTrail && autoEnableTrail) { TrailStop(); TrailBeginAt(BladeOriginPos()); }

            Vector2 baseAim = _smoothedAim;

            // 蓄势预览
            yield return CoWindupPreview(baseAim, cleaveWindup);

            // 进入挥砍瞬间：一次性喷发粒子（数量= vfxOneShotCount）
            FireBladeOneShot(sectorRadius * HeightScale());

            bool grounded = IsGrounded();
            float originalGravity = 0f;
            if (!grounded && _rb)
            {
                originalGravity = _rb.gravityScale;
                _rb.gravityScale = originalGravity * airPlungeGravityMul;
                var v = _rb.linearVelocity; v.y = airPlungeDownImpulse; _rb.linearVelocity = v;
            }

            // 本段挥砍：重置已命中集合 + 冷却
            _hitSet.Clear();
            _nextSweepAllowed = -999f;

            float elapsed = 0f, total = cleaveSwing, half = sectorAngle * 0.5f;
            EnableBeams(true);

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / total);
                float eased = swingEase.Evaluate(u);

                float from = dirSign > 0 ? +half : -half;
                float to = dirSign > 0 ? -half : +half;
                Vector2 dir = Rotate(baseAim, Mathf.Lerp(from, to, eased));

                // 轻微步进（Root Motion 替代）
                RootStep(dir, cleaveStepForward * Time.deltaTime / total);

                // 画出当前刀身（线宽/颜色/粒子朝向等）
                var tip = DrawBlade(dir, sectorRadius, u);
                if (companion) companion.FollowTip(tip);

                // —— 整片扇形一次性判定：同一段内最多每 sweepCooldown 结算一次 —— 
                if (Time.time >= _nextSweepAllowed)
                {
                    bool hit = DoSectorHits(BladeOriginPos(), dir, sectorRadius,
                                            baseDamage * (dirSign > 0 ? 1f : launcherDamageMul),
                                            true, dirSign < 0);
                    if (hit && enableHitStop) StartCoroutine(CoGlobalSlowMo(globalSlowDuration, hitStopTimeScale));
                    _nextSweepAllowed = Time.time + Mathf.Max(0.01f, sweepCooldown);
                }

                yield return null;
            }

            // 淡出 & 震屏 & 轻微后撤
            StartCoroutine(CoLinger(lingerBladeFade));
            TryShake(0.11f, 0.22f, 18f);
            SmallRecoil(baseAim, recoilBack);

            // 收招 & 连段窗口
            yield return WaitScaled(Mathf.Max(0f, cleaveRecover - earlyComboOpen));
            _lastComboTime = Time.unscaledTime;
            yield return WaitScaled(earlyComboOpen);

            // 空中落地强化：地震波
            if (!grounded)
            {
                float t = 0.25f;
                while (t > 0f && !IsGrounded()) { t -= Time.deltaTime; yield return null; }
                if (IsGrounded()) SpawnGroundShock(transform.position, bigCleaveAoERadius, baseDamage * bigCleaveAoEDamageMul);
                if (_rb) _rb.gravityScale = originalGravity > 0f ? originalGravity : _rb.gravityScale;
            }

            EndStep(baseAim);
        }


        // ③ 三次小刺：短距离三连突进
        private IEnumerator CoTripleStab()
        {
            _busy = true;
            if (companion) companion.AttachTo(bladeOrigin ? bladeOrigin : transform);
            Vector2 baseAim = _smoothedAim;

            for (int i = 0; i < 3; i++)
            {
                if (tipTrail && autoEnableTrail) { TrailStop(); TrailBeginAt(BladeOriginPos()); }
                Vector2 dir = SoftLockDir(_smoothedAim, softLockAngle, softLockRadius);
                yield return CoWindupPreview(dir, smallThrustWindup, thrust: true);
                // Windup结束，进入挥砍瞬间：一次性喷发
                FireBladeOneShot(sectorRadius * HeightScale());

                yield return DoThrust(dir, smallThrustDistance, smallThrustDuration, baseDamage * 0.85f, false);
                TryShake(0.08f, 0.18f, 18f);
                SmallRecoil(dir, recoilBack * 0.66f);
                if (i < 2) yield return WaitScaled(smallThrustGap); // 间隔
            }

            yield return WaitScaled(Mathf.Max(0f, smallThrustRecover - earlyComboOpen));
            _lastComboTime = Time.unscaledTime;
            yield return WaitScaled(earlyComboOpen);
            EndStep(baseAim);
        }

        // ④ 超级大刺：更长距离/更高伤害/短无敌
        private IEnumerator CoMegaThrust()
        {
            _busy = true;
            if (companion) companion.AttachTo(bladeOrigin ? bladeOrigin : transform);
            Vector2 dir = SoftLockDir(_smoothedAim, softLockAngle, softLockRadius);

            if (tipTrail && autoEnableTrail) { TrailStop(); TrailBeginAt(BladeOriginPos()); }
            yield return CoWindupPreview(dir, megaWindup, thrust: true);
            // Windup结束，进入挥砍瞬间：一次性喷发
            FireBladeOneShot(sectorRadius * HeightScale());

            yield return DoThrust(dir, megaThrustDistance, megaDuration, baseDamage * thrustDamageMul * 1.35f, true);
            TryShake(0.16f, 0.35f, 14f);
            SmallRecoil(dir, recoilBack);

            yield return WaitScaled(Mathf.Max(0f, megaRecover - earlyComboOpen));
            _lastComboTime = Time.unscaledTime;
            yield return WaitScaled(earlyComboOpen);
            EndStep(dir);
        }

        // 统一的突进实现（可被③/④复用）
        private IEnumerator DoThrust(Vector2 dir, float distance, float duration, float damage, bool strong)
        {
            _thrustHitSet.Clear();
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)(dir.normalized * distance);

            StartCoroutine(CoIFRames(thrustIFRames * (strong ? 1.25f : 1f)));
            EnableBeams(true);

            float elapsed = 0f;
            Vector3 prev = start;
            bool blocked = false;

            while (elapsed < duration && !blocked)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / duration);
                Vector3 p = Vector3.Lerp(start, end, u);
                if (_rb) _rb.MovePosition(p);

                Vector3 moveDir = (p - prev);
                float segLen = moveDir.magnitude + 0.0001f;
                Vector3 step = moveDir.normalized * Mathf.Max(sampleStep, hitRadius * 0.8f);
                for (float d = 0f; d <= segLen; d += step.magnitude)
                {
                    Vector3 probe = prev + moveDir.normalized * d;
                    if (DoPointHits(probe, damage, dir))
                    {
                        blocked = true;
                        if (enableHitStop) StartCoroutine(CoGlobalSlowMo(globalSlowDuration, hitStopTimeScale));
                        break;
                    }
                }

                var tip = DrawBlade(dir, sectorRadius * (strong ? 0.9f : 0.7f), u, lineLengthOverride: (p - start).magnitude);
                if (companion) companion.FollowTip(tip);

                prev = p;
                yield return null;
            }

            StartCoroutine(CoLinger(lingerBladeFade * (strong ? 1.25f : 1f)));
        }

        /// <summary>沿攻击方向的小幅位移（替代动画 RootMotion）</summary>
        private void RootStep(Vector2 dir, float step)
        {
            if (_rb) _rb.MovePosition(_rb.position + dir.normalized * step);
        }

        /// <summary>收招小幅后撤</summary>
        private void SmallRecoil(Vector2 baseAim, float distance)
        {
            if (!_rb || distance <= 0f) return;
            _rb.MovePosition(_rb.position - baseAim.normalized * distance);
        }

       


        private bool DoPointHits(Vector3 point, float damage, Vector2 moveDir)
        {
            int __n2 = Physics2D.OverlapCircleNonAlloc(point, hitRadius, _hitsBuffer, hitMask);
            for (int __j = 0; __j < __n2; __j++)
            {
                var c = _hitsBuffer[__j];
                var dmg = c.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                if (dmg != null && !dmg.IsDead)
                {
                    if (_thrustHitSet.Contains(dmg)) continue;
                    _thrustHitSet.Add(dmg);

                    bool large = false;
                    if (useMassToDetectLarge)
                    {
                        var rbm = c.attachedRigidbody ? c.attachedRigidbody : c.GetComponentInParent<Rigidbody2D>();
                        if (rbm && rbm.mass >= largeMassThreshold) large = true;
                    }
                    if (!large && c.bounds.size.magnitude >= largeBoundsSize) large = true;

                    dmg.TakeDamage(damage);

                    var rb2 = c.attachedRigidbody ? c.attachedRigidbody : c.GetComponentInParent<Rigidbody2D>();
                    if (rb2)
                    {
                        Vector2 push = moveDir.normalized * (large ? 3.0f : 5.0f);
                        rb2.AddForce(push, ForceMode2D.Impulse);
                    }
                    if (large)
                    {
                        if (_rb)
                        {
                            Vector2 to = (Vector2)point - (Vector2)_rb.worldCenterOfMass;
                            float len = Mathf.Max(0f, to.magnitude - 0.2f);
                            _rb.MovePosition(_rb.position + to.normalized * len);
                        }
                        return true; // 阻挡，提前结束
                    }
                }
            }
            return false;
        }

        private void SpawnGroundShock(Vector3 center, float radius, float damage)
        {
            int __n3 = Physics2D.OverlapCircleNonAlloc(center, radius, _hitsBuffer, hitMask);
            for (int __k = 0; __k < __n3; __k++)
            {
                var c = _hitsBuffer[__k];
                var d = c.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                if (d != null && !d.IsDead) d.TakeDamage(damage);
                var rb = c.attachedRigidbody ? c.attachedRigidbody : c.GetComponentInParent<Rigidbody2D>();
                if (rb) rb.AddForce((rb.worldCenterOfMass - (Vector2)center).normalized * 4f, ForceMode2D.Impulse);
            }
        }

        private void EnableBeams(bool on)
        {
            // —— 实体光剑（只开关，不改材质/不改粒子数）——
            if (_lrCore)
            {
                _lrCore.enabled = on;
                _lrCore.positionCount = on ? 2 : 0;

                var c = coreColor; c.a = lrCoreAlpha;
                _lrCore.startColor = c;
                _lrCore.endColor = c;

                float w = Mathf.Max(0.04f, widthByPhase.Evaluate(0f));
                _lrCore.startWidth = w;
                _lrCore.endWidth = w;
            }

            // Glow 线保持关闭
            if (_lrGlow1) { _lrGlow1.enabled = false; _lrGlow1.positionCount = 0; }
            if (_lrGlow2) { _lrGlow2.enabled = false; _lrGlow2.positionCount = 0; }

            // —— 粒子系统：仅负责 Play/Stop，不改发射率（你自己在粒子里设“瞬发50”）——
            var vfx = GetBladeVFX();
            if (vfx)
            {
                var em = vfx.emission; em.enabled = true;
                if (on)
                {
                    if (!vfx.isPlaying) vfx.Play(true);
                    // 出现瞬间做一个发光闪光
                    if (_glowFlashCo != null) StopCoroutine(_glowFlashCo);
                    _glowFlashCo = StartCoroutine(CoBladeGlowFlash());
                }
                else
                {
                    vfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            // 拖尾
            if (!on && tipTrail && autoEnableTrail && _trail)
            {
                _trail.emitting = false;
                _trail.enabled = false;
            }
        }



        // 在真正开始挥砍/突进的那一刻调用
        private void FireBladeOneShot(float currentBladeLength)
        {
            var vfx = GetBladeVFX();
            if (!vfx) return;

            // 只在开始时一次性喷发，不再持续调发射率
            if (vfxOneShotOnSwing && vfxOneShotCount > 0)
            {
                vfx.Emit(vfxOneShotCount);
            }

            // 将粒子“长度”绑定到光剑长度：
            // 1) Stretched Billboard 可用 renderer.lengthScale
            // 2) 另外调整 startSpeed 以拉长拖影粒子
            var rend = vfx.GetComponent<ParticleSystemRenderer>();
            if (rend && rend.renderMode == ParticleSystemRenderMode.Stretch)
            {
                rend.lengthScale = Mathf.Max(0.01f, currentBladeLength * saberLenToRendererScale);
            }

            var main = vfx.main;
            main.startSpeed = Mathf.Max(0.01f, currentBladeLength * saberLenToParticleSpeed);
        }

        // 将光剑长度与玩家“高度/缩放”绑定，避免分离感
        private float HeightScale()
        {
            var root = playerRoot ? playerRoot : transform.root;
            return root ? Mathf.Max(0.01f, root.lossyScale.y * heightBindMultiplier) : 1f;
        }

        // 出现时的“发光闪光”，对常见着色器做兼容处理
        private IEnumerator CoBladeGlowFlash()
        {
            if (_coreMat == null) yield break;

            // 读取当前颜色
            Color baseC = _coreMat.HasProperty(_emissiveId) ? _coreMat.GetColor(_emissiveId) :
                          _coreMat.HasProperty(_baseColorId) ? _coreMat.GetColor(_baseColorId) :
                          _coreMat.HasProperty(_colorId) ? _coreMat.GetColor(_colorId) :
                          Color.white;

            // 提升后目标颜色（不改变色相，只放大发光强度）
            Color target = baseC * appearGlowBoost;

            float t = 0f;
            while (t < appearGlowDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / appearGlowDuration);
                Color now = Color.Lerp(target, baseC, u);

                if (_coreMat.HasProperty(_emissiveId)) _coreMat.SetColor(_emissiveId, now);
                else if (_coreMat.HasProperty(_baseColorId)) _coreMat.SetColor(_baseColorId, now);
                else if (_coreMat.HasProperty(_colorId)) _coreMat.SetColor(_colorId, now);

                yield return null;
            }
        }



        private Vector3 DrawBlade(Vector2 dir, float len, float phase01, float lineLengthOverride = -1f)
        {
            Vector3 p0 = BladeOriginPos();
            float L = (lineLengthOverride > 0f ? lineLengthOverride : len);
            Vector2 nDir = (dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right);
            Vector3 p1 = p0 + (Vector3)(nDir * L);

            // —— 实体光剑 —— 
            if (_lrCore)
            {
                _lrCore.enabled = true;
                _lrCore.positionCount = 2;
                _lrCore.SetPosition(0, p0);
                _lrCore.SetPosition(1, p1);

                float w = Mathf.Max(0.04f, widthByPhase.Evaluate(Mathf.Clamp01(phase01)));
                _lrCore.startWidth = w;
                _lrCore.endWidth = w;

                var c = coreColor; c.a = lrCoreAlpha;
                _lrCore.startColor = c;
                _lrCore.endColor = c;
                // 不改材质：由你在 Inspector 里选择
            }

            // —— 粒子：仅位置/朝向 & 发射率（智能阶段控制），不改 Shape/Scale —— 
            var vfx = GetBladeVFX();
            if (vfx)
            {
                var tr = vfx.transform;
                tr.position = p0 + (Vector3)(nDir * (L * 0.35f)); // 轻微前移，避免身后可见
                tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(nDir.y, nDir.x) * Mathf.Rad2Deg);

                // 出手期：发射率随阶段上升到峰值
                float rt = Mathf.Lerp(40f, 140f, Mathf.SmoothStep(0f, 1f, phase01));
                SetVFXRate(vfx, rt);
                if (!vfx.isPlaying) vfx.Play(true);
            }

            if (tipTrail) tipTrail.position = p1;
            return p1;
        }





        private IEnumerator CoWindupPreview(Vector2 dir, float duration, bool thrust = false)
        {
            EnableBeams(true);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = swingEase.Evaluate(u);
                float len = Mathf.Lerp(sectorRadius * (thrust ? 0.30f : 0.40f), sectorRadius * (thrust ? 0.70f : 0.90f), eased);
                DrawBladePreview(dir, len, Mathf.Lerp(0.25f, previewIntensityMax, eased), thrust);
                yield return null;
            }
        }

        private void DrawBladePreview(Vector2 dir, float len, float intensity01, bool thrust)
        {
            Vector3 p0 = BladeOriginPos();

            float heightK = HeightScale();
            float L = len * heightK;

            Vector2 nDir = (dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right);
            Vector3 p1 = p0 + (Vector3)(nDir * L);

            if (_lrCore)
            {
                _lrCore.enabled = true;
                _lrCore.positionCount = 2;
                _lrCore.SetPosition(0, p0);
                _lrCore.SetPosition(1, p1);

                float w = Mathf.Max(0.04f, widthByPhase.Evaluate(intensity01)) * Mathf.Lerp(0.7f, 1.0f, intensity01);
                _lrCore.startWidth = w;
                _lrCore.endWidth = w;

                var c = coreColor; c.a = lrCoreAlpha * Mathf.Lerp(0.4f, previewIntensityMax, intensity01);
                _lrCore.startColor = c;
                _lrCore.endColor = c;
            }

            var vfx = GetBladeVFX();
            if (!vfx) return;

            var tr = vfx.transform;
            tr.position = p0 + (Vector3)(nDir * (L * 0.25f));
            tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(nDir.y, nDir.x) * Mathf.Rad2Deg);

            // 预览阶段也绑定一下长度（仅速度/拉伸，不改发射率）
            UpdateParticleLengthBinding(L);
        }






        private System.Collections.IEnumerator CoLinger(float fade)
        {
            var vfx = GetBladeVFX();
            if (vfx)
            {
                var em = vfx.emission;
                var rate = em.rateOverTime;
                float t = 0f;
                float r0 = rate.constant;

                while (t < fade)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Clamp01(t / fade);
                    rate.constant = Mathf.Lerp(r0, 0f, u);
                    em.rateOverTime = rate;
                    yield return null;
                }
                vfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else
            {
                float t = 0f; while (t < fade) { t += Time.deltaTime; yield return null; }
            }

            // 实体光剑关闭（材质仍保持你的设置）
            if (_lrCore)
            {
                _lrCore.positionCount = 0;
                _lrCore.enabled = false;
            }
        }

        // 取得刀光粒子（优先使用 Inspector 拖入的 bladeVFX；否则在子物体里找包含“BladeVFX”的）
        private ParticleSystem GetBladeVFX()
        {
            if (bladeVFX) return bladeVFX;
            var all = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in all)
            {
                if (!ps) continue;
                if (ps.name.IndexOf("BladeVFX", System.StringComparison.OrdinalIgnoreCase) >= 0) return ps;
            }
            return null;
        }

        // 只调发射率，不改 Shape/Scale
        private void SetVFXRate(ParticleSystem ps, float rateOverTime)
        {
            var em = ps.emission;
            var rate = em.rateOverTime;
            rate.constant = rateOverTime;
            em.rateOverTime = rate;
        }



        private Gradient MakeGradient(Color c, float a = 1f)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(a * 0.9f, 1f) }
            );
            return g;
        }

        private Vector3 BladeOriginPos() => bladeOrigin ? bladeOrigin.position : transform.position;

        private Vector2 AimDirRaw()
        {
            if (_cam == null) _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            Vector3 mp = Input.mousePosition;
            float depth = Mathf.Abs((_cam ? _cam.transform.position.z : 0f) - BladeOriginPos().z);
            mp.z = depth <= 0.001f ? 10f : depth;
            Vector3 world = _cam ? _cam.ScreenToWorldPoint(mp) : (Vector3)transform.right + BladeOriginPos();
            world.z = BladeOriginPos().z;
            return (world - BladeOriginPos()).normalized;
        }

        private Vector2 AimDir() => _smoothedAim.sqrMagnitude > 0.001f ? _smoothedAim.normalized : Vector2.right;

        private Vector2 SoftLockDir(Vector2 aimDir, float maxAngle, float radius)
        {
            Collider2D best = null;
            float bestSqr = float.MaxValue;
            var cols = Physics2D.OverlapCircleAll(transform.position, radius, hitMask);
            foreach (var c in cols)
            {
                Vector2 d = (Vector2)c.bounds.center - (Vector2)transform.position;
                float ang = Vector2.Angle(aimDir, d);
                if (ang <= maxAngle)
                {
                    float s = d.sqrMagnitude;
                    if (s < bestSqr) { bestSqr = s; best = c; }
                }
            }
            if (!best) return aimDir;

            Vector2 to = ((Vector2)best.bounds.center - (Vector2)transform.position).normalized;
            float delta = Vector2.SignedAngle(aimDir, to);
            float clamp = Mathf.Clamp(delta, -8f, 8f);
            return Rotate(aimDir, clamp);
        }

        private Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(rad), sa = Mathf.Sin(rad);
            return new Vector2(ca * v.x - sa * v.y, sa * v.x + ca * v.y);
        }

        private bool IsGrounded()
        {
            if (!groundCheck) return false;
            return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
        }

        private IEnumerator CoIFRames(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        private void EndStep(Vector2 lastDir)
        {
            _busy = false;
            _lastComboTime = Time.unscaledTime;
            if (_comboStep >= 4) _comboStep = 0;
            if (companion)
            {
                companion.ReturnToOrbitDelayed(comboWindow * 0.6f);
                companion.TransitionFlourish(lastDir);
            }
        }

        private void SetupLR(LineRenderer lr, int sortingOrder)
        {
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 8; lr.numCornerVertices = 4;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = sortingOrder;
        }

        private LineRenderer CreateChildLR(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            SetupLR(lr, sortingOrder);
            lr.material = _lrCore ? _lrCore.material : null;
            lr.enabled = false;
            return lr;
        }

        private IEnumerator WaitScaled(float sec)
        {
            float t = 0f;
            while (t < sec) { t += Time.unscaledDeltaTime; yield return null; }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.25f);
            Vector2 aim = Application.isPlaying ? AimDir() : Vector2.right;
            float half = sectorAngle * 0.5f;
            Vector3 c = transform.position;
            Vector3 a = c + (Vector3)Rotate(aim, -half) * sectorRadius;
            Vector3 b = c + (Vector3)Rotate(aim, half) * sectorRadius;
            Gizmos.DrawLine(c, a); Gizmos.DrawLine(c, b);
            Vector3 prev = a;
            for (int i = 1; i <= 20; i++)
            {
                float tt = -half + (sectorAngle) * (i / 20f);
                Vector3 p = c + (Vector3)Rotate(aim, tt) * sectorRadius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif

        // ======== 小工具：更紧凑的 HashSet 别名，避免长泛型名占行 ========


        private void SpawnMeleeHitVFX(Vector3 pos, Vector2 dir)
        {
            if (!meleeHitVFX) return;
            var go = Instantiate(meleeHitVFX, pos, Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
            if (meleeHitVFXLife > 0f) Destroy(go, meleeHitVFXLife);
        }

        private class HashSetFaded : HashSet<FadedDreams.Enemies.IDamageable> { }
    }
}
