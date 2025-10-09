using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.UI
{
    [DisallowMultipleComponent]
    public class BracketVisual : MonoBehaviour
    {
        [Header("Refs")]
        public SpriteRenderer shellRenderer;    // ���ſ���
        public SpriteRenderer liquidRenderer;   // Һ�壨�� SpriteMask Ӱ�죩
        public Light2D rimLight;                // ��ѡ����Ե���

        [Header("Appearance")]
        [Range(0f, 1f)] public float shellAlpha = 0.6f;
        public Color redColor = new Color(1f, 0.25f, 0.25f, 1f);
        public Color greenColor = new Color(0.25f, 1f, 0.25f, 1f);
        public string sortingLayerName = "UI";
        public int sortingOrder = 5000;

        [Header("Anim (Fill)")]
        public float fillLerpSpeed = 3.0f;
        public float minFillY = 0.02f;
        private float _targetFill01;            // Ŀ�� 0..1
        private float _currentFill01;           // ��ǰ 0..1

        [Header("Feedback")]
        public float usePulseScale = 1.08f;
        public float usePulseTime = 0.12f;
        public float gainFlashIntensity = 1.4f;
        public float gainFlashTime = 0.15f;

        [Header("Use Highlight (New)")]
        [Tooltip("ʹ��ʱ�ĸ���ʱ��")]
        public float useHighlightTime = 0.18f;
        [Tooltip("ʹ��ʱ��Ե��ǿ�ȵĶ��ⱶ������rimLight�����ϳ������ϵ����ֵ��")]
        public float useHighlightBoost = 1.8f;
        [Tooltip("ʹ��ʱ�Ƿ��Һ����ɫ������������������ϵ����")]
        public bool brightenLiquidOnUse = true;
        [Range(1f, 2.5f)] public float liquidBrightenMul = 1.4f;
        
        [Header("Active Breathing")]
        [Tooltip("呼吸动画速度")]
        public float breathSpeed = 0.8f;
        [Tooltip("呼吸缩放幅度")]
        [Range(0f, 0.2f)] public float breathScaleAmount = 0.05f;
        [Tooltip("呼吸光照强度倍率")]
        [Range(1f, 2f)] public float breathLightMul = 1.3f;

        [Header("FX (Optional)")]
        [Tooltip("ʹ��ʱ���ŵ�������Ч����ѡ������������С�������������/��Ƭ��Ч")]
        public ParticleSystem useFX;
        [Tooltip("��������ʱ���ŵ�������Ч����ѡ��")]
        public ParticleSystem gainFX;
        [Tooltip("FXʵ���ĸ����壬������վ͹��ڱ�������")]
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
        
        // 呼吸效果状态
        private bool _isActive = false;  // 当前是否为激活的颜色模式
        private Vector3 _shellBaseScale;
        private float _breathPhase = 0f;

        private void Awake()
        {
            if (shellRenderer)
            {
                var c = shellRenderer.color; c.a = shellAlpha; shellRenderer.color = c;
                shellRenderer.sortingLayerName = sortingLayerName;
                shellRenderer.sortingOrder = sortingOrder;
                _shellBaseScale = shellRenderer.transform.localScale;
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
                // �ø����ڼ�Ĺ�ǿ��֡˥������һ�㣩
                rimLight.intensity = Mathf.MoveTowards(rimLight.intensity, _lightBaseIntensity, dt * (useHighlightBoost / useHighlightTime));
            }
            
            // 如果是激活状态，应用呼吸效果
            if (_isActive)
            {
                ApplyBreathingEffect(dt);
            }

            if (debugLogs && Time.unscaledTime >= _nextDebugTime)
            {
                _nextDebugTime = Time.unscaledTime + debugInterval;
                Debug.Log($"[BKT] TickFill({name}): cur={_currentFill01:F3}, tgt={_targetFill01:F3}, scaleY={liquidRenderer?.transform.localScale.y}, active={_isActive}");
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
        
        /// <summary>
        /// 设置是否为当前激活的颜色模式（激活时会有呼吸效果）
        /// </summary>
        public void SetActive(bool active)
        {
            if (_isActive != active)
            {
                _isActive = active;
                
                if (!active)
                {
                    // 取消激活时恢复原始缩放和光照
                    ResetBreathingEffect();
                }
                
                if (debugLogs)
                    Debug.Log($"[BKT] SetActive({name}): {active}");
            }
        }
        
        /// <summary>
        /// 应用呼吸效果（缩放+光照）
        /// </summary>
        private void ApplyBreathingEffect(float dt)
        {
            // 更新呼吸相位
            _breathPhase += dt * breathSpeed * Mathf.PI * 2f;
            if (_breathPhase > Mathf.PI * 2f) _breathPhase -= Mathf.PI * 2f;
            
            // 计算呼吸值 (0.5 ~ 1.0，平滑的正弦波)
            float breathValue = 0.5f + 0.5f * Mathf.Sin(_breathPhase);
            
            // 应用到外壳缩放
            if (shellRenderer != null)
            {
                float scaleMultiplier = 1f + (breathScaleAmount * breathValue);
                shellRenderer.transform.localScale = _shellBaseScale * scaleMultiplier;
            }
            
            // 应用到光照强度
            if (rimLight != null)
            {
                float lightMultiplier = Mathf.Lerp(1f, breathLightMul, breathValue);
                rimLight.intensity = _lightBaseIntensity * lightMultiplier;
            }
        }
        
        /// <summary>
        /// 重置呼吸效果到基础状态
        /// </summary>
        private void ResetBreathingEffect()
        {
            if (shellRenderer != null)
            {
                shellRenderer.transform.localScale = _shellBaseScale;
            }
            
            if (rimLight != null)
            {
                rimLight.intensity = _lightBaseIntensity;
            }
            
            _breathPhase = 0f;
        }

        /// <summary>ʹ��ʱ�ġ�����+FX��ͳһ��ڣ��£�</summary>
        public void PlayUseHighlightAndFX()
        {
            if (useHighlightTime > 0f) StartCoroutine(CoUseHighlight());
            if (useFX) SpawnOneShot(useFX);
        }

        /// <summary>��������ʱ��FX������ԭ��FlashGain��ͬʱ֧�ֶ���FX��</summary>
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

            // ���䣺����ʱ�Ŀ�ѡ����
            PlayGainFX();
        }

        private System.Collections.IEnumerator CoUseHighlight()
        {
            _useHighlighting = true;

            // 1) ��Ե��˲ʱ��ǿ
            if (rimLight)
                rimLight.intensity = _lightBaseIntensity * useHighlightBoost;

            // 2) Һ����ɫ������������ѡ��
            Color recover = _liquidBaseColor;
            if (brightenLiquidOnUse && liquidRenderer)
            {
                Color c = _liquidBaseColor;
                // ���������������alpha��
                c.r = Mathf.Min(1f, c.r * liquidBrightenMul);
                c.g = Mathf.Min(1f, c.g * liquidBrightenMul);
                c.b = Mathf.Min(1f, c.b * liquidBrightenMul);
                liquidRenderer.color = c;
            }

            // 3) ʱ��������ָ�
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
