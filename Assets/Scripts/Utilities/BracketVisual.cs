using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.UI
{
    [DisallowMultipleComponent]
    public class BracketVisual : MonoBehaviour
    {
        [Header("Refs")]
        public SpriteRenderer shellRenderer;    // 括号壳体
        public SpriteRenderer liquidRenderer;   // 液体（受 SpriteMask 影响）
        public Light2D rimLight;                // 可选：边缘柔光

        [Header("Appearance")]
        [Range(0f, 1f)] public float shellAlpha = 0.6f;
        public Color redColor = new Color(1f, 0.25f, 0.25f, 1f);
        public Color greenColor = new Color(0.25f, 1f, 0.25f, 1f);
        public string sortingLayerName = "UI";
        public int sortingOrder = 5000;

        [Header("Anim (Fill)")]
        public float fillLerpSpeed = 3.0f;
        public float minFillY = 0.02f;
        private float _targetFill01;            // 目标 0..1
        private float _currentFill01;           // 当前 0..1

        [Header("Feedback")]
        public float usePulseScale = 1.08f;
        public float usePulseTime = 0.12f;
        public float gainFlashIntensity = 1.4f;
        public float gainFlashTime = 0.15f;

        [Header("Use Highlight (New)")]
        [Tooltip("使用时的高亮时长")]
        public float useHighlightTime = 0.18f;
        [Tooltip("使用时边缘光强度的额外倍增（在rimLight基础上乘上这个系数峰值）")]
        public float useHighlightBoost = 1.8f;
        [Tooltip("使用时是否把液体颜色短暂提亮（乘上亮度系数）")]
        public bool brightenLiquidOnUse = true;
        [Range(1f, 2.5f)] public float liquidBrightenMul = 1.4f;

        [Header("FX (Optional)")]
        [Tooltip("使用时播放的粒子特效（可选）。建议做个小型向外喷的闪光/碎片特效")]
        public ParticleSystem useFX;
        [Tooltip("增加能量时播放的粒子特效（可选）")]
        public ParticleSystem gainFX;
        [Tooltip("FX实例的父物体，如果留空就挂在本物体下")]
        public Transform fxAnchor;

        [Header("Debug")]
        public bool debugLogs = false;
        public float debugInterval = 0.5f;

        private Vector3 _liquidBaseScale;
        private Color _liquidBaseColor;
        private float _lightBaseIntensity = 1f;
        private bool _pulsing;
        private bool _flashing;
        private bool _useHighlighting;
        private float _nextDebugTime;

        private void Awake()
        {
            if (shellRenderer)
            {
                var c = shellRenderer.color; c.a = shellAlpha; shellRenderer.color = c;
                shellRenderer.sortingLayerName = sortingLayerName;
                shellRenderer.sortingOrder = sortingOrder;
            }
            if (liquidRenderer)
            {
                _liquidBaseScale = liquidRenderer.transform.localScale;
                _liquidBaseColor = liquidRenderer.color;
                liquidRenderer.sortingLayerName = sortingLayerName;
                liquidRenderer.sortingOrder = sortingOrder + 1;
            }
            if (rimLight) _lightBaseIntensity = rimLight.intensity;

            if (debugLogs)
            {
                string liMask = liquidRenderer ? liquidRenderer.maskInteraction.ToString() : "NULL";
                Debug.Log($"[BKT] Awake on {name}: shell={(bool)shellRenderer}, liquid={(bool)liquidRenderer} (mask={liMask}), light={(bool)rimLight}");
            }
        }

        public void ConfigureSide(bool isRedSide)
        {
            Color liquid = isRedSide ? redColor : greenColor;
            if (liquidRenderer) { liquidRenderer.color = liquid; _liquidBaseColor = liquid; }
            if (rimLight) rimLight.color = liquid;

            if (debugLogs)
                Debug.Log($"[BKT] ConfigureSide({(isRedSide ? "RED" : "GREEN")}): color={liquid}");
        }

        public void SetTargetFill01(float v)
        {
            _targetFill01 = Mathf.Clamp01(v);
            if (debugLogs)
                Debug.Log($"[BKT] SetTargetFill01({name}): target={_targetFill01:F3}");
        }

        public void TickFill(float dt)
        {
            _currentFill01 = Mathf.MoveTowards(_currentFill01, _targetFill01, fillLerpSpeed * dt);
            ApplyFill();

            if (_useHighlighting && rimLight)
            {
                // 让高亮期间的光强逐帧衰减（稳一点）
                rimLight.intensity = Mathf.MoveTowards(rimLight.intensity, _lightBaseIntensity, dt * (useHighlightBoost / useHighlightTime));
            }

            if (debugLogs && Time.unscaledTime >= _nextDebugTime)
            {
                _nextDebugTime = Time.unscaledTime + debugInterval;
                Debug.Log($"[BKT] TickFill({name}): cur={_currentFill01:F3}, tgt={_targetFill01:F3}, scaleY={liquidRenderer?.transform.localScale.y}");
            }
        }

        private void ApplyFill()
        {
            if (!liquidRenderer)
            {
                if (debugLogs) Debug.LogWarning($"[BKT] ApplyFill: liquidRenderer is NULL on {name}");
                return;
            }

            float y = Mathf.Lerp(minFillY, 1f, _currentFill01);
            var s = _liquidBaseScale;
            s.y = y * _liquidBaseScale.y;
            liquidRenderer.transform.localScale = s;
        }

        public void PulseUse() { if (!_pulsing) StartCoroutine(CoPulse()); }
        public void FlashGain() { if (!_flashing) StartCoroutine(CoFlash()); }

        /// <summary>使用时的“高亮+FX”统一入口（新）</summary>
        public void PlayUseHighlightAndFX()
        {
            if (useHighlightTime > 0f) StartCoroutine(CoUseHighlight());
            if (useFX) SpawnOneShot(useFX);
        }

        /// <summary>增加能量时的FX（保留原先FlashGain，同时支持额外FX）</summary>
        public void PlayGainFX()
        {
            if (gainFX) SpawnOneShot(gainFX);
        }

        private System.Collections.IEnumerator CoPulse()
        {
            _pulsing = true;
            float t = 0f;
            var tr = liquidRenderer ? liquidRenderer.transform : null;
            Vector3 baseScale = tr ? tr.localScale : Vector3.one;
            while (t < usePulseTime)
            {
                float k = 1f + (usePulseScale - 1f) * Mathf.Sin((t / usePulseTime) * Mathf.PI);
                if (tr) tr.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z);
                t += Time.deltaTime;
                yield return null;
            }
            if (tr) tr.localScale = baseScale;
            _pulsing = false;
        }

        private System.Collections.IEnumerator CoFlash()
        {
            _flashing = true;
            float t = 0f;
            while (t < gainFlashTime)
            {
                float k = Mathf.Lerp(gainFlashIntensity, 1f, t / gainFlashTime);
                if (rimLight) rimLight.intensity = _lightBaseIntensity * k;
                t += Time.deltaTime;
                yield return null;
            }
            if (rimLight) rimLight.intensity = _lightBaseIntensity;
            _flashing = false;

            // 补充：增益时的可选粒子
            PlayGainFX();
        }

        private System.Collections.IEnumerator CoUseHighlight()
        {
            _useHighlighting = true;

            // 1) 边缘光瞬时加强
            if (rimLight)
                rimLight.intensity = _lightBaseIntensity * useHighlightBoost;

            // 2) 液体颜色短暂提亮（可选）
            Color recover = _liquidBaseColor;
            if (brightenLiquidOnUse && liquidRenderer)
            {
                Color c = _liquidBaseColor;
                // 线性提亮（避免改alpha）
                c.r = Mathf.Min(1f, c.r * liquidBrightenMul);
                c.g = Mathf.Min(1f, c.g * liquidBrightenMul);
                c.b = Mathf.Min(1f, c.b * liquidBrightenMul);
                liquidRenderer.color = c;
            }

            // 3) 时长结束后恢复
            float t = 0f;
            while (t < useHighlightTime)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (rimLight) rimLight.intensity = _lightBaseIntensity;
            if (brightenLiquidOnUse && liquidRenderer) liquidRenderer.color = recover;

            _useHighlighting = false;
        }

        private void SpawnOneShot(ParticleSystem prefab)
        {
            if (!prefab) return;
            Transform parent = fxAnchor ? fxAnchor : transform;
            var ps = Instantiate(prefab, parent.position, parent.rotation, parent);
            ps.Play(true);
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 0.2f);
        }
    }
}
