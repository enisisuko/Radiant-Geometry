// Torch.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using FadedDreams.Player; // RedLightController

namespace FadedDreams.World.Light
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RedLightController))]
    public class Torch : MonoBehaviour
    {
        // ========= 可视与音效 =========
        [Header("Visuals")]
        public Light2D light2D;
        public ParticleSystem flameVfx;
        public AudioSource igniteSfx;

        [Tooltip("Light2D 在满红（=100）时的基础强度（1 倍光照）。启动时如为 0 会读取 light2D.intensity")]
        public float baseLightIntensity = 1f;

        // ========= 火把自身红光（=热量槽）=========
        [Header("Torch Red (Self)")]
        [Tooltip("火把自身的红光槽（=热量）——与本体同节点的 RedLightController")]
        public RedLightController selfRed;
        [Tooltip("火把点亮后每秒自然冷却（降到0会熄灭）")]
        public float torchCoolPerSec = 10f;

        // ========= 发光/点燃阈值与爆燃参数 =========
        [Header("Lighting & Ignite Rules")]
        [Tooltip("低于该“红光值”则完全不发光（直接关灯）")]
        public float minLightRedThreshold = 15f;  // 新：最低发光红光值
        [Tooltip("允许被点燃所需的入射光照阈值（建议与激光的光照强度同一量纲 0-100）")]
        public float igniteLightThreshold = 30f;  // 新：点燃所需光照阈值
        [Tooltip("点燃时亮度拉升倍率")]
        public float igniteFlashMultiplier = 2f;
        [Tooltip("点燃时从当前亮度提升到 Flash 倍的时间")]
        public float igniteFlashRiseSeconds = 0.25f;
        [Tooltip("点燃后从 Flash 倍回落到正常亮度的时间")]
        public float igniteFlashFallSeconds = 1f;

        // ========= 范围 / 玩家 / 激光 =========
        [Header("Area & Player & Laser")]
        [Tooltip("回能量的范围（建议放在子物体上，必须为 Trigger）")]
        public Collider2D regenArea;                       // 允许是子物体
        [Tooltip("识别为'激光子物体'的层；用于'需要激光才给玩家回红'的规则")]
        public LayerMask laserLayers;
        [Tooltip("玩家每秒回红速度")]
        public float regenPerSec = 25f;
        [Tooltip("玩家的红光控制器（直接拖拽）")]
        public RedLightController redTarget;

        // ========= 给圈内其他对象回红 =========
        [Header("Affect Others (Inside Area)")]
        [Tooltip("每秒给圈内任何携带 RedLightController 的对象（由 RedTargets 过滤）增加的红光")]
        public float enemyRedPerSec = 15f;
        [Tooltip("会被视作'可回红对象'的层（敌人/机关/其他火把等）")]
        public LayerMask redTargets;

        // ========= 激光点燃判定（可选区域）=========
        [Header("Ignite Hit (Optional)")]
        [Tooltip("激光命中允许点燃的判定区域（不填=任意命中火把即可点燃）")]
        public Collider2D igniteArea;                      // 只用于 OverlapPoint
        [Tooltip("是否必须命中 igniteArea 内才能点燃")]
        public bool requireIgniteArea = false;

        // ========= 规则开关 =========
        [Header("Rules")]
        [Tooltip("给玩家回红时，是否要求'圈内检测到激光子物体'")]
        public bool requireLaserForRegen = false;
        [Tooltip("给玩家回红时，是否要求'玩家必须在圈内'")]
        public bool requirePlayerInside = true;

        // ========= 运行时 =========
        private bool _lit;                    // 是否认为“已点亮”（>= minLightRedThreshold）
        private bool _playerInside;
        private readonly HashSet<Collider2D> _insideLasers = new();
        private readonly HashSet<RedLightController> _insideRedables = new();

        private float _flashMul = 1f;         // 爆燃阶段的临时亮度倍率
        private Coroutine _flashCo;

        private void Reset()
        {
            if (!regenArea) regenArea = GetComponent<Collider2D>();
            if (regenArea) regenArea.isTrigger = true;
        }

        private void Awake()
        {
            if (!selfRed) selfRed = GetComponent<RedLightController>();

            if (light2D)
            {
                if (baseLightIntensity <= 0f) baseLightIntensity = light2D.intensity;
            }

            // 订阅自身红光的“归零/复燃”，统一切视觉（注意：这里的“复燃”仅表状态，不处理爆燃动画）
            if (selfRed)
            {
                if (selfRed.onDepleted == null) selfRed.onDepleted = new UnityEngine.Events.UnityEvent();
                if (selfRed.onRelit == null) selfRed.onRelit = new UnityEngine.Events.UnityEvent();
                selfRed.onDepleted.AddListener(() => ApplyLitVisuals(_lit = false));
                selfRed.onRelit.AddListener(() => ApplyLitVisuals(_lit = true));
            }

            // 初始视觉：按“是否达到最小发光阈值”判定
            _lit = selfRed && selfRed.Current >= minLightRedThreshold;
            ApplyLitVisuals(_lit);

            // RegenArea 事件转发器
            if (regenArea)
            {
                regenArea.isTrigger = true;
                var fwd = regenArea.GetComponent<TorchAreaForwarder>();
                if (!fwd) fwd = regenArea.gameObject.AddComponent<TorchAreaForwarder>();
                fwd.owner = this;
            }
            else
            {
                Debug.LogWarning($"[Torch] {name}: RegenArea 未指定，回能范围不会生效。");
            }

            // IgniteArea optional trigger forwarding: ignite when laserLayers overlap.
            if (igniteArea)
            {
                igniteArea.isTrigger = true;
                var ign = igniteArea.GetComponent<TorchIgniteForwarder>();
                if (!ign) ign = igniteArea.gameObject.AddComponent<TorchIgniteForwarder>();
                ign.owner = this;
            }
        }

        private void Update()
        {
            // 自然冷却
            if (selfRed && torchCoolPerSec > 0f && selfRed.Current > 0f)
                selfRed.TryConsume(torchCoolPerSec * Time.deltaTime);

            // 根据红光值更新“是否点亮”状态：低于阈值直接关灯
            bool nowLit = selfRed && selfRed.Current >= minLightRedThreshold;
            if (nowLit != _lit)
            {
                _lit = nowLit;
                ApplyLitVisuals(_lit);
            }

            // 持续更新 Light 强度（随红光值&爆燃倍率）
            UpdateLightIntensity();

            if (!_lit) return;

            // 给圈内“任何可回红对象”回红（包含敌人、机关、其他火把——前提：其图层被 redTargets 勾选）
            if (_insideRedables.Count > 0 && enemyRedPerSec > 0f)
            {
                float inc = enemyRedPerSec * Time.deltaTime;
                foreach (var r in _insideRedables)
                    if (r) r.Add(inc);
            }

            // 给玩家回红（可选约束：必须在圈内 / 必须检测到激光）
            if (redTarget
                && (!requirePlayerInside || _playerInside)
                && (!requireLaserForRegen || _insideLasers.Count > 0))
            {
                redTarget.Add(regenPerSec * Time.deltaTime);
            }
        }

        // ======== 视觉控制 ========
        private void ApplyLitVisuals(bool lit)
        {
            if (light2D) light2D.enabled = lit;
            if (flameVfx)
            {
                if (lit && !flameVfx.isPlaying) flameVfx.Play();
                else if (!lit && flameVfx.isPlaying) flameVfx.Stop();
            }
            if (lit && igniteSfx) igniteSfx.Play();
        }

        private void UpdateLightIntensity()
        {
            if (!light2D)
                return;

            if (!selfRed || selfRed.Current < minLightRedThreshold)
            {
                light2D.enabled = false;
                return;
            }

            // 红光值 -> [0..1] 的发光比例（15 对应 0，100 对应 1）
            float max = Mathf.Max(1f, selfRed.Max);
            float denom = Mathf.Max(1f, max - minLightRedThreshold);
            float red = Mathf.Clamp(selfRed.Current, 0f, max);
            float t = Mathf.Clamp01((red - minLightRedThreshold) / denom);

            light2D.enabled = true;
            light2D.intensity = baseLightIntensity * t * _flashMul; // 爆燃倍率叠乘
        }

        // ======== 激光命中 → 点燃并给自己加红（与回能范围无关） ========
        // 旧接口：保持兼容（按 100 的入射光处理）
        public void OnLaserFirstHit() => TryIgniteByLaser(default, false, 100f);
        public void OnLaserHitAt(Vector2 hitPoint) => TryIgniteByLaser(hitPoint, true, 100f);

        // 新接口：提供“入射光照”量级（0~100），只有 > igniteLightThreshold 才会点燃
        public void OnLaserHitAtLevel(Vector2 hitPoint, float incomingLight01to100)
            => TryIgniteByLaser(hitPoint, true, incomingLight01to100);

        private void TryIgniteByLaser(Vector2 hitPoint, bool hasPoint, float incomingLight)
        {
            if (!selfRed) selfRed = GetComponent<RedLightController>();

            // 已经亮着就不重复点燃
            if (_lit) return;

            // 命中区域检查（仅用于点燃，不参与回能范围）
            if (requireIgniteArea && igniteArea && hasPoint)
            {
                if (!igniteArea.OverlapPoint(hitPoint)) return;
            }

            // 入射光照阈值
            if (incomingLight <= igniteLightThreshold) return;

            // 点燃：瞬间把红光设为 100
            float targetRed = 100f;
            if (selfRed)
            {
                float max = Mathf.Max(selfRed.Max, targetRed);
                selfRed.Set(Mathf.Min(targetRed, max));
            }

            // 切换成点亮视觉并执行爆燃动画
            _lit = true;
            ApplyLitVisuals(true);
            StartIgniteFlash();
        }

        /// <summary>
        /// 点燃（用于 igniteArea 的触发重叠），不需要精确命中点。
        /// </summary>
        internal void IgniteByOverlap()
        {
            TryIgniteByLaser(default, false, 100f);
        }


        private void StartIgniteFlash()
        {
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(Co_IgniteFlash());
        }

        private IEnumerator Co_IgniteFlash()
        {
            // 从 1 → igniteFlashMultiplier
            float t = 0f;
            float rise = Mathf.Max(0.01f, igniteFlashRiseSeconds);
            while (t < rise)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / rise);
                _flashMul = Mathf.Lerp(1f, Mathf.Max(1f, igniteFlashMultiplier), k);
                UpdateLightIntensity();
                yield return null;
            }

            // 从 igniteFlashMultiplier → 1
            t = 0f;
            float fall = Mathf.Max(0.01f, igniteFlashFallSeconds);
            float startMul = _flashMul;
            while (t < fall)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fall);
                _flashMul = Mathf.Lerp(startMul, 1f, k);
                UpdateLightIntensity();
                yield return null;
            }

            _flashMul = 1f;
            _flashCo = null;
        }

        // ======== 由子物体上的 RegenArea 转发过来的事件 ========
        internal void OnRegenAreaEnter(Collider2D other)
        {
            int l = other.gameObject.layer;

            // 识别“激光子物体”
            if ((laserLayers.value & (1 << l)) != 0)
                _insideLasers.Add(other);

            // 识别“可回红对象”（敌人/机关/火把等——需被 redTargets 勾选）
            if ((redTargets.value & (1 << l)) != 0)
            {
                var r = other.GetComponentInParent<RedLightController>();
                if (r && r != selfRed) _insideRedables.Add(r);
            }

            // 玩家是否在圈内（通过比较你拖进来的 redTarget）
            var pr = other.GetComponentInParent<RedLightController>();
            if (pr && pr == redTarget) _playerInside = true;
        }

        internal void OnRegenAreaExit(Collider2D other)
        {
            _insideLasers.Remove(other);

            int l = other.gameObject.layer;
            if ((redTargets.value & (1 << l)) != 0)
            {
                var r = other.GetComponentInParent<RedLightController>();
                if (r) _insideRedables.Remove(r);
            }

            var pr = other.GetComponentInParent<RedLightController>();
            if (pr && pr == redTarget) _playerInside = false;
        }
    }

    /// <summary>
    /// 安装在“RegenArea 所在的 GameObject”上的事件转发器，把 Trigger 事件转给父上的 Torch。
    /// </summary>

    [AddComponentMenu("")]
    public class TorchIgniteForwarder : MonoBehaviour
    {
        public Torch owner;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!owner) return;
            int l = other.gameObject.layer;
            if ((owner.laserLayers.value & (1 << l)) != 0)
                owner.IgniteByOverlap();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!owner) return;
            int l = other.gameObject.layer;
            if ((owner.laserLayers.value & (1 << l)) != 0)
                owner.IgniteByOverlap();
        }
    }

    [AddComponentMenu("")]
    public class TorchAreaForwarder : MonoBehaviour
    {
        public Torch owner;

        private void OnTriggerEnter2D(Collider2D other) => owner?.OnRegenAreaEnter(other);
        private void OnTriggerExit2D(Collider2D other) => owner?.OnRegenAreaExit(other);
    }
}
