using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

namespace FadedDreams.Player
{
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerMeleeLaser : MonoBehaviour
    {
        public LayerMask hitMask;

        [Header("Charge Length")]
        public float minLen = 1.2f;
        public float maxLen = 3.2f;
        public float chargeMaxTime = 0.8f;

        [Header("Sweep Attack")]
        public float rotateDuration = 0.35f;
        public float damage = 25f;
        public float hitRadius = 0.2f;
        [Tooltip("扫击角速度曲线：0→1 输入，输出再去插值角度（起手快/中段慢/收尾快）")]
        public AnimationCurve sweepEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Visual (Width by Len)")]
        public AnimationCurve widthByLen = AnimationCurve.Linear(0, 0.12f, 1, 0.25f);
        public float refMinLen = 1f;
        public float refMaxLen = 3.5f;
        public Transform tipTrail;
        public bool autoEnableTrail = true;

        // ===================== 蓄力期 FX =====================
        [Header("Charge FX - General")]
        public bool enableChargeFX = true;
        [Tooltip("光束核心颜色（HDR 推荐）")] public Color coreColor = new Color(1.0f, 0.25f, 0.2f, 1f);
        [Tooltip("外层辉光颜色（HDR 推荐）")] public Color glowColor = new Color(1.0f, 0.4f, 0.0f, 0.9f);
        [Tooltip("蓄力脉冲频率（Hz）")] public float pulseFreq = 4.5f;
        [Tooltip("末段颤动强度（靠近满蓄力提高线抖动幅度）")] public float endJitterStrength = 0.06f;
        [Tooltip("线条抖动采样频率（越大越细碎）")] public float jitterFrequency = 38f;

        [Header("Charge FX - Lights")]
        [Tooltip("是否在手柄和刀尖放置2D点光")] public bool use2DLights = true;
        [Min(0)] public float baseLightMaxIntensity = 2.5f;
        [Min(0)] public float tipLightMaxIntensity = 4.0f;
        [Min(0)] public float baseLightOuterRadius = 1.6f;
        [Min(0)] public float tipLightOuterRadius = 2.4f;

        [Header("Release FX")]
        [Tooltip("松手瞬间爆闪（2D点光）")] public bool releaseFlash = true;
        [Min(0)] public float releaseFlashIntensity = 8f;
        [Min(0.05f)] public float releaseFlashFade = 0.2f;
        [Tooltip("冲击波（圆环LineRenderer）半径/时长")] public float shockwaveMaxRadius = 4.5f;
        public float shockwaveDuration = 0.3f;

        // ===================== 新增：命中粒子 + 击退 =====================
        [Header("On-Hit FX & Knockback")]
        [Tooltip("命中时实例化的粒子预制体，可为空")]
        public ParticleSystem hitParticlePrefab;
        [Tooltip("水平击退力度（Impulse）")]
        public float knockbackForce = 6.5f;
        [Tooltip("向上抬升，避免被推进地形")]
        public float knockbackUpward = 0.8f;
        [Tooltip("粒子大小随蓄力长度的缩放系数")]
        public float particleScaleByCharge = 0.6f;

        // =========================================================

        private LineRenderer _lr;            // 原始核心线
        private LineRenderer _lrGlow1;       // 外层辉光1
        private LineRenderer _lrGlow2;       // 外层辉光2（更宽更淡）
        private PlayerColorModeController _mode;
        private Camera _cam;
        private TrailRenderer _trail;
        private bool _chargingOrAttacking;

        // 蓄力期“能量圈”与灯光
        private LineRenderer _ringLR;
        private Transform _ringRoot;
        private Light2D _baseLight, _tipLight;

        // 命中去重
        private readonly HashSet<FadedDreams.Enemies.IDamageable> _hitSet = new();

        // 抖动随机种
        private float _seedCore, _seedGlow;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _mode = GetComponentInParent<PlayerColorModeController>();
            _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            _lr.enabled = false;
            if (tipTrail) _trail = tipTrail.GetComponent<TrailRenderer>();

            // 生成两层“辉光”线
            _lrGlow1 = CreateChildLR("_Glow1", 0.55f);
            _lrGlow2 = CreateChildLR("_Glow2", 0.85f);

            // 生成“能量圈”
            _ringRoot = new GameObject("ChargeRing").transform;
            _ringRoot.SetParent(transform, false);
            _ringLR = _ringRoot.gameObject.AddComponent<LineRenderer>();
            // （保留注释掉的圆环样式初始化）

            // 2D 灯光
            if (use2DLights)
            {
                _baseLight = CreateLight("ChargeLight_Base", baseLightMaxIntensity, baseLightOuterRadius);
                _tipLight = CreateLight("ChargeLight_Tip", tipLightMaxIntensity, tipLightOuterRadius);
            }

            _seedCore = Random.value * 100f;
            _seedGlow = Random.value * 200f;
        }

        private void Update()
        {
            if (_mode.Mode != ColorMode.Red) return;
            if (Input.GetMouseButtonDown(0) && !_chargingOrAttacking && _mode.TrySpendAttackCost())
                StartCoroutine(CoChargeAndRelease());
        }

        private IEnumerator CoChargeAndRelease()
        {
            _chargingOrAttacking = true;
            _lr.enabled = true;
            _lrGlow1.enabled = _lrGlow2.enabled = enableChargeFX;

            if (tipTrail && autoEnableTrail) { _trail?.Clear(); _trail.enabled = false; } // 蓄力期先关掉，释放时再开
            if (_ringLR) _ringLR.enabled = enableChargeFX;
            SetLightsActive(true);

            float charge = 0f;
            Vector2 lastDir = transform.right;

            // —— 蓄力：指向鼠标，长度随蓄力增长；FX 随进度加强
            while (Input.GetMouseButton(0))
            {
                if (_cam == null) _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                Vector3 mouse = _cam ? _cam.ScreenToWorldPoint(Input.mousePosition) : (Vector3)transform.right + transform.position;
                mouse.z = transform.position.z;
                Vector2 dir = (mouse - transform.position).normalized;
                lastDir = dir;

                charge = Mathf.Min(chargeMaxTime, charge + Time.deltaTime);
                float r = Mathf.Clamp01(charge / chargeMaxTime);
                float len = Mathf.Lerp(minLen, maxLen, r);

                // 末段颤动：靠近满蓄力时加入 Perlin 抖动
                Vector2 jitter = Vector2.zero;
                if (enableChargeFX)
                {
                    float jAmp = endJitterStrength * Mathf.SmoothStep(0, 1, Mathf.Clamp01((r - 0.65f) / 0.35f));
                    float t = Time.time * jitterFrequency;
                    jitter = new Vector2(
                        (Mathf.PerlinNoise(_seedCore, t) - 0.5f),
                        (Mathf.PerlinNoise(_seedGlow, t * 1.3f) - 0.5f)
                    ) * jAmp;
                }

                ApplyLR(dir + jitter, len, r);
                UpdateChargeFX(dir, len, r);
                yield return null;
            }

            // —— 松开：一圈旋转攻击（非匀速），整段判定；释放 FX：爆闪 + 冲击波
            float finalR = Mathf.Clamp01(charge / chargeMaxTime);
            float finalLen = Mathf.Lerp(minLen, maxLen, finalR);

            if (tipTrail && autoEnableTrail) { _trail?.Clear(); _trail.enabled = true; }

            // 爆闪
            if (releaseFlash && use2DLights)
                StartCoroutine(CoReleaseFlash(finalLen));

            // 冲击波环
            if (enableChargeFX && shockwaveDuration > 0.05f && shockwaveMaxRadius > 0.1f)
                StartCoroutine(CoShockwave(transform.position + (Vector3)(lastDir.normalized * finalLen)));

            _hitSet.Clear();
            float tRot = 0f;
            while (tRot < rotateDuration)
            {
                tRot += Time.deltaTime;
                float u = Mathf.Clamp01(tRot / rotateDuration);
                float eased = sweepEase.Evaluate(u);
                float a = Mathf.Lerp(0f, 360f, eased);
                Vector2 dir = Quaternion.Euler(0, 0, a) * lastDir;

                ApplyLR(dir, finalLen, finalR);
                DoHitsAlongShaft(transform.position, dir, finalLen, finalLen); // 传入用于粒子缩放
                UpdateChargeFXDuringRelease(dir, finalLen, u);
                yield return null;
            }

            _lr.enabled = false;
            _lrGlow1.enabled = _lrGlow2.enabled = false;
            if (tipTrail && autoEnableTrail) _trail.enabled = false;
            if (_ringLR) _ringLR.enabled = false;
            SetLightsActive(false);

            _chargingOrAttacking = false;
        }

        // ====== 渲染主/辉光线 ======
        private void ApplyLR(Vector2 dir, float len, float chargeRatio)
        {
            Vector3 p0 = transform.position;
            Vector3 p1 = p0 + (Vector3)(dir.normalized * len);

            // 核心线
            _lr.positionCount = 2;
            _lr.SetPosition(0, p0);
            _lr.SetPosition(1, p1);

            float w = widthByLen.Evaluate(Mathf.InverseLerp(refMinLen, refMaxLen, len));
            // 核心线随脉冲微微呼吸
            float pulse = 1f + 0.25f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseFreq);
            _lr.startWidth = w * Mathf.Lerp(1f, 1.25f, chargeRatio) * pulse;
            _lr.endWidth = _lr.startWidth * 0.92f;

            // 辉光1/2：更宽更淡，颜色取 HDR
            if (enableChargeFX)
            {
                SetLRPair(_lrGlow1, p0, p1, _lr.startWidth * 1.6f, Color.Lerp(coreColor, glowColor, 0.6f), 0.55f + 0.45f * chargeRatio);
                SetLRPair(_lrGlow2, p0, p1, _lr.startWidth * 2.3f, Color.Lerp(coreColor, glowColor, 0.8f), 0.35f + 0.65f * chargeRatio);
            }

            if (tipTrail) tipTrail.position = p1;
            if (_tipLight) _tipLight.transform.position = p1;
        }

        private void SetLRPair(LineRenderer lr, Vector3 p0, Vector3 p1, float width, Color c, float alpha)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);
            lr.startWidth = width;
            lr.endWidth = width * 0.95f;

            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.85f, 1f) }
            );
            lr.colorGradient = g;
        }

        // ====== 蓄力期 FX 更新 ======
        private void UpdateChargeFX(Vector2 dir, float len, float r)
        {
            // 能量圈：半径/透明度/旋转速度随 r 增长
            if (_ringLR)
            {
                float radius = Mathf.Lerp(0.35f, 0.85f, r) * (1f + 0.15f * Mathf.Sin(Time.time * 6f));
                float alpha = Mathf.Lerp(0.25f, 0.9f, r);
                // 这里保留圆环绘制相关代码为注释
                _ringRoot.Rotate(0, 0, Mathf.Lerp(60f, 260f, r) * Time.deltaTime, Space.Self);
            }

            // 灯光：强度/半径随 r 增强
            if (use2DLights)
            {
                if (_baseLight)
                {
                    _baseLight.intensity = Mathf.Lerp(0f, baseLightMaxIntensity, EaseOutCubic(r));
                    _baseLight.pointLightOuterRadius = Mathf.Lerp(baseLightOuterRadius * 0.6f, baseLightOuterRadius, r);
                }
                if (_tipLight)
                {
                    _tipLight.intensity = Mathf.Lerp(0f, tipLightMaxIntensity, EaseOutCubic(r));
                    _tipLight.pointLightOuterRadius = Mathf.Lerp(tipLightOuterRadius * 0.6f, tipLightOuterRadius, r);
                }
            }
        }

        // 释放期收束：圈透明度快速降，灯光稍做拖尾
        private void UpdateChargeFXDuringRelease(Vector2 dir, float len, float u)
        {
            if (_ringLR)
            {
                float a = Mathf.Lerp(0.8f, 0f, EaseInCubic(u));
                // 这里保留圆环绘制更新为注释
            }
            if (use2DLights)
            {
                if (_baseLight) _baseLight.intensity = Mathf.Lerp(_baseLight.intensity, 0f, Time.deltaTime * 12f);
                if (_tipLight) _tipLight.intensity = Mathf.Lerp(_tipLight.intensity, 0f, Time.deltaTime * 12f);
            }
        }

        // ====== 命中判定（加入击退+粒子） ======
        private void DoHitsAlongShaft(Vector3 origin, Vector2 dir, float len, float finalLenForFX = -1f)
        {
            int samples = Mathf.CeilToInt(len / Mathf.Max(0.15f, hitRadius * 0.8f));
            Vector3 step = (Vector3)dir.normalized * (len / samples);
            Vector3 p = origin + step;

            for (int i = 0; i < samples; i++, p += step)
            {
                var cols = Physics2D.OverlapCircleAll(p, hitRadius, hitMask);
                foreach (var c in cols)
                {
                    if (c.attachedRigidbody && c.attachedRigidbody.gameObject == gameObject) continue;

                    var dmg = c.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                    if (dmg != null && !dmg.IsDead && !_hitSet.Contains(dmg))
                    {
                        _hitSet.Add(dmg);
                        dmg.TakeDamage(damage);

                        // ==== NEW: 击退 ====
                        var rb = c.attachedRigidbody ? c.attachedRigidbody : c.GetComponentInParent<Rigidbody2D>();
                        if (rb != null)
                        {
                            Vector2 pushDir = ((Vector2)rb.worldCenterOfMass - (Vector2)origin).normalized;
                            Vector2 impulse = (pushDir * knockbackForce) + (Vector2.up * knockbackUpward);
                            rb.AddForce(impulse, ForceMode2D.Impulse);
                        }

                        // ==== NEW: 命中粒子 ====
                        if (hitParticlePrefab)
                        {
                            var ps = Instantiate(hitParticlePrefab, p, Quaternion.identity);
                            float baseScale = 0.7f;
                            float lenRef = finalLenForFX > 0 ? finalLenForFX : len;
                            float strength = Mathf.Clamp01(lenRef / Mathf.Max(0.001f, maxLen)) * particleScaleByCharge + baseScale;
                            ps.transform.localScale *= strength;
                            ps.transform.right = ((Vector2)p - (Vector2)origin).normalized;
                            ps.Play();
                            Destroy(ps.gameObject, 3f);
                        }
                    }
                }
            }
        }

        // ===================== 工具函数 & 生成器 =====================
        private LineRenderer CreateChildLR(string name, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.enabled = false;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 8;
            lr.numCornerVertices = 4;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingLayerID = _lr.sortingLayerID;
            lr.sortingOrder = _lr.sortingOrder - 1; // 辉光在核心之下
            lr.material = _lr.material;            // 复用你的材质（建议加 Bloom）
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(glowColor, 0f), new GradientColorKey(glowColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) }
            );
            lr.colorGradient = g;
            return lr;
        }

        private Light2D CreateLight(string name, float maxIntensity, float outerRadius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = glowColor;
            l.intensity = 0f;
            l.pointLightOuterRadius = outerRadius * 0.6f;
            l.pointLightInnerRadius = outerRadius * 0.25f;
            l.shadowIntensity = 0f;
            return l;
        }

        private void SetLightsActive(bool on)
        {
            if (!use2DLights) return;
            if (_baseLight) _baseLight.enabled = on;
            if (_tipLight) _tipLight.enabled = on;
        }

        // —— 能量圈（圆环 LineRenderer） 留作可选 ——（初始化/更新代码保持注释）

        private IEnumerator CoShockwave(Vector3 center)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = center;
            var lr = go.AddComponent<LineRenderer>();
            // 初始化圆环样式（留空使用默认）
            lr.sortingOrder = _lr.sortingOrder - 3;
            lr.enabled = true;

            float t = 0f;
            while (t < shockwaveDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / shockwaveDuration);
                float r = Mathf.SmoothStep(0.1f, shockwaveMaxRadius, u);
                float a = 1f - u;
                // 轻微锯齿感：半径上叠加小扰动
                float jag = (Mathf.PerlinNoise(_seedCore, t * 12f) - 0.5f) * 0.12f;
                // 此处留作运行时圆环绘制（已注释）
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator CoReleaseFlash(float finalLen)
        {
            var go = new GameObject("ReleaseFlashLight");
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = Color.white;
            l.intensity = releaseFlashIntensity;
            l.pointLightOuterRadius = Mathf.Max(2f, tipLightOuterRadius);
            l.pointLightInnerRadius = l.pointLightOuterRadius * 0.25f;
            go.transform.position = tipTrail ? tipTrail.position : (transform.position + transform.right * finalLen);

            float t = 0f;
            while (t < releaseFlashFade)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / releaseFlashFade);
                l.intensity = Mathf.Lerp(releaseFlashIntensity, 0f, EaseInCubic(u));
                yield return null;
            }
            Destroy(go);
        }

        // Easing
        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        private static float EaseInCubic(float x) => x * x * x;
    }
}
