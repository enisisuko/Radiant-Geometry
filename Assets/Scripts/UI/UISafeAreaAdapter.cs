using UnityEngine;
using UnityEngine.UI; // 需要用于 LayoutRebuilder

namespace FadedDreams.UI
{
    /// <summary>
    /// UI安全区域适配器
    /// 处理刘海屏、异形屏等特殊屏幕的安全区域适配
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteInEditMode]
    public class UISafeAreaAdapter : MonoBehaviour
    {
        [Header("安全区域设置")]
        [Tooltip("是否在Awake时自动应用安全区域")]
        public bool applyOnAwake = true;
        
        [Tooltip("是否实时监听安全区域变化")]
        public bool listenToSafeAreaChange = true;
        
        [Header("适配选项")]
        [Tooltip("适配顶部（处理刘海屏等）")]
        public bool adaptTop = true;
        
        [Tooltip("适配底部（处理虚拟按键等）")]
        public bool adaptBottom = true;
        
        [Tooltip("适配左侧")]
        public bool adaptLeft = true;
        
        [Tooltip("适配右侧")]
        public bool adaptRight = true;
        
        [Header("额外边距")]
        [Tooltip("在安全区域基础上额外添加的边距")]
        public RectOffset additionalPadding = new RectOffset(0, 0, 0, 0);
        
        [Header("调试信息")]
        [SerializeField] private Rect currentSafeArea;
        [SerializeField] private Vector2 screenSize;
        
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2 lastScreenSize;
        
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            if (applyOnAwake)
            {
                ApplySafeArea();
            }
        }
        
        void Start()
        {
            // 确保在Start时也应用一次
            ApplySafeArea();
        }
        
        void Update()
        {
            // 编辑器模式下实时更新（用于预览）
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                // 编辑器模式下模拟安全区域
                SimulateSafeAreaInEditor();
#endif
                return;
            }
            
            // 运行时监听安全区域变化
            if (listenToSafeAreaChange)
            {
                Rect safeArea = Screen.safeArea;
                Vector2 screenSize = new Vector2(Screen.width, Screen.height);
                
                if (safeArea != lastSafeArea || screenSize != lastScreenSize)
                {
                    lastSafeArea = safeArea;
                    lastScreenSize = screenSize;
                    ApplySafeArea();
                }
            }
        }
        
        /// <summary>
        /// 应用安全区域适配
        /// </summary>
        public void ApplySafeArea()
        {
            if (rectTransform == null) return;
            
            // 获取屏幕安全区域
            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            
            // 更新调试信息
            currentSafeArea = safeArea;
            this.screenSize = screenSize;
            
            // 计算锚点位置（归一化坐标）
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            
            // 如果不适配某个方向，则使用全屏
            if (!adaptLeft) anchorMin.x = 0;
            if (!adaptBottom) anchorMin.y = 0;
            if (!adaptRight) anchorMax.x = screenSize.x;
            if (!adaptTop) anchorMax.y = screenSize.y;
            
            // 归一化到0-1范围
            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;
            
            // 应用锚点
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            
            // 应用额外边距
            rectTransform.offsetMin = new Vector2(
                adaptLeft ? additionalPadding.left : rectTransform.offsetMin.x,
                adaptBottom ? additionalPadding.bottom : rectTransform.offsetMin.y
            );
            rectTransform.offsetMax = new Vector2(
                adaptRight ? -additionalPadding.right : rectTransform.offsetMax.x,
                adaptTop ? -additionalPadding.top : rectTransform.offsetMax.y
            );
            
            // 强制更新布局
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
        
        /// <summary>
        /// 在编辑器中模拟安全区域（用于预览）
        /// </summary>
        private void SimulateSafeAreaInEditor()
        {
#if UNITY_EDITOR
            // 在编辑器中使用Game窗口的分辨率作为模拟
            // 可以根据需要调整模拟的安全区域比例
            if (rectTransform == null) return;
            
            // 简单模拟：假设有5%的边距
            float simulatedMarginPercent = 0.05f;
            
            rectTransform.anchorMin = new Vector2(
                adaptLeft ? simulatedMarginPercent : 0,
                adaptBottom ? simulatedMarginPercent : 0
            );
            rectTransform.anchorMax = new Vector2(
                adaptRight ? (1f - simulatedMarginPercent) : 1,
                adaptTop ? (1f - simulatedMarginPercent) : 1
            );
            
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
#endif
        }
        
        /// <summary>
        /// 设置额外边距
        /// </summary>
        public void SetAdditionalPadding(int left, int right, int top, int bottom)
        {
            additionalPadding = new RectOffset(left, right, top, bottom);
            ApplySafeArea();
        }
        
        /// <summary>
        /// 重置为全屏（取消安全区域适配）
        /// </summary>
        [ContextMenu("重置为全屏")]
        public void ResetToFullScreen()
        {
            if (rectTransform == null) return;
            
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            Debug.Log("[安全区域] 已重置为全屏模式");
        }
        
        /// <summary>
        /// 仅适配顶部（适合标题栏）
        /// </summary>
        [ContextMenu("仅适配顶部（标题栏）")]
        public void AdaptTopOnly()
        {
            adaptTop = true;
            adaptBottom = false;
            adaptLeft = false;
            adaptRight = false;
            ApplySafeArea();
            Debug.Log("[安全区域] 已设置为仅适配顶部模式");
        }
        
        /// <summary>
        /// 仅适配底部（适合底部导航栏）
        /// </summary>
        [ContextMenu("仅适配底部（导航栏）")]
        public void AdaptBottomOnly()
        {
            adaptTop = false;
            adaptBottom = true;
            adaptLeft = false;
            adaptRight = false;
            ApplySafeArea();
            Debug.Log("[安全区域] 已设置为仅适配底部模式");
        }
        
        /// <summary>
        /// 适配所有方向（推荐）
        /// </summary>
        [ContextMenu("适配所有方向（推荐）")]
        public void AdaptAllSides()
        {
            adaptTop = true;
            adaptBottom = true;
            adaptLeft = true;
            adaptRight = true;
            ApplySafeArea();
            Debug.Log("[安全区域] 已设置为适配所有方向模式");
        }
    }
}

