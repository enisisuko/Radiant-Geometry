// BossC3_HealthRingUI.cs
// 血条环UI - 负责环形血条的渲染、血量显示更新和视觉效果
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using UnityEngine;
using FD.Bosses.C3;

namespace FD.Bosses.C3
{
    /// <summary>
    /// BossC3血条环UI - 负责环形血条的渲染、血量显示更新和视觉效果
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC3_HealthRingUI : MonoBehaviour
    {
        [Header("== Health Ring Settings ==")]
        public bool autoCreateRing = true;
        public float ringRadius = 2.9f;
        public float ringWidth = 0.18f;
        public int ringSegments = 64;

        [Header("== Ring Materials ==")]
        public Material ringMaterial;
        public Material bossRingMaterial;

        [Header("== Ring Gradients ==")]
        public Gradient ringGradientP1;
        public Gradient ringGradientP2;

        [Header("== Visual Effects ==")]
        public float flashDuration = 0.2f;
        public Color flashColor = Color.white;
        public float flashIntensity = 2f;
        public AnimationCurve flashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== Animation ==")]
        public bool enablePulseAnimation = true;
        public float pulseSpeed = 2f;
        public float pulseIntensity = 0.1f;
        public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 组件引用
        private BossC3_CombatSystem combatSystem;
        private BossC3_PhaseManager phaseManager;
        private StellarRing _ring;

        // 渲染组件
        private LineRenderer _lineRenderer;
        private MeshRenderer _meshRenderer;

        // 动画状态
        private Coroutine _flashCoroutine;
        private Coroutine _pulseCoroutine;
        private bool _isFlashing = false;
        private bool _isPulsing = false;

        // 原始设置
        private float _originalRadius;
        private Color _originalColor;

        // 事件
        public event Action OnRingCreated;
        public event Action OnRingDestroyed;
        public event Action OnFlashStarted;
        public event Action OnFlashCompleted;

        #region Unity Lifecycle

        private void Awake()
        {
            combatSystem = GetComponent<BossC3_CombatSystem>();
            phaseManager = GetComponent<BossC3_PhaseManager>();

            _originalRadius = ringRadius;
        }

        private void Start()
        {
            if (autoCreateRing)
            {
                CreateHealthRing();
            }

            // 订阅事件
            if (combatSystem != null)
            {
                combatSystem.OnHealthChanged += UpdateHealthRing;
                combatSystem.OnDamageTaken += OnDamageTaken;
                combatSystem.OnPhase2Triggered += OnPhase2Triggered;
            }

            if (phaseManager != null)
            {
                phaseManager.OnPhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (combatSystem != null)
            {
                combatSystem.OnHealthChanged -= UpdateHealthRing;
                combatSystem.OnDamageTaken -= OnDamageTaken;
                combatSystem.OnPhase2Triggered -= OnPhase2Triggered;
            }

            if (phaseManager != null)
            {
                phaseManager.OnPhaseChanged -= OnPhaseChanged;
            }
        }

        private void Update()
        {
            UpdatePulseAnimation();
        }

        #endregion

        #region Ring Creation

        /// <summary>
        /// 创建血条环
        /// </summary>
        public void CreateHealthRing()
        {
            if (_ring != null)
            {
                DestroyHealthRing();
            }

            // 创建StellarRing组件
            _ring = gameObject.AddComponent<StellarRing>();
            _ring.Setup(ringRadius, ringWidth, ringSegments);

            // 设置材质
            if (ringMaterial != null)
            {
                _ring.SetMaterial(ringMaterial);
            }

            // 初始化血条
            UpdateHealthRing(combatSystem.GetCurrentHealth(), combatSystem.GetMaxHealth());

            OnRingCreated?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC3_HealthRingUI] Health ring created");
        }

        /// <summary>
        /// 销毁血条环
        /// </summary>
        public void DestroyHealthRing()
        {
            if (_ring != null)
            {
                Destroy(_ring);
                _ring = null;
            }

            OnRingDestroyed?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC3_HealthRingUI] Health ring destroyed");
        }

        #endregion

        #region Health Updates

        /// <summary>
        /// 更新血条环
        /// </summary>
        private void UpdateHealthRing(float currentHP, float maxHP)
        {
            if (_ring == null) return;

            float healthPercentage = maxHP > 0f ? currentHP / maxHP : 0f;
            
            // 更新血条填充
            _ring.SetFillAmount(healthPercentage);

            // 更新颜色
            UpdateRingColor(healthPercentage);

            if (verboseLogs)
                Debug.Log($"[BossC3_HealthRingUI] Health ring updated: {healthPercentage:P1}");
        }

        /// <summary>
        /// 更新环颜色
        /// </summary>
        private void UpdateRingColor(float healthPercentage)
        {
            if (_ring == null) return;

            Phase currentPhase = phaseManager.GetCurrentPhase();
            Gradient gradient = (currentPhase == Phase.P1) ? ringGradientP1 : ringGradientP2;

            Color ringColor = gradient.Evaluate(healthPercentage);
            _ring.SetColor(ringColor);
        }

        #endregion

        #region Visual Effects

        /// <summary>
        /// 开始闪烁效果
        /// </summary>
        public void StartFlash()
        {
            if (_isFlashing) return;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashCoroutine());
        }

        /// <summary>
        /// 闪烁协程
        /// </summary>
        private IEnumerator FlashCoroutine()
        {
            _isFlashing = true;
            OnFlashStarted?.Invoke();

            float elapsed = 0f;
            float duration = flashDuration;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curveValue = flashCurve.Evaluate(t);

                // 计算闪烁颜色
                Color flashColorWithIntensity = flashColor * flashIntensity * curveValue;
                
                if (_ring != null)
                {
                    _ring.SetColor(flashColorWithIntensity);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 恢复原始颜色
            if (_ring != null)
            {
                UpdateRingColor(combatSystem.GetHealthPercentage());
            }

            _isFlashing = false;
            OnFlashCompleted?.Invoke();
        }

        /// <summary>
        /// 开始脉冲动画
        /// </summary>
        public void StartPulse()
        {
            if (_isPulsing || !enablePulseAnimation) return;

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
            }

            _pulseCoroutine = StartCoroutine(PulseCoroutine());
        }

        /// <summary>
        /// 停止脉冲动画
        /// </summary>
        public void StopPulse()
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            _isPulsing = false;

            // 恢复原始半径
            if (_ring != null)
            {
                _ring.SetRadius(_originalRadius);
            }
        }

        /// <summary>
        /// 脉冲协程
        /// </summary>
        private IEnumerator PulseCoroutine()
        {
            _isPulsing = true;

            while (_isPulsing)
            {
                float time = Time.time * pulseSpeed;
                float pulseValue = pulseCurve.Evaluate(Mathf.PingPong(time, 1f));
                float currentRadius = _originalRadius + (pulseValue * pulseIntensity);

                if (_ring != null)
                {
                    _ring.SetRadius(currentRadius);
                }

                yield return null;
            }
        }

        /// <summary>
        /// 更新脉冲动画
        /// </summary>
        private void UpdatePulseAnimation()
        {
            if (!_isPulsing || !enablePulseAnimation) return;

            float time = Time.time * pulseSpeed;
            float pulseValue = pulseCurve.Evaluate(Mathf.PingPong(time, 1f));
            float currentRadius = _originalRadius + (pulseValue * pulseIntensity);

            if (_ring != null)
            {
                _ring.SetRadius(currentRadius);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 受到伤害时的处理
        /// </summary>
        private void OnDamageTaken(float damage, BossColor sourceColor)
        {
            // 受伤时闪烁
            StartFlash();
        }

        /// <summary>
        /// 阶段切换时的处理
        /// </summary>
        private void OnPhaseChanged(Phase newPhase)
        {
            // 更新环颜色
            UpdateRingColor(combatSystem.GetHealthPercentage());

            if (verboseLogs)
                Debug.Log($"[BossC3_HealthRingUI] Phase changed to {newPhase}, updated ring color");
        }

        /// <summary>
        /// P2阶段触发时的处理
        /// </summary>
        private void OnPhase2Triggered()
        {
            // P2阶段开始脉冲动画
            StartPulse();

            if (verboseLogs)
                Debug.Log("[BossC3_HealthRingUI] Phase 2 triggered, started pulse animation");
        }

        #endregion

        #region Ring Control

        /// <summary>
        /// 设置环半径
        /// </summary>
        public void SetRingRadius(float radius)
        {
            ringRadius = radius;
            _originalRadius = radius;

            if (_ring != null)
            {
                _ring.SetRadius(radius);
            }
        }

        /// <summary>
        /// 设置环宽度
        /// </summary>
        public void SetRingWidth(float width)
        {
            ringWidth = width;

            if (_ring != null)
            {
                _ring.SetWidth(width);
            }
        }

        /// <summary>
        /// 设置环材质
        /// </summary>
        public void SetRingMaterial(Material material)
        {
            ringMaterial = material;

            if (_ring != null)
            {
                _ring.SetMaterial(material);
            }
        }

        /// <summary>
        /// 设置环可见性
        /// </summary>
        public void SetRingVisible(bool visible)
        {
            if (_ring != null)
            {
                _ring.SetVisible(visible);
            }
        }

        /// <summary>
        /// 设置环透明度
        /// </summary>
        public void SetRingAlpha(float alpha)
        {
            if (_ring != null)
            {
                _ring.SetAlpha(alpha);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取血条环是否已创建
        /// </summary>
        public bool IsRingCreated()
        {
            return _ring != null;
        }

        /// <summary>
        /// 获取当前环半径
        /// </summary>
        public float GetCurrentRadius()
        {
            return _ring != null ? _ring.GetRadius() : ringRadius;
        }

        /// <summary>
        /// 获取当前环宽度
        /// </summary>
        public float GetCurrentWidth()
        {
            return _ring != null ? _ring.GetWidth() : ringWidth;
        }

        /// <summary>
        /// 检查是否正在闪烁
        /// </summary>
        public bool IsFlashing()
        {
            return _isFlashing;
        }

        /// <summary>
        /// 检查是否正在脉冲
        /// </summary>
        public bool IsPulsing()
        {
            return _isPulsing;
        }

        /// <summary>
        /// 重置血条环UI
        /// </summary>
        public void ResetHealthRingUI()
        {
            // 停止所有动画
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            _isFlashing = false;
            _isPulsing = false;

            // 重新创建血条环
            if (autoCreateRing)
            {
                CreateHealthRing();
            }

            if (verboseLogs)
                Debug.Log("[BossC3_HealthRingUI] Health ring UI reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Ring: {(_ring != null ? "Created" : "Not Created")}, Flashing: {_isFlashing}, Pulsing: {_isPulsing}, Radius: {GetCurrentRadius():F2}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制血条环轮廓
            Gizmos.color = Color.red;
            Gizmos.DrawWireCircle(transform.position, ringRadius);

            // 绘制内圈
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCircle(transform.position, ringRadius - ringWidth);
        }

        #endregion
    }
}
