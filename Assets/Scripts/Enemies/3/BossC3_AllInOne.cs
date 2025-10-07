// BossC3_AllInOne.cs11111111111111111111111111111
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）
// 特色：星环血条 / Orb 自治小技 / 红绿相性 / 基础小招 / 大招编排 / 传送防穿地形 / 2D/3D通吃
// 本版改动要点：
// - 移除全部“摄像头控制”逻辑（删除 CameraShakeManager 类与所有 TryShake 调用）【保留说明】
// - 设定 Boss 总血量 2200，HP ≤ 1100 时从 P1 切到 P2
// - 阶段环绕体数量：P1=4，P2=6（切阶段时即时应用）
// - ★ 新增：Aggro Camera（进圈放大视角；退出自动还原），单脚本内实现
// - ★ 新增：AI Movement Planning（计划—执行式移动）；原 UpdateMovement 逻辑保留为 UpdateMovement_Legacy()

// 关键：引入你项目的 IDamageable 接口所在命名空间（见 EnemyHealth.cs）
using FadedDreams.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Android;

namespace FD.Bosses.C3
{
    public enum Phase { P1, P2 }
    public enum BossColor { Red, Green }
    public enum Stage { TELL, WINDUP, ACTIVE, RECOVER }

    [DisallowMultipleComponent]
    public class BossC3_AllInOne : MonoBehaviour
    {
        // === 玩家颜色接口（嵌套，供 PlayerColorBridge.cs 使用） ===
        public interface IColorState
        {
            BossColor GetColorMode();
        }

        [Header("== Core Refs ==")]
        public Transform orbAnchor;
        public Transform player;
        public Renderer colorRenderer;

        [Header("== Phase / Color ==")]
        public Phase phase = Phase.P1;
        public BossColor color = BossColor.Red;
        [Tooltip("P1=4, P2=6")]
        public int p1Orbs = 4;
        public int p2Orbs = 6;
        public float baseRadius = 2.8f;

        [Header("== Orbs ==")]
        public List<GameObject> orbPrefabs = new List<GameObject>();
        public bool autoBindOrbs = true;

        [Header("== Color & Emission ==")]
        public bool tintUseEmission = true;
        public float emissionIntensity = 2.2f;

        [Header("== Movement & Safety ==")]
        public bool use2DPhysics = true;
        public float moveSpeed = 5.0f;
        public float preferRange = 6.0f;
        public float stopDistance = 3.0f;
        public float farTeleportDistance = 24f;
        public float teleportNearPlayerRadius = 3.5f;
        public LayerMask groundMask = -1;
        public float safeProbeRadius = 0.35f;

        [Header("== Player & Combat ==")]
        public LayerMask playerMask = 1 << 8;
        public string playerTag = "Player";
        public float defaultDamage = 6f;
        public bool playerColorImmunity = true; // 异色无效

        [Header("== Boss Health & Ring ==")]
        public float maxHP = 2200f;           // <<< 设定总血量
        public float currentHP = 2200f;       // <<< 初始当前血量
        public bool autoCreateRing = true;
        public float ringRadius = 2.9f;
        public float ringWidth = 0.18f;
        public Gradient ringGradientP1;
        public Gradient ringGradientP2;


        [Header("Red Micro Tuning (自由飞与回收节奏)")]
        [SerializeField] private float redMinFlyTime = 1.20f;   // 红色最短前冲时长（秒）
        [SerializeField] private float redHangTime = 0.35f;   // 前冲结束后的悬停时长（秒）
        [SerializeField] private float redReturnMul = 1.15f;   // 回收时间系数（>1 更慢、更有余韵）

        [Header("Stellar Ring Visual")]
        [SerializeField] private Material ringMaterial; // ← 在 Inspector 手动拖材质
        [Header("Stellar Ring Visual")]
        [SerializeField] private Material bossRingMaterial;


        [Header("== Debug ==")]
        public bool autoRun = true;
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        [Header("== Big Skill Scheduler ==")]
        public float bigSkillCooldownP1 = 22f;
        public float bigSkillCooldownP2 = 16f;

        private bool _suppressMicros = false;
        private float _bigReadyAt = 0f;
        private int _nextBigIndex = 0;

        private PrefabOrbConductor _conductor;
        private Rigidbody _rb3;
        private Rigidbody2D _rb2;

        [Header("== Micro Skill Concurrency ==")]
        public int p1MaxConcurrentMicros = 2;
        public int p2MaxConcurrentMicros = 3;
        private int _concurrentMicros = 0;

        [Header("== Boss Base Skills ==")]
        public float pulseCooldown = 7f;     // 指挥脉冲
        public float markCooldown = 9f;      // 点名延迟爆
        public float pulseRadius = 5.5f;
        public float pulseKnockSpeed = 3.2f;
        public float markDelay = 1.1f;
        public float markBlastRadius = 2.2f;
        public float markDamage = 12f;
        private float _nextPulse = 0f, _nextMark = 0f;


        // ===== Red Remote Fire (bullet settings) =====
        [Header("== Red Remote Fire ==")]
        public GameObject bulletPrefab;         // 可选：自定义子弹预制体（带 Rigidbody2D/3D 均可）
        public Material bulletMaterial;         // 可选：直接换材质
        public float bulletSpeed = 14f;
        public float bulletLifetime = 3.0f;
        public float bulletDamageMul = 1.0f;    // 伤害倍率（相对 _owner.defaultDamage）

        // 着陆点/防地形
        public float landingRadiusAroundPlayer = 10f;  // 在玩家 10f 范围找落点
        public float minPlayerClearance = 1.0f;        // 落点与玩家至少保持的净空

        [Header("== Terrain Clearance / Orbs ==")]
        [Tooltip("每个仍在环上的环绕体，半径内不希望出现地形的清场半径")]
        public float orbClearanceRadius = 3.5f;
        [Tooltip("清场推力的整体权重（越大越积极躲避）")]
        public float orbClearanceGain = 0.5f;
        [Tooltip("清场推力的上限（米/秒），避免过猛")]
        public float orbClearanceMax = 3f;
        [Tooltip("只对“仍在环上的环绕体”生效；乱飞/脱离时不计入")]
        public bool clearanceOnlyForAttached = true;

        // === Aggro / Battle State ===
        [Header("== Aggro / Battle ==")]
        [SerializeField] private bool battleStarted = false;         // 一旦为 true 就不回退
        [SerializeField] private float aggroRadius = 0f;             // 0=使用 detectRadius
        [SerializeField] private bool lockBattleOnceStarted = true;  // 开战后不退出
        private Coroutine _mainLoopCR;


        [Header("== Smart Obstacle/Teleport ==")]
        [Tooltip("使用全新智能移动与避障（建议开启）")]
        public bool useSmartMovement = true;

        [Tooltip("前向试探长度")]
        public float smartProbeAhead = 1.2f;
        [Tooltip("侧向试探长度")]
        public float smartSideProbe = 1.0f;
        [Tooltip("左右各试探档位数（越大越稳，但更耗）")]
        [Range(1, 4)] public int smartSideSamples = 3;

        [Tooltip("远距保持压迫：希望距离下限/上限")]
        public float smartDesiredMin = 2.8f;
        public float smartDesiredMax = 4.2f;
        public float smartApproachSpeed = 6.0f;
        public float smartStrafeSpeed = 2.8f;
        public float smartMaxAccel = 18f;

        [Tooltip("视线检测是否要求无遮挡（多层地形建议开）")]
        public bool requireLineOfSightForAggro = true;

        [Tooltip("传送：只在满足条件时触发（远+无视线+卡住+冷却好）")]
        public bool smartTeleportEnabled = true;
        [Tooltip("认为“卡住”的最小速度（m/s）")]
        public float stuckSpeedThreshold = 0.2f;
        [Tooltip("连续多久判定为卡住（秒）")]
        public float stuckTimeToTeleport = 1.25f;
        [Tooltip("触发一次传送后的冷却（秒）")]
        public float teleportCooldown = 4.0f;
        [Tooltip("仅在距玩家超过此距离才考虑传送")]
        public float teleportMinDistance = 18f;



        // === [Drops & VFX / Hit Recovery Settings] ===
        [Header("Orb Hit (Same-Color) Reaction")]
        [SerializeField] private GameObject orbHitExplosionVfx;   // 命中时的爆炸特效（可选，允许为空）
        [SerializeField] private float orbGhostAlpha = 0.35f;     // 半透明显示的 alpha
        [SerializeField] private float orbRecallSpeed = 12f;      // 回到Boss身边的速度（世界单位/秒）
        [SerializeField] private float orbStunDuration = 2f;      // 等待时长（禁用AI）

        [Header("Energy Pickup Prefabs (Opposite Color)")]
        [SerializeField] private GameObject energyPickupPrefab;   // 用你的泛用 EnergyPickup 预制体（脚本会改 energyColor）














        private float AggroRadius => (aggroRadius > 0f ? aggroRadius : detectRadius);

        private bool IsPlayerInAggro()
        {
            if (!player) return false;
            return Vector3.Distance(player.position, transform.position) <= AggroRadius;
        }

        private void EnsureBattleStart()
        {
            if (battleStarted) return;
            if (IsPlayerInAggro())
            {
                battleStarted = true;
                // 开战：启动主循环（若未启动）
                if (_mainLoopCR == null) _mainLoopCR = StartCoroutine(MainLoop());
                // 同时应用战斗镜头（避免等待下一帧）
                AggroCamera_Apply();
                if (verboseLogs) Debug.Log("[Boss] Battle started (entered aggro).");
            }
        }












        public enum MicroIdP1 { RedPierce, ShatterArc, VoltNeedle, ReverseSaber, HarmonicHit }
        public enum MicroIdP2 { TwinSpiral, MirrorRay, HunterLoop, FoldNova, GravBind, ChainDash, AerialDrop, LineSweep, RefractPulse, ChromaEcho }
        public enum BigIdP1 { RingBurst, QuadrantMerge }
        public enum BigIdP2 { PrismSymphony, FallingOrbit, ChromaReverse, FinalGeometry }

        private StellarRing _ring;

        // =======（旧横移节拍用的 static）=======
        static bool __strafeInitialized = false;
        static float __nextStrafeFlipAt = 0f;
        static int __strafeSign = +1;

        // 阶段切换阈值（满足你的明确要求：掉到 1100 进入 P2）
        const float PHASE2_THRESHOLD = 1100f;

        // ===================== ★ 新增：Aggro Camera（单脚本内置） =====================
        [Header("== Aggro Camera ==")]
        [SerializeField] Camera cam;               // 为空自动抓 Camera.main
        [SerializeField] float detectRadius = 18f; // 进入此半径触发放大
        [SerializeField] bool requireLineOfSight = false;
        [SerializeField] LayerMask losMask = -1;

        // Ortho
        [SerializeField] float orthoSizeMul = 1.25f; // 正交相机放大倍率

        // Perspective
        [SerializeField] bool useDollyBack = true;   // 透视相机后退
        [SerializeField] float dollyBackDistance = 6f;
        [SerializeField] bool useFovBoost = false;   // 透视相机提升FOV
        [SerializeField] float fovMul = 1.15f;
        [SerializeField] float perspectiveLerp = 4.5f;

        [Tooltip("如果你的相机有跟随脚本（如 CameraFollow2D），拖进来可在战斗中偏向玩家")]
        [SerializeField] Component cameraFollowLike; // 例如 CameraFollow2D
        [SerializeField] bool usePlayerAnchor = true;
        [SerializeField] Vector2 softSizeAtBoss = new Vector2(4.8f, 4.5f);
        [SerializeField] float anchorLerp = 10f;

        // runtime cache（相机）
        bool _camModified;
        float _origOrthoSize, _origFov;
        Vector3 _origCamPos;
        Transform _anchor;           // 临时锚点（把相机“目标”温柔吸向玩家）
        Transform _origFollowTarget; // 兼容你的跟随脚本
        Vector2 _origSoftOffset, _origSoftSize;

        // 你的跟随脚本的简易反射缓存（避免强依赖）
        System.Reflection.PropertyInfo _pTarget, _pSoftOffset, _pSoftSize;

        // ===================== ★ 新增：AI Movement Planning =====================
        [Header("== AI Movement Planning ==")]
        [Tooltip("基础决策周期（秒），到点后才读一次玩家位置并决定模式）")]
        public float aiReplanBase = 1.20f;
        public Vector2 aiReplanJitter = new Vector2(-0.35f, 0.45f);
        public float strafeSpeed = 2.8f;
        [Tooltip("每秒最大变速（用于速度插值，类似加速度上限）")]
        public float maxSpeedChange = 12f;
        [Tooltip("远距时更偏向接近的权重")]
        public float planBiasFar = 0.6f;
        [Tooltip("近距时更偏向后撤/横移的权重")]
        public float planBiasClose = 0.6f;



        // —— 传送可关（默认关掉，避免“飞走/瞬移”错觉）——
        [Tooltip("是否允许脱战距离传送；为了排查Boss乱跑，先默认关闭")]
        public bool enableFarTeleport = false;


        [Header("P2 Spin Speed")]
        [Tooltip("以每秒转圈的圈数计（RPS）。0.5 = 2秒1圈。")]
        public float p2OrbitRPS = 0.5f; // 默认就是 2 秒 1 圈




        // —— 软牵绳 + 硬边界（防飞出）——
        [Header("Arena Clamp")]
        public bool clampToArena = true;
        public Vector2 arenaCenter = Vector2.zero;   // 为空则用出生点
        public float arenaRadius = 18f;              // 软边界半径
        public float hardWallRadius = 20f;           // 硬边界半径（绝不越界）
        public float wallPushStrength = 12f;         // 贴边时的“推回力”


        [Header("== Player-Hit Energy Drops ==")]
        [Tooltip("开启：玩家对Boss造成伤害时，按伤害数额掉异色能量拾取物")]
        [SerializeField] private bool dropEnergyOnPlayerHit = true;

        [Tooltip("掉落量 = 伤害 * 这个系数")]
        [SerializeField] private float dropPerDamageScale = 1f;

        [Tooltip("掉落的随机散布半径（米）")]
        [SerializeField] private float dropScatterRadius = 0.6f;



        [Header("== Big-Skill Aggressive Approach ==")]
        [Tooltip("大招释放瞬间是否主动贴近玩家")]
        public bool bigApproachOnRelease = true;

        [Tooltip("主动贴近的目标距离（米）：到达该距离即停止靠近")]
        public float bigApproachTargetRange = 4.0f;

        [Tooltip("靠近速度（世界单位/秒）")]
        public float bigApproachSpeed = 7.5f;

        [Tooltip("最多持续靠近多久（秒），避免过度挤墙/抢位）")]
        public float bigApproachMaxTime = 2.5f;



        // —— 新增：大招所需 VFX / 数值 —— 
        [Header("Big Skill VFX & Tunables")]
        [SerializeField] GameObject p1DashExplodeVfx;   // P1 冲到玩家脚下的爆炸特效
        [SerializeField] float p1ExplodeRadius = 3f;
        [SerializeField] float p1ExplodeDamage = 25f;

        [SerializeField] GameObject p2RamHitVfx;        // P2 冲撞命中的特效（可复用现有）
        [SerializeField] float p2RamDamage = 20f;
        [SerializeField] float p2RamKnockSpeed = 12f; // 击退强度

        // === Distance Aggro Tuning ===
        [Header("== Distance Aggro Tuning ==")]
        [Tooltip("希望与玩家保持的最近距离（米）")]
        public float desiredMinRange = 3f;
        [Tooltip("希望与玩家保持的最远距离（米）")]
        public float desiredMaxRange = 15f;
        [Tooltip("当距离 <= desiredMinRange 时的攻速最大加成倍数（冷却会除以这个倍数）")]
        public float closeBoostMax = 2.5f;

        /// <summary>根据与玩家的距离，返回攻击频率加成倍数（越近越快，<=Min 达到 closeBoostMax）。</summary>
        public float GetAggroRateMul()
        {
            if (!player) return 1f;
            float d = Vector3.Distance(player.position, transform.position);
            if (d >= desiredMinRange) return 1f;
            // 线性插值：d==Min 时=closeBoostMax；d 越小越接近 closeBoostMax
            float u = Mathf.InverseLerp(desiredMinRange, 0f, Mathf.Clamp(d, 0f, desiredMinRange));
            return Mathf.Lerp(1f, Mathf.Max(1f, closeBoostMax), u);
        }

        [Header("== Big Skill Leash ==")]
        [Tooltip("大招期间，Boss 与玩家允许的最大距离（米）")]
        public float bigLeashMaxDistance = 10f;

        [Tooltip("因超过最大距离而触发“就近传送”的冷却（秒）")]
        public float bigLeashTeleportCooldown = 5f;

        [Tooltip("传送到玩家附近的半径（米），为空则复用 teleportNearPlayerRadius")]
        public float bigLeashTeleportRadius = 3.0f;

        private float _nextBigLeashTeleportAt = 0f;
        private bool _inBigSkill = false;
        private Coroutine _bigLeashCR;


































        // —— 大招期间的“距离牵绳”：超出则就近传一次（有冷却）——
        private IEnumerator BigLeashGuardCR()
        {
            while (_inBigSkill)
            {
                if (!player) yield break;

                float d = Vector3.Distance(transform.position, player.position);

                if (d > Mathf.Max(0.1f, bigLeashMaxDistance)
                    && Time.time >= _nextBigLeashTeleportAt)
                {
                    // 选一个玩家附近的安全点（已有通用采样工具）
                    float nearR = teleportNearPlayerRadius > 0 ? teleportNearPlayerRadius : bigLeashTeleportRadius;
                    Vector3 dst = SampleSafeNearPlayer_Global(
                        nearRadius: Mathf.Max(0.5f, nearR),
                        minClear: Mathf.Max(0.6f, minPlayerClearance),
                        requireLoS: requireLineOfSightForAggro
                    );
                    dst.z = transform.position.z;
                    _move?.OrderTeleport(dst);        // 统一由移动系统处理瞬移与速度清零


                    _nextBigLeashTeleportAt = Time.time + Mathf.Max(0.1f, bigLeashTeleportCooldown);
                }

                yield return null;
            }
        }




        private void Reset()
        {
            if (!orbAnchor) orbAnchor = transform;
            if (ringGradientP1 == null) ringGradientP1 = DefaultGrad(new Color(1f, .35f, .35f), new Color(1f, .75f, .55f));
            if (ringGradientP2 == null) ringGradientP2 = DefaultGrad(new Color(.35f, 1f, .55f), new Color(.75f, 1f, .85f));
        }

        private Gradient DefaultGrad(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }

        private Vector3 _spawnPos;




        // —— 采样：玩家附近的“安全点”（不与地形重叠，保留最小净空，可选 LoS）——
        private Vector3 SampleSafeNearPlayer_Global(float nearRadius, float minClear, bool requireLoS)
        {
            if (!player) return transform.position;
            Vector3 center = player.position;
            Vector3 best = transform.position;

            bool Blocked(Vector3 p)
                => use2DPhysics
                    ? Physics2D.OverlapCircle(p, safeProbeRadius, groundMask)
                    : Physics.CheckSphere(p, safeProbeRadius, groundMask);

            bool HasLineOfSight(Vector3 from, Vector3 to)
            {
                Vector3 dir = to - from; float len = dir.magnitude;
                if (len < 1e-4f) return true; dir /= len;
                if (use2DPhysics) return !Physics2D.Raycast((Vector2)from, (Vector2)dir, len, groundMask);
                return !Physics.Raycast(from, dir, len, groundMask);
            }

            // 先近圈再远圈
            float[] rings = new float[] { Mathf.Max(0.5f, nearRadius * 0.6f), Mathf.Max(nearRadius, minClear + 0.5f) };
            int[] nums = new int[] { 18, 28 };
            for (int r = 0; r < rings.Length; r++)
            {
                float R = rings[r];
                int N = nums[r];
                for (int i = 0; i < N; i++)
                {
                    float a = ((i + UnityEngine.Random.value * 0.35f) / N) * Mathf.PI * 2f;
                    Vector3 p = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * R;
                    p.z = transform.position.z;

                    if (Blocked(p)) continue;
                    if (Vector3.Distance(p, center) < Mathf.Max(0.1f, minPlayerClearance)) continue;
                    if (requireLoS && !HasLineOfSight(p, center)) continue;
                    return p;
                }
            }

            // 兜底：随机撒点直到找到一个不重叠位置
            for (int i = 0; i < 64; i++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float r = UnityEngine.Random.Range(minPlayerClearance, nearRadius);
                Vector3 p = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;
                p.z = transform.position.z;
                if (!Blocked(p)) return p;
            }
            return best;
        }




        // —— 大招开场短促靠近（并行于大招流程，不依赖 Update() 的移动开关）——
        private IEnumerator BigApproachCR(float maxTime, float stopAtRange)
        {
            float t = 0f;
            while (t < maxTime)
            {
                if (!player) yield break;

                // 目标：玩家附近安全点；优先直视线
                Vector3 dst = SampleSafeNearPlayer_Global(
                    nearRadius: teleportNearPlayerRadius > 0 ? teleportNearPlayerRadius : 3.5f,
                    minClear: Mathf.Max(0.6f, minPlayerClearance),
                    requireLoS: requireLineOfSightForAggro
                );

                float d = Vector3.Distance(transform.position, player.position);
                if (d <= Mathf.Max(0.1f, stopAtRange)) yield break;

                _move?.OrderSeek(dst, Mathf.Max(0.1f, bigApproachSpeed));  // 逐帧由 MovementDirector 推进


                t += Time.deltaTime;
                yield return null;
            }
        }




        // 将 BossColor 映射到玩家的 ColorMode（保留）
        private static FadedDreams.Player.ColorMode ToPlayerColor(BossColor c)
        {
            return (c == BossColor.Red) ? FadedDreams.Player.ColorMode.Red
                                        : FadedDreams.Player.ColorMode.Green;
        }
        private BossColor Opposite(BossColor c) => (c == BossColor.Red) ? BossColor.Green : BossColor.Red;

        /// <summary>
        /// 掉“玩家当前颜色的相反色”能量，数量与伤害成比例；与 BOSS 自身无关。
        /// </summary>
        private void DropOppositeEnergyPickup(float amount, Vector3 worldPos, BossColor playerColorAtHit)
        {
            if (!dropEnergyOnPlayerHit) return;
            if (!energyPickupPrefab) return;

            amount = Mathf.Max(0.01f, amount * Mathf.Max(0f, dropPerDamageScale));

            // 玩家色的相反色
            var oppositePlayer = Opposite(playerColorAtHit);
            var pickupColor = ToPlayerColor(oppositePlayer);

            // 随机小散布（手感）
            if (dropScatterRadius > 0f)
            {
                var r = UnityEngine.Random.insideUnitCircle * dropScatterRadius;
                worldPos += new Vector3(r.x, r.y, 0f);
            }

            var go = Instantiate(energyPickupPrefab, worldPos, Quaternion.identity);

            // 反射式写入常见字段（兼容你的通用拾取预制体）
            var monos = go.GetComponents<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m == null) continue;
                var t = m.GetType();

                var fEnergyColor = t.GetField("energyColor");
                var fColor = t.GetField("color");
                var pEnergyColor = t.GetProperty("energyColor");
                var pColor = t.GetProperty("Color");

                var fAmount = t.GetField("amount");
                var fEnergyAmount = t.GetField("energyAmount");
                var pAmount = t.GetProperty("Amount");

                try { if (fEnergyColor != null) fEnergyColor.SetValue(m, pickupColor); } catch { }
                try { if (fColor != null) fColor.SetValue(m, pickupColor); } catch { }
                try { if (pEnergyColor != null && pEnergyColor.CanWrite) pEnergyColor.SetValue(m, pickupColor, null); } catch { }
                try { if (pColor != null && pColor.CanWrite) pColor.SetValue(m, pickupColor, null); } catch { }

                try { if (fAmount != null) fAmount.SetValue(m, amount); } catch { }
                try { if (fEnergyAmount != null) fEnergyAmount.SetValue(m, amount); } catch { }
                try { if (pAmount != null && pAmount.CanWrite) pAmount.SetValue(m, amount, null); } catch { }
            }
        }



        private void Awake()
        {
            if (!player)
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go) player = go.transform;
            }

            _rb2 = GetComponent<Rigidbody2D>();
            _rb3 = GetComponent<Rigidbody>();

            // —— 关键：禁用一切重力/旋转，外力不推动Boss —— 
            if (use2DPhysics && _rb2)
            {
                _rb2.bodyType = RigidbodyType2D.Kinematic;   // 不吃物理力
                _rb2.gravityScale = 0f;
                _rb2.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            else if (!use2DPhysics && _rb3)
            {
                _rb3.isKinematic = true;
                _rb3.useGravity = false;
                _rb3.constraints = RigidbodyConstraints.FreezeRotation;
            }

            _spawnPos = transform.position;
            // 在 _spawnPos = transform.position; 之后加：
            _moveTuning.enableArenaClamp = clampToArena;  // 沿用你旧开关
            _moveTuning.arenaCenter = (arenaCenter == Vector2.zero)
                ? new Vector2(_spawnPos.x, _spawnPos.y)   // 默认用出生点
                : arenaCenter;                            // 若你在 Inspector 手填了就用手填的

            _moveTuning.arenaSoftRadius = Mathf.Max(1f, arenaRadius);
            _moveTuning.arenaHardRadius = Mathf.Max(_moveTuning.arenaSoftRadius + 0.5f, hardWallRadius);

            // 给回推强度一个合适值（太大也会感觉“被拽”）
            _moveTuning.leashAccel = 6f; // 你也可以在 Inspector 调




            if (arenaCenter == Vector2.zero) arenaCenter = new Vector2(_spawnPos.x, _spawnPos.y);
            if (hardWallRadius < arenaRadius) hardWallRadius = arenaRadius + 1.0f;

            if (!GetComponent<DamageAdapter>()) gameObject.AddComponent<DamageAdapter>().Setup(this);

            if (autoBindOrbs && orbPrefabs.Count == 0) AutoBindOrbPrefabs();

            var orbCount = (phase == Phase.P1) ? p1Orbs : p2Orbs;
            _conductor = new PrefabOrbConductor(
                anchor: orbAnchor ? orbAnchor : transform,
                orbPrefabs: orbPrefabs,
                initialCount: orbCount,
                baseRadius: baseRadius,
                use2D: use2DPhysics,
                tintUseEmission: tintUseEmission,
                emissionIntensity: emissionIntensity,
                playerMask: playerMask,
                playerTag: playerTag,
                owner: this
            );

            ApplyPhaseCounts(force: true);
            _conductor.ExitLineMode(0.01f);
            _conductor.SetIdleSpin(32f);

            _bigReadyAt = Time.time + ((phase == Phase.P1) ? bigSkillCooldownP1 : bigSkillCooldownP2);

            if (autoCreateRing)
            {
                var go = new GameObject("StellarRing");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                _ring = go.AddComponent<StellarRing>();
                _ring.Setup(this, ringRadius, ringWidth, (phase == Phase.P1) ? ringGradientP1 : ringGradientP2);
                _ring.SetMaterial(bossRingMaterial);   // ← 新增这行

            }

            AggroCamera_InitOnce();



        }

        /// <summary>
        /// 计算“为了让环绕体附近留出 orbClearanceRadius 的净空”，Boss 该受的推力（世界速度，米/秒）
        /// 逻辑：对每个仍在环上的环绕体做 Overlap，若有地形，则用“最近地形点→环绕体”的向量按侵入深度加权求和。
        /// </summary>
        // ★ 本体净空推力：仅围绕 Boss 本体做净空（半径=orbClearanceRadius）
        // 命中地形时，按“最近地形点→Boss中心”的方向给一股很轻的推力



        private void OnEnable()
        {
            // 不再在启用时直接开战，仅初始化相机缓存
            AggroCamera_InitOnce();

            // 若“战斗已开始”再恢复主循环；否则等待玩家进圈再开
            if (battleStarted && _mainLoopCR == null)
                _mainLoopCR = StartCoroutine(MainLoop());
        }

        private void OnDisable()
        {
            // 停一切协程与相机复原
            if (_mainLoopCR != null) { StopCoroutine(_mainLoopCR); _mainLoopCR = null; }
            StopAllCoroutines();
            AggroCamera_Restore();
        }




        private void Update()
        {
            // 开战管理与视觉更新：沿用你原有逻辑
            EnsureBattleStart();

            // 未开战：不移动，只做轻量视觉
            if (!battleStarted)
            {
                _conductor?.Tick(Time.deltaTime);
                _ring?.Tick(Time.deltaTime, phase, color, currentHP / Mathf.Max(1f, maxHP));
                return;
            }

            // 已开战：环绕体/血环照常
            _conductor?.Tick(Time.deltaTime);
            _ring?.Tick(Time.deltaTime, phase, color, currentHP / Mathf.Max(1f, maxHP));

            // —— 仅此一处接管移动 —— 
            _move?.Tick(Time.deltaTime);
        }



        // ★ 新增：LateUpdate 仅用于相机锚点跟随
        private void LateUpdate()
        {
            AggroCamera_LateTick();
        }

        private IEnumerator MainLoop()
        {
            AttachOrbUnits();

            while (true)
            {
                ApplyPhaseCounts();
                TryPlayBaseSkills();

                if (Time.time >= _bigReadyAt)
                {
                    if (verboseLogs) Debug.Log("[Boss] Big-skill window opened.");
                    yield return StartCoroutine(PlayBigSkill());
                    _bigReadyAt = Time.time + ((phase == Phase.P1) ? bigSkillCooldownP1 : bigSkillCooldownP2);
                }

                _concurrentMicros = CountActiveMicros();
                yield return null;
            }
        }

        private void TryPlayBaseSkills()
        {
            if (_suppressMicros) return;

            float rateMul = GetAggroRateMul(); // 靠得越近倍率越大（冷却会被除以它）

            if (Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + (pulseCooldown / Mathf.Max(1f, rateMul))
                                         + UnityEngine.Random.Range(-0.6f, 0.6f);
                StartCoroutine(DoCommandPulse());
            }

            if (Time.time >= _nextMark)
            {
                _nextMark = Time.time + (markCooldown / Mathf.Max(1f, rateMul))
                                        + UnityEngine.Random.Range(-0.6f, 0.6f);
                StartCoroutine(DoMarkBeacon());
            }
        }


        private IEnumerator DoCommandPulse()
        {
            _ring?.Burst(0.35f, 1.15f);
            if (player)
            {
                var kb = new KnockPreset { baseSpeed = pulseKnockSpeed, duration = 0.22f, verticalBoost = 0f };
                ApplyKnockbackTo(player, kb, from: transform.position);
            }
            int n = _conductor.OrbCount;
            for (int i = 0; i < n; i++)
            {
                var tr = _conductor.GetOrb(i);
                if (!tr) continue;
                var unit = tr.GetComponent<OrbUnit>();
                if (unit) unit.AddSpinBonus(0.10f, 2.5f);
            }
            yield return null;
        }

        private IEnumerator DoMarkBeacon()
        {
            if (!player) yield break;

            Vector3 pos = player.position;
            _ring?.PingAtWorld(pos, 1.0f);
            yield return new WaitForSeconds(markDelay);

            // 颜色相性仍生效：只有“与 Boss 当前色相同”的玩家才会被结算
            if (use2DPhysics)
            {
                var cols = Physics2D.OverlapCircleAll(pos, markBlastRadius, playerMask);
                foreach (var c in cols)
                {
                    var ic = c.GetComponent<IColorState>();
                    if (ic != null && ic.GetColorMode() != this.color) continue;

                    // 能量优先；找不到能量系统则回退为 HP 伤害
                    var pcm = c.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                    if (pcm)
                    {
                        float cost = Mathf.Max(1f, markDamage);
                        pcm.SpendEnergy(pcm.Mode, cost);
                    }
                    else
                    {
                        var hp = c.GetComponent<IDamageable>();
                        if (hp != null) hp.TakeDamage(markDamage);
                    }

                    var rb = c.attachedRigidbody;
                    if (rb != null)
                    {
                        var dir = (c.transform.position - pos).normalized;
                        rb.AddForce(dir * 6f, ForceMode2D.Impulse);
                    }
                }
            }
            else
            {
                var cols = Physics.OverlapSphere(pos, markBlastRadius, playerMask);
                foreach (var c in cols)
                {
                    var ic = c.GetComponent<IColorState>();
                    if (ic != null && ic.GetColorMode() != this.color) continue;

                    var pcm = c.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                    if (pcm)
                    {
                        float cost = Mathf.Max(1f, markDamage);
                        pcm.SpendEnergy(pcm.Mode, cost);
                    }
                    else
                    {
                        var hp = c.GetComponent<IDamageable>();
                        if (hp != null) hp.TakeDamage(markDamage);
                    }

                    var rb = c.attachedRigidbody;
                    if (rb != null)
                    {
                        var dir = (c.transform.position - pos).normalized;
                        rb.AddForce(dir * 6f, ForceMode.Impulse);
                    }
                }
            }
            yield return null;
        }


        private void ApplyPhaseCounts(bool force = false)
        {
            int want = (phase == Phase.P1) ? p1Orbs : p2Orbs;
            if (!force && _conductor != null && _conductor.OrbCount == want) return;
            if (_conductor != null)
            {
                _conductor.SetOrbCount(want);
                if (verboseLogs) Debug.Log($"[Boss] ApplyPhaseCounts → {want} orbs for {phase}");
            }
        }

        private void AttachOrbUnits()
        {
            int n = _conductor.OrbCount;
            for (int i = 0; i < n; i++)
            {
                var tr = _conductor.GetOrb(i);
                if (!tr) continue;
                var unit = tr.GetComponent<OrbUnit>();
                if (!unit)
                {
                    unit = tr.gameObject.AddComponent<OrbUnit>();
                    unit.Initialize(i, this, _conductor);
                }
            }
        }

        private IEnumerator PlayBigSkill()
        {
            // 整段禁用小技与位移/传送
            _suppressMicros = true;
            BroadcastStopMicros();
            _conductor.SetLocked(true);

            // ★ 关键：清空遗留调度，避免小技的发射穿插到大招
            if (_conductor != null) _conductor.ClearPending();

            // 回收所有已脱离的环绕体，确保干净阵列
            _conductor.RecallAll(0.6f, null);
            float waitMax = 8f; float t0 = Time.time;
            while (_conductor.DetachedCount() > 0)
            {
                _conductor.RecallAll(0.5f, null);
                if (Time.time - t0 > waitMax) break;
                yield return null;
            }

            // 保守：先退出线阵，避免旧线阵缓动影响 TELL 编排
            _conductor.ExitLineMode(0.01f);



            if (bigApproachOnRelease)
                StartCoroutine(BigApproachCR(bigApproachMaxTime, bigApproachTargetRange));

            _inBigSkill = true;
            _bigLeashCR = StartCoroutine(BigLeashGuardCR());





            // —— 统一为“10 秒超级大招”（TELL 3.0s + ACTIVE 7.0s）——
            if (phase == Phase.P1)
            {
                var big = (BigIdP1)(_nextBigIndex % 2);
                _nextBigIndex++;
                yield return StartCoroutine(PlayBigP1(big));
            }
            else
            {
                var big = (BigIdP2)(_nextBigIndex % 4);
                _nextBigIndex++;
                yield return StartCoroutine(PlayBigP2(big));
            }

            // 收束与复位
            _conductor.AttackOff();                // ★ 里面也会清空调度（见下）
            _conductor.ClearPending();             // 双保险
            _conductor.ExitLineMode(0.35f);
            _conductor.SetIdleSpin(32f);
            _conductor.SetLocked(false);
            yield return new WaitForSeconds(0.20f);
            _suppressMicros = false;


            _inBigSkill = false;
            if (_bigLeashCR != null) { StopCoroutine(_bigLeashCR); _bigLeashCR = null; }
        }




        private IEnumerator PlayBigP1(BigIdP1 big)
        {
            Transform center = orbAnchor ? orbAnchor : transform;

            switch (big)
            {
                // ===== P1-ULT-1 ===== 环半径+2，角速度×3，自由攻击（红色更激进）
                case BigIdP1.RingBurst:
                    {
                        const float TOTAL = 10f, TELL = 0.8f, ACTIVE = TOTAL - TELL;
                        const float FIRE_GAP = 1.0f; // ★ 每 1 秒一轮
                        _ring?.Burst(0.6f, 1.10f);
                        _conductor.PreBlendColor(color, 0.5f);

                        float R = baseRadius + 2f;
                        _conductor.SetRadius(R, 0.25f);
                        _conductor.SetLocked(false);

                        yield return new WaitForSeconds(TELL);

                        // 大招期间自由攻击（忽略相性闸门）
                        _conductor.AttackOn(1.0f, ignoreColorGate: true);

                        Coroutine redAssault = null;
                        if (color == BossColor.Red)
                            redAssault = StartCoroutine(_conductor.RedAssaultBurst(player, ACTIVE,
                                fractionPerVolley: 0.70f, flyTime: 0.42f, arc: 0.35f, gap: 0.35f, recallTime: 0.30f));

                        float degPerSec = 32f * 3f;
                        float angle0 = 0f;
                        int n = _conductor.OrbCount;
                        float t = 0f;

                        // ★ 新增：发弹计时器
                        float fireTimer = 0f;

                        while (t < ACTIVE)
                        {
                            angle0 += degPerSec * Time.deltaTime;

                            // 排布环上未脱离的球
                            for (int i = 0; i < n; i++)
                            {
                                if (_conductor.IsDetached(i)) continue;
                                float angDeg = angle0 + (360f / Mathf.Max(1, n)) * i;
                                float rad = angDeg * Mathf.Deg2Rad;
                                Vector3 pos = center.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * R;
                                var tr = _conductor.GetOrb(i);
                                if (tr) tr.position = pos;
                            }

                            // 非红色时：保留中等密度的常规发射
                            if (color != BossColor.Red && player)
                            {
                                _conductor.LaunchFractionAtTarget(player, fraction: 0.45f,
                                    flyTime: 0.48f, arc: 0.28f, stagger: 0.08f, ease: null);
                            }

                            // ★ 每 1 秒，让每个环绕体各自朝玩家发 1 枚子弹
                            fireTimer += Time.deltaTime;
                            if (fireTimer >= FIRE_GAP)
                            {
                                fireTimer = 0f;
                                _conductor.FireAllOrbsAtPlayerOnce();
                            }

                            yield return null;
                            t += Time.deltaTime;
                        }

                        if (redAssault != null) StopCoroutine(redAssault);

                        _conductor.AttackOff();
                        _conductor.SetRadius(baseRadius, 0.30f);
                        yield return null;
                        break;
                    }

                // ===== P1-ULT-2 ===== 以玩家为中心；半径+1；角速度×2；能自由攻击
                case BigIdP1.QuadrantMerge:
                    {
                        const float TOTAL = 7f, TELL = 0.7f, ACTIVE = TOTAL - TELL;
                        const float FIRE_GAP = 1.0f; // ★ 每 1 秒一轮
                        _conductor.PreBlendColor(color, 0.6f);
                        _ring?.Burst(0.5f, 1.08f);

                        float R = baseRadius + 1f;
                        _conductor.SetRadius(R, 0.25f);
                        _conductor.SetLocked(false);

                        yield return new WaitForSeconds(TELL);

                        // 大招期间自由攻击（忽略相性闸门）
                        _conductor.AttackOn(1.0f, ignoreColorGate: true);

                        Coroutine redAssault = null;
                        if (color == BossColor.Red)
                        {
                            redAssault = StartCoroutine(_conductor.RedAssaultBurst(player, ACTIVE,
                                fractionPerVolley: 0.60f, flyTime: 0.40f, arc: 0.30f, gap: 0.42f, recallTime: 0.28f));
                        }

                        float degPerSec = 32f * 2f;
                        float angle0 = 0f;
                        int n = _conductor.OrbCount;
                        float t = 0f;

                        // ★ 新增：发弹计时器
                        float fireTimer = 0f;

                        while (t < ACTIVE)
                        {
                            // ★ 新增：忙碌标记，避免被环排布抢位
                            bool[] busy = new bool[n];
                            float dashTimer = 0f;

                            while (t < ACTIVE)
                            {
                                Vector3 centerPos = (player ? player.position : center.position);
                                angle0 += degPerSec * Time.deltaTime;

                                // 只摆“还在环上且不忙碌”的球
                                for (int i = 0; i < n; i++)
                                {
                                    if (_conductor.IsDetached(i)) continue;
                                    if (busy[i]) continue; // ★ 忙碌中的球由协程自己移动
                                    float angDeg = angle0 + (360f / Mathf.Max(1, n)) * i;
                                    float rad = angDeg * Mathf.Deg2Rad;
                                    Vector3 pos = centerPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * R;
                                    var tr = _conductor.GetOrb(i);
                                    if (tr) tr.position = pos;
                                }

                                // 非红色时保留中等密度常规调度（可要可不要，看你喜好）
                                if (color != BossColor.Red && player)
                                {
                                    _conductor.LaunchFractionAtTarget(player, fraction: 0.40f,
                                        flyTime: 0.50f, arc: 0.22f, stagger: 0.10f, ease: null);
                                }

                                // ★ 每 1 秒：随机挑 1 个不忙的环绕体冲向“此刻玩家位置”，到点爆炸（范围伤害+VFX）
                                dashTimer += Time.deltaTime;
                                if (dashTimer >= 1.0f && player)
                                {
                                    dashTimer = 0f;
                                    // 抽签：非脱离+不忙的 idx
                                    List<int> pool = new List<int>(n);
                                    for (int i = 0; i < n; i++) if (!_conductor.IsDetached(i) && !busy[i]) pool.Add(i);
                                    if (pool.Count > 0)
                                    {
                                        int idx = pool[UnityEngine.Random.Range(0, pool.Count)];
                                        busy[idx] = true;
                                        StartCoroutine(Co_P1DashExplode(idx, () => busy[idx] = false));
                                    }
                                }

                                yield return null;
                                t += Time.deltaTime;
                            }

                        }


                        // —— P1：随机环绕体冲到玩家脚下并爆炸 —— 
                        IEnumerator Co_P1DashExplode(int idx, System.Action onDone)
                        {
                            var tr = _conductor.GetOrb(idx);
                            if (!tr) { onDone?.Invoke(); yield break; }

                            // 记录开始与目标（锁定当前玩家位置，避免“追不上的抖动”）
                            Vector3 start = tr.position;
                            Vector3 target = player ? player.position : tr.position;

                            // dash：0.25s 直线加速 + 自旋
                            float dur = 0.25f, tt = 0f;
                            while (tt < dur && tr)
                            {
                                tt += Time.deltaTime;
                                float u = Mathf.Clamp01(tt / dur);
                                float eased = u * u; // 逐步加速
                                tr.position = Vector3.Lerp(start, target, eased);
                                tr.Rotate(0, 0, 720f * Time.deltaTime);
                                yield return null;
                            }

                            // 到点：VFX + AOE 伤害（优先能量，否则 HP），与“标记爆炸”同风格
                            if (p1DashExplodeVfx) { var fx = Instantiate(p1DashExplodeVfx, target, Quaternion.identity); Destroy(fx, 3f); }

                            if (use2DPhysics)
                            {
                                var cols = Physics2D.OverlapCircleAll((Vector2)target, p1ExplodeRadius, playerMask);
                                foreach (var c in cols)
                                {
                                    var ic = c.GetComponent<IColorState>();
                                    // 本招不受相性限制：直接结算（如需受限可按 ic/this.color 过滤）
                                    var pcm = c.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                                    if (pcm) pcm.SpendEnergy(pcm.Mode, Mathf.Max(1f, p1ExplodeDamage));
                                    else { var hp = c.GetComponent<IDamageable>(); if (hp != null) hp.TakeDamage(p1ExplodeDamage); }
                                }
                            }
                            else
                            {
                                var cols = Physics.OverlapSphere(target, p1ExplodeRadius, playerMask);
                                foreach (var c in cols)
                                {
                                    var ic = c.GetComponent<IColorState>();
                                    var pcm = c.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                                    if (pcm) pcm.SpendEnergy(pcm.Mode, Mathf.Max(1f, p1ExplodeDamage));
                                    else { var hp = c.GetComponent<IDamageable>(); if (hp != null) hp.TakeDamage(p1ExplodeDamage); }
                                }
                            }

                            // 结束：交回环（下帧外圈排布会“把它摆回去”）
                            onDone?.Invoke();
                        }

                        if (redAssault != null) StopCoroutine(redAssault);

                        _conductor.AttackOff();
                        _conductor.SetRadius(baseRadius, 0.30f);
                        yield return null;
                        break;
                    }
            }

            yield return null;
        }









        private IEnumerator PlayBigP2(BigIdP2 big)
        {
            Transform bossCenter = transform;

            // —— 工具：从 Boss 顶层读震爆弹预制体（拖了就用，否则回退为普通子弹）——
            GameObject GetShockwavePrefab_Safe()
            {
                try
                {
                    var fld = typeof(BossC3_AllInOne).GetField("shockwaveBombPrefab",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    return fld != null ? (GameObject)fld.GetValue(this) : null;
                }
                catch { return null; }
            }

            // —— 工具：发射震爆弹（没配就回退普通子弹单发）——
            void FireShockwaveOrFallback(Vector3 origin, Vector3 towardNorm)
            {
                var prefab = GetShockwavePrefab_Safe();
                if (prefab == null)
                {
                    _conductor?.SpawnBulletFan(origin, towardNorm, 1, 0f, 1f);
                    return;
                }

                if (towardNorm.sqrMagnitude > 1e-6f) towardNorm.Normalize();
                else towardNorm = Vector3.right;

                var go = Instantiate(prefab, origin, Quaternion.LookRotation(Vector3.forward, towardNorm));
                float spd = Mathf.Max(6f, shockwaveBombSpeed > 0 ? shockwaveBombSpeed : bulletSpeed * 0.75f);

                var rb2 = go.GetComponent<Rigidbody2D>();
                var rb3 = go.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
                if (rb2) rb2.linearVelocity = (Vector2)(towardNorm * spd);
                if (rb3) rb3.linearVelocity = towardNorm * spd;
#else
        if (rb2) rb2.velocity = (Vector2)(towardNorm * spd);
        if (rb3) rb3.velocity = towardNorm * spd;
#endif
                Destroy(go, Mathf.Max(0.1f, shockwaveBombLifetime > 0 ? shockwaveBombLifetime : bulletLifetime * 1.5f));
            }

            switch (big)
            {
                // ===== P2-ULT-1：半径来回变化 + 每个环绕体 0.3~0.6s 独立射击 =====
                case BigIdP2.PrismSymphony:
                    {
                        const float TOTAL = 10f;
                        const float TELL = 2.2f;
                        const float ACTIVE = TOTAL - TELL;

                        _ring?.Burst(0.7f, 1.14f);
                        _conductor.PreBlendColor(color, 0.7f);

                        _conductor.SetLocked(false);
                        yield return new WaitForSeconds(TELL);

                        // 开 Bumper
                        for (int i = 0; i < _conductor.OrbCount; i++)
                        {
                            var tr = _conductor.GetOrb(i); if (!tr) continue;
                            var ag = tr.GetComponent<OrbAgent>(); if (ag) ag.SetBumperMode(true, true, color);
                        }

                        float degPerSec = Mathf.Max(30f, p2OrbitRPS * 360f);
                        float angle0 = 0f;
                        int n = _conductor.OrbCount;

                        // 每个环绕体自己的 0.3~0.6s 开火时钟
                        float[] nextFire = new float[n];
                        for (int i = 0; i < n; i++) nextFire[i] = Time.time + UnityEngine.Random.Range(0.15f, 0.45f);

                        float t = 0f;
                        while (t < ACTIVE)
                        {
                            // 半径摆动（base-1 ↔ base+2）
                            float u = t / ACTIVE;
                            float s = Mathf.Sin(u * Mathf.PI * 2f);   // -1..1
                            float R = baseRadius + Mathf.Lerp(-1f, +2f, (s + 1f) * 0.5f);

                            angle0 += degPerSec * Time.deltaTime;

                            for (int i = 0; i < n; i++)
                            {
                                float angDeg = angle0 + (360f / Mathf.Max(1, n)) * i;
                                float rad = angDeg * Mathf.Deg2Rad;
                                Vector3 pos = bossCenter.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * R;
                                var tr = _conductor.GetOrb(i);
                                if (tr) tr.position = pos;

                                // 独立计时开火
                                if (player && Time.time >= nextFire[i])
                                {
                                    Vector3 dir = (player.position - pos);
                                    if (dir.sqrMagnitude > 1e-5f)
                                        _conductor.SpawnBulletFan(pos, dir.normalized, 1, 0f, 1f);
                                    nextFire[i] = Time.time + UnityEngine.Random.Range(0.3f, 0.6f);
                                }
                            }


                            yield return null;
                            t += Time.deltaTime;
                        }

                        // 退 Bumper
                        for (int i = 0; i < _conductor.OrbCount; i++)
                        {
                            var tr = _conductor.GetOrb(i); if (!tr) continue;
                            var ag = tr.GetComponent<OrbAgent>(); if (ag) ag.SetBumperMode(false, false, color);
                        }
                        yield return null;
                        break;
                    }

                // ===== P2-ULT-2：“变大乱飞”——全部环绕体真正脱离 + 0.5s/发 =====
                case BigIdP2.FallingOrbit:
                    {
                        const float TOTAL_TIME = 10f;
                        const float TELL_TIME = 2.0f;
                        const float ACTIVE_TIME = TOTAL_TIME - TELL_TIME;

                        _conductor.PreBlendColor(color, 0.7f);
                        _ring?.Burst(0.6f, 1.12f);

                        // 读条：锁阵，避免提前重排
                        _conductor.SetLocked(true);
                        yield return new WaitForSeconds(TELL_TIME);

                        int orbCount = _conductor.OrbCount;

                        // 这里不放大/不真正脱离，维持环阵；如需擦伤可打开 Bumper
                        for (int i = 0; i < orbCount; i++)
                        {
                            var tr = _conductor.GetOrb(i); if (!tr) continue;
                            var ag = tr.GetComponent<OrbAgent>();
                            if (ag) ag.SetBumperMode(false, false, color); // 保守关掉贴身擦伤
                        }

                        // 忙碌标记，避免被环排布覆盖
                        bool[] busy = new bool[orbCount];
                        float degPerSec = 32f * 2.0f;
                        float baseAngleDeg = 0f;
                        float pickTimer = 0f;

                        float activeT = 0f;
                        while (activeT < ACTIVE_TIME)
                        {
                            baseAngleDeg += degPerSec * Time.deltaTime;

                            // 常规环排布（跳过忙碌的）
                            float ringR = baseRadius + 1.2f;
                            for (int i = 0; i < orbCount; i++)
                            {
                                if (busy[i]) continue;
                                float ang = baseAngleDeg + (360f / Mathf.Max(1, orbCount)) * i;
                                Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0f) * ringR;
                                var tr = _conductor.GetOrb(i);
                                if (tr) tr.position = pos;
                            }

                            // 每 0.5 秒触发 1 次冲撞
                            pickTimer += Time.deltaTime;
                            if (pickTimer >= 0.5f && player)
                            {
                                pickTimer = 0f;
                                // 抽一个不忙的
                                List<int> pool = new List<int>(orbCount);
                                for (int i = 0; i < orbCount; i++) if (!busy[i]) pool.Add(i);
                                if (pool.Count > 0)
                                {
                                    int idx = pool[UnityEngine.Random.Range(0, pool.Count)];
                                    busy[idx] = true;
                                    StartCoroutine(Co_P2SpinRam(idx, () => busy[idx] = false));
                                }
                            }

                            yield return null;
                            activeT += Time.deltaTime;
                        }

                        yield return null;
                        break;

                        // —— P2：随机环绕体“旋转加速冲撞 → 命中伤害+击退+VFX → 回正” —— 
                        IEnumerator Co_P2SpinRam(int idx, System.Action onDone)
                        {
                            var tr = _conductor.GetOrb(idx);
                            if (!tr || !player) { onDone?.Invoke(); yield break; }

                            Vector3 start = tr.position;
                            Vector3 target = player.position;

                            // ① 预备加速 1.0s（自旋 + 逼近到 85%）
                            float prep = 1.0f, tt = 0f;
                            while (tt < prep && tr)
                            {
                                tt += Time.deltaTime;
                                float u = Mathf.Clamp01(tt / prep);
                                float eased = u * u;
                                tr.position = Vector3.Lerp(start, target, eased * 0.85f);
                                tr.Rotate(0, 0, 540f * Time.deltaTime);
                                yield return null;
                            }

                            // ② 冲撞 0.15s：到点 → 结算伤害/击退/VFX
                            float dash = 0.15f; tt = 0f;
                            while (tt < dash && tr)
                            {
                                tt += Time.deltaTime;
                                float u = Mathf.Clamp01(tt / dash);
                                tr.position = Vector3.Lerp(tr.position, target, u);
                                tr.Rotate(0, 0, 720f * Time.deltaTime);
                                yield return null;
                            }

                            // 命中
                            if (p2RamHitVfx) { var fx = Instantiate(p2RamHitVfx, target, Quaternion.identity); Destroy(fx, 3f); }
                            var pcm = player.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                            if (pcm) pcm.SpendEnergy(pcm.Mode, Mathf.Max(1f, p2RamDamage));
                            else { var hp = player.GetComponent<IDamageable>(); if (hp != null) hp.TakeDamage(p2RamDamage); }
                            var kb = new KnockPreset { baseSpeed = p2RamKnockSpeed, duration = 0.25f, verticalBoost = 0f };
                            ApplyKnockbackTo(player, kb, from: tr.position);

                            // ③ 回正 1.0s：平滑回到当前环上的应有位置
                            float back = 1.0f; tt = 0f;
                            while (tt < back && tr)
                            {
                                tt += Time.deltaTime;
                                float u = Mathf.Clamp01(tt / back);
                                float angNow = Time.time * 64f + (360f / Mathf.Max(1, _conductor.OrbCount)) * idx;
                                Vector3 should = transform.position + new Vector3(Mathf.Cos(angNow * Mathf.Deg2Rad), Mathf.Sin(angNow * Mathf.Deg2Rad), 0f) * (baseRadius + 1.2f);
                                tr.position = Vector3.Lerp(target, should, u);
                                yield return null;
                            }

                            onDone?.Invoke();
                        }
                    }


                // ===== P2-ULT-3：大阵（母体先脱离→每个母体复制10个→克隆绕母体环绕；
                // 母体每2s发1枚震爆弹；克隆各自1~3s随机向玩家发射普通子弹）=====
                case BigIdP2.FinalGeometry:
                    {
                        const float TOTAL = 15f;     // 稍长一点，视觉更完整
                        const float TELL = 2.0f;
                        const float ACTIVE = TOTAL - TELL;

                        _ring?.Burst(0.9f, 1.18f);
                        _conductor.PreBlendColor(color, 0.85f);

                        // 读条阶段锁阵，避免提前重排
                        _conductor.SetLocked(true);
                        yield return new WaitForSeconds(TELL);

                        int baseCount = _conductor.OrbCount; // 预期 6
                        if (baseCount <= 0) yield break;

                        // ① 让 6 个母体脱离轨道并独立行动（不瞬间“复制出阵列”）
                        try
                        {
                            var f = typeof(PrefabOrbConductor).GetField("_detached",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var list = f?.GetValue(_conductor) as List<bool>;
                            if (list != null)
                            {
                                for (int i = 0; i < Mathf.Min(baseCount, list.Count); i++)
                                {
                                    list[i] = true;
                                    var tr = _conductor.GetOrb(i);
                                    if (tr) tr.SetParent(null, true);
                                }
                            }
                        }
                        catch { }

                        // 母体轻度增大 + 开 Bumper
                        for (int i = 0; i < baseCount; i++)
                        {
                            var tr = _conductor.GetOrb(i); if (!tr) continue;
                            tr.localScale *= 1.4f;
                            var ag = tr.GetComponent<OrbAgent>(); if (ag) ag.SetBumperMode(true, true, color);
                        }

                        // ② 为每个母体生成 10 个克隆，并挂在一个临时容器下（便于统一清理）
                        var clonesPerMother = new List<List<Transform>>(baseCount);
                        var cloneRoot = new GameObject("~C3_FinalGeometry_Clones").transform;
                        var mothers = new List<Transform>(baseCount);
                        for (int i = 0; i < baseCount; i++) mothers.Add(_conductor.GetOrb(i));

                        Transform MakeCloneFrom(Transform mother)
                        {
                            if (!mother) return null;

                            // 复制母体 → 作为“克隆”放到 cloneRoot 下面
                            var t = Instantiate(mother.gameObject, mother.position, mother.rotation, cloneRoot).transform;

                            // 确保有 OrbAgent 并进入可碰撞进攻态
                            var ag = t.GetComponent<OrbAgent>();
                            if (!ag) ag = t.gameObject.AddComponent<OrbAgent>();
                            ag.Setup(this, playerMask, defaultDamage * 0.6f, playerColorImmunity);
                            ag.SetBumperMode(true, true, color);

                            // 克隆略小一点
                            t.localScale = mother.localScale * 0.85f;

                            // ★★★ 关键：只在“克隆体”上关闭一切拖尾，不动母体 ★★★

                            // 1) TrailRenderer 直接关掉（最常见的拖尾组件）
                            var trails = t.GetComponentsInChildren<TrailRenderer>(true);
                            foreach (var tr in trails)
                            {
                                tr.emitting = false;
                                tr.enabled = false;
                            }

                            // 2) 粒子系统里的 Trails/Emission 可能也被当作拖尾，用名字兜底关闭
                            var pss = t.GetComponentsInChildren<ParticleSystem>(true);
                            foreach (var ps in pss)
                            {
                                try
                                {
                                    // 2.1 粒子 Trails 子模块
                                    var ts = ps.trails;
                                    if (ts.enabled) { ts.enabled = false; }

                                    // 2.2 若这个粒子节点名里有“trail/tail”，直接关掉发射
                                    if (ps.name.IndexOf("trail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        ps.name.IndexOf("tail", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        var em = ps.emission; em.enabled = false;
                                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                                    }
                                }
                                catch { /* 某些渲染管线下子模块访问可能抛异常，忽略即可 */ }
                            }

                            // 3) 其他少见的“线状拖尾”（如 LineRenderer）也统一关闭
                            var lrs = t.GetComponentsInChildren<LineRenderer>(true);
                            foreach (var lr in lrs)
                            {
                                if (lr.name.IndexOf("trail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    lr.name.IndexOf("tail", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    lr.enabled = false;
                                }
                            }

                            return t;
                        }


                        for (int i = 0; i < baseCount; i++)
                        {
                            var list = new List<Transform>(10);
                            for (int k = 0; k < 10; k++) list.Add(MakeCloneFrom(mothers[i]));
                            clonesPerMother.Add(list);
                        }

                        // ③ 行为：克隆围绕“各自母体”做小半径环绕；母体每2秒发震爆；克隆 1~3s 随机射击
                        //    轨道半径和角速度可微抖，避免整齐机械感
                        float[] motherFireNext = new float[baseCount];
                        for (int i = 0; i < baseCount; i++) motherFireNext[i] = Time.time + 2f; // 2s一发

                        // 给每个克隆一个独立的发射时钟
                        var cloneFireNext = new Dictionary<Transform, float>(baseCount * 10);
                        foreach (var group in clonesPerMother)
                            foreach (var c in group)
                                cloneFireNext[c] = Time.time + UnityEngine.Random.Range(0.4f, 1.0f); // 初相位随机

                        float t = 0f;
                        while (t < ACTIVE)
                        {
                            // 更新克隆的环绕
                            for (int i = 0; i < baseCount; i++)
                            {
                                var mother = mothers[i]; if (!mother) continue;
                                var list = clonesPerMother[i];

                                // 母体本身做一点“缓慢游走”，并缓慢朝向玩家
                                if (player)
                                {
                                    Vector3 bias = (player.position - mother.position).normalized * 0.6f;
                                    mother.position = Vector3.Lerp(mother.position, mother.position + bias, Time.deltaTime * 0.6f);
                                    mother.rotation = Quaternion.Slerp(mother.rotation,
                                        Quaternion.LookRotation(Vector3.forward, (player.position - mother.position).normalized), Time.deltaTime * 6f);
                                }

                                float orbitR = 6.6f + 0.35f * Mathf.Sin((Time.time * 1.3f) + i * 0.7f); // 小范围呼吸
                                float w = 120f + 20f * Mathf.Sin(Time.time * 0.8f + i);           // 角速度（度/秒）

                                int cnt = list.Count;
                                for (int k = 0; k < cnt; k++)
                                {
                                    var c = list[k]; if (!c) continue;
                                    float ang = (360f / Mathf.Max(1, cnt)) * k + (Time.time * w);
                                    float rad = ang * Mathf.Deg2Rad;
                                    Vector3 pos = mother.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitR;
                                    c.position = pos;
                                    c.rotation = Quaternion.Euler(0, 0, ang - 90f);

                                    // 克隆各自 1~3s 随机发射普通子弹
                                    if (player && Time.time >= cloneFireNext[c])
                                    {
                                        Vector3 dir = (player.position - c.position);
                                        if (dir.sqrMagnitude > 1e-5f)
                                            _conductor.SpawnBulletFan(c.position, dir.normalized, 1, 0f, 1f);
                                        cloneFireNext[c] = Time.time + UnityEngine.Random.Range(1f, 3f);
                                    }
                                }

                                // 母体每 2 秒发 1 枚震爆弹
                                if (player && Time.time >= motherFireNext[i])
                                {
                                    Vector3 dir = (player.position - mother.position);
                                    if (dir.sqrMagnitude > 1e-5f) FireShockwaveOrFallback(mother.position, dir.normalized);
                                    motherFireNext[i] = Time.time + 2f;
                                }
                            }

                            yield return null;
                            t += Time.deltaTime;
                        }

                        // ④ 清理：销毁全部克隆，关 Bumper，母体回收
                        if (cloneRoot) Destroy(cloneRoot.gameObject);
                        for (int i = 0; i < baseCount; i++)
                        {
                            var tr = _conductor.GetOrb(i); if (!tr) continue;
                            tr.localScale = tr.localScale / 1.4f;
                            var ag = tr.GetComponent<OrbAgent>(); if (ag) ag.SetBumperMode(false, false, color);
                        }

                        _conductor.RecallAll(0.50f, null);
                        float start = Time.time;
                        while (_conductor.DetachedCount() > 0 && Time.time - start < 4f) { yield return null; }

                        _conductor.SetLocked(false);
                        _conductor.SetIdleSpin(32f);
                        yield return null;
                        break;
                    }
            }

            // 大招收束统一处理
            _conductor.AttackOff();
            _conductor.ClearPending();
            _conductor.ExitLineMode(0.35f);
            _conductor.SetIdleSpin(32f);
            _conductor.SetLocked(false);
            yield return new WaitForSeconds(0.20f);
            _suppressMicros = false;
        }



        // === Shockwave Bomb：接口 + 生成工具 ===
        // 放在 class BossC3_AllInOne 内（任何方法之外）

        [Header("P2 Shockwave Bomb")]
        [SerializeField] private GameObject shockwaveBombPrefab;   // 在 Inspector 拖入你的“震爆弹”预制体
        [Min(0.1f)] public float shockwaveBombSpeed = 12f;         // 震爆弹飞行速度
        [Min(0.1f)] public float shockwaveBombLifetime = 3f;       // 震爆弹生存时间（秒）

        /// <summary>
        /// 运行时设置震爆弹接口（可选，不用也行，直接在 Inspector 拖就好）
        /// </summary>
        public void SetShockwaveBomb(GameObject prefab, float speed = 12f, float lifetime = 3f)
        {
            shockwaveBombPrefab = prefab;
            shockwaveBombSpeed = Mathf.Max(0.1f, speed);
            shockwaveBombLifetime = Mathf.Max(0.1f, lifetime);
        }

        /// <summary>
        /// 从 origin 朝 dir 方向发射一枚“震爆弹”。如果没绑定预制体，则回退为普通子弹单发，不会报错。
        /// </summary>
        private void SpawnShockwaveBomb(Vector3 origin, Vector3 dirNorm)
        {
            if (dirNorm.sqrMagnitude > 1e-6f) dirNorm.Normalize();
            else dirNorm = Vector3.right;

            if (shockwaveBombPrefab == null)
            {
                // 回退：用现有的普通子弹生成一发顶上，保证不中断
                _conductor?.SpawnBulletFan(origin, dirNorm, 1, 0f, 1f);
                return;
            }

            // 生成震爆弹
            var go = Instantiate(shockwaveBombPrefab, origin, Quaternion.LookRotation(Vector3.forward, dirNorm));
            float spd = shockwaveBombSpeed > 0 ? shockwaveBombSpeed : 12f;

            // 兼容 2D/3D 刚体
            var rb2 = go.GetComponent<Rigidbody2D>();
            var rb3 = go.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
            if (rb2) rb2.linearVelocity = (Vector2)(dirNorm * spd);
            if (rb3) rb3.linearVelocity = dirNorm * spd;
#else
    if (rb2) rb2.velocity = (Vector2)(dirNorm * spd);
    if (rb3) rb3.velocity = dirNorm * spd;
#endif

            // 自动销毁
            Destroy(go, Mathf.Max(0.1f, shockwaveBombLifetime));
        }














        // === Boss 本体强震（不动相机） ===
        Coroutine _shakeCR;
        void DoSelfShake(float seconds, float amplitude)
        {
            if (_shakeCR != null) StopCoroutine(_shakeCR);
            _shakeCR = StartCoroutine(SelfShakeCR(seconds, amplitude));
        }

        // ============================================================
        // == 本体净空推力（Boss 身边不希望贴地形）===================
        // ============================================================
        Vector3 ComputeBodyClearancePush()
        {
            Vector3 center = transform.position;
            float R = Mathf.Max(0.01f, orbClearanceRadius);

            Vector3 sum = Vector3.zero;
            float weight = 0f;

            if (use2DPhysics)
            {
                var hits = Physics2D.OverlapCircleAll((Vector2)center, R, groundMask);
                foreach (var col in hits)
                {
                    if (!col) continue;
                    Vector2 cp = col.ClosestPoint((Vector2)center);
                    float dist = Vector2.Distance(cp, (Vector2)center);
                    float pen = Mathf.Clamp(R - dist, 0f, R);
                    if (pen <= 0f) continue;

                    Vector2 dir = ((Vector2)center - cp);
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
                    dir.Normalize();

                    float w = (pen / R);
                    w = w * w * 0.6f;
                    sum += new Vector3(dir.x, dir.y, 0f) * w;
                    weight += w;
                }
            }
            else
            {
                var hits = Physics.OverlapSphere(center, R, groundMask);
                foreach (var col in hits)
                {
                    if (!col) continue;
                    Vector3 cp = col.ClosestPoint(center);
                    float dist = Vector3.Distance(cp, center);
                    float pen = Mathf.Clamp(R - dist, 0f, R);
                    if (pen <= 0f) continue;

                    Vector3 dir = (center - cp);
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
                    dir.Normalize();

                    float w = (pen / R);
                    w = w * w * 0.6f;
                    sum += dir * w;
                    weight += w;
                }
            }

            if (weight <= 0f) return Vector3.zero;

            Vector3 dirAvg = sum.normalized;
            float baseMag = orbClearanceGain * weight;
            float softMax = Mathf.Max(0.01f, orbClearanceMax);
            float mag = baseMag / (1f + baseMag / softMax);

            return dirAvg * mag;
        }

        // 兼容旧调用名：转发到本体净空推力
        Vector3 ComputeOrbClearancePush()
        {
            return ComputeBodyClearancePush();
        }


        IEnumerator SelfShakeCR(float seconds, float amp)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float u = 1f - (t / Mathf.Max(0.001f, seconds));   // 衰减
                Vector2 off2 = UnityEngine.Random.insideUnitCircle * (amp * u);
                // 只给移动系统一个“附加抖动”
                SetAdditiveOffset(new Vector3(off2.x, off2.y, 0f));
                yield return null;
            }
            // 归零叠加位移
            SetAdditiveOffset(Vector3.zero);
            _shakeCR = null;
        }






        // 只在“重规划时刻”调用：挑一个移动模式并给出期望速度
        // 只在“重规划时刻”调用：挑一个移动模式并给出期望速度




        private Vector3 FindSafePointNear(Vector3 around, int rays, float radius)
        {
            for (int i = 0; i < rays; i++)
            {
                float a = (360f / rays) * i * Mathf.Deg2Rad;
                var p = around + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
                bool blocked = use2DPhysics
                    ? Physics2D.OverlapCircle(p, safeProbeRadius, groundMask)
                    : Physics.CheckSphere(p, safeProbeRadius, groundMask);
                if (!blocked) return p;
            }
            return around;
        }

        private void AutoBindOrbPrefabs()
        {
            var goAll = GameObject.FindObjectsOfType<Transform>(true);
            foreach (var tr in goAll)
            {
                if (tr == transform || tr.IsChildOf(transform)) continue;
                string n = tr.name.ToLowerInvariant();
                if (n.Contains("orb") || n.Contains("blade") || n.Contains("petal") || n.Contains("ring"))
                {
                    var prefab = tr.gameObject;
                    if (!orbPrefabs.Contains(prefab)) orbPrefabs.Add(prefab);
                    if (orbPrefabs.Count >= 8) break;
                }
            }
        }

        private void BroadcastStopMicros()
        {
            int n = _conductor.OrbCount;
            for (int i = 0; i < n; i++)
            {
                var tr = _conductor.GetOrb(i);
                if (!tr) continue;
                var unit = tr.GetComponent<OrbUnit>();
                if (unit) unit.RequestStopAndReturn();
            }
        }

        private int CountActiveMicros()
        {
            int c = 0;
            int n = _conductor.OrbCount;
            for (int i = 0; i < n; i++)
            {
                var tr = _conductor.GetOrb(i);
                if (!tr) continue;
                var unit = tr.GetComponent<OrbUnit>();
                if (unit != null && unit.IsBusy) c++;
            }
            return c;
        }

        public int GetMicroConcurrencyLimit() => (phase == Phase.P1) ? p1MaxConcurrentMicros : p2MaxConcurrentMicros;
        public int GetMicroConcurrencyNow() => _concurrentMicros;
        public bool IsMicrosSuppressed() => _suppressMicros;

        // === Boss 带色受伤（供 DamageAdapter 调用） ===
        // === Boss 带色受伤（供 DamageAdapter 调用） ===
        // === Boss 受伤（无色版本：不做同色判定） ===
        public void TakeDamage(float damage, BossColor? sourcePlayerColor = null)
        {
            float dmg = Mathf.Max(0f, Mathf.Abs(damage));
            if (dmg <= 0f) return;

            currentHP = Mathf.Max(0f, currentHP - dmg);
            _ring?.OnDamagePulse(dmg / Mathf.Max(1f, maxHP));

            // 掉落：若拿到“玩家当前颜色”，就按“异色”掉落；拿不到则不掉（可按需改为默认某色）
            if (dropEnergyOnPlayerHit && dmg > 0.01f && sourcePlayerColor.HasValue)
            {
                DropOppositeEnergyPickup(dmg, transform.position, sourcePlayerColor.Value);
            }

            // 受伤反馈
            DoSelfShake(Mathf.Clamp(0.12f + dmg * 0.01f, 0.12f, 0.28f), 0.35f);

            // 阶段检查：P1→P2（HP ≤ 1100）
            if (phase == Phase.P1 && currentHP <= PHASE2_THRESHOLD)
            {
                EnterPhase2();
            }

            if (currentHP <= 0f) StartCoroutine(DeathSequence());
        }



        private void EnterPhase2()
        {
            phase = Phase.P2;
            ApplyPhaseCounts(force: true);                // 更新到 6 个环绕体
            _conductor.ExitLineMode(0.25f);
            _conductor.SetIdleSpin(32f);
            _bigReadyAt = Time.time + bigSkillCooldownP2; // 调整大招计时
            if (verboseLogs) Debug.Log("[Boss] Phase switched to P2 (HP ≤ 1100).");
        }

        private IEnumerator DeathSequence()
        {
            _suppressMicros = true;
            _conductor.RecallAll(0.5f, null);
            _ring?.Collapse();
            yield return new WaitForSeconds(1.5f);
            if (verboseLogs) Debug.Log("[Boss] Defeated.");
            gameObject.SetActive(false);
        }

        // === 内置桥接器：把你项目里的一切“无色伤害”转成带色伤害打到 Boss 上 ===
        [DisallowMultipleComponent]
        private class DamageAdapter : MonoBehaviour, IDamageable
        {
            private BossC3_AllInOne _boss;
            private IColorState _playerColor;

            public void Setup(BossC3_AllInOne boss)
            {
                _boss = boss;
                TryBindColorProvider();
            }
            // 在 BossC3_AllInOne.DamageAdapter 内部加入：
            public bool IsDead
            {
                get
                {
                    // 只要 Boss 存活，就视为未死亡
                    return _boss != null ? (_boss.currentHP <= 0f) : false;
                }
            }

            private void Awake()
            {
                if (_boss == null) _boss = GetComponent<BossC3_AllInOne>();
                TryBindColorProvider();
                // —— 新移动系统接管 —— 
                if (_boss != null)
                    _boss.InitMovementDirector(this);


            }

            void TryBindColorProvider()
            {
                if (_boss && _boss.player)
                    _playerColor = _boss.player.GetComponent<IColorState>();
            }

            // 你项目的 IDamageable：只有 float，没有颜色
            // 在 BossC3_AllInOne.DamageAdapter 内：
            public void TakeDamage(float amount)
            {
                BossColor? src = null;
                if (_playerColor != null)
                {
                    var pc = _playerColor.GetColorMode();
                    src = (pc == BossColor.Red) ? BossColor.Red : BossColor.Green;
                }
                _boss?.TakeDamage(amount, src);
            }

        }

        // --------------- 指挥官接口/实现（含单体API） ---------------
        public interface IOrbConductor
        {
            void SetOrbCount(int count);
            int OrbCount { get; }
            Transform GetOrb(int idx);
            void SetLocked(bool v);
            void SetRadius(float r, float dur = 0.25f);
            void Gather(float scale, float dur = 0.25f);
            void Spread(float scale, float dur = 0.25f);
            void Spin(float degPerSec);
            void SetIdleSpin(float degPerSec);
            void Pulse(float time, float scale = 1.1f);
            void AimAt(Transform t);
            void Telegraph(float intensity, float dur);
            void AttackOn(float dmgMul = 1f);
            void AttackOff();
            void DashToward(Transform t, float speed, float time);
            void PreBlendColor(BossColor color, float dur);
            void FormLine(float angleDeg, float length, float spacing, float dur);
            void SweepLine(float angularSpeedDegPerSec);
            void LaunchFractionAtTarget(Transform target, float fraction, float flyTime, float arc, float stagger, AnimationCurve ease);
            void RecallAll(float flyTime, AnimationCurve ease);
            void ExitLineMode(float relayoutDur = 0.35f);
            void Tick(float dt);

            bool IsDetached(int idx);
            int DetachedCount();
            void LaunchSingle(int idx, Transform target, float flyTime, float arc, AnimationCurve ease);
            void RecallSingle(int idx, float flyTime, AnimationCurve ease);
            void SetOrbTint(int idx, Color tint, float emission);
        }

        [Serializable]
        public class PrefabOrbConductor : IOrbConductor
        {
            Transform _anchor;
            List<GameObject> _prefabs;
            List<Transform> _orbs = new List<Transform>();
            List<OrbAgent> _agents = new List<OrbAgent>();
            List<Flight> _flights = new List<Flight>();
            List<bool> _detached = new List<bool>();

            int _count = 0;
            float _baseRadius = 2.5f;
            bool _locked = false;

            float _spinDegPerSec = 0f;
            float _idleSpinDegPerSec = 0f;
            float _spinPhaseDeg = 0f;
            float _radius = 2.5f;
            float _lerpT = 0f, _lerpTotal = 0f;
            Vector3[] _homeLocal;
            Vector3[] _startLocal, _targetLocal;

            bool _lineMode = false;
            float _lineAngle = 0f, _lineLength = 6f, _lineSpacing = 1f;
            float _lineSweepDegPerSec = 0f;

            bool _tintUseEmission;
            float _emissionIntensity;
            bool _use2D;
            LayerMask _playerMask;
            string _playerTag;
            BossC3_AllInOne _owner;

            struct LaunchSchedule { public int idx; public float delay; public float fly; public float arc; public AnimationCurve ease; public Transform target; }
            List<LaunchSchedule> _schedule = new List<LaunchSchedule>();
            public int IndexOf(Transform tr)
            {
                if (tr == null) return -1;
                for (int i = 0; i < _orbs.Count; i++)
                    if (_orbs[i] == tr) return i;
                return -1;
            }

            class Flight
            {
                public bool active;
                public bool returning;
                public bool forward;               // ★ 新增：标记是否为前向飞行（起飞段）
                public float t;
                public float total;
                public Vector3 p0, p1, p2;
                public Transform tr;
                public AnimationCurve ease;
                public TrailRenderer trail;        // ★ 新增：可选降级拖尾
            }
            // ===== 工具：从某点发射 N 枚子弹（2D/3D 自适配）=====
            // ===== 工具：从某点发射 N 枚子弹（2D/3D 自适配）=====

            // PrefabOrbConductor 内新增：让所有环绕体各自朝玩家发 1 枚子弹（间隔由外层控制）
            public void FireAllOrbsAtPlayerOnce()
            {
                if (_owner == null) return;
                Transform pl = _owner.player;
                int n = OrbCount;
                for (int i = 0; i < n; i++)
                {
                    var orb = GetOrb(i);
                    if (!orb) continue;

                    // 计算朝向玩家的基向量
                    Vector3 toP = (pl ? (pl.position - orb.position) : orb.up);
                    // 复用现有工具：count=1、spread=0 即为单发直射
                    SpawnBulletFan(orb.position, toP, count: 1, spreadDeg: 0f, speedScale: 1.0f);
                }
            }


            // 统一的发弹工厂：无论红/绿/任何技能，子弹一律使用 Boss 顶层拖入的 bulletPrefab / bulletMaterial
            // PrefabOrbConductor 内：统一发弹工厂（红/绿/任何技能都走这里）
            // ★★ 用这段完整替换你当前的 SpawnBulletFan(...) ★★
            public void SpawnBulletFan(Vector3 origin, Vector3 toward, int count, float spreadDeg, float speedScale = 1f)
            {
                if (_owner == null) return;

                // 强制使用玩家拖入的预制体与材质
                var prefab = _owner.bulletPrefab;
                var mat = _owner.bulletMaterial;

                if (!prefab)
                {
                    // 未配置预制体就不发弹，避免回落到旧的默认弹
                    // Debug.LogWarning("[BossC3] bulletPrefab 未配置，跳过发弹。");
                    return;
                }

                // 顶层可调
                float baseSpeed = Mathf.Max(0.01f, _owner.bulletSpeed);
                float lifetime = Mathf.Max(0.01f, _owner.bulletLifetime);
                float damageMul = _owner.bulletDamageMul; // 如你的弹体脚本会读取，可在预制体上配置；这里不强制写入，避免签名不匹配

                // 方向与扇形
                Vector3 dir = (toward.sqrMagnitude < 1e-6f) ? Vector3.up : toward.normalized;
                int shots = Mathf.Max(1, count);
                float half = (shots == 1) ? 0f : 0.5f * spreadDeg;
                float step = (shots == 1) ? 0f : (spreadDeg / (shots - 1));

                for (int i = 0; i < shots; i++)
                {
                    float off = -half + step * i;
                    Quaternion rot = Quaternion.AngleAxis(off, Vector3.forward);
                    Vector3 d = (rot * dir).normalized;

                    // 实例化子弹（朝向Z朝外、Y为前）
                    var go = UnityEngine.Object.Instantiate(prefab, origin, Quaternion.LookRotation(Vector3.forward, d));

                    // 应用材质（如果有 Renderer）
                    if (mat)
                    {
                        var r = go.GetComponentInChildren<Renderer>();
                        if (r && r.sharedMaterial != mat) r.sharedMaterial = mat;
                    }

                    // 速度（Unity 6.2：使用 linearVelocity）
                    float speed = baseSpeed * Mathf.Max(0.05f, speedScale);
                    var rb2 = go.GetComponent<Rigidbody2D>();
                    if (rb2)
                    {
#if UNITY_6000_0_OR_NEWER
                        rb2.linearVelocity = (Vector2)(d * speed);
#else
            rb2.velocity = (Vector2)(d * speed);
#endif
                    }
                    var rb3 = go.GetComponent<Rigidbody>();
                    if (rb3)
                    {
#if UNITY_6000_0_OR_NEWER
                        rb3.linearVelocity = d * speed;
#else
            rb3.velocity = d * speed;
#endif
                    }

                    // 生命周期兜底（伤害/相性由你的预制体脚本自身控制）
                    UnityEngine.Object.Destroy(go, lifetime);
                }
            }


            // ===== 简易直线子弹（无刚体时使用）=====
            class LinearBullet : MonoBehaviour
            {
                Vector3 _v; float _life;
                public void Setup(Vector3 v, float life) { _v = v; _life = life; }
                void Update()
                {
                    transform.position += _v * Time.deltaTime;
                    _life -= Time.deltaTime;
                    if (_life <= 0f) Destroy(gameObject);
                }
            }

            // ===== 子弹伤害（能量→HP），与 OrbAgent 撞击逻辑一致 =====
            class BulletDamage : MonoBehaviour
            {
                BossC3_AllInOne _owner;
                LayerMask _playerMask;
                float _damage;
                bool _colorGate;

                public void Setup(BossC3_AllInOne owner, LayerMask playerMask, float damage, bool colorGate)
                {
                    _owner = owner; _playerMask = playerMask; _damage = damage; _colorGate = colorGate;
                    var col3 = GetComponent<Collider>();
                    var col2 = GetComponent<Collider2D>();
                    if (col3) { try { col3.isTrigger = true; } catch { } }
                    if (col2) col2.isTrigger = true;
                    if (!col3 && !col2) gameObject.AddComponent<BoxCollider2D>().isTrigger = true;
                }

                // 子弹伤害：不做颜色闸门
                void Hit(GameObject other)
                {
                    var pcm = other.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                    if (pcm != null) pcm.SpendEnergy(pcm.Mode, Mathf.Max(1f, _damage));
                    else
                    {
                        var hp = other.GetComponent<FadedDreams.Enemies.IDamageable>();
                        if (hp != null) hp.TakeDamage(_damage);
                    }
                    Destroy(gameObject);
                }


                void OnTriggerEnter(Collider other) { if (((1 << other.gameObject.layer) & _playerMask) != 0) Hit(other.gameObject); }
                void OnTriggerEnter2D(Collider2D o) { if (((1 << o.gameObject.layer) & _playerMask) != 0) Hit(o.gameObject); }
            }


            public PrefabOrbConductor(Transform anchor, List<GameObject> orbPrefabs, int initialCount, float baseRadius,
                                      bool use2D, bool tintUseEmission, float emissionIntensity,
                                      LayerMask playerMask, string playerTag, BossC3_AllInOne owner)
            {
                _anchor = anchor;
                _prefabs = orbPrefabs;
                _baseRadius = baseRadius;
                _use2D = use2D;
                _tintUseEmission = tintUseEmission;
                _emissionIntensity = emissionIntensity;
                _playerMask = playerMask;
                _playerTag = playerTag;
                _owner = owner;

                SetOrbCount(initialCount);


            }

            public int OrbCount => _count;
            public Transform GetOrb(int idx) => (idx >= 0 && idx < _orbs.Count) ? _orbs[idx] : null;

            public void SetOrbCount(int count)
            {
                count = Mathf.Max(0, count);
                while (_orbs.Count < count)
                {
                    var prefab = _prefabs.Count > 0 ? _prefabs[Mathf.Min(_orbs.Count, _prefabs.Count - 1)] : null;
                    Transform tr;
                    if (prefab) tr = GameObject.Instantiate(prefab, _anchor).transform;
                    else
                    {
                        var go = new GameObject("Orb_" + _orbs.Count);
                        tr = go.transform;
                        tr.SetParent(_anchor, worldPositionStays: false);
                    }
                    tr.localPosition = Vector3.zero;
                    tr.localRotation = Quaternion.identity;

                    var agent = tr.GetComponent<OrbAgent>();
                    if (!agent) agent = tr.gameObject.AddComponent<OrbAgent>();
                    agent.Setup(_owner, _playerMask, _owner.defaultDamage, _owner.playerColorImmunity);

                    _orbs.Add(tr);
                    _agents.Add(agent);
                    _flights.Add(new Flight() { active = false, tr = tr });
                    _detached.Add(false);
                }
                while (_orbs.Count > count)
                {
                    var last = _orbs[_orbs.Count - 1];
                    if (last) GameObject.Destroy(last.gameObject);
                    _orbs.RemoveAt(_orbs.Count - 1);
                    _agents.RemoveAt(_agents.Count - 1);
                    _flights.RemoveAt(_flights.Count - 1);
                    _detached.RemoveAt(_detached.Count - 1);
                }

                _count = count;

                _radius = _baseRadius;
                _homeLocal = new Vector3[_count];
                _startLocal = new Vector3[_count];
                _targetLocal = new Vector3[_count];

                LayoutRingInstant();
            }

            void LayoutRingInstant()
            {
                for (int i = 0; i < _count; i++)
                {
                    float ang = (360f / Mathf.Max(1, _count)) * i * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * _radius;
                    _homeLocal[i] = p;
                    _orbs[i].localPosition = p;
                    _orbs[i].localRotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg + 90f);
                }
            }

            void LayoutLerp(float dur, Func<int, Vector3> targetGetter, AnimationCurve ease = null)
            {
                dur = Mathf.Max(0.001f, dur);
                _lerpT = 0f;
                _lerpTotal = dur;
                for (int i = 0; i < _count; i++)
                {
                    _startLocal[i] = _orbs[i].localPosition;
                    _targetLocal[i] = targetGetter(i);
                }
            }

            public void SetLocked(bool v) { _locked = v; }

            public void SetRadius(float r, float dur = 0.25f)
            {
                _radius = Mathf.Max(0.1f, r);
                LayoutLerp(dur, (i) =>
                {
                    float ang = (360f / Mathf.Max(1, _count)) * i * Mathf.Deg2Rad;
                    return new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * _radius;
                });
            }

            public void Gather(float scale, float dur = 0.25f) => SetRadius(_baseRadius * Mathf.Clamp(scale, 0.2f, 2.0f), dur);
            public void Spread(float scale, float dur = 0.25f) => SetRadius(_baseRadius * Mathf.Clamp(scale, 0.2f, 3.0f), dur);
            public void Spin(float degPerSec) { _spinDegPerSec = degPerSec; }
            public void SetIdleSpin(float degPerSec) { _idleSpinDegPerSec = degPerSec; }

            public void Pulse(float time, float scale = 1.1f)
            {
                float to = _radius * scale;
                SetRadius(to, time * 0.5f);
                _schedule.Add(new LaunchSchedule { idx = -1, delay = time * 0.5f, fly = 0f, arc = 0f, ease = null, target = null });
            }

            public void AimAt(Transform t) { }

            public void Telegraph(float intensity, float dur)
            {
                foreach (var a in _agents) a.SetState(OrbAgent.State.Telegraph, intensity);
            }

            // === PrefabOrbConductor 内：完整替换 AttackOn / AttackOff ===
            public void AttackOn(float dmgMul = 1f)
            {
                // 默认：不忽略相性（保持与你现在一致的行为）
                AttackOn(dmgMul, ignoreColorGate: false);
            }

            Vector3 FindSafeLandingNearPlayer(float radius, float minClear)
            {
                Transform pl = _owner ? _owner.player : null;
                if (!pl) return _anchor.position;

                Vector3 basePos = pl.position;
                for (int i = 0; i < 28; i++)
                {
                    float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float r = UnityEngine.Random.Range(minClear, radius);
                    Vector3 p = basePos + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;

                    bool blocked = _owner.use2DPhysics
                        ? Physics2D.OverlapCircle(p, _owner.safeProbeRadius, _owner.groundMask)
                        : Physics.CheckSphere(p, _owner.safeProbeRadius, _owner.groundMask);

                    if (!blocked) return p;
                }
                // 兜底：用 Boss 身边
                return _anchor.position + (_anchor.right * 2f);
            }

            // 新增：带 ignoreColorGate 的版本（大招用）
            public void AttackOn(float dmgMul, bool ignoreColorGate)
            {
                for (int i = 0; i < _agents.Count; i++)
                {
                    var a = _agents[i];
                    if (a == null) continue;

                    a.SetState(OrbAgent.State.Attack, 1f, Mathf.Max(0.3f, dmgMul));

                    // 1) 正常的门：设成 Boss 当前色（供非忽略模式使用）
                    var gateColor = (_owner != null) ? _owner.color : BossColor.Red;
                    a.EnsureAttackableGate(gateColor, true);

                    // 2) 忽略相性开关（大招自由攻击用）
                    a.ForceIgnoreColor(ignoreColorGate);
                }

                // 给点最低相位，避免视觉“静止”
                _spinDegPerSec = Mathf.Max(_spinDegPerSec, _idleSpinDegPerSec + 15f);
            }









            public void DashToward(Transform t, float speed, float time) { }

            public void PreBlendColor(BossColor color, float dur)
            {
                var c = (color == BossColor.Red) ? new Color(1, 0.25f, 0.25f) : new Color(0.25f, 1, 0.25f);
                foreach (var a in _agents) a.SetTint(c, _tintUseEmission ? _emissionIntensity : 0f);
            }

            public void FormLine(float angleDeg, float length, float spacing, float dur)
            {
                _lineMode = true;
                _lineAngle = angleDeg;
                _lineLength = length;
                _lineSpacing = spacing;

                int used = _count;
                LayoutLerp(dur, (i) =>
                {
                    float half = (used - 1) * spacing * 0.5f;
                    float x = -half + spacing * i;
                    var dir = new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad), 0);
                    var perp = new Vector3(-dir.y, dir.x, 0);
                    return dir * 0f + perp * x;
                });
            }

            public void ExitLineMode(float relayoutDur = 0.35f)
            {
                _lineMode = false;
                SetRadius(_radius, relayoutDur);
            }

            public void SweepLine(float degPerSec) { _lineSweepDegPerSec = degPerSec; }





            public bool IsDetached(int idx) => (idx >= 0 && idx < _detached.Count) ? _detached[idx] : false;

            public int DetachedCount()
            {
                int c = 0;
                for (int i = 0; i < _count; i++) if (_detached[i]) c++;
                return c;
            }


            // ========== OrbAgent：仅用 MPB 调整发光；绿色发射启用手动材质子物体 ==========
            public void SetTint(Color c, float emission)
            { /* moved to OrbAgent */ }

            void ApplyTint()
            { /* moved to OrbAgent */ }


            public void OnLaunch()
            { /* moved to OrbAgent */ }

            public void OnReturnedHome()
            { /* moved to OrbAgent */ }
            // === OrbAgent 内新增（放在类中任意位置即可） ===




            public void SetOrbTint(int idx, Color tint, float emission)
            {
                if (idx < 0 || idx >= _count) return;
                _agents[idx].SetTint(tint, emission);
            }




            // ========== PrefabOrbConductor 增补：清空计划 ==========
            public void ClearPending()
            {
                _schedule.Clear();
            }

            IEnumerator TrembleAt(Transform orb, float seconds, float amplitude = 0.12f, float freq = 18f)
            {
                Vector3 basePos = orb.position;
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.deltaTime;
                    float u = t / Mathf.Max(0.01f, seconds);
                    float amp = amplitude * Mathf.Lerp(0.35f, 1f, u);
                    Vector2 off = UnityEngine.Random.insideUnitCircle * amp;
                    orb.position = basePos + new Vector3(off.x, off.y, 0f);
                    yield return null;
                }
                orb.position = basePos;
            }

            // 将第 idx 个环绕体，沿贝塞尔曲线飞到 dest（不依赖目标 Transform）
            IEnumerator FlyToWorld(int idx, Vector3 dest, float flyTime, float arc = 0.35f)
            {
                if (idx < 0 || idx >= _count) yield break;
                if (_detached[idx]) yield break;

                // 临时“假目标”对象，便于复用现有 BuildFlightForward 逻辑
                var dummy = new GameObject("~OrbDummyTarget").transform;
                dummy.position = dest;
                BuildFlightForward(idx, dummy, Mathf.Max(0.1f, flyTime), arc, null);

                // 等飞行结束（forward flight 完成后，_agents[i].OnReachedTarget() 会被调用）
                float t = 0f;
                while (IsInForwardFlight(idx) && t < flyTime * 1.2f)
                {
                    t += Time.deltaTime; yield return null;
                }
                GameObject.Destroy(dummy.gameObject);
            }



            // =============== 红·远程招式 A：伏击齐射（站桩+扇形大密度）================
            public IEnumerator RedRemote_AmbushBombard(float total = 5.5f)
            {
                int n = OrbCount;
                if (n <= 0) yield break;

                AttackOn(1.0f, ignoreColorGate: true);

                // 1) 分派落点并飞过去（注意 _owner. 前缀 & 用 _owner.StartCoroutine）
                Vector3[] land = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float r = _owner != null ? _owner.landingRadiusAroundPlayer : 10f;
                    float c = _owner != null ? _owner.minPlayerClearance : 1f;
                    land[i] = FindSafeLandingNearPlayer(r, c);
                    if (_owner) _owner.StartCoroutine(FlyToWorld(i, land[i], 0.45f, 0.35f));
                }
                yield return new WaitForSeconds(0.55f);

                // 2) 落点颤抖 + 齐射三轮
                for (int i = 0; i < n; i++)
                {
                    var orb = GetOrb(i); if (!orb) continue;
                    if (_owner) _owner.StartCoroutine(TrembleAt(orb, 2.0f, 0.14f, 22f));
                }
                yield return new WaitForSeconds(2.0f);

                // 第1轮：宽扇形
                for (int i = 0; i < n; i++)
                {
                    var orb = GetOrb(i); if (!orb) continue;
                    Vector3 toP = (_owner != null && _owner.player ? (_owner.player.position - orb.position) : orb.up);
                    SpawnBulletFan(orb.position, toP, 22, 60f, 1.00f);
                }
                yield return new WaitForSeconds(0.35f);

                // 第2轮：中扇形
                for (int i = 0; i < n; i++)
                {
                    var orb = GetOrb(i); if (!orb) continue;
                    Vector3 toP = (_owner != null && _owner.player ? (_owner.player.position - orb.position) : orb.up);
                    SpawnBulletFan(orb.position, toP, 18, 40f, 1.00f);
                }
                yield return new WaitForSeconds(0.30f);

                // 第3轮：窄扇形
                for (int i = 0; i < n; i++)
                {
                    var orb = GetOrb(i); if (!orb) continue;
                    Vector3 toP = (_owner != null && _owner.player ? (_owner.player.position - orb.position) : orb.up);
                    SpawnBulletFan(orb.position, toP, 14, 24f, 1.05f);
                }

                yield return new WaitForSeconds(Mathf.Max(0f, total - 2.0f - 0.35f - 0.30f));
                RecallAll(0.45f, null);
                AttackOff();
            }


            // =============== 红·远程招式 B：侧翼扫射（“拉枪线”边跑边射） ==============
            public IEnumerator RedRemote_StrafeFan(float total = 6.0f)
            {
                int n = OrbCount;
                if (n <= 0) yield break;
                AttackOn(1.0f, ignoreColorGate: true);

                // 对半分左右翼
                List<int> left = new List<int>(), right = new List<int>();
                for (int i = 0; i < n; i++) ((i % 2) == 0 ? left : right).Add(i);

                // 左翼落位
                foreach (var i in left)
                {
                    float r = _owner != null ? _owner.landingRadiusAroundPlayer : 10f;
                    float c = _owner != null ? _owner.minPlayerClearance : 1f;
                    if (_owner) _owner.StartCoroutine(FlyToWorld(i, FindSafeLandingNearPlayer(r, c), 0.42f, 0.30f));
                }
                yield return new WaitForSeconds(0.28f);

                // 右翼落位
                foreach (var i in right)
                {
                    float r = _owner != null ? _owner.landingRadiusAroundPlayer : 10f;
                    float c = _owner != null ? _owner.minPlayerClearance : 1f;
                    if (_owner) _owner.StartCoroutine(FlyToWorld(i, FindSafeLandingNearPlayer(r, c), 0.42f, 0.30f));
                }
                yield return new WaitForSeconds(0.40f);

                float t = 0f;
                while (t < total)
                {
                    // 交替扫射
                    List<int> wing = (Mathf.FloorToInt(t * 2f) % 2 == 0) ? left : right;
                    foreach (var i in wing)
                    {
                        var orb = GetOrb(i); if (!orb) continue;
                        Vector3 toP = (_owner != null && _owner.player ? (_owner.player.position - orb.position) : orb.up);
                        SpawnBulletFan(orb.position, toP, 12, 36f, 1.0f);
                    }
                    yield return new WaitForSeconds(0.30f);

                    // 轻微“换位”
                    foreach (var i in wing)
                    {
                        var orb = GetOrb(i); if (!orb) continue;
                        Vector2 step = UnityEngine.Random.insideUnitCircle * 0.8f;
                        orb.position += new Vector3(step.x, step.y, 0f);
                    }

                    t += 0.30f;
                }

                RecallAll(0.45f, null);
                AttackOff();
            }


            // =============== 红·远程招式 C：聚焦迫击（收束→延迟→大爆发）================
            public IEnumerator RedRemote_FocusMortar(float total = 5.0f)
            {
                int n = OrbCount;
                if (n <= 0) yield break;
                AttackOn(1.0f, ignoreColorGate: true);

                // 1) 更靠近玩家就位
                for (int i = 0; i < n; i++)
                {
                    float near = Mathf.Max(2.5f, (_owner != null ? _owner.landingRadiusAroundPlayer : 10f) * 0.6f);
                    if (_owner) _owner.StartCoroutine(FlyToWorld(i, FindSafeLandingNearPlayer(near, 1.0f), 0.40f, 0.30f));
                }
                yield return new WaitForSeconds(0.45f);

                // 2) 强烈颤抖 2s
                for (int i = 0; i < n; i++)
                {
                    var orb = GetOrb(i); if (!orb) continue;
                    if (_owner) _owner.StartCoroutine(TrembleAt(orb, 2.0f, 0.18f, 24f));
                }
                yield return new WaitForSeconds(2.0f);

                // 3) 三轮递进扇形
                for (int round = 0; round < 3; round++)
                {
                    float spread = Mathf.Lerp(48f, 16f, round / 2f); // 越来越窄
                    int shots = Mathf.RoundToInt(Mathf.Lerp(18, 24, round / 2f));

                    for (int i = 0; i < n; i++)
                    {
                        var orb = GetOrb(i); if (!orb) continue;
                        Vector3 toP = (_owner != null && _owner.player ? (_owner.player.position - orb.position) : orb.up);
                        SpawnBulletFan(orb.position, toP, shots, spread, 1.08f);
                    }
                    yield return new WaitForSeconds(0.28f);
                }

                yield return new WaitForSeconds(Mathf.Max(0f, total - 2.0f - 3 * 0.28f));
                RecallAll(0.45f, null);
                AttackOff();
            }




            // === PrefabOrbConductor 内，新增：红色强袭批量脱离调度 ===
            public IEnumerator RedAssaultBurst(Transform target, float duration, float fractionPerVolley, float flyTime, float arc, float gap, float recallTime)
            {
                float t = 0f;
                while (t < duration)
                {
                    int n = OrbCount;
                    if (n <= 0) yield break;

                    // 同步一次性“洗牌并取前 k 个”做发射
                    int want = Mathf.CeilToInt(Mathf.Clamp01(fractionPerVolley) * n);
                    List<int> ids = new List<int>(n);
                    for (int i = 0; i < n; i++) ids.Add(i);
                    for (int i = n - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        (ids[i], ids[j]) = (ids[j], ids[i]);
                    }
                    int take = Mathf.Min(want, ids.Count);
                    for (int k = 0; k < take; k++)
                    {
                        int idx = ids[k];
                        if (_detached[idx]) continue;
                        // 更大的飞行抛物弧 + 略短飞行时间，保证位移明显且节奏紧
                        LaunchSingle(idx, target, flyTime, arc, null);
                    }

                    // 留一点点时间给飞行产生画面位移
                    float live = Mathf.Max(0.05f, flyTime * 0.75f);
                    float tt = 0f;
                    while (tt < live) { tt += Time.deltaTime; t += Time.deltaTime; yield return null; }

                    // 回收（避免“越积越多”的脱离）
                    for (int i = 0; i < n; i++)
                        RecallSingle(i, recallTime, null);

                    // volley 间隔
                    float g = Mathf.Max(0.02f, gap);
                    float gg = 0f;
                    while (gg < g) { gg += Time.deltaTime; t += Time.deltaTime; yield return null; }
                }
            }


            // ========== AttackOff：关伤害 + 清空所有待发 ==========
            public void AttackOff()
            {
                foreach (var a in _agents) a.SetState(OrbAgent.State.Passive, 0f);
                _spinDegPerSec = Mathf.Max(0f, _spinDegPerSec - 45f);
                _schedule.Clear(); // ★ 防止残留发射在大招后乱入
            }

            // ========== LaunchFractionAtTarget：更稳的“批量挑选 + 洗牌 + 去重” ==========
            public void LaunchFractionAtTarget(Transform target, float fraction, float flyTime, float arc, float stagger, AnimationCurve ease)
            {
                int want = Mathf.CeilToInt(Mathf.Clamp01(fraction) * _count);
                if (want <= 0) return;

                // 组一个候选池：未脱离 + 未在飞 + 未被计划
                List<int> pool = new List<int>(_count);
                bool[] planned = new bool[_count];
                for (int s = 0; s < _schedule.Count; s++)
                {
                    var it = _schedule[s];
                    if (it.idx >= 0 && it.idx < planned.Length) planned[it.idx] = true;
                }
                for (int i = 0; i < _count; i++)
                {
                    if (_detached[i]) continue;
                    var f = _flights[i];
                    if (f != null && f.active) continue;
                    if (planned[i]) continue;
                    pool.Add(i);
                }
                if (pool.Count == 0) return;

                // 洗牌（Fisher-Yates）
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }

                int take = Mathf.Min(want, pool.Count);
                float st = Mathf.Max(0f, stagger);
                for (int j = 0; j < take; j++)
                {
                    _schedule.Add(new LaunchSchedule
                    {
                        idx = pool[j],
                        delay = j * st,
                        fly = Mathf.Max(0.05f, flyTime),
                        arc = arc,
                        ease = ease,
                        target = target
                    });
                }
            }

            // ========== RecallAll：只对已脱离的做回收 ==========
            public void RecallAll(float flyTime, AnimationCurve ease)
            {
                for (int i = 0; i < _count; i++)
                    RecallSingle(i, flyTime, ease);
            }

            // 重载：兼容旧调用（不提供 ease 时默认 null）
            public void LaunchSingle(int idx, Transform target, float flyTime, float arc)
            {
                LaunchSingle(idx, target, flyTime, arc, null);
            }


            // ========== LaunchSingle：幂等保护 ==========
            public void LaunchSingle(int idx, Transform target, float flyTime, float arc, AnimationCurve ease)
            {
                if (idx < 0 || idx >= _count) return;
                if (_detached[idx]) return;
                var f = _flights[idx];
                if (f != null && f.active) return; // 已在飞就不重复发

                BuildFlightForward(idx, target, flyTime, arc, ease);
            }

            // ========== RecallSingle：幂等保护 ==========
            public void RecallSingle(int idx, float flyTime, AnimationCurve ease)
            {
                if (idx < 0 || idx >= _count) return;
                if (!_detached[idx]) return;                 // 未脱离无需回收
                var f = _flights[idx];
                if (f != null && f.active && f.returning)    // 已在回程中
                    return;

                BuildFlightReturn(idx, flyTime, ease);
            }

            // ========== Tick：限幅 dt + 计划合法化 + 飞行推进 ==========
            public void Tick(float dt)
            {
                if (_count == 0) return;

                // ★ 限幅（掉帧不至于一下子推进太远）
                dt = Mathf.Clamp(dt, 0f, 0.05f);

                // 旋转/线阵更新（不动已脱离者）
                if (!_locked)
                {
                    float baseSpin = (_lineMode ? 0f : _idleSpinDegPerSec);
                    float spin = Mathf.Abs(_spinDegPerSec) > 0.01f ? _spinDegPerSec : baseSpin;
                    _spinPhaseDeg += spin * dt;
                }
                if (_lineMode && Mathf.Abs(_lineSweepDegPerSec) > 0.01f)
                {
                    _lineAngle += _lineSweepDegPerSec * dt;
                }
                if (_lerpTotal > 0f && _lerpT < _lerpTotal)
                {
                    _lerpT += dt;
                    float u = Mathf.Clamp01(_lerpT / _lerpTotal);
                    for (int i = 0; i < _count; i++)
                    {
                        var p = Vector3.LerpUnclamped(_startLocal[i], _targetLocal[i], Smooth(u));
                        _orbs[i].localPosition = p;
                    }
                }

                if (!_lineMode)
                {
                    for (int i = 0; i < _count; i++)
                    {
                        float angDeg = (360f / Mathf.Max(1, _count)) * i + _spinPhaseDeg;
                        _homeLocal[i] = new Vector3(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad), 0) * _radius;
                        if (!_detached[i])
                        {
                            _orbs[i].localPosition = _homeLocal[i];
                            _orbs[i].localRotation = Quaternion.Euler(0, 0, angDeg + 90f);
                        }
                    }
                }
                else
                {
                    int used = _count;
                    float spacing = _lineSpacing;
                    float half = (used - 1) * spacing * 0.5f;
                    var dir = new Vector3(Mathf.Cos(_lineAngle * Mathf.Deg2Rad), Mathf.Sin(_lineAngle * Mathf.Deg2Rad), 0);
                    var perp = new Vector3(-dir.y, dir.x, 0);
                    for (int i = 0; i < _count; i++)
                    {
                        float x = -half + spacing * i;
                        _homeLocal[i] = dir * 0f + perp * x;
                        if (!_detached[i])
                        {
                            _orbs[i].localPosition = _homeLocal[i];
                            _orbs[i].localRotation = Quaternion.LookRotation(Vector3.forward, perp);
                        }
                    }
                }

                // —— 计划发射：若目标 orb 暂不可用，轻微延迟；超时则丢弃，避免“永远排队” —— 
                for (int s = _schedule.Count - 1; s >= 0; s--)
                {
                    var it = _schedule[s];
                    it.delay -= dt;
                    if (it.delay <= 0f)
                    {
                        if (it.idx >= 0)
                        {
                            bool busy = (it.idx < 0 || it.idx >= _count) || _detached[it.idx] || (_flights[it.idx] != null && _flights[it.idx].active);
                            if (busy)
                            {
                                it.delay = 0.02f + UnityEngine.Random.Range(0f, 0.02f); // 轻微错后
                                                                                        // 若已经错后多次（>0.5s）则丢弃
                                                                                        // 简化：把负 delay 当“累计错后”，限定最小 -0.5
                                if (it.delay < -0.5f) { _schedule.RemoveAt(s); continue; }
                                _schedule[s] = it;
                                continue;
                            }
                            BuildFlightForward(it.idx, it.target, it.fly, it.arc, it.ease);
                        }
                        _schedule.RemoveAt(s);
                    }
                    else _schedule[s] = it;
                }

                // —— 飞行推进 —— 
                for (int i = 0; i < _count; i++)
                {
                    var f = _flights[i];
                    if (f == null || !f.active) continue;

                    f.t += dt;
                    float u = Mathf.Clamp01(f.t / Mathf.Max(0.001f, f.total));
                    float e = f.ease != null ? Mathf.Clamp01(f.ease.Evaluate(u)) : Smooth(u);

                    Vector3 prev = f.tr.position;
                    Vector3 pos = QuadraticBezier(f.p0, f.p1, f.p2, e);
                    f.tr.position = pos;

                    // 朝向速度
                    Vector3 v = (pos - prev);
                    if (v.sqrMagnitude > 1e-6f)
                    {
                        var fwd = v.normalized;
                        float ang = Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
                        f.tr.rotation = Quaternion.Euler(0, 0, ang - 90f);
                    }

                    if (u >= 1f)
                    {
                        f.active = false;

                        if (f.returning)
                        {
                            _detached[i] = false;
                            f.tr.SetParent(_anchor, true);
                            f.tr.localPosition = _homeLocal[i];
                            f.tr.localRotation = Quaternion.identity;
                            _agents[i].OnReturnedHome();
                            PlayOneShotFXAt(f.tr, "FX_Return", f.tr.position, f.tr.rotation);
                        }
                        else
                        {
                            PlayOneShotFXAt(f.tr, "FX_Hit", f.p2, f.tr.rotation);
                            _agents[i].OnReachedTarget();
                        }
                    }
                    _flights[i] = f;
                }
            }

            // ========== BuildFlightForward：更稳的目标与 Bezier 控制 ==========
            void BuildFlightForward(int idx, Transform target, float flyTime, float arc, AnimationCurve ease)
            {
                if (idx < 0 || idx >= _count) return;
                if (_detached[idx]) return;

                var tr = _orbs[idx];
                var f = _flights[idx];

                _detached[idx] = true;
                tr.SetParent(null, true);

                // 目标回退：目标不存在就朝锚点右侧 3m
                Transform tgt = target;
                if (!tgt) tgt = _owner && _owner.player ? _owner.player : null;
                Vector3 p0 = tr.position;
                Vector3 p2 = (tgt ? tgt.position : (_anchor.position + _anchor.right * 3f));

                // 控制点：以“朝向 × 90°”的侧向作为弧高方向
                Vector3 dir = (p2 - p0);
                float dist = Mathf.Max(0.001f, dir.magnitude);
                Vector3 up = new Vector3(-dir.y, dir.x, 0).normalized; // 2D侧向
                Vector3 p1 = (p0 + p2) * 0.5f + up * arc * dist;

                f.active = true;
                f.returning = false;
                f.forward = true;
                f.t = 0f;
                f.total = Mathf.Max(0.05f, flyTime);
                f.p0 = p0; f.p1 = p1; f.p2 = p2;
                f.tr = tr; f.ease = ease;
                _flights[idx] = f;

                PlayOneShotFXAt(tr, "FX_Launch", p0, tr.rotation);
                _agents[idx].OnLaunch();
            }

            // ========== BuildFlightReturn：稳健回收 ==========
            void BuildFlightReturn(int idx, float flyTime, AnimationCurve ease)
            {
                if (idx < 0 || idx >= _count) return;

                var tr = _orbs[idx];
                var f = _flights[idx];

                Vector3 p0 = tr.position;
                Vector3 p2 = _anchor.TransformPoint(_homeLocal[idx]);
                Vector3 dir = (p2 - p0);
                float dist = Mathf.Max(0.001f, dir.magnitude);
                Vector3 up = new Vector3(-dir.y, dir.x, 0).normalized;
                Vector3 p1 = (p0 + p2) * 0.5f + up * 0.35f * dist; // 回收弧低一点更利落

                f.active = true;
                f.returning = true;
                f.forward = false;
                f.t = 0f;
                f.total = Mathf.Max(0.05f, flyTime);
                f.p0 = p0; f.p1 = p1; f.p2 = p2;
                f.tr = tr; f.ease = ease;
                _flights[idx] = f;
            }







            // —— Flight/FX 辅助 —— 
            public bool IsInForwardFlight(int idx)
            {
                if (idx < 0 || idx >= _count) return false;
                var f = _flights[idx];
                return f.active && !f.returning;
            }

            // 仅保留这一份【唯一】的递归查找
            static Transform FindChildRecursive(Transform root, string name)
            {
                if (!root) return null;
                for (int i = 0; i < root.childCount; i++)
                {
                    var c = root.GetChild(i);
                    if (c.name == name) return c;
                    var sub = FindChildRecursive(c, name);
                    if (sub) return sub;
                }
                return null;
            }

            // —— 解析粒子来源：Inspector 槽位优先，子物体命名回退 ——
            // 优先从 BossFXSlots/OrbFXSlots 获取；否则按子物体名查找
            static ParticleSystem ResolveFX(Transform root, string fxName)
            {
                if (!root) return null;

                var slots = root.GetComponent<BossFXSlots>();
                if (!slots && root.root) slots = root.root.GetComponent<BossFXSlots>();
                if (slots)
                {
                    var ps = slots.Get(fxName);
                    if (ps) return ps;
                }

                var t = FindChildRecursive(root, fxName);
                if (!t) return null;
                return t.GetComponent<ParticleSystem>();
            }

            static bool HasChildFX(Transform root, string fxName)
            {
                return ResolveFX(root, fxName) != null;
            }

            // —— 循环型粒子（如拖尾）的开关 ——
            // 仍然优先 Inspector 槽位；若未配置则回退子物体名
            static void ToggleChildFX(Transform root, string fxName, bool on)
            {
                var ps = ResolveFX(root, fxName);
                if (!ps) return;

                var em = ps.emission;
                if (on)
                {
                    if (!ps.isPlaying) ps.Play(true);
                    em.enabled = true;
                }
                else
                {
                    em.enabled = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            // —— 一次性粒子（起飞/命中/回收等）的播放 ——
            // 用实例化避免污染预制体播放状态
            static void PlayOneShotFXAt(Transform source, string fxName, Vector3 pos, Quaternion rot)
            {
                var ps = ResolveFX(source, fxName);
                if (!ps) return;

                var go = GameObject.Instantiate(ps.gameObject, pos, rot);
                var ps2 = go.GetComponent<ParticleSystem>();
                if (ps2) ps2.Play(true);

                float life = 2f;
                if (ps2)
                {
                    var m = ps2.main;
                    life = Mathf.Max(life, m.duration + (
                        m.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                            ? m.startLifetime.constantMax
                            : m.startLifetime.constant));
                }
                GameObject.Destroy(go, life + 0.5f);
            }

            static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
            {
                float u = 1f - t;
                return u * u * p0 + 2f * u * t * p1 + t * t * p2;
            }

            static float Smooth(float u) => u * u * (3f - 2f * u);

        }

        // --------------- Orb 碰撞/色相/击退（相机震动已移除） ---------------
        // --------------- Orb 碰撞/色相/击退（相机震动已移除） ---------------
        [DisallowMultipleComponent]
        public class OrbAgent : MonoBehaviour
        {
            public enum State { Passive, Telegraph, Attack }

            State _state = State.Passive;
            Renderer _r;
            MaterialPropertyBlock _mpb;
            Color _base = Color.white;
            float _emission = 0f;

            BossC3_AllInOne _owner;
            LayerMask _playerMask;
            float _damage;
            bool _colorGate;

            Collider _col3;
            Collider2D _col2;

            // === Bumper 模式 ===
            bool _isBumper = false;      // 贴身撞击只扣能量
            bool _preferEnergy = true;
            BossColor _current = BossColor.Red;     // ←← 用命名空间级 BossColor



            // ========== 命中转发器（3D） ==========
            class OrbChildHitRelay : MonoBehaviour
            {
                OrbAgent _parent;
                BossC3_AllInOne _owner;
                LayerMask _playerMask;

                public void Init(OrbAgent parent, BossC3_AllInOne owner, LayerMask playerMask)
                {
                    _parent = parent; _owner = owner; _playerMask = playerMask;
                }

                void OnTriggerEnter(Collider other)
                {
                    if (_parent == null || _owner == null) return;
                    if (((1 << other.gameObject.layer) & _playerMask) == 0) return;

                    // 与父层相同的“同色闸门”逻辑：尝试从攻击体上拿到颜色
                    var ic = other.GetComponent<BossC3_AllInOne.IColorState>();
                    if (ic == null && _owner.player) ic = _owner.player.GetComponent<BossC3_AllInOne.IColorState>();

                    // 如果玩家/攻击体提供了颜色，就把该颜色送去父OrbAgent的“被玩家同色命中”入口
                    if (ic != null) _parent.OnHitByPlayerColor(ic.GetColorMode(), null);
                }
            }

            // ========== 命中转发器（2D） ==========
            class OrbChildHitRelay2D : MonoBehaviour
            {
                OrbAgent _parent;
                BossC3_AllInOne _owner;
                LayerMask _playerMask;

                public void Init(OrbAgent parent, BossC3_AllInOne owner, LayerMask playerMask)
                {
                    _parent = parent; _owner = owner; _playerMask = playerMask;
                }

                void OnTriggerEnter2D(Collider2D other)
                {
                    if (_parent == null || _owner == null) return;
                    if (((1 << other.gameObject.layer) & _playerMask) == 0) return;

                    var ic = other.GetComponent<BossC3_AllInOne.IColorState>();
                    if (ic == null && _owner.player) ic = _owner.player.GetComponent<BossC3_AllInOne.IColorState>();

                    if (ic != null) _parent.OnHitByPlayerColor(ic.GetColorMode(), null);
                }
            }






            // === OrbAgent：放在 OrbAgent 类里 ===
            public void ForceIgnoreColor(bool on)
            {
                // on=true → 无视相性；on=false → 恢复按当前 _current 判定
                _colorGate = !on;

                // 若强制进攻，则顺手把碰撞器打开，避免“看得到飞行但不触发”的错觉
                if (on && _state != State.Attack) SetState(State.Attack, 1f, 1f);

                EnableColliders(on);
            }



            // OrbAgent.Setup(...) 里，EnableColliders(false); 之后追加/或把 Setup 改为以下整段：
            public void Setup(BossC3_AllInOne owner, LayerMask playerMask, float damage, bool colorGate)
            {
                _owner = owner;
                _playerMask = playerMask;
                _damage = damage;
                _colorGate = colorGate;

                _r = GetComponentInChildren<Renderer>();
                if (!_r) _r = GetComponent<Renderer>();
                if (!_r) _r = gameObject.AddComponent<MeshRenderer>();

                _col3 = GetComponent<Collider>();
                _col2 = GetComponent<Collider2D>();

                // ★ 强制 Trigger：确保触发 OnTrigger*
                if (_col3) { try { _col3.isTrigger = true; } catch { } }
                if (_col2) { _col2.isTrigger = true; }

                EnableColliders(false);
                ApplyTint();
            }


            void ApplyTint()
            {
                if (!_r) return;
                _mpb ??= new MaterialPropertyBlock();
                _r.GetPropertyBlock(_mpb);

                if (_r.sharedMaterial && _r.sharedMaterial.HasProperty("_BaseColor"))
                    _mpb.SetColor("_BaseColor", _base);
                if (_r.sharedMaterial && _r.sharedMaterial.HasProperty("_Color"))
                    _mpb.SetColor("_Color", _base);
                if (_r.sharedMaterial && _r.sharedMaterial.HasProperty("_TintColor"))
                    _mpb.SetColor("_TintColor", _base);

                if (_r.sharedMaterial && _r.sharedMaterial.HasProperty("_Emission"))
                    _mpb.SetFloat("_Emission", _emission);

                _r.SetPropertyBlock(_mpb);
            }

            public void SetTint(Color c, float emission) { _base = c; _emission = emission; ApplyTint(); }

            // 公开三参重载，兼容指挥官调用（修复 CS0122）
            public void SetState(State s, float telegraphIntensity = 0f, float damageMul = 1f)
            {
                _state = s;
                float baseD = (_owner != null ? _owner.defaultDamage : 6f);
                _damage = baseD * ((s == State.Attack) ? 1f : 0.5f) * Mathf.Max(0.01f, damageMul);

                switch (s)
                {
                    case State.Passive:
                        EnableColliders(false);
                        _emission = 0f;
                        break;
                    case State.Telegraph:
                        EnableColliders(false);
                        _emission = 0.6f + 0.6f * Mathf.Clamp01(telegraphIntensity);
                        break;
                    case State.Attack:
                        EnableColliders(true);
                        _emission = 1.2f;
                        break;
                }
                ApplyTint();
            }

            public void OnLaunch() { SetState(State.Attack, 1f, 1f); EnableColliders(true); }
            public void OnReturnedHome() { SetState(State.Passive, 0f, 1f); EnableColliders(false); }

            // ★ 补齐：被 PrefabOrbConductor.Tick() 调用（修 CS1061）
            public void OnReachedTarget()
            {
                // 命中后的小型反馈可以按需加（hitstop/粒子已在 Conductor 那边处理）
                SetState(State.Passive, 0f, 1f);
                EnableColliders(false);
            }

            void EnableColliders(bool on)
            {
                if (_col3) _col3.enabled = on;
                if (_col2) _col2.enabled = on;
            }

            // 进攻兜底（红色命中链路增强）
            // OrbAgent 内：完整替换
            public void EnsureAttackableGate(BossColor bossColor, bool enableColliders)
            {
                _current = bossColor;

                // 关键：红色=猛攻 → 关掉同色闸门；绿色仍走相性
                _colorGate = !(bossColor == BossColor.Red);

                if (_state != State.Attack) SetState(State.Attack, 1f, 1f);
                if (enableColliders) EnableColliders(true);
            }


            // Bumper 模式开关（签名改为 BossColor）
            // OrbAgent 内：完整替换
            public void SetBumperMode(bool on, bool preferEnergy, BossColor currentColor)
            {
                _isBumper = on;
                _preferEnergy = preferEnergy;
                _current = currentColor;

                // 红色时关闭闸门；绿色保持相性
                _colorGate = !(currentColor == BossColor.Red);

                if (_isBumper) { SetState(State.Attack, 0.5f, 0.8f); EnableColliders(true); }
                else { SetState(State.Passive, 0f, 1f); EnableColliders(false); }
            }


            // 同色被玩家命中 → 击退 + 回收（签名改为 BossColor）
            public void OnHitByPlayerColor(BossColor hitColor, string optionalFx = null)
            {
                // 只有“同色”才触发这套表现
                if (hitColor != _current) return;
                if (_owner == null) return;

                // 交给 Boss 顶层做：爆炸VFX + 掉异色能量 + 半透明 + 回收 + 暂停AI
                _owner.HandleOrbSameColorHit(transform, _current);

                // 旧逻辑（击退/回收/关碰撞）不再执行，避免与上面的“吸回+眩晕”重复
            }


            // —— 命中：3D —— 
            private void OnTriggerEnter(Collider other)
            {
                if (((1 << other.gameObject.layer) & _playerMask) == 0) return;

                // 统一同色闸门（若玩家没实现 IColorState，则默认放行）
                var ic = other.GetComponent<BossC3_AllInOne.IColorState>();
                if (_colorGate && ic != null && ic.GetColorMode() != _current) return;

                if (_isBumper)
                {
                    // 能量优先，其次 HP
                    var pcm = other.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                    if (pcm != null) { pcm.SpendEnergy(pcm.Mode, Mathf.Max(1f, _owner != null ? _owner.defaultDamage * 0.8f : 5f)); BossC3_AllInOne.Hitstop.Do(0.02f); }
                    else
                    {
                        var hp = other.GetComponent<IDamageable>();
                        if (hp != null) hp.TakeDamage(Mathf.Max(1f, _owner != null ? _owner.defaultDamage * 0.8f : 5f));
                    }
                    return;
                }

                if (_state != State.Attack) return;

                var pcm2 = other.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                if (pcm2 != null) pcm2.SpendEnergy(pcm2.Mode, Mathf.Max(1f, _owner != null ? _owner.defaultDamage : 6f));
                else
                {
                    var hp = other.GetComponent<IDamageable>();
                    if (hp != null) hp.TakeDamage(_damage);
                }

                var kb2 = new BossC3_AllInOne.KnockPreset { baseSpeed = 4.2f, duration = 0.22f, verticalBoost = 0.2f };
                if (_owner) _owner.ApplyKnockbackTo(other.transform, kb2, from: transform.position);
                BossC3_AllInOne.Hitstop.Do(0.03f);
            }

            // —— 命中：2D —— 
            private void OnTriggerEnter2D(Collider2D other)
            {
                if (((1 << other.gameObject.layer) & _playerMask) == 0) return;

                // —— 删掉同色门控 ——（原来有 _colorGate / ic 比对）
                // 命中后直接生效
                var pcm2 = other.GetComponent<FadedDreams.Player.PlayerColorModeController>();
                if (pcm2 != null) pcm2.SpendEnergy(pcm2.Mode, Mathf.Max(1f, _owner != null ? _owner.defaultDamage : 6f));
                else
                {
                    var hp = other.GetComponent<IDamageable>();
                    if (hp != null) hp.TakeDamage(_damage);
                }

                var kb2 = new BossC3_AllInOne.KnockPreset { baseSpeed = 4.2f, duration = 0.22f, verticalBoost = 0.2f };
                if (_owner) _owner.ApplyKnockbackTo(other.transform, kb2, from: transform.position);
                BossC3_AllInOne.Hitstop.Do(0.03f);
            }

        }




        // --------------- OrbUnit：充能自旋→发射→执行→回收 ---------------
        [DisallowMultipleComponent]
        public class OrbUnit : MonoBehaviour
        {
            public bool IsBusy { get; private set; }

            int _index;
            BossC3_AllInOne _boss;
            PrefabOrbConductor _con;
            OrbAgent _agent;

            float _cdRemain = 0f;
            bool _stopRequested = false;

            float _chargeU = 0f;
            bool _chargeToRed = true;

            float _spinPeak = 0f;
            float _spinNow = 0f;
            float _spinBonusMul = 1f;
            float _spinBonusRemain = 0f;

            public void Initialize(int index, BossC3_AllInOne boss, PrefabOrbConductor con)
            {
                _index = index;
                _boss = boss;
                _con = con;
                _agent = GetComponent<OrbAgent>();
                StartCoroutine(RunLoop());
            }

            public void RequestStopAndReturn() { _stopRequested = true; }

            public void AddSpinBonus(float mul, float time)
            {
                _spinBonusMul = Mathf.Max(_spinBonusMul, 1f + Mathf.Abs(mul));
                _spinBonusRemain = Mathf.Max(_spinBonusRemain, time);
            }

            public IEnumerator RunLoop()
            {
                // 你原有的开始/执行逻辑保持不变……
                // while(true) 或 for(;;) 的循环体中，按你原有的“播放微招、等待”等流程来
                // ...
                // FINISH / 冷却段：
                float mul = _boss ? _boss.GetAggroRateMul() : 1f;   // 距离越近 -> 攻速越快
                                                                    // 以“随机挑选的一种微招模板”的冷却作为基准，再按距离倍率加速
                var tpl = PickMicro();
                _cdRemain = tpl.cooldown / Mathf.Max(1f, mul);

                IsBusy = false;
                yield return null;  // 或你原有的 yield 逻辑
            }





            void ForceReturn()
            {
                if (_con.IsDetached(_index))
                    _con.RecallSingle(_index, 0.45f, null);
                _spinNow = 0f;
            }

            MicroTemplate PickMicro()
            {
                if (_boss.phase == Phase.P1)
                {
                    int r = UnityEngine.Random.Range(0, 5);
                    switch (r)
                    {
                        default: return MicroTemplate.RedPierce();
                        case 1: return MicroTemplate.ShatterArc();
                        case 2: return MicroTemplate.VoltNeedle();
                        case 3: return MicroTemplate.ReverseSaber();
                        case 4: return MicroTemplate.HarmonicHit();
                    }
                }
                else
                {
                    int r = UnityEngine.Random.Range(0, 10);
                    switch (r)
                    {
                        default: return MicroTemplate.TwinSpiral();
                        case 1: return MicroTemplate.MirrorRay();
                        case 2: return MicroTemplate.HunterLoop();
                        case 3: return MicroTemplate.FoldNova();
                        case 4: return MicroTemplate.GravBind();
                        case 5: return MicroTemplate.ChainDash();
                        case 6: return MicroTemplate.AerialDrop();
                        case 7: return MicroTemplate.LineSweep();
                        case 8: return MicroTemplate.RefractPulse();
                        case 9: return MicroTemplate.ChromaEcho();
                    }
                }
            }

            struct MicroTemplate
            {
                public bool toRed;
                public float chargeTime;
                public float flyTime;
                public float execTime;
                public float returnTime;
                public float cooldown;
                public float arc;

                // ===== P1 =====
                public static MicroTemplate RedPierce() => new MicroTemplate { toRed = true, chargeTime = 0.35f, flyTime = 0.45f, execTime = 0.10f, returnTime = 0.35f, cooldown = 3.8f, arc = 0.15f };
                public static MicroTemplate ShatterArc() => new MicroTemplate { toRed = true, chargeTime = 0.40f, flyTime = 0.55f, execTime = 0.15f, returnTime = 0.35f, cooldown = 4.2f, arc = 0.30f };
                public static MicroTemplate VoltNeedle() => new MicroTemplate { toRed = false, chargeTime = 0.50f, flyTime = 0.50f, execTime = 0.10f, returnTime = 0.35f, cooldown = 4.0f, arc = 0.55f };
                public static MicroTemplate ReverseSaber() => new MicroTemplate { toRed = true, chargeTime = 0.35f, flyTime = 0.60f, execTime = 0.20f, returnTime = 0.40f, cooldown = 4.2f, arc = 0.35f };
                public static MicroTemplate HarmonicHit() => new MicroTemplate { toRed = false, chargeTime = 0.60f, flyTime = 0.35f, execTime = 0.50f, returnTime = 0.35f, cooldown = 4.5f, arc = 0.10f };

                // ===== P2 =====
                public static MicroTemplate TwinSpiral() => new MicroTemplate { toRed = true, chargeTime = 0.40f, flyTime = 0.55f, execTime = 0.20f, returnTime = 0.40f, cooldown = 2.8f, arc = 0.40f };
                public static MicroTemplate MirrorRay() => new MicroTemplate { toRed = false, chargeTime = 0.50f, flyTime = 0.60f, execTime = 0.25f, returnTime = 0.40f, cooldown = 3.2f, arc = 0.20f };
                public static MicroTemplate HunterLoop() => new MicroTemplate { toRed = true, chargeTime = 0.45f, flyTime = 0.65f, execTime = 0.25f, returnTime = 0.45f, cooldown = 3.0f, arc = 0.35f };
                public static MicroTemplate FoldNova() => new MicroTemplate { toRed = false, chargeTime = 0.45f, flyTime = 0.40f, execTime = 0.35f, returnTime = 0.40f, cooldown = 3.2f, arc = 0.15f };
                public static MicroTemplate GravBind() => new MicroTemplate { toRed = false, chargeTime = 0.60f, flyTime = 0.50f, execTime = 0.80f, returnTime = 0.50f, cooldown = 3.5f, arc = 0.10f };
                public static MicroTemplate ChainDash() => new MicroTemplate { toRed = true, chargeTime = 0.35f, flyTime = 0.35f, execTime = 0.30f, returnTime = 0.40f, cooldown = 2.6f, arc = 0.25f };
                public static MicroTemplate AerialDrop() => new MicroTemplate { toRed = true, chargeTime = 0.50f, flyTime = 0.55f, execTime = 0.15f, returnTime = 0.45f, cooldown = 3.0f, arc = 0.55f };
                public static MicroTemplate LineSweep() => new MicroTemplate { toRed = true, chargeTime = 0.45f, flyTime = 0.60f, execTime = 0.25f, returnTime = 0.50f, cooldown = 3.2f, arc = 0.20f };
                public static MicroTemplate RefractPulse() => new MicroTemplate { toRed = false, chargeTime = 0.50f, flyTime = 0.50f, execTime = 0.35f, returnTime = 0.45f, cooldown = 3.0f, arc = 0.30f };
                public static MicroTemplate ChromaEcho() => new MicroTemplate { toRed = false, chargeTime = 0.45f, flyTime = 0.55f, execTime = 0.30f, returnTime = 0.45f, cooldown = 3.2f, arc = 0.15f };
            }
        }

        // --------------- “星环血条” StellarRing（正确版，仅负责绘制/动画） ---------------
        [RequireComponent(typeof(LineRenderer))]
        public class StellarRing : MonoBehaviour
        {
            LineRenderer _lr;
            BossC3_AllInOne _owner;
            float _radius;
            float _width;
            Gradient _grad;
            int _segments = 128;
            float _spin;

            float _distortAmp = 0f;
            float _distortFreq = 1.3f;

            // ★ 新增：本地缓存的材质指针（不要访问外层字段）
            Material _mat;

            public void SetMaterial(Material mat)
            {
                _mat = mat;
                if (_lr != null) _lr.sharedMaterial = _mat;
            }

            // 正确签名：和 Awake 里的调用一致
            public void Setup(BossC3_AllInOne owner, float radius, float width, Gradient grad)
            {
                _owner = owner;
                _radius = Mathf.Max(0.01f, radius);
                _width = Mathf.Max(0.01f, width);
                _grad = grad;

                _lr = GetComponent<LineRenderer>();
                if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();

                // 基础参数
                _lr.positionCount = _segments;
                _lr.loop = true;
                _lr.useWorldSpace = false;
                _lr.alignment = LineAlignment.TransformZ;
                _lr.textureMode = LineTextureMode.Stretch;
                _lr.generateLightingData = false;
                _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lr.receiveShadows = false;

                // ★ 先用 SetMaterial 传进来的；否则退回到外层 inspector 拖的 ringMaterial
                if (_mat != null)
                {
                    _lr.sharedMaterial = _mat;
                }
                else if (_owner != null && _owner.ringMaterial != null)
                {
                    _mat = _owner.ringMaterial;
                    _lr.sharedMaterial = _mat;
                }

                // 宽度与颜色
                _lr.widthMultiplier = _width;
                if (_grad != null) _lr.colorGradient = _grad;

                Rebuild();
            }

            public void Tick(float dt, Phase phase, BossColor color, float hp01)
            {
                float baseSpin = (phase == Phase.P1) ? 22f : 12f;
                baseSpin *= (color == BossColor.Red) ? 1.25f : 0.85f;
                _spin += baseSpin * dt;

                _distortAmp = Mathf.Lerp(0f, 0.25f, Mathf.Clamp01(1f - hp01));
                _distortFreq = Mathf.Lerp(1.3f, 3.2f, Mathf.Clamp01(1f - hp01));

                if (_grad != null)
                    _lr.colorGradient = (phase == Phase.P1) ? _owner.ringGradientP1 : _owner.ringGradientP2;

                _lr.widthMultiplier = Mathf.Lerp(_width, _width * 1.25f, 1f - hp01);

                Rebuild();
            }

            public void OnDamagePulse(float amount01)
            {
                StartCoroutine(PulseCR(Mathf.Lerp(0.1f, 0.35f, amount01 * 6f), 1.15f));
            }

            public void Burst(float dur, float scale) => StartCoroutine(PulseCR(dur, scale));
            public void PingAtWorld(Vector3 world, float dur) => StartCoroutine(PulseCR(dur * 0.5f, 1.08f));
            public void Collapse() => StartCoroutine(CollapseCR());

            IEnumerator PulseCR(float duration, float scale)
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Sin(Mathf.PI * (t / duration));
                    _lr.widthMultiplier = _width * Mathf.Lerp(1f, scale, u);
                    yield return null;
                }
                _lr.widthMultiplier = _width;
            }

            IEnumerator CollapseCR()
            {
                float t = 0f; float dur = 1.2f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    _distortAmp = Mathf.Lerp(_distortAmp, 0.6f, 0.15f);
                    _lr.widthMultiplier = Mathf.Lerp(_lr.widthMultiplier, _width * 0.5f, 0.1f);
                    _spin += 120f * Time.deltaTime;
                    Rebuild();
                    yield return null;
                }
            }

            void Rebuild()
            {
                if (_lr == null) return;
                for (int i = 0; i < _segments; i++)
                {
                    float a = ((float)i / _segments) * Mathf.PI * 2f + _spin * Mathf.Deg2Rad;
                    float r = _radius * (1f + _distortAmp * Mathf.Sin(a * _distortFreq + i * 0.17f));
                    var p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
                    _lr.SetPosition(i, p);
                }
            }
        }


        // --------------- 震屏/Hitstop/击退 工具（相机震动已移除，仅保留 Hitstop 与击退） ---------------
        public struct KnockPreset { public float baseSpeed; public float duration; public float verticalBoost; }

        public void ApplyKnockbackTo(Transform target, KnockPreset preset, Vector3 from)
        {
            if (!target) return;
            Vector3 dir = (target.position - from).normalized;
            if (use2DPhysics)
            {
                var rb = target.GetComponent<Rigidbody2D>();
                if (rb != null) rb.AddForce((Vector2)(dir * preset.baseSpeed) + Vector2.up * preset.verticalBoost, ForceMode2D.Impulse);
            }
            else
            {
                var rb = target.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(dir * preset.baseSpeed + Vector3.up * preset.verticalBoost, ForceMode.Impulse);
            }
        }

        public static class Hitstop
        {
            static bool _doing = false;
            static Runner _ownerRunner;
            public static void Do(float seconds)
            {
                if (_doing) return;
                _ownerRunner ??= new GameObject("~Hitstop").AddComponent<Runner>();
                _ownerRunner.Run(seconds);
            }
            public class Runner : MonoBehaviour
            {
                public void Run(float s) { StartCoroutine(DoCR(s)); DontDestroyOnLoad(gameObject); }
                IEnumerator DoCR(float s)
                {
                    _doing = true;
                    float old = Time.timeScale;
                    Time.timeScale = 0f;
                    yield return new WaitForSecondsRealtime(s);
                    Time.timeScale = old;
                    _doing = false;
                }
            }
        }

        // ===================== 相机：初始化/应用/恢复/逐帧 =====================
        void AggroCamera_InitOnce()
        {
            if (!cam) cam = Camera.main;
            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag(playerTag);
                if (p) player = p.transform;
            }
            if (cam)
            {
                _origOrthoSize = cam.orthographicSize;
                _origFov = cam.fieldOfView;
                _origCamPos = cam.transform.position;
            }

            // 兼容常见的相机跟随脚本（如 CameraFollow2D），以反射方式获取字段
            if (cameraFollowLike)
            {
                var t = cameraFollowLike.GetType();
                _pTarget = t.GetProperty("target");
                _pSoftOffset = t.GetProperty("softZoneCenterOffset");
                _pSoftSize = t.GetProperty("softZoneSize");

                // 记录原值（若存在）
                if (_pTarget != null) _origFollowTarget = _pTarget.GetValue(cameraFollowLike) as Transform;
                if (_pSoftOffset != null) _origSoftOffset = (Vector2)_pSoftOffset.GetValue(cameraFollowLike);
                if (_pSoftSize != null) _origSoftSize = (Vector2)_pSoftSize.GetValue(cameraFollowLike);
            }
        }

        void AggroCamera_Tick()
        {
            if (!cam || !player) return;

            // 进入索敌圈 → 开战（只触发一次）
            if (!battleStarted && IsPlayerInAggro())
            {
                EnsureBattleStart();
            }

            // 相机的持续“后撤/跟随”仅在已开战时生效
            if (!battleStarted) return;

            // 透视镜头：已修改期间持续做“后撤”平滑
            if (_camModified && !cam.orthographic && useDollyBack)
            {
                Vector3 target = _origCamPos - cam.transform.forward * dollyBackDistance;
                cam.transform.position = Vector3.Lerp(cam.transform.position, target, Time.deltaTime * perspectiveLerp);
            }
        }



        void AggroCamera_LateTick()
        {
            if (!_camModified || !player) return;

            // 锚点吸附：让“相机跟随目标”缓慢吸向玩家，构图更沉稳
            if (cameraFollowLike && usePlayerAnchor)
            {
                if (!_anchor)
                {
                    var go = new GameObject("BossCamAnchor_C3");
                    _anchor = go.transform;
                    _anchor.position = player.position;
                    if (_pTarget != null) _pTarget.SetValue(cameraFollowLike, _anchor);
                    if (_pSoftSize != null) _pSoftSize.SetValue(cameraFollowLike, softSizeAtBoss);
                }
                _anchor.position = Vector3.Lerp(_anchor.position, player.position, Time.deltaTime * anchorLerp);

                // 若跟随脚本支持 softZoneCenterOffset，则缓回 0 让玩家更居中
                if (_pSoftOffset != null)
                {
                    Vector2 cur = (Vector2)_pSoftOffset.GetValue(cameraFollowLike);
                    Vector2 nxt = Vector2.Lerp(cur, Vector2.zero, Time.deltaTime * 5f);
                    _pSoftOffset.SetValue(cameraFollowLike, nxt);
                }
            }
        }

        void AggroCamera_Apply()
        {
            if (!cam) return;

            if (cam.orthographic) cam.orthographicSize = _origOrthoSize * orthoSizeMul;
            else
            {
                if (useDollyBack) cam.transform.position = _origCamPos - cam.transform.forward * dollyBackDistance;
                if (useFovBoost) cam.fieldOfView = Mathf.Lerp(_origFov, _origFov * fovMul, 0.5f);
            }
            _camModified = true;
        }

        void AggroCamera_Restore()
        {
            if (!cam || !_camModified) return;

            if (cam.orthographic) cam.orthographicSize = _origOrthoSize;
            else
            {
                if (useFovBoost) cam.fieldOfView = _origFov;
                if (useDollyBack) cam.transform.position = _origCamPos;
            }

            if (cameraFollowLike)
            {
                if (_pTarget != null) _pTarget.SetValue(cameraFollowLike, _origFollowTarget);
                if (_pSoftOffset != null) _pSoftOffset.SetValue(cameraFollowLike, _origSoftOffset);
                if (_pSoftSize != null) _pSoftSize.SetValue(cameraFollowLike, _origSoftSize);
            }

            if (_anchor) Destroy(_anchor.gameObject);
            _anchor = null;

            _camModified = false;
        }

        // —— 当“玩家以同色命中某个环绕体”时，从 OrbAgent 调用此函数 —— 
        public void HandleOrbSameColorHit(Transform orbTr, BossColor orbColor)
        {
            if (!orbTr) return;

            // 1) 爆炸特效
            if (orbHitExplosionVfx)
            {
                var v = Instantiate(orbHitExplosionVfx, orbTr.position, Quaternion.identity);
                // 可选：Destroy(v, 3f);
            }

            // 2) 掉异色能量（红→掉绿；绿→掉红）
            if (energyPickupPrefab)
            {
                var drop = Instantiate(energyPickupPrefab, orbTr.position, Quaternion.identity);

                // 如你的 EnergyPickup 有可写入的颜色字段，这里设置它
                // 下行命名空间按你的脚本实际为准（若不同，替换成你的命名空间/字段名）
                var ep = drop.GetComponent<FadedDreams.World.EnergyPickup>();
                if (ep)
                {
                    ep.energyColor = (orbColor == BossColor.Red)
                        ? FadedDreams.Player.ColorMode.Green
                        : FadedDreams.Player.ColorMode.Red;
                }
            }

            // 3) 视觉半透明 + 暂停环绕体AI（仅保留渲染/灯光）
            var sprite = orbTr.GetComponentInChildren<SpriteRenderer>(true);
            var light2d = orbTr.GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
            float originalAlpha = 1f; Color c;

            if (sprite) { c = sprite.color; originalAlpha = c.a; c.a = Mathf.Clamp01(orbGhostAlpha); sprite.color = c; }
            if (light2d) { c = light2d.color; c.a = Mathf.Clamp01(orbGhostAlpha); light2d.color = c; }

            ToggleOrbAI(orbTr, false);

            // 4) 回到Boss身边并“眩晕”orbStunDuration秒，然后恢复
            var rb2 = orbTr.GetComponent<Rigidbody2D>();
            var rb3 = orbTr.GetComponent<Rigidbody>();
            StartCoroutine(Co_OrbRecallAndRecover(orbTr, originalAlpha, rb2, rb3));
        }

        // 统一开关：屏蔽环绕体上的大多数脚本（保留渲染/灯光）
        private void ToggleOrbAI(Transform orbTr, bool enabled)
        {
            var behaviours = orbTr.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var b in behaviours)
            {
                if (!b) continue;
                if (b is SpriteRenderer) continue;
                if (b is UnityEngine.Rendering.Universal.Light2D) continue;
                if (b == this) continue; // 极少见：防误关自己
                b.enabled = enabled;
            }
        }

        // 协程：吸回Boss → 等待 → 恢复
        private IEnumerator Co_OrbRecallAndRecover(Transform orbTr, float originalAlpha, Rigidbody2D rb2, Rigidbody rb3)
        {
            float nearDist = 0.6f;
            float timeout = 3.5f;
            float t = 0f;

            while (t < timeout && orbTr)
            {
                Vector3 bossPos = transform.position;
                Vector3 dir = (bossPos - orbTr.position);
                float dist = dir.magnitude;
                if (dist <= nearDist) break;

                Vector3 step = dir.normalized * (orbRecallSpeed * Time.deltaTime);
                if (rb2) { rb2.linearVelocity = Vector2.zero; rb2.angularVelocity = 0f; }
                if (rb3) { rb3.linearVelocity = Vector3.zero; rb3.angularVelocity = Vector3.zero; }
                orbTr.position += step;

                t += Time.deltaTime;
                yield return null;
            }

            // 等待（眩晕）
            float end = Time.time + Mathf.Max(0f, orbStunDuration);
            while (Time.time < end) yield return null;

            // 恢复视觉
            var sprite = orbTr ? orbTr.GetComponentInChildren<SpriteRenderer>(true) : null;
            var light2d = orbTr ? orbTr.GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>(true) : null;
            if (sprite) { var c = sprite.color; c.a = originalAlpha; sprite.color = c; }
            if (light2d) { var c = light2d.color; c.a = originalAlpha; light2d.color = c; }

            // 恢复AI
            if (orbTr) ToggleOrbAI(orbTr, true);
        }


        // ===================== 新版 MovementDirector 子系统（单脚本内嵌） =====================
        [System.Serializable]
        public struct MovementTuning
        {
            public bool enableArenaClamp;
            public Vector2 arenaCenter;
            public float arenaSoftRadius;
            public float arenaHardRadius;
            public float leashAccel;
            public float maxSpeed;
            public float maxAccel;

            public static MovementTuning Default2D() => new MovementTuning
            {
                enableArenaClamp = true,
                arenaCenter = Vector2.zero,
                arenaSoftRadius = 10f,
                arenaHardRadius = 12.5f,
                leashAccel = 6f,
                maxSpeed = 8f,
                maxAccel = 30f,
            };
        }

        [SerializeField] private MovementTuning _moveTuning = MovementTuning.Default2D();
        private MovementDirector _move;

        private void InitMovementDirector(MonoBehaviour host)
        {
            if (_move != null) return;
            _move = new MovementDirector(
                owner: this,
                tr: transform,
                rb2: GetComponent<Rigidbody2D>(),
                rb3: GetComponent<Rigidbody>()
            );
            _move.ApplyTuning(_moveTuning);
            if (_moveTuning.enableArenaClamp)
            {
                _move.SetArenaClamp(new MovementDirector.ArenaClamp
                {
                    center = _moveTuning.arenaCenter,
                    softRadius = _moveTuning.arenaSoftRadius,
                    hardRadius = _moveTuning.arenaHardRadius,
                    leashBackAccel = _moveTuning.leashAccel
                });
            }
        }


        // 统一对外接口（以后所有地方只调用这些）
        public void Move_Halt() => _move?.OrderHalt();
        public void Move_HoldAt(Vector3 worldPos) => _move?.OrderHold(worldPos);
        public void Move_Seek(Vector3 worldPos, float maxSpeed) => _move?.OrderSeek(worldPos, maxSpeed);
        public void Move_ApproachPlayer(float stopAtRange)
        {
            if (player) _move?.OrderApproach(player, stopAtRange, _moveTuning.maxSpeed);
        }
        public void Move_OrbitPlayer(float radius, float angularSpeed)
        {
            if (player) _move?.OrderOrbit(player, radius, angularSpeed);
        }
        public void Move_DashTo(Vector3 target, float dashSpeed, float maxSeconds = 1f)
            => _move?.OrderDash(target, dashSpeed, maxSeconds);
        public void Move_Teleport(Vector3 target) => _move?.OrderTeleport(target);
        public void Move_TeleportNearPlayer(float minR, float maxR)
        {
            if (!player) return;
            Vector3 p = player.position;
            Vector3 dir = (transform.position - p).normalized;
            if (dir.sqrMagnitude < 1e-4f) dir = UnityEngine.Random.insideUnitSphere.normalized;
            Vector3 dst = p + dir * Mathf.Lerp(minR, maxR, UnityEngine.Random.value);
            dst.z = transform.position.z;
            _move?.OrderTeleport(dst);
        }

        // 仅供 SelfShakeCR 使用
        public void SetAdditiveOffset(Vector3 off) => _move?.SetAdditiveOffset(off);

        // =============== 实现 ===============
        private class MovementDirector
        {
            public struct ArenaClamp
            {
                public Vector2 center;
                public float softRadius;
                public float hardRadius;
                public float leashBackAccel;
            }

            private readonly BossC3_AllInOne _owner;
            private readonly Transform _tr;
            private readonly Rigidbody2D _rb2;
            private readonly Rigidbody _rb3;
            private MovementTuning _cfg;
            private ArenaClamp? _arena;
            private Vector3 _vel;
            private Vector3 _additiveOffset; // 抖动等叠加位移

            // 命令状态
            private enum Mode { Halt, Hold, Seek, Approach, Orbit, Dash, TeleportOnce }
            private Mode _mode = Mode.Halt;
            private Vector3 _holdPos, _seekPos, _dashTarget, _teleportTarget;
            private Transform _approachTarget, _orbitTarget;
            private float _stopAtRange, _seekMaxSpeed, _orbitRadius, _orbitOmega;
            private float _dashSpeed, _dashRemain;

            public MovementDirector(BossC3_AllInOne owner, Transform tr, Rigidbody2D rb2, Rigidbody rb3)
            {
                _owner = owner; _tr = tr; _rb2 = rb2; _rb3 = rb3;
            }

            public void ApplyTuning(MovementTuning cfg) => _cfg = cfg;
            public void SetArenaClamp(ArenaClamp a) => _arena = a;
            public void SetAdditiveOffset(Vector3 off) => _additiveOffset = off;

            // —— 命令接口 ——
            public void OrderHalt() { _mode = Mode.Halt; _vel = Vector3.zero; }
            public void OrderHold(Vector3 pos) { _holdPos = pos; _mode = Mode.Hold; }
            public void OrderSeek(Vector3 pos, float maxSpeed) { _seekPos = pos; _seekMaxSpeed = Mathf.Max(0.1f, maxSpeed); _mode = Mode.Seek; }
            public void OrderApproach(Transform t, float stopR, float maxSpeed) { _approachTarget = t; _stopAtRange = Mathf.Max(0.05f, stopR); _seekMaxSpeed = Mathf.Max(0.1f, maxSpeed); _mode = Mode.Approach; }
            public void OrderOrbit(Transform t, float r, float omega) { _orbitTarget = t; _orbitRadius = Mathf.Max(0.05f, r); _orbitOmega = omega; _mode = Mode.Orbit; }
            public void OrderDash(Vector3 target, float speed, float maxSeconds) { _dashTarget = target; _dashSpeed = Mathf.Max(0.1f, speed); _dashRemain = Mathf.Max(0.01f, maxSeconds); _mode = Mode.Dash; }
            public void OrderTeleport(Vector3 target) { _teleportTarget = target; _mode = Mode.TeleportOnce; }

            public void Tick(float dt)
            {
                // 未开战或宿主禁止细分时：直接停住
                if (!_owner.battleStarted || _owner._suppressMicros) { ApplyStep(Vector3.zero, true); return; }

                Vector3 pos = _tr.position;
                Vector3 desired = Vector3.zero;

                switch (_mode)
                {
                    case Mode.Halt:
                        desired = Vector3.zero;
                        _vel = Vector3.MoveTowards(_vel, Vector3.zero, _cfg.maxAccel * dt);
                        break;

                    case Mode.Hold:
                        desired = (_holdPos - pos); desired.z = 0f;
                        desired = desired.normalized * _cfg.maxSpeed;
                        break;

                    case Mode.Seek:
                        desired = (_seekPos - pos); desired.z = 0f;
                        desired = desired.normalized * _seekMaxSpeed;
                        break;

                    case Mode.Approach:
                        if (_approachTarget)
                        {
                            Vector3 to = _approachTarget.position - pos; to.z = 0f;
                            float d = to.magnitude;
                            if (d <= _stopAtRange) { desired = Vector3.zero; _vel = Vector3.zero; }
                            else desired = to.normalized * _seekMaxSpeed;
                        }
                        break;

                    case Mode.Orbit:
                        if (_orbitTarget)
                        {
                            Vector3 to = _orbitTarget.position - pos; to.z = 0f;
                            float d = to.magnitude;
                            Vector3 radial = (d > 1e-4f) ? to / d : Vector3.right;
                            Vector3 tangent = new Vector3(-radial.y, radial.x, 0f);
                            Vector3 want = Vector3.zero;
                            // 保持半径 + 切线旋绕
                            float radialErr = d - _orbitRadius;
                            want += (-radial * radialErr * 3f);     // 简易径向弹簧
                            want += (tangent * _orbitOmega);        // 切线角速度
                            desired = Vector3.ClampMagnitude(want, _cfg.maxSpeed);
                        }
                        break;

                    case Mode.Dash:
                        Vector3 dir = (_dashTarget - pos); dir.z = 0f;
                        float need = dir.magnitude;
                        float step = Mathf.Min(need, _dashSpeed * dt);
                        desired = (need > 1e-4f) ? dir.normalized * (_dashSpeed) : Vector3.zero;
                        _dashRemain -= dt;
                        if (need <= 0.01f || _dashRemain <= 0f) { _mode = Mode.Halt; _vel = Vector3.zero; }
                        break;

                    case Mode.TeleportOnce:
                        ApplyTeleport(_teleportTarget);
                        _mode = Mode.Halt;
                        return;
                }

                // 速度朝向 desired 逼近
                Vector3 dv = desired - _vel;
                float acc = Mathf.Max(0.01f, _cfg.maxAccel);
                Vector3 add = Vector3.ClampMagnitude(dv, acc * dt);
                _vel += add;

                // 竞技场软/硬边界（可选）
                if (_arena.HasValue)
                {
                    var a = _arena.Value;
                    Vector2 c = a.center;
                    Vector2 p = new Vector2(pos.x, pos.y);
                    Vector2 d = p - c;
                    float r = d.magnitude;

                    if (r > a.softRadius)
                    {
                        Vector2 inward = (-d.normalized) * a.leashBackAccel;
                        _vel += new Vector3(inward.x, inward.y, 0f) * dt; // 轻推回
                    }
                    if (r > a.hardRadius)
                    {
                        // 直接夹到硬半径
                        Vector2 clamped = c + d.normalized * a.hardRadius;
                        pos = new Vector3(clamped.x, clamped.y, pos.z);
                        _vel = Vector3.zero;
                    }
                }

                ApplyStep(_vel * dt, false);
            }

            private void ApplyTeleport(Vector3 dst)
            {
                if (_rb2) _rb2.position = dst;
                else if (_rb3) _rb3.position = dst;
                else _tr.position = dst;
                _vel = Vector3.zero;
            }

            private void ApplyStep(Vector3 delta, bool forceStop)
            {
                Vector3 next = _tr.position + delta + _additiveOffset;
                if (_rb2) _rb2.MovePosition(next);
                else if (_rb3) _rb3.MovePosition(next);
                else _tr.position = next;

                if (forceStop) _vel = Vector3.zero;
            }
        }



        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(1, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, stopDistance);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, preferRange);
            Gizmos.color = new Color(1, 1, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, farTeleportDistance);

            // 相机触发范围
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }

        // === 仅供克隆体使用的简洁环绕组件 ===
        [DisallowMultipleComponent]
        private class OrbitFollower : MonoBehaviour
        {
            Transform _center;
            float _radius;
            float _omegaDegPerSec;
            float _angle; // 当前角度（度）

            public void Init(Transform center, float radius, float periodSeconds)
            {
                _center = center;
                _radius = Mathf.Max(0.01f, radius);
                _omegaDegPerSec = 360f / Mathf.Max(0.01f, periodSeconds); // 2 秒一圈 = 180°/s
                                                                          // 以当前相对中心的方向初始化角度（避免“跳变”）
                if (_center)
                {
                    Vector2 v = (Vector2)(transform.position - _center.position);
                    _angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                }
            }

            void Update()
            {
                if (!_center) return;
                _angle += _omegaDegPerSec * Time.deltaTime;
                float rad = _angle * Mathf.Deg2Rad;
                var c = _center.position;
                var pos = new Vector3(c.x + Mathf.Cos(rad) * _radius,
                                      c.y + Mathf.Sin(rad) * _radius,
                                      transform.position.z);
                transform.position = pos; // 克隆体是纯表现/攻击单位，不走 MovementDirector
            }
        }



    }



}


[DisallowMultipleComponent]
public class BossFXSlots : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string name;              // 例如：FX_Launch / FX_Trail / FX_Hit / FX_Return / FX_Big_TellSweep ...
        public ParticleSystem particle;  // 直接把粒子/VFX Graph 包装的 ParticleSystem 拖进来
    }

    // 你可以在 Inspector 里任意增减、重命名
    public Entry[] entries = new Entry[0];

    // 运行时按名称取粒子
    public ParticleSystem Get(string fxName)
    {
        if (string.IsNullOrEmpty(fxName) || entries == null) return null;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e != null && e.particle && e.name == fxName)
                return e.particle;
        }
        return null;
    }
}


