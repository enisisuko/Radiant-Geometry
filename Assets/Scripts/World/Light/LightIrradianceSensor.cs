using System.Collections;
using UnityEngine;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal; // Light2D
#endif
using FadedDreams.World;                // LightSource2D
using FadedDreams.Player;               // PlayerLightController

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    public class LightIrradianceSensor : MonoBehaviour
    {
        [Header("Sampling Area")]
        [Tooltip("采样半径（世界单位），传感器从此半径内的光源/Light2D 累计光照")]
        public float radius = 2.5f;
        [Tooltip("用于 Physics2D.OverlapCircle 的 LayerMask（留空=全部）")]
        public LayerMask sampleMask = ~0;

        [Header("Normalization")]
        [Tooltip("认为“满格”所需的原始强度。越大则越不容易满格。")]
        public float fullIntensity = 5f;
        [Tooltip("每秒自然衰减（防止抖动），0=不衰减")]
        public float decayPerSecond = 0f;

        [Header("Hysteresis")]
        [Tooltip("达到此比例（0..1）判定为已满格")]
        [Range(0f, 1f)] public float saturateThreshold01 = 1f;
        [Tooltip("跌破此比例（0..1）判定为不满格（迟滞防抖）")]
        [Range(0f, 1f)] public float desaturateThreshold01 = 0.9f;

        [Header("Darkness Fallback (Smooth Return)")]
        [Tooltip("当环境亮度低于阈值并持续一段时间，开始“缓慢回到起点”")]
        public bool returnToOriginOnDark = true;
        [Tooltip("把 Irradiance01 ≤ 此阈值视为“无光”（0..1）")]
        [Range(0f, 1f)] public float darknessThreshold01 = 0.01f;
        [Tooltip("需要连续“无光”的时间（秒），用来防抖")]
        public float darkHoldSeconds = 0.5f;

        [Tooltip("返回位移的目标速度（单位/秒）")]
        public float returnTargetSpeed = 4f;
        [Tooltip("返回位移的加速度（单位/秒²）")]
        public float returnAcceleration = 20f;
        [Tooltip("接近目标距离阈值（单位），小于此即认为到达并对齐到目标点")]
        public float arriveEpsilon = 0.03f;

        [Tooltip("返回过程中如果重新检测到“变亮”，是否取消返回")]
        public bool cancelReturnWhenBright = true;
        [Tooltip("认为“变亮”的阈值（0..1），大于此值则可取消返回")]
        [Range(0f, 1f)] public float cancelBrightThreshold01 = 0.05f;

        [Tooltip("可选：覆写“起点”。若为空则以 Awake 时的位置为起点")]
        public Transform originOverride;

        [Header("Debug")]
        public bool drawGizmo = true;
        public Color gizmoColor = new Color(1, 1, 0, 0.2f);

        // 输出：原始强度与0..1 归一
        public float IrradianceRaw { get; private set; }
        public float Irradiance01 => fullIntensity <= 0f ? 0f : Mathf.Clamp01(IrradianceRaw / fullIntensity);
        public bool IsSaturated { get; private set; }

        // 缓存，避免 GC
        readonly Collider2D[] _hits = new Collider2D[32];

        // —— 回到最初位置：内部状态 ——
        Vector3 _originPos;
        float _darkTimer;
        Rigidbody2D _rb2;

        // 平滑返回状态
        bool _isReturning;
        Vector3 _returnVel;    // 速度向量（由加速度平滑逼近）

        void Awake()
        {
            _originPos = originOverride ? originOverride.position : transform.position;
            _rb2 = GetComponent<Rigidbody2D>();
        }

        /// <summary>运行时更新“起始位置”为当前点（例如关卡中途刷新锚点可调用）</summary>
        public void ResetOriginToHere()
        {
            _originPos = transform.position;
        }

        void Update()
        {
            float raw = SampleIrradiance();

            // 平滑衰减读数（可选）
            if (decayPerSecond > 0f)
            {
                if (raw >= IrradianceRaw) IrradianceRaw = raw;
                else IrradianceRaw = Mathf.Max(0f, IrradianceRaw - decayPerSecond * Time.deltaTime);
            }
            else
            {
                IrradianceRaw = raw;
            }

            // 迟滞判断（满格/不满格）
            float k = Irradiance01;
            if (IsSaturated)
            {
                if (k < desaturateThreshold01) IsSaturated = false;
            }
            else
            {
                if (k >= saturateThreshold01) IsSaturated = true;
            }

            // —— 无光回位判定（仅负责切换“是否开始返回”，具体运动放到 FixedUpdate）——
            if (returnToOriginOnDark)
            {
                bool isDark = k <= darknessThreshold01;

                if (!_isReturning)
                {
                    // 统计“无光”时间
                    _darkTimer = isDark ? (_darkTimer + Time.deltaTime) : 0f;
                    if (_darkTimer >= darkHoldSeconds)
                    {
                        _darkTimer = 0f;
                        BeginSmoothReturn();
                    }
                }
                else
                {
                    // 正在返回：如果“变亮”则可取消返回（可选）
                    if (cancelReturnWhenBright && k > cancelBrightThreshold01)
                    {
                        _isReturning = false;
                        _returnVel = Vector3.zero;
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (!_isReturning) return;

            Vector3 target = originOverride ? originOverride.position : _originPos;
            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            float dist = to.magnitude;

            // 到达：对齐并停止
            if (dist <= arriveEpsilon)
            {
                if (_rb2) _rb2.MovePosition(target);
                else transform.position = target;
                _isReturning = false;
                _returnVel = Vector3.zero;
                return;
            }

            // 期望速度：朝向目标的 returnTargetSpeed
            Vector3 desiredVel = (to / Mathf.Max(dist, 1e-6f)) * Mathf.Max(0f, returnTargetSpeed);
            // 加速度限制
            _returnVel = Vector3.MoveTowards(_returnVel, desiredVel, Mathf.Max(0f, returnAcceleration) * Time.fixedDeltaTime);

            // 位移
            Vector3 next = pos + _returnVel * Time.fixedDeltaTime;
            if (_rb2)
            {
                // 清理角速度（避免漂移）
                _rb2.angularVelocity = 0f;
                _rb2.MovePosition(next);
            }
            else
            {
                transform.position = next;
            }
        }

        void BeginSmoothReturn()
        {
            _isReturning = true;
            // 回位前清零刚体线速度，避免回位时被旧速度拖拽
            if (_rb2)
            {
#if UNITY_6000_0_OR_NEWER
                _rb2.linearVelocity = Vector2.zero;
#else
                _rb2.velocity = Vector2.zero;
#endif
                _rb2.angularVelocity = 0f;
            }
            _returnVel = Vector3.zero;
        }

        float SampleIrradiance()
        {
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _hits, sampleMask);
            float sum = 0f;

            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                if (!col) continue;

                // 1) 场景静态光源（自带强度/反射设置）
                var src = col.GetComponent<LightSource2D>();
                if (src != null)
                {
                    var comp = src.light2DAny;
#if UNITY_RENDERING_UNIVERSAL
                    if (src.light2D) sum += Mathf.Max(0f, src.light2D.intensity);
                    else if (comp) sum += TryGetIntensityViaReflection(comp);
#else
                    if (comp) sum += TryGetIntensityViaReflection(comp);
#endif
                    continue;
                }

                // 2) URP Light2D 直接采样
#if UNITY_RENDERING_UNIVERSAL
                var l2d = col.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                if (l2d) { sum += Mathf.Max(0f, l2d.intensity); continue; }
#endif

                // 3) 玩家本体发光（取最近 Light2D 的 intensity）
                if (col.CompareTag("Player"))
                {
                    sum += EstimatePlayerLight(col.transform);
                }
            }

            return sum;
        }

        float EstimatePlayerLight(Transform player)
        {
            float best = 0f;
#if UNITY_RENDERING_UNIVERSAL
            var lights = player.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
            float bestDist = float.MaxValue;
            foreach (var l in lights)
            {
                float d = (l.transform.position - player.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = Mathf.Max(best, l.intensity); }
            }
#else
            var comps = player.GetComponentsInChildren<Component>(true);
            float bestDist = float.MaxValue;
            foreach (var c in comps)
            {
                if (!c) continue;
                if (c.GetType().Name != "Light2D") continue;
                float d = (c.transform.position - player.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Mathf.Max(best, TryGetIntensityViaReflection(c));
                }
            }
#endif
            return best;
        }

        float TryGetIntensityViaReflection(Component comp)
        {
            if (!comp) return 0f;
            var t = comp.GetType();
            var p = t.GetProperty("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float))
            {
                object v = p.GetValue(comp, null);
                return v is float f ? Mathf.Max(0f, f) : 0f;
            }
            var f0 = t.GetField("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f0 != null && f0.FieldType == typeof(float))
            {
                object v = f0.GetValue(comp);
                return v is float f ? Mathf.Max(0f, f) : 0f;
            }
            return 0f;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
