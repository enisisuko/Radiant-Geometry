// BossC2_EnergyUI.cs
// 能量条UI - 负责HP条和能量条创建、LineRenderer血条渲染和实时更新
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using UnityEngine;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// BossC2能量条UI - 负责HP条和能量条创建、LineRenderer血条渲染和实时更新
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC2_EnergyUI : MonoBehaviour
    {
        [Header("== HP Bar Settings ==")]
        public bool createHPBar = true;
        public float hpBarWidth = 4f;
        public float hpBarHeight = 0.3f;
        public float hpBarOffset = 2f;
        public Color hpBarColor = Color.red;
        public Color hpBarBackgroundColor = Color.gray;

        [Header("== Energy Bar Settings ==")]
        public bool createEnergyBar = true;
        public float energyBarWidth = 3f;
        public float energyBarHeight = 0.2f;
        public float energyBarOffset = 1.5f;
        public Color energyBarColor = Color.yellow;
        public Color energyBarBackgroundColor = Color.gray;

        [Header("== Line Renderer Settings ==")]
        public Material lineMaterial;
        public float lineWidth = 0.1f;
        public int lineSegments = 32;

        [Header("== Visibility ==")]
        public bool alwaysVisible = false;
        public float visibilityDistance = 20f;
        public float fadeDistance = 15f;

        [Header("== Animation ==")]
        public bool enablePulseAnimation = true;
        public float pulseSpeed = 2f;
        public float pulseIntensity = 0.1f;
        public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 组件引用
        private BossC2_Core core;
        private BossC2_TorchSystem torchSystem;
        private Camera playerCamera;

        // UI元素
        private LineRenderer _hpBarRenderer;
        private LineRenderer _hpBarBackgroundRenderer;
        private LineRenderer _energyBarRenderer;
        private LineRenderer _energyBarBackgroundRenderer;

        // 状态
        private bool _isVisible = true;
        private Coroutine _pulseCoroutine;

        // 事件
        public event Action OnHPBarCreated;
        public event Action OnEnergyBarCreated;
        public event Action OnVisibilityChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<BossC2_Core>();
            torchSystem = GetComponent<BossC2_TorchSystem>();

            // 查找玩家相机
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<Camera>();
            }
        }

        private void Start()
        {
            // 创建UI元素
            if (createHPBar)
            {
                CreateHPBar();
            }

            if (createEnergyBar)
            {
                CreateEnergyBar();
            }

            // 订阅事件
            if (core != null)
            {
                core.OnDamageTaken += OnDamageTaken;
            }

            if (torchSystem != null)
            {
                torchSystem.OnEnergyGained += OnEnergyGained;
            }

            // 启动脉冲动画
            if (enablePulseAnimation)
            {
                StartPulseAnimation();
            }
        }

        private void Update()
        {
            UpdateVisibility();
            UpdateBarPositions();
            UpdateBarValues();
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (core != null)
            {
                core.OnDamageTaken -= OnDamageTaken;
            }

            if (torchSystem != null)
            {
                torchSystem.OnEnergyGained -= OnEnergyGained;
            }

            // 停止协程
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
            }
        }

        #endregion

        #region HP Bar Creation

        /// <summary>
        /// 创建HP条
        /// </summary>
        private void CreateHPBar()
        {
            // 创建HP条背景
            _hpBarBackgroundRenderer = CreateLineRenderer("HPBarBackground");
            SetupHPBarRenderer(_hpBarBackgroundRenderer, hpBarBackgroundColor);

            // 创建HP条
            _hpBarRenderer = CreateLineRenderer("HPBar");
            SetupHPBarRenderer(_hpBarRenderer, hpBarColor);

            OnHPBarCreated?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC2_EnergyUI] HP bar created");
        }

        /// <summary>
        /// 设置HP条渲染器
        /// </summary>
        private void SetupHPBarRenderer(LineRenderer renderer, Color color)
        {
            renderer.material = lineMaterial;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.startWidth = lineWidth;
            renderer.endWidth = lineWidth;
            renderer.positionCount = 4;
            renderer.useWorldSpace = true;
            renderer.sortingOrder = 10;
        }

        #endregion

        #region Energy Bar Creation

        /// <summary>
        /// 创建能量条
        /// </summary>
        private void CreateEnergyBar()
        {
            // 创建能量条背景
            _energyBarBackgroundRenderer = CreateLineRenderer("EnergyBarBackground");
            SetupEnergyBarRenderer(_energyBarBackgroundRenderer, energyBarBackgroundColor);

            // 创建能量条
            _energyBarRenderer = CreateLineRenderer("EnergyBar");
            SetupEnergyBarRenderer(_energyBarRenderer, energyBarColor);

            OnEnergyBarCreated?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC2_EnergyUI] Energy bar created");
        }

        /// <summary>
        /// 设置能量条渲染器
        /// </summary>
        private void SetupEnergyBarRenderer(LineRenderer renderer, Color color)
        {
            renderer.material = lineMaterial;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.startWidth = lineWidth;
            renderer.endWidth = lineWidth;
            renderer.positionCount = 4;
            renderer.useWorldSpace = true;
            renderer.sortingOrder = 10;
        }

        #endregion

        #region Line Renderer Creation

        /// <summary>
        /// 创建LineRenderer
        /// </summary>
        private LineRenderer CreateLineRenderer(string name)
        {
            GameObject lineObj = new GameObject(name);
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;

            return lineObj.AddComponent<LineRenderer>();
        }

        #endregion

        #region Bar Updates

        /// <summary>
        /// 更新条位置
        /// </summary>
        private void UpdateBarPositions()
        {
            Vector3 bossPosition = transform.position;

            // 更新HP条位置
            if (_hpBarRenderer != null && _hpBarBackgroundRenderer != null)
            {
                Vector3 hpBarPosition = bossPosition + Vector3.up * hpBarOffset;
                UpdateBarPosition(_hpBarRenderer, hpBarPosition, hpBarWidth, hpBarHeight);
                UpdateBarPosition(_hpBarBackgroundRenderer, hpBarPosition, hpBarWidth, hpBarHeight);
            }

            // 更新能量条位置
            if (_energyBarRenderer != null && _energyBarBackgroundRenderer != null)
            {
                Vector3 energyBarPosition = bossPosition + Vector3.up * energyBarOffset;
                UpdateBarPosition(_energyBarRenderer, energyBarPosition, energyBarWidth, energyBarHeight);
                UpdateBarPosition(_energyBarBackgroundRenderer, energyBarPosition, energyBarWidth, energyBarHeight);
            }
        }

        /// <summary>
        /// 更新条位置
        /// </summary>
        private void UpdateBarPosition(LineRenderer renderer, Vector3 position, float width, float height)
        {
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            Vector3[] positions = new Vector3[4]
            {
                position + new Vector3(-halfWidth, -halfHeight, 0),
                position + new Vector3(halfWidth, -halfHeight, 0),
                position + new Vector3(halfWidth, halfHeight, 0),
                position + new Vector3(-halfWidth, halfHeight, 0)
            };

            renderer.SetPositions(positions);
        }

        /// <summary>
        /// 更新条数值
        /// </summary>
        private void UpdateBarValues()
        {
            // 更新HP条
            if (_hpBarRenderer != null && core != null)
            {
                float hpPercentage = core.GetHealthPercentage();
                UpdateBarFill(_hpBarRenderer, hpPercentage);
            }

            // 更新能量条
            if (_energyBarRenderer != null && torchSystem != null)
            {
                float energyPercentage = torchSystem.GetEnergyPercentage();
                UpdateBarFill(_energyBarRenderer, energyPercentage);
            }
        }

        /// <summary>
        /// 更新条填充
        /// </summary>
        private void UpdateBarFill(LineRenderer renderer, float percentage)
        {
            if (renderer == null) return;

            Vector3[] positions = new Vector3[4];
            renderer.GetPositions(positions);

            // 计算填充宽度
            float totalWidth = positions[1].x - positions[0].x;
            float fillWidth = totalWidth * percentage;

            // 更新填充位置
            positions[1].x = positions[0].x + fillWidth;
            positions[2].x = positions[0].x + fillWidth;

            renderer.SetPositions(positions);
        }

        #endregion

        #region Visibility Management

        /// <summary>
        /// 更新可见性
        /// </summary>
        private void UpdateVisibility()
        {
            if (alwaysVisible)
            {
                SetVisibility(true);
                return;
            }

            if (playerCamera == null) return;

            float distance = Vector3.Distance(transform.position, playerCamera.transform.position);
            bool shouldBeVisible = distance <= visibilityDistance;

            if (shouldBeVisible != _isVisible)
            {
                SetVisibility(shouldBeVisible);
            }

            // 更新透明度
            if (_isVisible && distance > fadeDistance)
            {
                float fadeAlpha = Mathf.Lerp(1f, 0f, (distance - fadeDistance) / (visibilityDistance - fadeDistance));
                SetBarsAlpha(fadeAlpha);
            }
            else if (_isVisible)
            {
                SetBarsAlpha(1f);
            }
        }

        /// <summary>
        /// 设置可见性
        /// </summary>
        private void SetVisibility(bool visible)
        {
            if (_isVisible == visible) return;

            _isVisible = visible;

            // 设置所有条的可见性
            SetBarVisibility(_hpBarRenderer, visible);
            SetBarVisibility(_hpBarBackgroundRenderer, visible);
            SetBarVisibility(_energyBarRenderer, visible);
            SetBarVisibility(_energyBarBackgroundRenderer, visible);

            OnVisibilityChanged?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC2_EnergyUI] Visibility changed: {visible}");
        }

        /// <summary>
        /// 设置条可见性
        /// </summary>
        private void SetBarVisibility(LineRenderer renderer, bool visible)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        /// <summary>
        /// 设置条透明度
        /// </summary>
        private void SetBarsAlpha(float alpha)
        {
            SetBarAlpha(_hpBarRenderer, alpha);
            SetBarAlpha(_hpBarBackgroundRenderer, alpha);
            SetBarAlpha(_energyBarRenderer, alpha);
            SetBarAlpha(_energyBarBackgroundRenderer, alpha);
        }

        /// <summary>
        /// 设置条透明度
        /// </summary>
        private void SetBarAlpha(LineRenderer renderer, float alpha)
        {
            if (renderer != null)
            {
                Color color = renderer.startColor;
                color.a = alpha;
                renderer.startColor = color;
            renderer.endColor = color;
            }
        }

        #endregion

        #region Pulse Animation

        /// <summary>
        /// 开始脉冲动画
        /// </summary>
        private void StartPulseAnimation()
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
            }

            _pulseCoroutine = StartCoroutine(PulseAnimationCoroutine());
        }

        /// <summary>
        /// 脉冲动画协程
        /// </summary>
        private IEnumerator PulseAnimationCoroutine()
        {
            while (enablePulseAnimation)
            {
                float time = Time.time * pulseSpeed;
                float pulseValue = pulseCurve.Evaluate(Mathf.PingPong(time, 1f));
                float scale = 1f + (pulseValue * pulseIntensity);

                // 应用脉冲缩放
                ApplyPulseScale(scale);

                yield return null;
            }
        }

        /// <summary>
        /// 应用脉冲缩放
        /// </summary>
        private void ApplyPulseScale(float scale)
        {
            // 这里可以实现脉冲缩放效果
            // 例如：调整条的大小或颜色强度
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 受到伤害时的处理
        /// </summary>
        private void OnDamageTaken(float damage)
        {
            // 可以在这里添加受伤时的UI效果
            // 例如：闪烁、颜色变化等
        }

        /// <summary>
        /// 能量变化时的处理
        /// </summary>
        private void OnEnergyGained(float amount)
        {
            // 可以在这里添加能量变化时的UI效果
            // 例如：闪烁、颜色变化等
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取是否可见
        /// </summary>
        public bool IsVisible() => _isVisible;

        /// <summary>
        /// 设置HP条颜色
        /// </summary>
        public void SetHPBarColor(Color color)
        {
            hpBarColor = color;
            if (_hpBarRenderer != null)
            {
                _hpBarRenderer.startColor = color;
                _hpBarRenderer.endColor = color;
            }
        }

        /// <summary>
        /// 设置能量条颜色
        /// </summary>
        public void SetEnergyBarColor(Color color)
        {
            energyBarColor = color;
            if (_energyBarRenderer != null)
            {
                _energyBarRenderer.startColor = color;
                _energyBarRenderer.endColor = color;
            }
        }

        /// <summary>
        /// 设置条材质
        /// </summary>
        public void SetBarMaterial(Material material)
        {
            lineMaterial = material;

            if (_hpBarRenderer != null) _hpBarRenderer.material = material;
            if (_hpBarBackgroundRenderer != null) _hpBarBackgroundRenderer.material = material;
            if (_energyBarRenderer != null) _energyBarRenderer.material = material;
            if (_energyBarBackgroundRenderer != null) _energyBarBackgroundRenderer.material = material;
        }

        /// <summary>
        /// 设置脉冲动画
        /// </summary>
        public void SetPulseAnimation(bool enabled)
        {
            enablePulseAnimation = enabled;

            if (enabled && _pulseCoroutine == null)
            {
                StartPulseAnimation();
            }
            else if (!enabled && _pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
        }

        /// <summary>
        /// 重置能量条UI
        /// </summary>
        public void ResetEnergyUI()
        {
            // 重新创建UI元素
            if (createHPBar)
            {
                if (_hpBarRenderer != null) Destroy(_hpBarRenderer.gameObject);
                if (_hpBarBackgroundRenderer != null) Destroy(_hpBarBackgroundRenderer.gameObject);
                CreateHPBar();
            }

            if (createEnergyBar)
            {
                if (_energyBarRenderer != null) Destroy(_energyBarRenderer.gameObject);
                if (_energyBarBackgroundRenderer != null) Destroy(_energyBarBackgroundRenderer.gameObject);
                CreateEnergyBar();
            }

            if (verboseLogs)
                Debug.Log("[BossC2_EnergyUI] Energy UI reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Visible: {_isVisible}, HP Bar: {(_hpBarRenderer != null)}, Energy Bar: {(_energyBarRenderer != null)}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制可见性范围
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, visibilityDistance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, fadeDistance);

            // 绘制条位置
            Vector3 bossPosition = transform.position;

            if (createHPBar)
            {
                Gizmos.color = hpBarColor;
                Vector3 hpBarPos = bossPosition + Vector3.up * hpBarOffset;
                Gizmos.DrawWireCube(hpBarPos, new Vector3(hpBarWidth, hpBarHeight, 0));
            }

            if (createEnergyBar)
            {
                Gizmos.color = energyBarColor;
                Vector3 energyBarPos = bossPosition + Vector3.up * energyBarOffset;
                Gizmos.DrawWireCube(energyBarPos, new Vector3(energyBarWidth, energyBarHeight, 0));
            }
        }

        #endregion
    }
}
