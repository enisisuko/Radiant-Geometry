using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

namespace FadedDreams.Player
{
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerRangedCharger : MonoBehaviour
    {
        [Header("Targeting")]
        public LayerMask enemyMask;          // 敌人层（用于找最近目标 & 爆炸结算）
        public LayerMask raycastMask;        // 光束的射线检测（地形/敌人等）

        [Header("Charge & Beam")]
        public float maxChargeTime = 1.0f;   // 蓄力上限时间
        public float widthMin = 0.1f;
        public float widthMax = 0.5f;
        public float baseDamage = 15f;
        public float damageAtMax = 45f;
        public float extraEnergyCostAtMax = 10f; // 额外能量消耗（基础消耗在控制器里）
        public float selfKnockbackForce = 8f;
        public float maxBeamDistance = 50f;

        [Header("VFX: Gameplay Explosion")]
        public GameObject explosionPrefab;
        public float explosionRadius = 2.2f;
        public float explosionDamage = 35f;

        [Header("Preview Line")]
        public LineRenderer previewLR;       // 预览线（建议 Unlit/Additive 材质开启 Bloom）
        public float previewWidth = 0.06f;
        public Color previewColorFrom = new Color(0.4f, 1f, 0.4f, 0.55f);
        public Color previewColorTo = new Color(0.9f, 1f, 0.6f, 0.95f);

        // ====== 新增：炫酷特效（无资源） ======
        [Header("FX: Charge Beam Layers")]
        public bool enableChargeFX = true;
        [Tooltip("核心色（HDR 推荐）")] public Color coreColor = new Color(0.7f, 1.0f, 0.8f, 1f);
        [Tooltip("辉光色（HDR 推荐）")] public Color glowColor = new Color(0.2f, 1.0f, 0.6f, 0.9f);
        [Tooltip("蓄力脉冲频率（Hz）")] public float pulseFreq = 4.2f;
        [Tooltip("末段颤动强度（接近满蓄力时抖动更明显）")] public float endJitterStrength = 0.05f;
        [Tooltip("颤动采样频率")] public float jitterFrequency = 36f;

        [Header("FX: Lights")]
        public bool use2DLights = true;
        public float baseLightMaxIntensity = 2.8f;
        public float tipLightMaxIntensity = 4.5f;
        public float baseLightOuterRadius = 1.7f;
        public float tipLightOuterRadius = 2.6f;

        [Header("FX: On Fire")]
        public bool releaseFlash = true;               // 发射瞬间爆闪
        public float releaseFlashIntensity = 8f;
        public float releaseFlashFade = 0.18f;
        public bool addAfterimage = true;              // 残影
        public int afterimageCount = 3;
        public float afterimageSpacing = 0.015f;
        public float afterimageFade = 0.18f;
        public bool cameraNudge = true;                // 轻微震屏（若你有 CameraShake2D 也会尝试调用）
        public float cameraNudgeStrength = 0.15f;

        // =========================================================

        private PlayerColorModeController _mode;
        private LineRenderer _shotLR;   // 主光束（发射瞬间/极短显示）
        private LineRenderer _glow1;    // 外层辉光 1
        private LineRenderer _glow2;    // 外层辉光 2（更宽更淡）
        private Camera _cam;
        private Light2D _baseLight, _tipLight;
        private float _seedA, _seedB;

        private void Awake()
        {
            _mode = GetComponentInParent<PlayerColorModeController>();
            _shotLR = GetComponent<LineRenderer>();
            _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            _shotLR.enabled = false;

            // 预览线
            if (previewLR)
            {
                previewLR.enabled = false;
                previewLR.startWidth = previewWidth;
                previewLR.endWidth = previewWidth;
            }

            // 两层辉光线（共用主材质即可）
            _glow1 = CreateChildLR("_Glow1");
            _glow2 = CreateChildLR("_Glow2");

            // 2D 灯光
            if (use2DLights)
            {
                _baseLight = CreateLight("ChargeLight_Base", baseLightMaxIntensity, baseLightOuterRadius);
                _tipLight = CreateLight("ChargeLight_Tip", tipLightMaxIntensity, tipLightOuterRadius);
            }

            _seedA = Random.value * 100f;
            _seedB = Random.value * 200f;
        }

        private void Update()
        {
            if (_mode.Mode != ColorMode.Green) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (_mode.TrySpendAttackCost())
                    StartCoroutine(CoChargePreviewAndShootAutoLock());
            }
        }

        private IEnumerator CoChargePreviewAndShootAutoLock()
        {
            float charge = 0f;
            if (previewLR) previewLR.enabled = true;
            SetLightsActive(true);

            Transform target = null;

            // ========== 蓄力阶段：自动锁最近敌，预览线渐变、脉冲、末段颤动 ==========
            while (Input.GetMouseButton(0))
            {
                charge = Mathf.Min(maxChargeTime, charge + Time.deltaTime);
                float r = Mathf.Clamp01(charge / maxChargeTime);

                // 目标/起止点
                target = FindNearestEnemy();
                Vector3 start = transform.position;
                Vector3 end;

                if (target)
                {
                    Vector2 dirToTarget = (target.position - start).normalized;
                    RaycastHit2D hit = Physics2D.Raycast(start, dirToTarget, maxBeamDistance, raycastMask);
                    end = hit.collider ? (Vector3)hit.point : start + (Vector3)dirToTarget * maxBeamDistance;
                }
                else
                {
                    if (_cam == null) _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                    Vector3 sp = _cam.WorldToScreenPoint(start);
                    Vector3 mouse = _cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, sp.z));
                    Vector2 dirToMouse = (mouse - start).normalized;

                    RaycastHit2D hit = Physics2D.Raycast(start, dirToMouse, maxBeamDistance, raycastMask);
                    end = hit.collider ? (Vector3)hit.point : start + (Vector3)dirToMouse * maxBeamDistance;
                }

                // 末段颤动（仅预览线末端抖动）
                Vector2 jitter = Vector2.zero;
                if (enableChargeFX)
                {
                    float amp = endJitterStrength * Mathf.SmoothStep(0, 1, Mathf.Clamp01((r - 0.6f) / 0.4f));
                    float t = Time.time * jitterFrequency;
                    jitter = new Vector2(
                        (Mathf.PerlinNoise(_seedA, t) - 0.5f),
                        (Mathf.PerlinNoise(_seedB, t * 1.2f) - 0.5f)
                    ) * amp;
                }
                end += (Vector3)jitter;

                // 预览线：颜色/透明度/宽度随 r 增强 + 脉冲呼吸
                if (previewLR)
                {
                    previewLR.positionCount = 2;
                    previewLR.SetPosition(0, start);
                    previewLR.SetPosition(1, end);

                    float pulse = 1f + 0.22f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseFreq);
                    float w = previewWidth * Mathf.Lerp(1f, 2.1f, r) * pulse;
                    previewLR.startWidth = w;
                    previewLR.endWidth = w * 0.92f;

                    Color c = Color.Lerp(previewColorFrom, previewColorTo, r);
                    var g = new Gradient();
                    g.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(Mathf.Lerp(0.35f, 0.95f, r), 0f), new GradientAlphaKey(Mathf.Lerp(0.25f, 0.8f, r), 1f) }
                    );
                    previewLR.colorGradient = g;
                }

                // 辉光层：跟随预览线构型，提前“点亮”感觉
                if (enableChargeFX)
                {
                    ApplyGlowBeams(start, end, r);
                    if (_tipLight) _tipLight.transform.position = end;
                }

                // 灯光强度随 r 增强
                UpdateChargeLights(r);

                yield return null;
            }

            if (previewLR) previewLR.enabled = false;
            SetGlowActive(false);
            SetLightsActive(false);

            // ========== 发射：计算宽度/伤害/能量；瞬间主光束 + 残影 + 爆闪 ==========
            float ratio = Mathf.Clamp01(charge / maxChargeTime);
            float width = Mathf.Lerp(widthMin, widthMax, ratio);
            float dmg = Mathf.Lerp(baseDamage, damageAtMax, ratio);

            float extra = ratio * extraEnergyCostAtMax;
            if (!_mode.SpendEnergy(ColorMode.Green, extra))
            {
                ratio *= 0.6f; width = Mathf.Lerp(widthMin, widthMax, ratio);
                dmg = Mathf.Lerp(baseDamage, damageAtMax, ratio);
            }

            // 发射方向：有敌人→向敌；无敌人→向鼠标
            Vector3 S = transform.position;
            Vector2 D;
            var tgt = FindNearestEnemy();
            if (tgt) D = ((Vector3)tgt.position - S).normalized;
            else
            {
                if (_cam == null) _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                Vector3 sp = _cam.WorldToScreenPoint(S);
                Vector3 mouse = _cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, sp.z));
                D = (mouse - S).normalized;
            }
            RaycastHit2D H = Physics2D.Raycast(S, D, maxBeamDistance, raycastMask);
            Vector3 E = H.collider ? (Vector3)H.point : S + (Vector3)D * maxBeamDistance;

            // 爆闪
            if (releaseFlash && use2DLights) StartCoroutine(CoReleaseFlash(E));

            // 残影
            if (addAfterimage) StartCoroutine(CoBeamAfterimages(S, E, width));

            // 显示一帧主光束
            yield return StartCoroutine(FlashBeamOnce(S, E, width));

            // 命中/爆炸/击退/镜头
            DoHitAndExplosion(E, dmg);
            if (width > 0.2f)
            {
                var rb = GetComponentInParent<Rigidbody2D>();
                if (rb) rb.AddForce(-D * selfKnockbackForce, ForceMode2D.Impulse);
            }
            if (cameraNudge) TryCameraShake();
        }

        private Transform FindNearestEnemy()
        {
            float radius = 30f;
            var cols = Physics2D.OverlapCircleAll(transform.position, radius, enemyMask);
            if (cols == null || cols.Length == 0) return null;

            float best = float.MaxValue; Transform pick = null;
            foreach (var c in cols)
            {
                float d = Vector2.SqrMagnitude(c.transform.position - transform.position);
                if (d < best) { best = d; pick = c.transform; }
            }
            return pick;
        }

        private IEnumerator FlashBeamOnce(Vector3 a, Vector3 b, float width)
        {
            _shotLR.enabled = true;
            _shotLR.positionCount = 2;
            _shotLR.SetPosition(0, a);
            _shotLR.SetPosition(1, b);

            // 主线用核心色 + 轻微呼吸
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseFreq);
            float w = width * pulse;
            _shotLR.startWidth = w;
            _shotLR.endWidth = w * 0.92f;

            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(coreColor, 0f), new GradientColorKey(coreColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 1f) }
            );
            _shotLR.colorGradient = g;

            yield return null; // 保持1帧
            _shotLR.enabled = false;
        }

        private void DoHitAndExplosion(Vector3 center, float directDamage)
        {
            // 直伤
            var hitCols = Physics2D.OverlapCircleAll(center, 0.15f, enemyMask);
            foreach (var c in hitCols)
            {
                var d = c.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                if (d != null && !d.IsDead) d.TakeDamage(directDamage);
            }

            // AoE
            if (explosionPrefab) { var fx = Object.Instantiate(explosionPrefab, center, Quaternion.identity); Object.Destroy(fx, 3f); }

            var cols = Physics2D.OverlapCircleAll(center, explosionRadius, enemyMask);
            foreach (var c in cols)
            {
                var d = c.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                if (d != null && !d.IsDead) d.TakeDamage(explosionDamage);
                var rb = c.attachedRigidbody;
                if (rb) rb.AddForce((rb.worldCenterOfMass - (Vector2)center).normalized * 8f, ForceMode2D.Impulse);
            }
        }

        // ========= 炫酷层：两条辉光线（随蓄力进度而变化） =========
        private void ApplyGlowBeams(Vector3 a, Vector3 b, float r)
        {
            if (!enableChargeFX) { SetGlowActive(false); return; }
            SetGlowActive(true);

            float pulse = 1f + 0.22f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseFreq);
            float coreW = Mathf.Lerp(0.08f, 0.18f, r) * pulse;

            // Glow1
            _glow1.positionCount = 2;
            _glow1.SetPosition(0, a);
            _glow1.SetPosition(1, b);
            _glow1.startWidth = coreW * 2.1f;
            _glow1.endWidth = _glow1.startWidth * 0.95f;
            SetLRColor(_glow1, Color.Lerp(coreColor, glowColor, 0.6f), Mathf.Lerp(0.45f, 0.85f, r));

            // Glow2（更宽更淡）
            _glow2.positionCount = 2;
            _glow2.SetPosition(0, a);
            _glow2.SetPosition(1, b);
            _glow2.startWidth = coreW * 3.1f;
            _glow2.endWidth = _glow2.startWidth * 0.95f;
            SetLRColor(_glow2, Color.Lerp(coreColor, glowColor, 0.85f), Mathf.Lerp(0.25f, 0.65f, r));
        }

        private void SetLRColor(LineRenderer lr, Color c, float alpha)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.85f, 1f) }
            );
            lr.colorGradient = g;
        }

        private void SetGlowActive(bool on)
        {
            if (_glow1) _glow1.enabled = on;
            if (_glow2) _glow2.enabled = on;
        }

        // ========= 灯光/爆闪 =========
        private Light2D CreateLight(string name, float maxIntensity, float outerRadius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = glowColor;
            l.intensity = 0f;
            l.pointLightOuterRadius = outerRadius * 0.7f;
            l.pointLightInnerRadius = outerRadius * 0.28f;
            l.shadowIntensity = 0f;
            return l;
        }

        private void SetLightsActive(bool on)
        {
            if (!use2DLights) return;
            if (_baseLight) _baseLight.enabled = on;
            if (_tipLight) _tipLight.enabled = on;
        }

        private void UpdateChargeLights(float r)
        {
            if (!use2DLights) return;
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

        private IEnumerator CoReleaseFlash(Vector3 pos)
        {
            var go = new GameObject("ReleaseFlashLight");
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = Color.white;
            l.intensity = releaseFlashIntensity;
            l.pointLightOuterRadius = Mathf.Max(2f, tipLightOuterRadius);
            l.pointLightInnerRadius = l.pointLightOuterRadius * 0.25f;
            go.transform.position = pos;

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

        // ========= 残影 =========
        private IEnumerator CoBeamAfterimages(Vector3 a, Vector3 b, float width)
        {
            for (int i = 0; i < afterimageCount; i++)
            {
                var lr = CreateTempLR("BeamAfterimage");
                lr.positionCount = 2;
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);
                lr.startWidth = width * Mathf.Lerp(1f, 1.8f, i / (float)afterimageCount);
                lr.endWidth = lr.startWidth * 0.92f;
                SetLRColor(lr, Color.Lerp(coreColor, glowColor, 0.7f), Mathf.Lerp(0.6f, 0.2f, i / (float)afterimageCount));

                float life = afterimageFade + i * afterimageSpacing;
                StartCoroutine(FadeAndKill(lr.gameObject, life));
                yield return new WaitForSeconds(afterimageSpacing);
            }
        }

        private IEnumerator FadeAndKill(GameObject go, float life)
        {
            var lr = go.GetComponent<LineRenderer>();
            float t = 0f;
            Gradient g0 = lr.colorGradient;
            while (t < life)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / life);
                // 线性淡出 Alpha
                var keys = g0.alphaKeys;
                for (int k = 0; k < keys.Length; k++)
                {
                    keys[k].alpha = Mathf.Lerp(keys[k].alpha, 0f, u);
                }
                var g = new Gradient();
                g.SetKeys(g0.colorKeys, keys);
                lr.colorGradient = g;
                yield return null;
            }
            Destroy(go);
        }

        private LineRenderer CreateChildLR(string name)
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
            lr.sortingLayerID = _shotLR.sortingLayerID;
            lr.sortingOrder = _shotLR.sortingOrder - 1; // 在主线之下
            lr.material = _shotLR.material;
            return lr;
        }

        private LineRenderer CreateTempLR(string name)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 8;
            lr.numCornerVertices = 4;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingLayerID = _shotLR.sortingLayerID;
            lr.sortingOrder = _shotLR.sortingOrder - 2; // 残影更靠下
            lr.material = _shotLR.material;
            return lr;
        }

        private void TryCameraShake()
        {
            // 若场景里有 CameraShake2D（我之前在 EnemyHealth 里给过），调用它
            var type = System.Type.GetType("FadedDreams.Enemies.CameraShake2D, Assembly-CSharp");
            if (type != null)
            {
                var m = type.GetMethod("Shake", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m != null) { m.Invoke(null, new object[] { 0.12f, 0.4f, 28f }); return; }
            }
            // 没有的话做个极简 Nudge
            if (_cam) _cam.transform.position += (Vector3)(Random.insideUnitCircle * cameraNudgeStrength * 0.5f);
        }

        // Easing
        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        private static float EaseInCubic(float x) => x * x * x;
    }
}
