
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FadedDreams.Player
{
    /// <summary>
    /// 远程（绿色形态）四段：
    /// ① 单发剑光；② 环形护刃（持续挡/反弹子弹）；③ 剑光蛋（碰撞爆炸，AOE）；④ 超大斩（多段判定+残像）
    /// 手感要点：输入缓冲、连段窗口、方向平滑、出手前短预览、NonAlloc 检测。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerRangedCharger : MonoBehaviour
    {
        // ================= Refs =================
        [Header("Anchors")]
        public Transform bladeOrigin;
        public CompanionBlade companion;

        [Header("Layers")]
        public LayerMask enemyMask;
        public LayerMask obstacleMask;
        [Tooltip("用于②护刃拦截/反弹的敌方子弹层")]
        public LayerMask bulletMask;

        [Header("Aim / Input")]
        public float aimSmooth = 16f;              // 鼠标方向平滑
        public KeyCode inputKey = KeyCode.Mouse0;  // 攻击键
        public float comboWindow = 1.0f;           // 段与段最大衔接窗口
        public float inputBuffer = 0.2f;           // 输入缓冲

        [Header("Soft Lock")]
        public float softLockAngle = 25f;
        public float softLockRadius = 16f;
        public float softLockMaxCorrection = 8f;

        // ====== Stage 1：单发 ======
        [Header("Stage ① 单发・前斩")]
        public float s1_preflashFrames = 4f;
        public float s1_speed = 22f;
        public float s1_range = 14f;
        public float s1_hitRadius = 0.54f;
        public int s1_pierceCount = 2;
        public float s1_damage = 22f;
        public float s1_recover = 0.35f;

        // ====== Stage 2：护刃（可挡子弹） ======
        [Header("Stage ② 环形护刃（可挡子弹）")]
        public float s2_spinTime = 0.20f;             // 激活前短预览
        public float s2_shieldDuration = 2.20f;       // 持续时间
        public float s2_shieldOuter = 1.90f;
        public float s2_shieldThickness = 0.90f;
        public float s2_shieldSpinDegPerSec = 120f;   // 旋转
        public float s2_pulseAmp = 0.12f;             // 呼吸脉动
        public bool s2_reflectProjectiles = true;
        public float s2_reflectSpeedMul = 1.10f;
        public float s2_recover = 0.35f;
        [Tooltip("允许在护刃激活一段时间后提前取消并转入下一段")] public bool s2_allowEarlyCancel = true;
        [Tooltip("护刃最短保持时间（秒），超过后若检测到下一次输入则提前结束本段")] public float s2_minActiveBeforeCancel = 0.40f;

        // ====== Stage 3：剑光蛋 ======
        [Header("Stage ③ 剑光蛋（碰撞爆炸）")]
        public float s3_preflashFrames = 5f;
        public float s3_eggSpeed = 18f;
        public float s3_eggMaxRange = 11f;     // <=0 忽略
        public float s3_eggLifeSeconds = 0.8f; // <=0 忽略
        public float s3_eggHitRadius = 0.45f;
        public float s3_eggDirectHitDamage = 18f;
        public float s3_explosionRadius = 3.2f;
        public float s3_explosionDamage = 50f;     // 爆心伤害（外圈按幂衰减）
        [Range(0.5f, 3f)] public float s3_explosionFalloffPow = 1.6f;
        public float s3_explosionPushSmall = 2.5f;
        public float s3_explosionPushLarge = 1.0f;
        public float s3_largeMassThreshold = 3.5f;
        public float s3_recover = 0.40f;

        // ====== Stage 4：超大斩 ======
        [Header("Stage ④ 超级大斩")]
        public float s4_preflashFrames = 6f;
        public float s4_speed = 20f;
        public float s4_length = 9.0f;
        public float s4_hitRadius = 1.10f;
        public float s4_damage = 70f;               // 会按多段分摊
        public int s4_multiHitsPerTarget = 3;
        public float s4_tickInterval = 0.05f;
        public float s4_fadeTail = 0.45f;
        [Range(100, 240)] public float s4_arcDeg = 160f;
        public float s4_outer = 2.10f;
        public float s4_thickness = 1.90f;
        public float s4_recover = 0.50f;

        // ====== Visuals ======
        [Header("Visuals (Line Preflash)")]
        public float lrAlpha = 1f;
        public AnimationCurve widthByPhase = AnimationCurve.Linear(0, 0.12f, 1, 0.24f);
        public AnimationCurve swingEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Color (auto by Mode)")]
        public Color redCore = new Color(1.00f, 0.28f, 0.22f, 1f);
        public Color redGlow = new Color(1.00f, 0.48f, 0.18f, 0.95f);
        public Color greenCore = new Color(0.70f, 1.00f, 0.80f, 1f);
        public Color greenGlow = new Color(0.20f, 1.00f, 0.60f, 0.95f);
        private Color coreColor, glowColor;

        [Header("Crescent Mesh Defaults")]
        public Material crescentMaterial;
        [Range(20, 160)] public float crescentArcDeg = 100f;
        public float crescentOuter = 1.10f;
        public float crescentThickness = 0.75f;

        [Header("Sorting")]
        public int sortingOrderCore = 20;
        public int sortingOrderGlow1 = 19;
        public int sortingOrderGlow2 = 18;




        [Header("Hit VFX (Ranged)")]
        [Tooltip("S1/S4 命中使用的小命中特效")]
        public GameObject vfxHitSmall;
        public float vfxHitSmallLife = 1.0f;

        [Tooltip("S3 爆炸使用的特效（与小命中特效不同）")]
        public GameObject vfxExplosion;
        public float vfxExplosionLife = 1.2f;
        

        // ================= Runtime =================
        private PlayerColorModeController _mode;
        private Camera _cam;
        private LineRenderer _lrCore, _lrGlow1, _lrGlow2;
        private Rigidbody2D _rb;
        private int _comboStep = 0;
        private float _lastComboTime = -999f;
        private bool _bufferedInput;
        private float _bufferUntil;
        private bool _busy;
        private Vector2 _smoothedAim = Vector2.right;
        // S2 runtime handle（护盾可跨出②段继续存在）
        private GameObject _s2ActiveRing;
        private Coroutine _s2Runner;



        // NonAlloc buffers
        private static readonly Collider2D[] _buf64 = new Collider2D[64];














        private void SpawnHitVFX(Vector3 pos, Vector2 dir)
        {
            if (!vfxHitSmall) return;
            var go = Instantiate(vfxHitSmall, pos,
                Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
            if (vfxHitSmallLife > 0f) Destroy(go, vfxHitSmallLife);
        }

        private void SpawnExplosionVFX(Vector3 pos)
        {
            if (!vfxExplosion) return;
            var go = Instantiate(vfxExplosion, pos, Quaternion.identity);
            if (vfxExplosionLife > 0f) Destroy(go, vfxExplosionLife);
        }








        // Optional camera shake (reflection)
        private static MethodInfo _shakeMI;
        private static void TryShake(float duration, float strength, float frequency)
        {
            if (_shakeMI == null)
            {
                var t = System.Type.GetType("CameraShake2D") ?? System.Type.GetType("FadedDreams.CameraFX.CameraShake2D") ?? System.Type.GetType("FadedDreams.Enemies.CameraShake2D");
                if (t != null) _shakeMI = t.GetMethod("Shake", BindingFlags.Public | BindingFlags.Static);
            }
            if (_shakeMI != null)
            {
                try { _shakeMI.Invoke(null, new object[] { duration, strength, frequency }); } catch { }
            }
        }

        private void Awake()
        {
            _mode = GetComponentInParent<PlayerColorModeController>();
            _rb = GetComponentInParent<Rigidbody2D>();
            _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

            _lrCore = GetComponent<LineRenderer>(); SetupLR(_lrCore, sortingOrderCore);
            _lrGlow1 = CreateChildLR("_Glow1", sortingOrderGlow1);
            _lrGlow2 = CreateChildLR("_Glow2", sortingOrderGlow2);
            _lrCore.enabled = _lrGlow1.enabled = _lrGlow2.enabled = false;
        }

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
            if (m == ColorMode.Green) { coreColor = greenCore; glowColor = greenGlow; }
            else { coreColor = redCore; glowColor = redGlow; }
        }

        private void Update()
        {
            // 仅绿色形态可用
            if (_mode == null || _mode.Mode != ColorMode.Green) return;

            // 平滑方向
            Vector2 aimNow = AimDir();
            _smoothedAim = Vector2.Lerp(_smoothedAim, aimNow, 1f - Mathf.Exp(-aimSmooth * Time.deltaTime));

            // 输入缓冲
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

        private void HandleComboInput()
        {
            if (!_mode.TrySpendAttackCost()) return;

            float now = Time.unscaledTime;
            bool withinCombo = (now - _lastComboTime) <= comboWindow;
            if (!withinCombo) _comboStep = 0;
            _comboStep = Mathf.Clamp(_comboStep + 1, 1, 4);
            _lastComboTime = now;

            if (companion) companion.AttachTo(bladeOrigin ? bladeOrigin : transform);

            switch (_comboStep)
            {
                case 1: StartCoroutine(CoSingleSlash()); break;
                case 2: StartCoroutine(CoShieldRing()); break;
                case 3: StartCoroutine(CoEggBomb()); break;
                case 4: StartCoroutine(CoUltraGiant()); break;
            }
        }

        // ================= ① 单发・剑气 =================
        private IEnumerator CoSingleSlash()
        {
            _busy = true;
            Vector2 dir = SoftLockDir(_smoothedAim, softLockAngle, softLockRadius);

            EnableBeams(true);
            int stick = Mathf.Max(1, Mathf.RoundToInt(s1_preflashFrames));
            for (int i = 0; i < stick; i++)
            {
                float u = (i + 1f) / (float)stick;
                float eased = swingEase.Evaluate(u);
                var tip = DrawBlade(dir, s1_range * 0.22f, eased);
                if (companion) companion.FollowTip(tip);
                yield return null;
            }
            EnableBeams(false);

            yield return StartCoroutine(FlyWaveCrescent(
                BladeOriginPos(), dir, s1_speed, s1_range, s1_hitRadius, s1_damage, s1_pierceCount,
                crescentArcDeg, crescentOuter, crescentThickness,
                onFirstHit: () => TryShake(0.08f, 0.12f, 22f)
            ));

            yield return WaitRecover(s1_recover);
            EndStep(dir);
        }

        // ================= ② 环形护刃（挡弹：护盾独立持续，不拉长招式） =================
        private IEnumerator CoShieldRing()
        {
            _busy = true;
            Vector2 baseDir = SoftLockDir(_smoothedAim, softLockAngle, softLockRadius);

            // --- 短预览（旋转线） ---
            EnableBeams(true);
            float t = 0f;
            while (t < s2_spinTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / s2_spinTime);
                float ang = Mathf.Lerp(0f, 360f, swingEase.Evaluate(u));
                Vector2 dir = Quaternion.Euler(0, 0, ang) * baseDir;
                var tip = DrawBlade(dir, s2_shieldOuter * 0.6f, u);
                if (companion) companion.FollowTip(tip);
                yield return null;
            }
            EnableBeams(false);

            // --- 生成护刃并交给独立协程管理（寿命= s2_shieldDuration） ---
            // 如果上一个护盾还在，先清理
            if (_s2Runner != null) { StopCoroutine(_s2Runner); _s2Runner = null; }
            if (_s2ActiveRing) { Destroy(_s2ActiveRing); _s2ActiveRing = null; }

            _s2ActiveRing = CreateRing("GreenCrescent_Shield_Persistent");
            var mf = _s2ActiveRing.GetComponent<MeshFilter>();
            var mesh = new Mesh();
            BuildRingMesh(mesh, s2_shieldOuter, s2_shieldThickness, 72);
            mf.sharedMesh = mesh;

            _s2Runner = StartCoroutine(CoRunShieldRing(_s2ActiveRing, s2_shieldDuration));

            // --- ②段只占用“最短保持时间 + 可选提前取消”的窗口，然后立即进入恢复 ---
            float gate = Mathf.Max(0f, s2_minActiveBeforeCancel);
            float elapse = 0f;
            while (elapse < gate)
            {
                elapse += Time.unscaledDeltaTime;
                // 这里不响应提前取消，确保最短读秒
                yield return null;
            }

            // 最短保持时间达成后：若允许提前取消，检测到输入就收招；否则直接收招
            if (s2_allowEarlyCancel)
            {
                // 若已经缓冲输入或此帧按下，立即收招；否则不再强制等待护盾结束
                if (!_bufferedInput)
                {
                    // 给一小段窗口，看玩家是否要立刻接③
                    float smallWindow = 0.12f;
                    float acc = 0f;
                    while (acc < smallWindow && !_bufferedInput && !Input.GetKeyDown(inputKey))
                    {
                        acc += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }

            // 收招（护盾继续存在，由 _s2Runner 按 s2_shieldDuration 自行淡出与销毁）
            yield return WaitRecover(s2_recover);
            EndStep(baseDir);
        }
        // 护刃运行：自管理寿命与逻辑（持续旋转、挡/反弹、到时淡出销毁）
        private IEnumerator CoRunShieldRing(GameObject ring, float duration)
        {
            if (!ring) yield break;

            float time = 0f;
            float innerR = Mathf.Max(0.01f, s2_shieldOuter - s2_shieldThickness);

            while (time < duration && ring)
            {
                time += Time.unscaledDeltaTime;

                // 跟随手柄（刀柄）位置
                Vector3 center = BladeOriginPos();
                ring.transform.position = center;
                ring.transform.rotation *= Quaternion.Euler(0, 0, s2_shieldSpinDegPerSec * Time.unscaledDeltaTime);

                // 轻微呼吸
                float pulse = 1f + Mathf.Sin(time * 6.0f) * s2_pulseAmp * 0.05f;
                ring.transform.localScale = new Vector3(pulse, pulse, 1f);

                // 挡/反弹子弹
                int n = Physics2D.OverlapCircleNonAlloc(center, s2_shieldOuter + 0.2f, _buf64, bulletMask);
                for (int i = 0; i < n; i++)
                {
                    var c = _buf64[i];
                    if (!c) continue;
                    Vector2 cp = c.bounds.center;
                    float dist = Vector2.Distance(cp, (Vector2)center);
                    if (dist < innerR - 0.1f || dist > s2_shieldOuter + 0.2f) continue;

                    var rb = c.attachedRigidbody ? c.attachedRigidbody : c.GetComponentInParent<Rigidbody2D>();
                    if (rb && s2_reflectProjectiles)
                    {
#if UNITY_6000_0_OR_NEWER
                        Vector2 v = rb.linearVelocity;
#else
                Vector2 v = rb.velocity;
#endif
                        Vector2 normal = (rb.worldCenterOfMass - (Vector2)center).normalized;
                        Vector2 rv = Vector2.Reflect(v, normal) * s2_reflectSpeedMul;
#if UNITY_6000_0_OR_NEWER
                        rb.linearVelocity = rv;
#else
                rb.velocity = rv;
#endif
                    }
                    else
                    {
                        Destroy(c.gameObject);
                    }
                    TryShake(0.05f, 0.10f, 26f);
                }

                yield return null;
            }

            // 结束：淡出并销毁
            if (ring)
            {
                yield return FadeAndKillMesh(ring, 0.25f);
                Destroy(ring);
            }
            if (_s2ActiveRing == ring) _s2ActiveRing = null;
            _s2Runner = null;
        }


        // ================= ③ 剑光蛋（碰撞即爆 / 范围伤害） =================
        // ================= ③ 剑光蛋（碰撞即爆 / 范围伤害） =================
        private IEnumerator CoEggBomb()
        {
            _busy = true;

            // 方向（含软锁）
            Vector2 aim = SoftLockDir(_smoothedAim, softLockAngle, softLockRadius);

            // —— 简短预览（用三根 LineRenderer 画一道“预备线”）——
            EnableBeams(true);
            int stick = Mathf.Max(1, Mathf.RoundToInt(s3_preflashFrames));
            for (int i = 0; i < stick; i++)
            {
                float u = (i + 1f) / (float)stick;
                float eased = swingEase.Evaluate(u);
                var tip = DrawBlade(aim, s3_eggMaxRange > 0 ? Mathf.Min(s3_eggMaxRange, 4.5f) : 4.5f, eased);
                if (companion) companion.FollowTip(tip);
                yield return null;
            }
            EnableBeams(false);

            // —— 生成“蛋”投射物（挂在一个 GO 上的内嵌组件）——
            var eggGO = new GameObject("SwordEggProjectile");
            eggGO.transform.position = BladeOriginPos();
            var egg = eggGO.AddComponent<SwordEggProjectile>();

            // 参数从 s3_* 写入 egg（用你脚本里已有的公共字段）
            egg.enemyMask = enemyMask;
            egg.obstacleMask = obstacleMask;
            egg.speed = s3_eggSpeed;
            egg.maxRange = Mathf.Max(0f, s3_eggMaxRange);
            egg.lifeSeconds = Mathf.Max(0f, s3_eggLifeSeconds);
            egg.hitRadius = s3_eggHitRadius;
            egg.directHitDamage = s3_eggDirectHitDamage;
            egg.explosionRadius = s3_explosionRadius;
            egg.explosionDamage = s3_explosionDamage;
            egg.explosionFalloffPow = s3_explosionFalloffPow;
            egg.pushSmall = s3_explosionPushSmall;
            egg.pushLarge = s3_explosionPushLarge;
            egg.largeMassThreshold = s3_largeMassThreshold;

            // 颜色/材质使用你在本脚本配置的“半月材质”
            egg.coreColor = coreColor;
            egg.useMaterial = crescentMaterial;

            // ✨ 传入第三段专用爆炸特效
            egg.explosionVFX = vfxExplosion;
            egg.explosionVFXLife = vfxExplosionLife;

            // 发射（内嵌类的 Launch 会负责移动与碰撞 → Explode）
            egg.Launch(aim, BladeOriginPos(), exploded: () => { TryShake(0.10f, 0.25f, 20f); });

            // 收招
            yield return WaitRecover(s3_recover);
            EndStep(aim);
        }




        // ================= ④ 超级大斩 =================
        private IEnumerator CoUltraGiant()
        {
            _busy = true;
            Vector2 baseDir = SoftLockDir(_smoothedAim, softLockAngle, softLockRadius);

            // 短预备
            EnableBeams(true);
            int stick = Mathf.Max(1, Mathf.RoundToInt(s4_preflashFrames));
            for (int i = 0; i < stick; i++)
            {
                float u = (i + 1f) / (float)stick;
                float eased = swingEase.Evaluate(u);
                Vector2 dir = Quaternion.Euler(0, 0, Mathf.Lerp(-10f, 10f, eased)) * baseDir;
                var tip = DrawBlade(dir, s4_length * 0.30f, eased, s4_length * 0.30f);
                if (companion) companion.FollowTip(tip);
                yield return null;
            }
            EnableBeams(false);

            bool shook = false;
            yield return StartCoroutine(FlyGiantCrescent(
                BladeOriginPos(), baseDir, s4_speed, s4_length,
                s4_arcDeg, s4_outer, s4_thickness,
                s4_hitRadius, s4_damage, s4_multiHitsPerTarget, s4_tickInterval,
                afterImgInterval: 0.040f, afterImgFade: 0.28f, fadeTail: s4_fadeTail,
                onRelease: () => { if (!shook) { TryShake(0.14f, 0.35f, 16f); shook = true; } }
            ));

            yield return WaitRecover(s4_recover);
            EndStep(baseDir);
        }

        // --------- 半月形飞行（普通） ---------
        private IEnumerator FlyWaveCrescent(
           Vector3 start, Vector2 dir, float speed, float range, float hitRadius,
           float damage, int pierce, float arcDeg, float outerR, float thickness, System.Action onFirstHit)
        {
            GameObject cres = CreateCrescent("GreenCrescent_S1S3");
            var mf = cres.GetComponent<MeshFilter>();
            var mesh = new Mesh();
            BuildCrescentMesh(mesh, arcDeg, outerR, thickness, 28);
            mf.sharedMesh = mesh;

            Vector3 pos = start + (Vector3)dir.normalized * 0.6f;
            float travelled = 0f;
            int pierced = 0;
            var hitSet = new HashSet<FadedDreams.Enemies.IDamageable>();
            bool firstHitDone = false;

            while (travelled < range)
            {
                float step = speed * Time.unscaledDeltaTime;
                travelled += step;
                pos += (Vector3)dir * step;

                int n = Physics2D.OverlapCircleNonAlloc(pos, hitRadius, _buf64, enemyMask);
                for (int i = 0; i < n; i++)
                {
                    var c = _buf64[i];
                    var d = c ? c.GetComponentInParent<FadedDreams.Enemies.IDamageable>() : null;
                    if (d != null && !d.IsDead && !hitSet.Contains(d))
                    {
                        hitSet.Add(d);
                        d.TakeDamage(damage);

                        // 播放小命中特效（命中点朝向按飞行方向）
                        if (vfxHitSmall)
                        {
                            Vector3 hpos = c.bounds.ClosestPoint(pos);
                            var go = Instantiate(vfxHitSmall, hpos,
                                Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
                            if (vfxHitSmallLife > 0f) Destroy(go, vfxHitSmallLife);
                        }

                        if (!firstHitDone) { onFirstHit?.Invoke(); firstHitDone = true; }
                        pierced++;
                        if (pierced > pierce) { travelled = range + 1f; break; }
                    }
                }

                cres.transform.position = pos;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                cres.transform.rotation = Quaternion.Euler(0, 0, ang);

                if (Physics2D.OverlapCircle(pos, hitRadius * 0.8f, obstacleMask)) break;
                yield return null;
            }

            yield return FadeAndKillMesh(cres, 0.16f);
        }

        // --------- 半月形飞行（巨型 + 残像 + 多段伤害） ---------
        private IEnumerator FlyGiantCrescent(
           Vector3 start, Vector2 dir, float speed, float length,
           float arcDeg, float outerR, float thickness,
           float hitRadius, float damage, int multiHitsPerTarget, float tickInterval,
           float afterImgInterval, float afterImgFade, float fadeTail,
           System.Action onRelease)
        {
            onRelease?.Invoke();

            GameObject head = CreateCrescent("GreenCrescent_S4_Head");
            var mf = head.GetComponent<MeshFilter>();
            var mesh = new Mesh();
            BuildCrescentMesh(mesh, arcDeg, outerR, thickness, 36);
            mf.sharedMesh = mesh;

            Vector3 pos = start + (Vector3)dir.normalized * 0.6f;
            float travelled = 0f;
            float tickAcc = 0f, imgAcc = 0f;
            var hitTicks = new Dictionary<FadedDreams.Enemies.IDamageable, int>();

            while (travelled < length)
            {
                float step = speed * Time.unscaledDeltaTime;
                travelled += step;
                pos += (Vector3)dir * step;

                head.transform.position = pos;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                head.transform.rotation = Quaternion.Euler(0, 0, ang);

                // 残像
                imgAcc += Time.unscaledDeltaTime;
                if (imgAcc >= afterImgInterval)
                {
                    imgAcc = 0f;
                    var ghost = new GameObject("CrescentGhost");
                    var gmf = ghost.AddComponent<MeshFilter>(); gmf.sharedMesh = mesh;
                    var gmr = ghost.AddComponent<MeshRenderer>(); gmr.sharedMaterial = head.GetComponent<MeshRenderer>().sharedMaterial;
                    ghost.transform.SetPositionAndRotation(pos, head.transform.rotation);
                    StartCoroutine(FadeAndKillMesh(ghost, afterImgFade));
                }

                // 周期性伤害 tick
                tickAcc += Time.unscaledDeltaTime;
                if (tickAcc >= tickInterval)
                {
                    tickAcc = 0f;
                    int samples = 6;
                    for (int i = 0; i < samples; i++)
                    {
                        Vector3 p = pos + (Vector3)dir.normalized * (i - samples / 2f) * 0.12f;
                        int n = Physics2D.OverlapCircleNonAlloc(p, hitRadius, _buf64, enemyMask);
                        for (int j = 0; j < n; j++)
                        {
                            var dcol = _buf64[j];
                            var d = dcol ? dcol.GetComponentInParent<FadedDreams.Enemies.IDamageable>() : null;
                            if (d != null && !d.IsDead)
                            {
                                int count = 0; hitTicks.TryGetValue(d, out count);
                                if (count < multiHitsPerTarget)
                                {
                                    d.TakeDamage(damage / Mathf.Max(1, multiHitsPerTarget));
                                    hitTicks[d] = count + 1;

                                    // 播放小命中特效（按当前段落命中点）
                                    if (vfxHitSmall)
                                    {
                                        Vector3 hpos = dcol.bounds.ClosestPoint(p);
                                        var go = Instantiate(vfxHitSmall, hpos,
                                            Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
                                        if (vfxHitSmallLife > 0f) Destroy(go, vfxHitSmallLife);
                                    }
                                }
                            }
                        }
                    }
                }

                if (Physics2D.OverlapCircle(pos, hitRadius * 0.8f, obstacleMask)) break;
                yield return null;
            }

            yield return FadeAndKillMesh(head, fadeTail);
        }


        // ============== Helpers ==============
        private void EndStep(Vector2 lastDir)
        {
            _busy = false;
            _lastComboTime = Time.unscaledTime;
            if (_comboStep >= 4) _comboStep = 0;
            if (companion)
            {
                companion.ReturnToOrbitDelayed(comboWindow);
                companion.TransitionFlourish(lastDir);
            }
        }

        private Vector3 BladeOriginPos() => bladeOrigin ? bladeOrigin.position : transform.position;

        private Vector2 AimDir()
        {
            if (_cam == null) _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            Vector3 mp = Input.mousePosition;
            float depth = Mathf.Abs((_cam ? _cam.transform.position.z : 0f) - BladeOriginPos().z);
            mp.z = depth <= 0.001f ? 10f : depth;
            Vector3 world = _cam ? _cam.ScreenToWorldPoint(mp) : (Vector3)transform.right + BladeOriginPos();
            world.z = BladeOriginPos().z;
            return (world - BladeOriginPos()).normalized;
        }

        private Vector2 SoftLockDir(Vector2 aimDir, float maxAngle, float radius)
        {
            Collider2D best = null;
            float bestSqr = float.MaxValue;
            int n = Physics2D.OverlapCircleNonAlloc(BladeOriginPos(), radius, _buf64, enemyMask);
            for (int i = 0; i < n; i++)
            {
                var c = _buf64[i];
                Vector2 d = (Vector2)c.bounds.center - (Vector2)BladeOriginPos();
                float ang = Vector2.Angle(aimDir, d);
                if (ang <= maxAngle)
                {
                    float s = d.sqrMagnitude;
                    if (s < bestSqr) { bestSqr = s; best = c; }
                }
            }
            if (!best) return aimDir;
            Vector2 to = ((Vector2)best.bounds.center - (Vector2)BladeOriginPos()).normalized;
            float delta = Vector2.SignedAngle(aimDir, to);
            float clamp = Mathf.Clamp(delta, -softLockMaxCorrection, softLockMaxCorrection);
            float rad = clamp * Mathf.Deg2Rad;
            float ca = Mathf.Cos(rad), sa = Mathf.Sin(rad);
            return new Vector2(ca * aimDir.x - sa * aimDir.y, sa * aimDir.x + ca * aimDir.y).normalized;
        }

        private void EnableBeams(bool on)
        {
            _lrCore.enabled = on;
            _lrGlow1.enabled = on;
            _lrGlow2.enabled = on;
        }

        private Vector3 DrawBlade(Vector2 dir, float len, float phase01, float lineLengthOverride = -1f)
        {
            Vector3 p0 = BladeOriginPos();
            Vector3 p1 = p0 + (Vector3)(dir.normalized * (lineLengthOverride > 0f ? lineLengthOverride : len));

            _lrCore.positionCount = 2; _lrCore.SetPosition(0, p0); _lrCore.SetPosition(1, p1);
            float w = widthByPhase.Evaluate(phase01);
            _lrCore.startWidth = w; _lrCore.endWidth = w * 0.92f;
            _lrCore.colorGradient = MakeGradient(coreColor, lrAlpha);

            _lrGlow1.positionCount = 2; _lrGlow1.SetPosition(0, p0); _lrGlow1.SetPosition(1, p1);
            _lrGlow1.startWidth = w * 1.6f; _lrGlow1.endWidth = w * 1.5f;
            _lrGlow1.colorGradient = MakeGradient(Color.Lerp(coreColor, glowColor, 0.6f), Mathf.Lerp(0.55f, 0.85f, phase01));

            _lrGlow2.positionCount = 2; _lrGlow2.SetPosition(0, p0); _lrGlow2.SetPosition(1, p1);
            _lrGlow2.startWidth = w * 2.3f; _lrGlow2.endWidth = w * 2.2f;
            _lrGlow2.colorGradient = MakeGradient(Color.Lerp(coreColor, glowColor, 0.8f), Mathf.Lerp(0.35f, 0.65f, phase01));
            return p1;
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
            lr.material = _lrCore ? _lrCore.sharedMaterial : null;
            lr.enabled = false;
            return lr;
        }

        private IEnumerator WaitRecover(float sec)
        {
            float t = 0f;
            while (t < sec) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private GameObject CreateCrescent(string name)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            if (crescentMaterial) mr.sharedMaterial = crescentMaterial;
            else
            {
                var sh = Shader.Find("Sprites/Default");
                var mat = new Material(sh);
                mat.color = coreColor;
                mr.sharedMaterial = mat;
            }
            return go;
        }

        private GameObject CreateRing(string name)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            if (crescentMaterial) mr.sharedMaterial = crescentMaterial;
            else
            {
                var sh = Shader.Find("Sprites/Default");
                var mat = new Material(sh);
                mat.color = coreColor;
                mr.sharedMaterial = mat;
            }
            return go;
        }

        public static void BuildCrescentMesh(Mesh mesh, float arcDeg, float outerR, float thickness, int seg = 20)
        {
            if (mesh == null) mesh = new Mesh();
            mesh.Clear();
            float innerR = Mathf.Max(0.01f, outerR - thickness);
            int vcount = (seg + 1) * 2;
            var verts = new Vector3[vcount];
            var tris = new int[seg * 6];

            float start = -arcDeg * 0.5f * Mathf.Deg2Rad;
            float step = arcDeg * Mathf.Deg2Rad / seg;

            for (int i = 0; i <= seg; i++)
            {
                float a = start + i * step;
                float ca = Mathf.Cos(a); float sa = Mathf.Sin(a);
                verts[i * 2] = new Vector3(ca * innerR, sa * innerR, 0);
                verts[i * 2 + 1] = new Vector3(ca * outerR, sa * outerR, 0);
                if (i < seg)
                {
                    int t = i * 6;
                    int vi = i * 2;
                    tris[t + 0] = vi;
                    tris[t + 1] = vi + 1;
                    tris[t + 2] = vi + 3;
                    tris[t + 3] = vi;
                    tris[t + 4] = vi + 3;
                    tris[t + 5] = vi + 2;
                }
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        public static void BuildRingMesh(Mesh mesh, float outerR, float thickness, int seg = 72)
        {
            if (mesh == null) mesh = new Mesh();
            mesh.Clear();
            float innerR = Mathf.Max(0.01f, outerR - thickness);
            int vcount = (seg + 1) * 2;
            var verts = new Vector3[vcount];
            var tris = new int[seg * 6];

            float start = -Mathf.PI;
            float step = 2f * Mathf.PI / seg;

            for (int i = 0; i <= seg; i++)
            {
                float a = start + i * step;
                float ca = Mathf.Cos(a); float sa = Mathf.Sin(a);
                verts[i * 2] = new Vector3(ca * innerR, sa * innerR, 0);
                verts[i * 2 + 1] = new Vector3(ca * outerR, sa * outerR, 0);
                if (i < seg)
                {
                    int t = i * 6;
                    int vi = i * 2;
                    tris[t + 0] = vi;
                    tris[t + 1] = vi + 1;
                    tris[t + 2] = vi + 3;
                    tris[t + 3] = vi;
                    tris[t + 4] = vi + 3;
                    tris[t + 5] = vi + 2;
                }
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private IEnumerator FadeAndKillMesh(GameObject go, float fade)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (!mr) { Destroy(go); yield break; }
            Color c0 = mr.sharedMaterial ? mr.sharedMaterial.color : Color.white;
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / fade);
                var c = c0; c.a = Mathf.Lerp(c0.a, 0f, u);
                mr.sharedMaterial.color = c;
                yield return null;
            }
            Destroy(go);
        }

        // ========== Nested: Egg projectile ==========
        private class SwordEggProjectile : MonoBehaviour
        {
            public LayerMask enemyMask, obstacleMask;
            public float speed = 18f;
            public float maxRange = 11f;
            public float lifeSeconds = 0.8f;
            public float hitRadius = 0.45f;
            public float directHitDamage = 18f;
            public float explosionRadius = 3.2f;
            public float explosionDamage = 50f;
            public float explosionFalloffPow = 1.6f;
            public float pushSmall = 2.5f;
            public float pushLarge = 1.0f;
            public float largeMassThreshold = 3.5f;
            public System.Action onExploded;
            public Color coreColor = Color.white;
            public Material useMaterial;
            public GameObject explosionVFX;
            public float explosionVFXLife = 1.2f;

            private Vector3 _start;
            private Vector2 _dir;
            private float _life;
            private bool _done;
            private static readonly Collider2D[] _buf = new Collider2D[64];
            private GameObject _vis;

            public void Launch(Vector2 dir, Vector3 origin, System.Action exploded)
            {
                _dir = dir.normalized;
                _start = origin + (Vector3)_dir * 0.6f;
                transform.position = _start;
                _life = lifeSeconds;
                onExploded = exploded;

                // simple visual（用半月网格近似蛋形）
                _vis = new GameObject("EggVisual");
                _vis.transform.SetParent(transform, false);
                var mf = _vis.AddComponent<MeshFilter>();
                var mr = _vis.AddComponent<MeshRenderer>();
                var mesh = new Mesh();
                BuildCrescentMesh(mesh, 320f, 0.7f, 0.35f, 22);
                mf.sharedMesh = mesh;
                if (useMaterial) mr.sharedMaterial = useMaterial;
                else { var mat = new Material(Shader.Find("Sprites/Default")); mat.color = coreColor; mr.sharedMaterial = mat; }
                _vis.transform.localScale = new Vector3(1.2f, 0.8f, 1f);
            }

            private void Update()
            {
                if (_done) return;
                float dt = Time.unscaledDeltaTime;

                Vector3 prev = transform.position;
                Vector3 next = prev + (Vector3)_dir * speed * dt;
                transform.position = next;
                transform.right = _dir;

                // 寿命/射程
                if (lifeSeconds > 0f) { _life -= dt; if (_life <= 0f) { Explode(prev); return; } }
                if (maxRange > 0f && (transform.position - _start).magnitude >= maxRange) { Explode(prev); return; }

                // 逐段探测，避免穿透
                Vector3 move = next - prev;
                float segLen = move.magnitude + 0.0001f;
                float step = Mathf.Max(hitRadius * 0.8f, 0.12f);
                for (float d = 0f; d <= segLen; d += step)
                {
                    Vector3 probe = prev + move.normalized * d;
                    int n = Physics2D.OverlapCircleNonAlloc((Vector2)probe, hitRadius, _buf, enemyMask | obstacleMask);
                    for (int i = 0; i < n; i++)
                    {
                        var c = _buf[i];
                        if (!c) continue;
                        // 直击伤害（若是敌人）
                        if (((1 << c.gameObject.layer) & enemyMask) != 0)
                        {
                            var dmg = c.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                            if (dmg != null && !dmg.IsDead) dmg.TakeDamage(directHitDamage);
                        }
                        Explode(probe); return;
                    }
                }
            }

            // 这是 PlayerRangedCharger 内部的嵌套类：SwordEggProjectile
            // 爆炸：按半径衰减伤害 + 推力 + 播放爆炸特效
            private void Explode(Vector3 center)
            {
                if (_done) return;
                _done = true;

                int n = Physics2D.OverlapCircleNonAlloc((Vector2)center, explosionRadius, _buf, enemyMask);
                for (int i = 0; i < n; i++)
                {
                    var col = _buf[i];
                    var dmg = col ? col.GetComponentInParent<FadedDreams.Enemies.IDamageable>() : null;
                    if (dmg != null && !dmg.IsDead)
                    {
                        float dist = Vector2.Distance(center, col.bounds.ClosestPoint(center));
                        float t = Mathf.Clamp01(dist / Mathf.Max(0.001f, explosionRadius));
                        float fall = Mathf.Pow(1f - t, Mathf.Max(0.1f, explosionFalloffPow));
                        float deal = explosionDamage * fall;
                        dmg.TakeDamage(deal);

                        var rb = col.attachedRigidbody ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody2D>();
                        if (rb)
                        {
                            bool large = rb.mass >= largeMassThreshold;
                            Vector2 push = ((Vector2)rb.worldCenterOfMass - (Vector2)center).normalized * (large ? pushLarge : pushSmall);
                            rb.AddForce(push, ForceMode2D.Impulse);
                        }
                    }
                }

                // —— 爆炸特效（第三段专用）——
                if (explosionVFX)
                {
                    var go = Instantiate(explosionVFX, center, Quaternion.identity);
                    if (explosionVFXLife > 0f) Destroy(go, explosionVFXLife);
                }

                // 可视淡出
                if (_vis)
                {
                    var mr = _vis.GetComponent<MeshRenderer>();
                    if (mr) StartCoroutine(CoFadeAndDie(mr, 0.25f));
                }
                else Destroy(gameObject);

                onExploded?.Invoke();
            }



            private IEnumerator CoFadeAndDie(MeshRenderer mr, float fade)
            {
                Color c0 = mr.sharedMaterial ? mr.sharedMaterial.color : Color.white;
                float t = 0f;
                while (t < fade)
                {
                    t += Time.unscaledDeltaTime;
                    var c = c0; c.a = Mathf.Lerp(c0.a, 0f, t / fade);
                    mr.sharedMaterial.color = c;
                    yield return null;
                }
                Destroy(gameObject);
            }
        }
    }
}
