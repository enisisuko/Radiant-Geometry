// FloatingMenuButton.cs
// 自由漂浮的菜单按键
// 功能：随机移动、防碰撞、悬停停止、边界反弹

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 自由漂浮的菜单按键组件
    /// 在屏幕上随机移动，避免与其他按键碰撞，悬停时停止
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class FloatingMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("移动设置")]
        [Tooltip("移动速度")]
        public float moveSpeed = 20f;

        [Tooltip("最小间距（与其他按键）")]
        public float minDistance = 150f;

        [Tooltip("边界边距")]
        public float boundaryMargin = 50f;

        [Header("视觉设置")]
        [Tooltip("基础缩放")]
        public float baseScale = 1f;

        [Tooltip("悬停缩放")]
        public float hoverScale = 1.2f;

        [Tooltip("缩放动画速度")]
        public float scaleSpeed = 8f;

        [Header("按键信息")]
        [Tooltip("按键类型")]
        public MenuButtonType buttonType = MenuButtonType.NewGame;

        [Tooltip("按键颜色")]
        public Color buttonColor = Color.white;

        [Tooltip("按键图标")]
        public Sprite buttonIcon;

        [Header("状态")]
        [Tooltip("是否正在悬停")]
        public bool isHovered = false;

        // 私有状态
        private RectTransform rectTransform;
        private Image buttonImage;
        private Vector2 moveDirection;
        private Vector2 targetScale;
        private Canvas canvas;
        private RectTransform canvasRect;
        
        // 防碰撞
        private static List<FloatingMenuButton> allButtons = new List<FloatingMenuButton>();

        // 事件
        public System.Action<FloatingMenuButton> OnHoverEnter;
        public System.Action<FloatingMenuButton> OnHoverExit;
        public System.Action<FloatingMenuButton> OnClick;

        public enum MenuButtonType
        {
            NewGame,      // 新游戏
            Continue,     // 继续游戏
            Coop,         // 双人模式
            Settings,     // 设置
            Support,      // 支持我
            Quit          // 退出游戏
        }

        private void Awake()
        {
            // 获取组件
            rectTransform = GetComponent<RectTransform>();
            buttonImage = GetComponent<Image>();
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas.GetComponent<RectTransform>();

            // 初始化随机方向
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            moveDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;

            // 设置初始缩放
            targetScale = Vector2.one * baseScale;
            rectTransform.localScale = Vector3.one * baseScale;

            // 应用颜色
            if (buttonImage != null)
            {
                buttonImage.color = buttonColor;
            }

            // 应用图标
            if (buttonIcon != null && buttonImage != null)
            {
                buttonImage.sprite = buttonIcon;
            }

            // 注册到全局列表
            allButtons.Add(this);
        }

        private void OnDestroy()
        {
            // 从全局列表移除
            allButtons.Remove(this);
        }

        private void Update()
        {
            // 如果悬停，不移动
            if (!isHovered)
            {
                MoveButton();
                CheckBoundaries();
                AvoidOtherButtons();
            }

            // 更新缩放动画
            UpdateScaleAnimation();
        }

        /// <summary>
        /// 移动按键
        /// </summary>
        private void MoveButton()
        {
            // 应用移动
            Vector2 movement = moveDirection * moveSpeed * Time.deltaTime;
            rectTransform.anchoredPosition += movement;
        }

        /// <summary>
        /// 检查边界并反弹
        /// </summary>
        private void CheckBoundaries()
        {
            if (canvasRect == null) return;

            Vector2 pos = rectTransform.anchoredPosition;
            Vector2 canvasSize = canvasRect.sizeDelta;
            float halfWidth = canvasSize.x * 0.5f - boundaryMargin;
            float halfHeight = canvasSize.y * 0.5f - boundaryMargin;

            bool bounced = false;

            // 左右边界
            if (pos.x < -halfWidth)
            {
                pos.x = -halfWidth;
                moveDirection.x = Mathf.Abs(moveDirection.x); // 反弹
                bounced = true;
            }
            else if (pos.x > halfWidth)
            {
                pos.x = halfWidth;
                moveDirection.x = -Mathf.Abs(moveDirection.x); // 反弹
                bounced = true;
            }

            // 上下边界
            if (pos.y < -halfHeight)
            {
                pos.y = -halfHeight;
                moveDirection.y = Mathf.Abs(moveDirection.y); // 反弹
                bounced = true;
            }
            else if (pos.y > halfHeight)
            {
                pos.y = halfHeight;
                moveDirection.y = -Mathf.Abs(moveDirection.y); // 反弹
                bounced = true;
            }

            if (bounced)
            {
                rectTransform.anchoredPosition = pos;
                moveDirection = moveDirection.normalized; // 归一化方向
            }
        }

        /// <summary>
        /// 避免与其他按键碰撞（软碰撞）
        /// </summary>
        private void AvoidOtherButtons()
        {
            Vector2 myPos = rectTransform.anchoredPosition;
            Vector2 avoidanceForce = Vector2.zero;

            foreach (var other in allButtons)
            {
                if (other == this || other == null) continue;

                Vector2 otherPos = other.rectTransform.anchoredPosition;
                float distance = Vector2.Distance(myPos, otherPos);

                // 如果距离太近，施加斥力
                if (distance < minDistance && distance > 0.1f)
                {
                    Vector2 direction = (myPos - otherPos).normalized;
                    float forceMagnitude = (minDistance - distance) / minDistance; // 0-1
                    avoidanceForce += direction * forceMagnitude;
                }
            }

            // 应用避让力（平滑混合）
            if (avoidanceForce.sqrMagnitude > 0.01f)
            {
                // 将避让力混合到移动方向
                moveDirection = Vector2.Lerp(moveDirection, avoidanceForce.normalized, Time.deltaTime * 2f);
                moveDirection = moveDirection.normalized;
            }
        }

        /// <summary>
        /// 更新缩放动画
        /// </summary>
        private void UpdateScaleAnimation()
        {
            // 平滑插值到目标缩放
            Vector3 currentScale = rectTransform.localScale;
            Vector3 target = new Vector3(targetScale.x, targetScale.y, 1f);
            rectTransform.localScale = Vector3.Lerp(currentScale, target, Time.deltaTime * scaleSpeed);
        }

        /// <summary>
        /// 设置随机初始位置（防止重叠）
        /// </summary>
        public void SetRandomPosition()
        {
            if (canvasRect == null) return;

            Vector2 canvasSize = canvasRect.sizeDelta;
            float halfWidth = canvasSize.x * 0.5f - boundaryMargin * 2f;
            float halfHeight = canvasSize.y * 0.5f - boundaryMargin * 2f;

            // 尝试多次找到不重叠的位置
            int maxAttempts = 50;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2 randomPos = new Vector2(
                    Random.Range(-halfWidth, halfWidth),
                    Random.Range(-halfHeight, halfHeight)
                );

                // 检查是否与其他按键重叠
                bool overlaps = false;
                foreach (var other in allButtons)
                {
                    if (other == this || other == null) continue;
                    float distance = Vector2.Distance(randomPos, other.rectTransform.anchoredPosition);
                    if (distance < minDistance)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    rectTransform.anchoredPosition = randomPos;
                    return;
                }
            }

            // 如果找不到好位置，使用随机位置
            rectTransform.anchoredPosition = new Vector2(
                Random.Range(-halfWidth, halfWidth),
                Random.Range(-halfHeight, halfHeight)
            );
        }

        /// <summary>
        /// 获取当前位置
        /// </summary>
        public Vector2 GetPosition()
        {
            return rectTransform.anchoredPosition;
        }

        /// <summary>
        /// 获取屏幕位置（用于聚光灯）
        /// </summary>
        public Vector2 GetScreenPosition()
        {
            return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position);
        }

        // === UI事件处理 ===

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            targetScale = Vector2.one * hoverScale;
            OnHoverEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            targetScale = Vector2.one * baseScale;
            OnHoverExit?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke(this);
        }

#if UNITY_EDITOR
        // 编辑器调试
        private void OnDrawGizmos()
        {
            if (rectTransform == null) return;

            // 绘制防碰撞范围
            Gizmos.color = Color.yellow;
            Vector3 worldPos = rectTransform.position;
            Gizmos.DrawWireSphere(worldPos, minDistance * 0.01f); // 缩放到合适大小
        }
#endif
    }
}

