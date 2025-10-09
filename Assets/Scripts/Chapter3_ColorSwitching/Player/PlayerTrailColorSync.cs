using UnityEngine;
using FadedDreams.Player;

namespace FadedDreams.Player
{
    /// <summary>
    /// 玩家拖尾颜色同步器 - 让TrailRenderer颜色随玩家颜色模式变化
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrailRenderer))]
    public class PlayerTrailColorSync : MonoBehaviour
    {
        [Header("颜色配置")]
        [Tooltip("红色模式的拖尾渐变")]
        public Gradient redGradient = new Gradient();
        
        [Tooltip("绿色模式的拖尾渐变")]
        public Gradient greenGradient = new Gradient();
        
        [Header("颜色过渡")]
        [Tooltip("颜色切换速度")]
        public float colorTransitionSpeed = 5f;
        
        [Tooltip("使用平滑过渡（false则立即切换）")]
        public bool smoothTransition = true;
        
        [Header("Debug")]
        public bool debugLogs = false;
        
        private TrailRenderer _trailRenderer;
        private PlayerColorModeController _colorController;
        private Gradient _currentGradient;
        private Gradient _targetGradient;
        private bool _isTransitioning = false;
        private float _transitionProgress = 0f;
        
        private void Awake()
        {
            _trailRenderer = GetComponent<TrailRenderer>();
            _colorController = GetComponent<PlayerColorModeController>();
            
            // 初始化默认渐变（如果没有设置）
            InitializeDefaultGradients();
            
            if (debugLogs)
                Debug.Log($"[TrailColorSync] Awake: trail={(bool)_trailRenderer}, controller={(bool)_colorController}");
        }
        
        private void Start()
        {
            // 订阅颜色模式改变事件
            if (_colorController != null)
            {
                _colorController.OnModeChanged.AddListener(OnModeChanged);
                
                // 立即应用当前模式的颜色
                ApplyModeColor(_colorController.Mode, immediate: true);
            }
            
            if (debugLogs)
                Debug.Log($"[TrailColorSync] Start: initial mode={_colorController?.Mode}");
        }
        
        private void Update()
        {
            // 如果正在过渡，更新颜色
            if (_isTransitioning && smoothTransition)
            {
                UpdateColorTransition(Time.deltaTime);
            }
        }
        
        private void OnDestroy()
        {
            // 取消订阅
            if (_colorController != null)
            {
                _colorController.OnModeChanged.RemoveListener(OnModeChanged);
            }
        }
        
        /// <summary>
        /// 初始化默认渐变
        /// </summary>
        private void InitializeDefaultGradients()
        {
            // 红色渐变：从深红到浅红透明
            if (redGradient.colorKeys.Length == 0)
            {
                redGradient = new Gradient();
                redGradient.SetKeys(
                    new GradientColorKey[] 
                    { 
                        new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f),
                        new GradientColorKey(new Color(1f, 0.5f, 0.5f), 1f)
                    },
                    new GradientAlphaKey[] 
                    { 
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
            }
            
            // 绿色渐变：从深绿到浅绿透明
            if (greenGradient.colorKeys.Length == 0)
            {
                greenGradient = new Gradient();
                greenGradient.SetKeys(
                    new GradientColorKey[] 
                    { 
                        new GradientColorKey(new Color(0.2f, 1f, 0.2f), 0f),
                        new GradientColorKey(new Color(0.5f, 1f, 0.5f), 1f)
                    },
                    new GradientAlphaKey[] 
                    { 
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
            }
        }
        
        /// <summary>
        /// 颜色模式改变回调
        /// </summary>
        private void OnModeChanged(ColorMode newMode)
        {
            if (debugLogs)
                Debug.Log($"[TrailColorSync] Mode changed to: {newMode}");
            
            ApplyModeColor(newMode, immediate: !smoothTransition);
        }
        
        /// <summary>
        /// 应用颜色模式
        /// </summary>
        private void ApplyModeColor(ColorMode mode, bool immediate = false)
        {
            if (_trailRenderer == null) return;
            
            // 确定目标渐变
            _targetGradient = (mode == ColorMode.Red) ? redGradient : greenGradient;
            
            if (immediate || !smoothTransition)
            {
                // 立即应用
                _trailRenderer.colorGradient = _targetGradient;
                _currentGradient = _targetGradient;
                _isTransitioning = false;
                
                if (debugLogs)
                    Debug.Log($"[TrailColorSync] Immediately applied {mode} gradient");
            }
            else
            {
                // 启动平滑过渡
                _currentGradient = _trailRenderer.colorGradient;
                _isTransitioning = true;
                _transitionProgress = 0f;
                
                if (debugLogs)
                    Debug.Log($"[TrailColorSync] Starting transition to {mode} gradient");
            }
        }
        
        /// <summary>
        /// 更新颜色过渡
        /// </summary>
        private void UpdateColorTransition(float deltaTime)
        {
            if (_trailRenderer == null || _targetGradient == null) return;
            
            _transitionProgress += deltaTime * colorTransitionSpeed;
            
            if (_transitionProgress >= 1f)
            {
                // 过渡完成
                _trailRenderer.colorGradient = _targetGradient;
                _isTransitioning = false;
                _transitionProgress = 1f;
                
                if (debugLogs)
                    Debug.Log($"[TrailColorSync] Transition completed");
            }
            else
            {
                // 插值渐变（简化版：直接切换，因为Gradient插值比较复杂）
                // 如果需要更平滑的过渡，可以在这里实现颜色键的插值
                float t = Mathf.SmoothStep(0f, 1f, _transitionProgress);
                if (t > 0.5f)
                {
                    _trailRenderer.colorGradient = _targetGradient;
                }
            }
        }
        
        /// <summary>
        /// 手动设置拖尾颜色（供外部调用）
        /// </summary>
        public void SetTrailColor(ColorMode mode)
        {
            ApplyModeColor(mode, immediate: false);
        }
        
        /// <summary>
        /// 立即设置拖尾颜色
        /// </summary>
        public void SetTrailColorImmediate(ColorMode mode)
        {
            ApplyModeColor(mode, immediate: true);
        }
    }
}

