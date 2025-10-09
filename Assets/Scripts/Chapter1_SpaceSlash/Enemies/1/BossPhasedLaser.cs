// BossPhasedLaser.cs — 以玩家为核心跟随 + 硬性 Keep-In-View（左右不出屏）
// 注：为避免 CameraShake2D 重名冲突，显式使用 FadedDreams.CameraFX 版本
using FadedDreams.Enemies;
using FadedDreams.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;   
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
// 显式别名，消除二义性
using CamShake = FadedDreams.CameraFX.CameraShake2D;

namespace FadedDreams.Boss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BossPhasedLaser : MonoBehaviour, IDamageable
    {
        [Header("Refs")]
        public Camera cam;                             // 若空，自动取 Camera.main
        public Transform player;                       // 若空，自动找 tag=Player
        public LaserBeamSegment2D laserPrefab;         // 激光段预制体
        public SpriteRenderer spriteRenderer;          // BOSS主体（用于渐隐渐现）
        public Light2D selfLight;                      // BOSS光源（渐隐时一并淡出）

        [Header("Phase Hit Counts")]
        public int phase1Hits = 10;
        public int phase2Hits = 10;
        public int phase3Hits = 10;

        [Header("Invulnerability / Presentation")]
        public float invulnerableAfterHit = 2f;        // “遁入虚无”的无敌总时长
        public float transformShowSeconds = 2.0f;      // 变身时颜色渐变时长
        public float spawnLeadVfxSeconds = 1.0f;       // 初次出现：先播特效
        public float spawnFadeSeconds = 0.6f;          // 初次出现淡入
        public float vanishFadeSeconds = 0.5f;         // 遁入虚无淡出
        public float appearFadeSeconds = 0.5f;         // 回归淡入

        [Header("Colors")]
        public Color phase1Color = Color.white;
        public Color phase2Color = new Color(1f, .55f, .1f, 1f);
        public Color phase3Color = Color.red;

        [Header("Stage 1 (Random-to-Player)")]
        public float s1MinRadius = 6f;
        public float s1MaxRadius = 12f;
        public float s1ChargeSeconds = 1.0f;
        public float s1LethalSeconds = 0.45f;
        public float s1Interval = 1.2f;
        public float s1Thickness = 0.16f;
        public float s1EnergyDamage = 18f;
        public Color s1ChargeStartColor = Color.white;
        public Color s1ChargeEndColor = new Color(1f, .2f, .2f, 1f);
        public float s1ThickenSeconds = 0.1f;
        public float s1ThickenMul = 2.0f;
        public float s1FadeOutSeconds = 0.4f;
        public float s1KnockupImpulse = 6f;

        [Header("Stage 2 (Screen Sweep)")]
        public int s2BeamsPerWave = 10;
        public float s2WaveInterval = 1.6f;
        public Vector2 s2SpeedRange = new Vector2(6f, 12f);
        public float s2Thickness = 0.14f;
        public float s2EnergyDamage = 15f;
        public float s2LifetimePadding = 0.5f;
        public float s2ChargeSeconds = 0.35f;
        public float s2KnockupImpulse = 6f;
        public float s2FadeOutSeconds = 0.35f;

        [Header("Stage 3 (Chaos + Homing Beam)")]
        public float s3S1IntervalMul = 0.35f;
        public float s3S2IntervalMul = 0.35f;
        public float s3S2SpeedMul = 1.25f;

        [Space(6)]
        public bool s3EnableHomingLaser = true;
        public float s3HomingDuration = 3.0f;
        public float s3HomingCooldown = 1.2f;
        public float s3HomingThickness = 0.18f;
        public float s3HomingDrainPerSecond = 12f;
        public float s3HomingFollowLerp = 4f;
        public float s3HomingMaxLength = 50f;

        [Header("Aggro / Simple AI")]
        public float detectRadius = 20f;
        public float moveSpeed = 3.2f;
        public float keepDistance = 6f;
        public float strafeSpeed = 1.6f;

        [Header("Boss HUD (phase HP bar)")]
        public Vector2 hpBarSize = new Vector2(3.8f, 0.15f);
        public Vector3 hpBarOffset = new Vector3(0, 2.4f, 0);

        [Header("Common VFX (可选)")]
        public GameObject vfxPhaseOut;
        public GameObject vfxPhaseIn;
        public GameObject vfxTransform12;
        public GameObject vfxTransform23;
        public GameObject vfxDeath;

        [Header("Laser Impact / Layers")]
        public GameObject vfxHitGround;
        public LayerMask groundMask = -1;

        [Header("Skill Audio")]
        public AudioClip skillSFX;  // 技能音效（钢琴2）
        [Range(0f, 1f)] public float skillVolume = 0.7f;
        [Range(0f, 0.5f)] public float skillPitchVariation = 0.15f;

        [Header("Scene Transition")]
        public string storySceneOnDeath = "STORY1";
        public float deathDelaySeconds = 3f;
        public float redCurtainFadeIn = 0.6f;
        public float redCurtainFadeOut = 0.6f;
        public float redCurtainHoldAtFull = 0.2f;

        [Header("Camera During Bossfight (Orthographic)")]
        public float camSizeMul = 1.25f;

        [Header("Camera During Bossfight (Perspective)")]
        public bool camUseDolly = true;        // 透视：后退拉远
        public float camBackDistance = 6f;
        public bool camUseFov = false;         // 透视：放大FOV
        public float camFovMul = 1.15f;
        public float camPerspectiveLerp = 4.5f;

        [Header("Camera Bias Toward Boss")]
        public float camBiasTowardBoss = 1.6f;
        public float camBiasLerp = 5f;

        [Header("Camera Composition (Player-first)")]
        public bool camUsePlayerFirstCompose = true;
        [Range(0f, 1f)] public float camPlayerCenterWeight = 1.0f;   // 锚点=玩家
        public float camAnchorLerp = 10f;
        public Vector2 camSoftSizeAtBoss = new Vector2(4.8f, 4.5f);   // 横向略收紧

        [Header("Camera Keep-In-View (Hard Guard)")]
        public bool camHardKeepPlayerInView = true;                  // 开关：玩家永不出框（左右）
        [Range(0f, 0.45f)] public float camHorizontalMargin01 = 0.18f; // 屏幕左右安全边（百分比）
        [Range(0.5f, 1.5f)] public float camGuardPullStrength = 1.0f;  // 拉回强度（1=镜像超出量）

        // 运行态
        private int _phase = 1;
        private int _hitsLeft;
        private bool _invul;
        private bool _dead;
        private bool _attacksEnabled;
        private bool _aggro;
        private readonly List<Coroutine> _attackCors = new List<Coroutine>();
        private Collider2D _col;
        private Rigidbody2D _rb;

        // 可见性/光
        private float _selfLightBase;
        private Color _spawnedBaseColor;

        // 相机缓存
        private float _origCamSize;
        private CameraFollow2D _camFollow;
        private Vector2 _origSoftOffset;
        private Vector2 _origSoftSize;
        private bool _camModified;
        private float _origFov;
        private Vector3 _origCamPos;
        private Vector3 _targetCamPos;

        // 构图用锚点（此版：锚点=玩家）
        private Transform _camAnchor;
        private Transform _origFollowTarget;

        // 简易阶段血条
        private LineRenderer _hpBg, _hpFill;

        void Awake()
        {
            if (!cam) cam = Camera.main;
            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
            _col = GetComponent<Collider2D>(); _col.isTrigger = false;
            _rb = GetComponent<Rigidbody2D>(); _rb.gravityScale = 0f;

            if (spriteRenderer)
            {
                _spawnedBaseColor = spriteRenderer.color;
                var c = _spawnedBaseColor; c.a = 0f;
                spriteRenderer.color = c;
            }
            if (selfLight) { _selfLightBase = selfLight.intensity; selfLight.intensity = 0f; }

            SetPhaseColor(phase1Color);
            _hitsLeft = phase1Hits;

            if (cam)
            {
                _origCamSize = cam.orthographicSize;
                _origFov = cam.fieldOfView;
                _origCamPos = cam.transform.position;
                _targetCamPos = _origCamPos;
                _camFollow = cam.GetComponent<CameraFollow2D>();
                if (_camFollow)
                {
                    _origSoftOffset = _camFollow.softZoneCenterOffset;
                    _origSoftSize = _camFollow.softZoneSize;
                }
            }

            BuildHpBar();
            UpdateHpBarVisual();
        }

        void OnEnable() => StartCoroutine(CoInitialAppear());

        void OnDisable()
        {
            StopAllAttacks();
            RestoreCamera();
        }

        void Update()
        {
            if (_dead) return;

            // 激怒检测
            if (!_aggro && player)
            {
                if (Vector2.Distance(player.position, transform.position) <= detectRadius)
                {
                    EnterAggro();
                }
            }

            // 简易AI
            if (_aggro && player && !_invul)
            {
                Vector2 toPlayer = (player.position - transform.position);
                float d = toPlayer.magnitude;
                Vector2 dir = d > 0.001f ? toPlayer / d : Vector2.zero;
                Vector2 vel = (d > keepDistance) ? dir * moveSpeed : new Vector2(-dir.y, dir.x) * strafeSpeed;
#if UNITY_600_0_OR_NEWER
                _rb.linearVelocity = vel;
#else
                _rb.linearVelocity = vel;
#endif
            }
            else
            {
#if UNITY_600_0_OR_NEWER
                _rb.linearVelocity = Vector2.zero;
#else
                _rb.linearVelocity = Vector2.zero;
#endif
            }

            // 透视相机拉远/FOV插值
            if (_camModified && cam && !cam.orthographic)
            {
                if (camUseDolly)
                    cam.transform.position = Vector3.Lerp(cam.transform.position, _targetCamPos, Time.deltaTime * camPerspectiveLerp);
                if (camUseFov)
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, _origFov * camFovMul, Time.deltaTime * camPerspectiveLerp);
            }

            // 相机构图：玩家优先锚点（锚点=玩家） + 硬性守卫（左右不出屏）
            if (_camModified && _camFollow && player && camUsePlayerFirstCompose && _camAnchor)
            {
                Vector3 p = player.position;
                Vector3 targetAnchor = p; // 默认紧贴玩家

                if (camHardKeepPlayerInView && cam)
                {
                    // 取玩家在相机视口中的位置（0..1）
                    Vector3 vp = cam.WorldToViewportPoint(p);
                    float m = camHorizontalMargin01;

                    if (vp.z > 0f) // 仅在相机前方处理
                    {
                        // 在“玩家的深度与垂直位置”上求左右安全边界的世界坐标
                        Vector3 worldL = cam.ViewportToWorldPoint(new Vector3(m, vp.y, vp.z));
                        Vector3 worldR = cam.ViewportToWorldPoint(new Vector3(1f - m, vp.y, vp.z));

                        if (vp.x < m)
                        {
                            // 玩家越过左界：沿着“玩家→左界”的反方向镜像一点锚点
                            Vector3 delta = worldL - p;                 // 指向左界
                            targetAnchor = p - delta * camGuardPullStrength;
                        }
                        else if (vp.x > 1f - m)
                        {
                            // 玩家越过右界：沿着“玩家→右界”的反方向镜像一点锚点
                            Vector3 delta = worldR - p;                 // 指向右界
                            targetAnchor = p - delta * camGuardPullStrength;
                        }
                    }
                }

                // 平滑更新锚点
                _camAnchor.position = Vector3.Lerp(_camAnchor.position, targetAnchor, Time.deltaTime * camAnchorLerp);

                // 软偏移回归 0，避免额外水平偏置
                _camFollow.softZoneCenterOffset =
                    Vector2.Lerp(_camFollow.softZoneCenterOffset, Vector2.zero, Time.deltaTime * camBiasLerp);
            }

            UpdateHpBarVisual();
        }

        // == 初次出现 ==
        private IEnumerator CoInitialAppear()
        {
            _invul = true; _attacksEnabled = false; _col.enabled = false;

            SpawnVfx(vfxPhaseIn, transform.position);
            yield return new WaitForSeconds(Mathf.Max(0f, spawnLeadVfxSeconds));
            yield return StartCoroutine(FadeVisible(true, spawnFadeSeconds));

            _invul = false; _col.enabled = true;
        }

        private void EnterAggro()
        {
            _aggro = true; _attacksEnabled = true;
            StartPhaseAttacks();
            ApplyCameraForBossfight();

            // 开场轻震（显式使用 CamShake）
            CamShake.Instance?.Shake(0.35f, 0.25f);
        }

        private void StartPhaseAttacks()
        {
            StopAllAttacks();
            if (!_attacksEnabled || _dead) return;

            if (_phase == 1) _attackCors.Add(StartCoroutine(CoPhase1Loop()));
            else if (_phase == 2) _attackCors.Add(StartCoroutine(CoPhase2Loop()));
            else if (_phase == 3)
            {
                _attackCors.Add(StartCoroutine(CoPhase1Loop(true)));
                _attackCors.Add(StartCoroutine(CoPhase2Loop(true)));
                if (s3EnableHomingLaser) _attackCors.Add(StartCoroutine(CoPhase3HomingLaser()));
            }
        }

        private void StopAllAttacks()
        {
            foreach (var c in _attackCors) if (c != null) StopCoroutine(c);
            _attackCors.Clear();
        }

        // == 受击 ==
        public void TakeDamage(float amount)
        {
            if (_dead || _invul) return;

            _hitsLeft = Mathf.Max(0, _hitsLeft - 1);
            UpdateHpBarVisual();

            if (_hitsLeft > 0) StartCoroutine(CoPhaseOutAndBack());
            else
            {
                if (_phase == 1) StartCoroutine(CoTransform(2));
                else if (_phase == 2) StartCoroutine(CoTransform(3));
                else if (_phase == 3) StartCoroutine(CoDeath());
            }
        }

        public bool IsDead { get; private set; }

        private IEnumerator CoPhaseOutAndBack()
        {
            _invul = true; _attacksEnabled = false;
            StopAllAttacks();
            SpawnVfx(vfxPhaseOut, transform.position);

            _col.enabled = false;
            yield return StartCoroutine(FadeVisible(false, vanishFadeSeconds));
            float stay = Mathf.Max(0f, invulnerableAfterHit - vanishFadeSeconds - appearFadeSeconds);
            if (stay > 0f) yield return new WaitForSeconds(stay);

            SpawnVfx(vfxPhaseIn, transform.position);
            yield return StartCoroutine(FadeVisible(true, appearFadeSeconds));
            _col.enabled = true;

            _invul = false; _attacksEnabled = true;
            StartPhaseAttacks();
        }

        private IEnumerator CoTransform(int nextPhase)
        {
            _invul = true; _attacksEnabled = false;
            StopAllAttacks();

            GameObject vfx = (_phase == 1 && nextPhase == 2) ? vfxTransform12 : vfxTransform23;
            SpawnVfx(vfx, transform.position);

            Color target = nextPhase == 2 ? phase2Color : phase3Color;
            yield return StartCoroutine(CoLerpColor(target, transformShowSeconds));

            _phase = nextPhase;
            _hitsLeft = (_phase == 2) ? phase2Hits : phase3Hits;
            UpdateHpBarVisual();

            _invul = false; _attacksEnabled = true;
            StartPhaseAttacks();
        }

        private IEnumerator CoDeath()
        {
            _dead = true; IsDead = true;
            _invul = true; _attacksEnabled = false;
            StopAllAttacks();
            _col.enabled = false;

            if (spriteRenderer) spriteRenderer.enabled = false;
            if (selfLight) selfLight.enabled = false;
            ToggleHpBar(false);

            SpawnVfx(vfxDeath, transform.position);
            yield return new WaitForSeconds(Mathf.Max(0f, deathDelaySeconds));

            bool usedTransition = false;

            var rcType = System.Type.GetType("RedCurtainTransition");
            if (rcType != null)
            {
                var go = rcType.GetMethod("Go", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (go != null)
                {
                    try { go.Invoke(null, new object[] { storySceneOnDeath, redCurtainFadeIn, redCurtainFadeOut, redCurtainHoldAtFull }); usedTransition = true; }
                    catch { }
                }
            }
            if (!usedTransition)
            {
                var fadeType = System.Type.GetType("FadeScreen");
                if (fadeType != null)
                {
                    var fadeMethod = fadeType.GetMethod("FadeOutAndLoad", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (fadeMethod != null)
                    {
                        try
                        {
                            var co = (IEnumerator)fadeMethod.Invoke(null, new object[] { storySceneOnDeath, redCurtainFadeIn, redCurtainFadeOut, redCurtainHoldAtFull });
                            if (co != null) { StartCoroutine(co); usedTransition = true; }
                        }
                        catch { }
                    }
                }
            }
            if (!usedTransition) SceneManager.LoadScene(storySceneOnDeath);

            RestoreCamera();
        }

        // == 攻击（保持不变） ==
        private IEnumerator CoPhase1Loop(bool thirdPhaseParams = false)
        {
            float interval = thirdPhaseParams ? s1Interval * s3S1IntervalMul : s1Interval;
            while (!_dead && _phase != 2)
            {
                if (_attacksEnabled && player && cam && laserPrefab)
                {
                    Vector2 dir = Random.insideUnitCircle.normalized;
                    float r = Random.Range(s1MinRadius, s1MaxRadius);
                    Vector3 a = player.position + (Vector3)(dir * r);
                    Vector3 b = player.position;

                    var sr = GetScreenRectOnPlane(transform.position.z, 3f);
                    Vector3 mid = (a + b) * 0.5f;
                    Vector2 abDir = (b - a).normalized;
                    float dia = Vector3.Distance(sr.bl, sr.tr) + 6f;
                    Vector3 A = mid - (Vector3)abDir * (dia * 0.5f);
                    Vector3 B = mid + (Vector3)abDir * (dia * 0.5f);

                    var laser = Instantiate(laserPrefab);
                    laser.name = "Boss_S1_ChargeBeam";
                    laser.Initialize(A, B, phase1Color, s1Thickness, s1ChargeSeconds, s1LethalSeconds,
                                     s1ChargeSeconds + s1LethalSeconds + s1FadeOutSeconds, false, Vector2.zero);

                    laser.useChargeColorLerp = true;
                    laser.chargeStartColor = s1ChargeStartColor;
                    laser.chargeEndColor = s1ChargeEndColor;
                    laser.thickenOnLethal = true;
                    laser.thickenMul = s1ThickenMul;
                    laser.thickenLerpSeconds = s1ThickenSeconds;
                    laser.fadeOutSeconds = s1FadeOutSeconds;

                    laser.energyDamage = s1EnergyDamage;
                    laser.knockupImpulse = s1KnockupImpulse;
                    laser.continuousDrain = false;

                    // 播放技能音效（钢琴2）
                    PlaySkillSound();

                    // 轻抖
                    CamShake.Instance?.AddTrauma(0.12f);
                }
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator CoPhase2Loop(bool thirdPhaseParams = false)
        {
            float waveInterval = thirdPhaseParams ? s2WaveInterval * s3S2IntervalMul : s2WaveInterval;
            float speedMul = thirdPhaseParams ? s3S2SpeedMul : 1f;

            while (!_dead && _phase >= 2)
            {
                if (_attacksEnabled && cam && laserPrefab)
                {
                    // 播放技能音效（钢琴2）
                    PlaySkillSound();

                    // 强脉冲
                    CamShake.Instance?.OnSweepBlast();

                    float zPlane = transform.position.z;
                    ScreenRect world = GetScreenRectOnPlane(zPlane, 1.2f);
                    for (int i = 0; i < s2BeamsPerWave; i++)
                    {
                        float ang = Random.Range(0f, 180f);
                        Color beamColor = (_phase == 2 ? phase2Color : phase3Color);
                        float spd = Random.Range(s2SpeedRange.x, s2SpeedRange.y) * speedMul;
                        SpawnSweepAcrossScreen(world, ang, spd, s2Thickness, s2EnergyDamage, beamColor, s2ChargeSeconds, s2KnockupImpulse, s2FadeOutSeconds);
                    }
                }
                yield return new WaitForSeconds(waveInterval);
            }
        }

        private IEnumerator CoPhase3HomingLaser()
        {
            while (!_dead && _phase == 3 && s3EnableHomingLaser)
            {
                if (_attacksEnabled && player && laserPrefab)
                {
                    // 播放技能音效（钢琴2）
                    PlaySkillSound();

                    var laser = Instantiate(laserPrefab);
                    laser.name = "Boss_S3_HomingBeam";
                    Vector3 origin = transform.position;
                    Vector3 target = player.position;
                    Vector3 dir = (target - origin).normalized;
                    Vector3 A = origin;
                    Vector3 B = origin + dir * s3HomingMaxLength;

                    laser.Initialize(A, B, phase3Color, s3HomingThickness, 0.3f, 999f,
                                     0.3f + s3HomingDuration + 0.35f, false, Vector2.zero);

                    laser.homing = true;
                    laser.homingOrigin = this.transform;
                    laser.homingTarget = player;
                    laser.homingFollowLerp = s3HomingFollowLerp;
                    laser.maxLength = s3HomingMaxLength;

                    laser.continuousDrain = true;
                    laser.energyDamage = s3HomingDrainPerSecond;
                    laser.fadeOutSeconds = 0.35f;

                    // 仅S3撞地特效
                    laser.vfxHitGround = vfxHitGround;
                    laser.groundMask = groundMask;

                    // 存在期间保持中等幅度的连续抖动
                    StartCoroutine(CoHoldShake(s3HomingDuration, 0.35f));
                }
                yield return new WaitForSeconds(s3HomingDuration + s3HomingCooldown);
            }
        }

        private IEnumerator CoHoldShake(float seconds, float strength)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                CamShake.Instance?.SetContinuous(strength);
                yield return null;
            }
        }

        // == 屏幕矩形工具 ==
        private struct ScreenRect { public Vector3 bl, br, tl, tr; }

        private ScreenRect GetScreenRectOnPlane(float z, float border = 0f)
        {
            var bl = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.orthographic ? 0f : (z - cam.transform.position.z)));
            var br = cam.ViewportToWorldPoint(new Vector3(1, 0, cam.orthographic ? 0f : (z - cam.transform.position.z)));
            var tl = cam.ViewportToWorldPoint(new Vector3(0, 1, cam.orthographic ? 0f : (z - cam.transform.position.z)));
            var tr = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.orthographic ? 0f : (z - cam.transform.position.z)));

            if (cam.orthographic)
            {
                float w = (br - bl).magnitude;
                float h = (tl - bl).magnitude;
                Vector3 center = (bl + tr) * 0.5f;
                bl = center + new Vector3(-w / 2 - border, -h / 2 - border, z);
                br = center + new Vector3(+w / 2 + border, -h / 2 - border, z);
                tl = center + new Vector3(-w / 2 - border, +h / 2 + border, z);
                tr = center + new Vector3(+w / 2 + border, +h / 2 + border, z);
            }
            else
            {
                bl.z = br.z = tl.z = tr.z = z;
                bl += new Vector3(-border, -border, 0);
                br += new Vector3(+border, -border, 0);
                tl += new Vector3(-border, +border, 0);
                tr += new Vector3(+border, +border, 0);
            }
            return new ScreenRect { bl = bl, br = br, tl = tl, tr = tr };
        }

        private void SpawnSweepAcrossScreen(ScreenRect r, float angleDeg, float speed, float thickness, float energyDamage, Color color, float chargeSeconds, float knockupImpulse, float fadeOutSeconds)
        {
            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad)).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x);
            Vector3 center = (r.bl + r.tr) * 0.5f;
            float dia = Vector3.Distance(r.bl, r.tr) + 8f;

            if (player)
            {
                Vector2 toPlayer = (Vector2)(player.position - center);
                if (Vector2.Dot(n, toPlayer) < 0f) n = -n;
            }

            Vector3 startCenter = center - (Vector3)n * (dia * 0.6f);
            Vector3 a = startCenter - (Vector3)dir * (dia * 0.5f);
            Vector3 b = startCenter + (Vector3)dir * (dia * 0.5f);

            var laser = Instantiate(laserPrefab);
            laser.name = "Boss_S2_SweepBeam";
            laser.Initialize(a, b, color, thickness,
                              Mathf.Max(0f, chargeSeconds), 999f,
                              (dia / speed) + s2LifetimePadding + chargeSeconds,
                              true, n * speed);

            laser.energyDamage = energyDamage;
            laser.knockupImpulse = knockupImpulse;
            laser.fadeOutSeconds = fadeOutSeconds;
        }

        // == 可见性/颜色/特效 ==
        private IEnumerator FadeVisible(bool show, float seconds)
        {
            seconds = Mathf.Max(0.001f, seconds);
            float t = 0f;

            Color fromS = spriteRenderer ? spriteRenderer.color : Color.white;
            Color toS = fromS; toS.a = show ? (_spawnedBaseColor.a == 0f ? 1f : _spawnedBaseColor.a) : 0f;

            float fromL = selfLight ? selfLight.intensity : 0f;
            float toL = show ? _selfLightBase : 0f;

            while (t < seconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / seconds);
                if (spriteRenderer) spriteRenderer.color = Color.Lerp(fromS, toS, u);
                if (selfLight) selfLight.intensity = Mathf.Lerp(fromL, toL, u);
                yield return null;
            }
            if (spriteRenderer) spriteRenderer.color = toS;
            if (selfLight) selfLight.intensity = toL;
        }

        private void SetPhaseColor(Color c)
        {
            if (spriteRenderer)
            {
                var sc = spriteRenderer.color;
                sc.r = c.r; sc.g = c.g; sc.b = c.b;
                spriteRenderer.color = sc;
            }
            if (selfLight) selfLight.color = c;
        }

        private IEnumerator CoLerpColor(Color target, float seconds)
        {
            Color fromS = spriteRenderer ? spriteRenderer.color : target;
            Color fromL = selfLight ? selfLight.color : target;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / seconds);
                if (spriteRenderer)
                {
                    var c = Color.Lerp(fromS, target, u);
                    c.a = fromS.a;
                    spriteRenderer.color = c;
                }
                if (selfLight) selfLight.color = Color.Lerp(fromL, target, u);
                yield return null;
            }
            SetPhaseColor(target);
        }

        private void SpawnVfx(GameObject prefab, Vector3 pos)
        {
            if (prefab) Instantiate(prefab, pos, Quaternion.identity);
        }

        // == 简易 HP 条 ==
        private void BuildHpBar()
        {
            GameObject root = new GameObject("BossHPBar");
            root.transform.SetParent(transform, false);

            _hpBg = root.AddComponent<LineRenderer>();
            SetupLR(_hpBg, 9999, new Color(0, 0, 0, 0.5f), hpBarSize.y);

            GameObject fg = new GameObject("Fill");
            fg.transform.SetParent(root.transform, false);
            _hpFill = fg.AddComponent<LineRenderer>();
            SetupLR(_hpFill, 10000, Color.white, hpBarSize.y * 1.15f);
        }

        private void SetupLR(LineRenderer lr, int order, Color c, float width)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 2;
            lr.sortingOrder = order;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            lr.material = mat;

            lr.startWidth = lr.endWidth = width;
        }

        private void ToggleHpBar(bool v)
        {
            if (_hpBg) _hpBg.enabled = v;
            if (_hpFill) _hpFill.enabled = v;
        }

        private void UpdateHpBarVisual()
        {
            if (_hpBg == null || _hpFill == null) return;

            Vector3 center = transform.position + hpBarOffset;
            float half = hpBarSize.x * 0.5f;
            Vector3 left = center + new Vector3(-half, 0, 0);
            Vector3 right = center + new Vector3(+half, 0, 0);

            _hpBg.SetPosition(0, left);
            _hpBg.SetPosition(1, right);

            int total = (_phase == 1) ? phase1Hits : (_phase == 2 ? phase2Hits : phase3Hits);
            float ratio = total <= 0 ? 0f : Mathf.Clamp01(_hitsLeft / (float)total);

            Vector3 fillRight = Vector3.Lerp(left, right, ratio);
            _hpFill.SetPosition(0, left);
            _hpFill.SetPosition(1, fillRight);

            Color c = (_phase == 1) ? phase1Color : (_phase == 2 ? phase2Color : phase3Color);
            if (_hpFill.material && _hpFill.material.HasProperty("_BaseColor"))
                _hpFill.material.SetColor("_BaseColor", c);
        }

        // == 相机处理 ==
        private void ApplyCameraForBossfight()
        {
            if (cam == null) return;

            if (cam.orthographic)
            {
                cam.orthographicSize = _origCamSize * camSizeMul;
            }
            else
            {
                _origFov = cam.fieldOfView;
                _origCamPos = cam.transform.position;
                _targetCamPos = _origCamPos;

                if (camUseDolly)
                    _targetCamPos = _origCamPos - cam.transform.forward * camBackDistance;
                if (camUseFov)
                    cam.fieldOfView = Mathf.Lerp(_origFov, _origFov * camFovMul, 0.5f);
            }

            _camModified = true;

            if (_camFollow)
            {
                if (camUsePlayerFirstCompose)
                {
                    // 锚点=玩家
                    if (_camAnchor == null)
                    {
                        var go = new GameObject("BossCamAnchor");
                        _camAnchor = go.transform;
                        _camAnchor.position = player ? player.position : transform.position;
                    }
                    _origFollowTarget = _camFollow.target;
                    _camFollow.target = _camAnchor;

                    // 横向软区略收紧，保持玩家更居中
                    _camFollow.softZoneSize = camSoftSizeAtBoss;
                }
            }
        }

        private void RestoreCamera()
        {
            if (cam == null) return;

            if (cam.orthographic)
            {
                cam.orthographicSize = _origCamSize;
            }
            else
            {
                if (camUseFov) cam.fieldOfView = _origFov;
                if (camUseDolly) cam.transform.position = _origCamPos;
            }

            if (_camFollow)
            {
                if (camUsePlayerFirstCompose)
                {
                    _camFollow.target = _origFollowTarget;
                    if (_camAnchor) Destroy(_camAnchor.gameObject);
                    _camAnchor = null;
                }
                _camFollow.softZoneCenterOffset = _origSoftOffset;
                _camFollow.softZoneSize = _origSoftSize;
            }
            _camModified = false;
        }

        // == 技能音效播放 ==
        private void PlaySkillSound()
        {
            if (skillSFX)
            {
                GameObject tempGO = new GameObject("TempSkillSFX");
                tempGO.transform.position = transform.position;
                AudioSource tempSource = tempGO.AddComponent<AudioSource>();
                tempSource.clip = skillSFX;
                tempSource.volume = skillVolume;
                tempSource.spatialBlend = 0f;  // 2D音效
                float randomPitch = 1f + Random.Range(-skillPitchVariation, skillPitchVariation);
                tempSource.pitch = randomPitch;
                tempSource.Play();
                Destroy(tempGO, skillSFX.length + 0.1f);
                Debug.Log($"🎹 BOSS技能音效！音调：{randomPitch:F2}");
            }
        }
    }
}
