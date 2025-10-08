// FluidMenuLayout.cs
// 布局管理 - 负责色块布局、动画更新和位置管理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using UnityEngine;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单布局管理 - 负责色块布局、动画更新和位置管理
    /// </summary>
    public class FluidMenuLayout : MonoBehaviour
    {
        [Header("== 布局设置 ==")]
        public float blockSpacing = 200f;
        public float centerBlockSize = 1.2f;
        public float cornerBlockSize = 1.0f;

        [Header("== 动画设置 ==")]
        public float hoverScale = 1.5f;
        public float squeezeScale = 0.7f;
        public float animationSpeed = 5f;
        public AnimationCurve squeezeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== 调试 ==")]
        public bool verboseLogs = true;

        // 组件引用
        private FluidMenuCore core;

        // 动画状态
        private float[] targetScales = new float[5];
        private float[] currentScales = new float[5];
        private Vector3[] targetPositions = new Vector3[5];
        private Vector3[] currentPositions = new Vector3[5];
        private float[] scaleVelocities = new float[5];
        private Vector3[] positionVelocities = new Vector3[5];

        // 布局状态
        private bool isLayoutActive = true;
        private Coroutine layoutUpdateCR;

        // 事件
        public event System.Action<int> OnBlockHovered;
        public event System.Action<int> OnBlockUnhovered;
        public event System.Action<int> OnBlockSelected;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<FluidMenuCore>();
        }

        private void Start()
        {
            InitializeLayout();
        }

        private void OnDestroy()
        {
            if (layoutUpdateCR != null)
            {
                StopCoroutine(layoutUpdateCR);
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化布局
        /// </summary>
        private void InitializeLayout()
        {
            if (core == null) return;

            // 初始化动画状态
            for (int i = 0; i < 5; i++)
            {
                targetScales[i] = (i == 2) ? centerBlockSize : cornerBlockSize; // 中心块更大
                currentScales[i] = targetScales[i];
                scaleVelocities[i] = 0f;
                positionVelocities[i] = Vector3.zero;
            }

            // 设置初始位置
            SetupInitialPositions();

            // 启动布局更新协程
            if (layoutUpdateCR != null)
            {
                StopCoroutine(layoutUpdateCR);
            }
            layoutUpdateCR = StartCoroutine(LayoutUpdateCoroutine());

            if (verboseLogs)
                Debug.Log("[FluidMenuLayout] Layout initialized");
        }

        /// <summary>
        /// 设置初始位置
        /// </summary>
        private void SetupInitialPositions()
        {
            if (core == null || core.canvas == null) return;

            Vector3 centerPosition = core.canvas.transform.position;

            // 设置色块位置（十字形布局）
            Vector3[] positions = new Vector3[]
            {
                centerPosition + Vector3.left * blockSpacing,    // 左
                centerPosition + Vector3.up * blockSpacing,      // 上
                centerPosition,                                  // 中
                centerPosition + Vector3.down * blockSpacing,    // 下
                centerPosition + Vector3.right * blockSpacing    // 右
            };

            for (int i = 0; i < 5; i++)
            {
                targetPositions[i] = positions[i];
                currentPositions[i] = positions[i];

                // 应用位置到色块
                FluidColorBlock colorBlock = core.GetColorBlock(i);
                if (colorBlock != null)
                {
                    colorBlock.SetPosition(positions[i]);
                }
            }
        }

        #endregion

        #region Layout Update

        /// <summary>
        /// 布局更新协程
        /// </summary>
        private IEnumerator LayoutUpdateCoroutine()
        {
            while (isLayoutActive)
            {
                UpdateLayout();
                yield return null;
            }
        }

        /// <summary>
        /// 更新布局
        /// </summary>
        private void UpdateLayout()
        {
            if (core == null) return;

            for (int i = 0; i < 5; i++)
            {
                // 更新缩放
                currentScales[i] = Mathf.SmoothDamp(currentScales[i], targetScales[i], ref scaleVelocities[i], 1f / animationSpeed);

                // 更新位置
                currentPositions[i] = Vector3.SmoothDamp(currentPositions[i], targetPositions[i], ref positionVelocities[i], 1f / animationSpeed);

                // 应用到色块
                FluidColorBlock colorBlock = core.GetColorBlock(i);
                if (colorBlock != null)
                {
                    colorBlock.SetScale(currentScales[i]);
                    colorBlock.SetPosition(currentPositions[i]);
                }
            }
        }

        #endregion

        #region Block Interaction

        /// <summary>
        /// 悬停色块
        /// </summary>
        public void HoverBlock(int index)
        {
            if (index < 0 || index >= 5) return;

            // 设置悬停缩放
            targetScales[index] = hoverScale;

            // 播放悬停音效
            if (core != null)
            {
                core.PlayHoverSound();
            }

            OnBlockHovered?.Invoke(index);

            if (verboseLogs)
                Debug.Log($"[FluidMenuLayout] Block {index} hovered");
        }

        /// <summary>
        /// 取消悬停色块
        /// </summary>
        public void UnhoverBlock(int index)
        {
            if (index < 0 || index >= 5) return;

            // 恢复原始缩放
            targetScales[index] = (index == 2) ? centerBlockSize : cornerBlockSize;

            OnBlockUnhovered?.Invoke(index);

            if (verboseLogs)
                Debug.Log($"[FluidMenuLayout] Block {index} unhovered");
        }

        /// <summary>
        /// 选择色块
        /// </summary>
        public void SelectBlock(int index)
        {
            if (index < 0 || index >= 5) return;

            // 播放点击音效
            if (core != null)
            {
                core.PlayClickSound();
            }

            // 触发挤压动画
            StartCoroutine(SqueezeAnimation(index));

            OnBlockSelected?.Invoke(index);

            if (verboseLogs)
                Debug.Log($"[FluidMenuLayout] Block {index} selected");
        }

        #endregion

        #region Animations

        /// <summary>
        /// 挤压动画
        /// </summary>
        private IEnumerator SqueezeAnimation(int index)
        {
            if (index < 0 || index >= 5) yield break;

            float originalScale = targetScales[index];
            float elapsed = 0f;
            float duration = 0.2f;

            // 挤压阶段
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curveValue = squeezeCurve.Evaluate(t);
                targetScales[index] = Mathf.Lerp(originalScale, squeezeScale, curveValue);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 恢复阶段
            elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curveValue = squeezeCurve.Evaluate(t);
                targetScales[index] = Mathf.Lerp(squeezeScale, originalScale, curveValue);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 确保最终值正确
            targetScales[index] = originalScale;
        }

        /// <summary>
        /// 重置所有色块
        /// </summary>
        public void ResetAllBlocks()
        {
            for (int i = 0; i < 5; i++)
            {
                targetScales[i] = (i == 2) ? centerBlockSize : cornerBlockSize;
                targetPositions[i] = currentPositions[i];
            }

            if (verboseLogs)
                Debug.Log("[FluidMenuLayout] All blocks reset");
        }

        #endregion

        #region Layout Management

        /// <summary>
        /// 激活布局
        /// </summary>
        public void ActivateLayout()
        {
            if (isLayoutActive) return;

            isLayoutActive = true;
            if (layoutUpdateCR != null)
            {
                StopCoroutine(layoutUpdateCR);
            }
            layoutUpdateCR = StartCoroutine(LayoutUpdateCoroutine());

            if (verboseLogs)
                Debug.Log("[FluidMenuLayout] Layout activated");
        }

        /// <summary>
        /// 停用布局
        /// </summary>
        public void DeactivateLayout()
        {
            if (!isLayoutActive) return;

            isLayoutActive = false;
            if (layoutUpdateCR != null)
            {
                StopCoroutine(layoutUpdateCR);
                layoutUpdateCR = null;
            }

            if (verboseLogs)
                Debug.Log("[FluidMenuLayout] Layout deactivated");
        }

        /// <summary>
        /// 设置色块间距
        /// </summary>
        public void SetBlockSpacing(float spacing)
        {
            blockSpacing = spacing;
            SetupInitialPositions();

            if (verboseLogs)
                Debug.Log($"[FluidMenuLayout] Block spacing set to {spacing}");
        }

        /// <summary>
        /// 设置动画速度
        /// </summary>
        public void SetAnimationSpeed(float speed)
        {
            animationSpeed = speed;

            if (verboseLogs)
                Debug.Log($"[FluidMenuLayout] Animation speed set to {speed}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取目标缩放
        /// </summary>
        public float GetTargetScale(int index)
        {
            if (index >= 0 && index < 5)
            {
                return targetScales[index];
            }
            return 1f;
        }

        /// <summary>
        /// 获取当前缩放
        /// </summary>
        public float GetCurrentScale(int index)
        {
            if (index >= 0 && index < 5)
            {
                return currentScales[index];
            }
            return 1f;
        }

        /// <summary>
        /// 获取目标位置
        /// </summary>
        public Vector3 GetTargetPosition(int index)
        {
            if (index >= 0 && index < 5)
            {
                return targetPositions[index];
            }
            return Vector3.zero;
        }

        /// <summary>
        /// 获取当前位置
        /// </summary>
        public Vector3 GetCurrentPosition(int index)
        {
            if (index >= 0 && index < 5)
            {
                return currentPositions[index];
            }
            return Vector3.zero;
        }

        /// <summary>
        /// 检查布局是否激活
        /// </summary>
        public bool IsLayoutActive()
        {
            return isLayoutActive;
        }

        /// <summary>
        /// 重置布局
        /// </summary>
        public void ResetLayout()
        {
            DeactivateLayout();
            InitializeLayout();

            if (verboseLogs)
                Debug.Log("[FluidMenuLayout] Layout reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Layout Active: {isLayoutActive}, Animation Speed: {animationSpeed}, Block Spacing: {blockSpacing}";
        }

        #endregion
    }
}
