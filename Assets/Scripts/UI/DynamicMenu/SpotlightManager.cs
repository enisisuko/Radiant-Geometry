// SpotlightManager.cs
// 聚光灯管理器
// 功能：管理所有聚光灯，协调转向，监听按键事件

using UnityEngine;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 聚光灯管理器
    /// 管理所有聚光灯并协调它们的行为
    /// </summary>
    public class SpotlightManager : MonoBehaviour
    {
        [Header("聚光灯列表")]
        [Tooltip("所有聚光灯控制器")]
        public List<SpotlightController> spotlights = new List<SpotlightController>();

        [Header("行为设置")]
        [Tooltip("是否所有聚光灯同时转向")]
        public bool allSpotlightsFollowTarget = true;

        [Tooltip("默认聚光灯颜色")]
        public Color[] defaultColors = new Color[6]
        {
            new Color(1f, 0.2f, 0.2f),    // 红色（新游戏）
            new Color(0.2f, 0.8f, 1f),    // 蓝色（继续）
            new Color(0.8f, 0.2f, 1f),    // 紫色（双人）
            new Color(1f, 0.5f, 0f),      // 橙色（设置）
            new Color(0.2f, 1f, 0.2f),    // 绿色（支持）
            new Color(1f, 0.8f, 0.2f)     // 黄色（退出）
        };

        [Header("调试")]
        [Tooltip("显示调试信息")]
        public bool showDebugInfo = false;

        // 私有状态
        private FloatingMenuButton currentTarget = null;
        private Vector2 currentTargetPosition;

        private void Start()
        {
            // 应用默认颜色
            ApplyDefaultColors();
        }

        private void Update()
        {
            // 持续更新所有聚光灯（即使没有目标，也确保它们指向正确的位置）
            if (currentTarget != null)
            {
                UpdateSpotlightsToTarget();
            }
        }

        /// <summary>
        /// 应用默认颜色
        /// </summary>
        private void ApplyDefaultColors()
        {
            for (int i = 0; i < spotlights.Count && i < defaultColors.Length; i++)
            {
                if (spotlights[i] != null)
                {
                    spotlights[i].SetColor(defaultColors[i]);
                }
            }
        }

        /// <summary>
        /// 设置目标按键（所有聚光灯将转向该按键）
        /// </summary>
        /// <param name="targetButton">目标按键</param>
        public void SetTarget(FloatingMenuButton targetButton)
        {
            if (targetButton == null) return;

            currentTarget = targetButton;
            
            if (allSpotlightsFollowTarget)
            {
                // 所有聚光灯转向同一个目标
                Vector2 targetPos = targetButton.GetPosition();
                
                foreach (var spotlight in spotlights)
                {
                    if (spotlight != null)
                    {
                        spotlight.SetTarget(targetPos);
                    }
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"[SpotlightManager] 设置目标: {targetButton.buttonType}");
            }
        }

        /// <summary>
        /// 更新聚光灯到目标位置（处理移动的按键）
        /// </summary>
        private void UpdateSpotlightsToTarget()
        {
            if (currentTarget == null) return;

            // 获取当前目标位置
            Vector2 targetPos = currentTarget.GetPosition();

            // 如果位置变化，更新所有聚光灯
            if (Vector2.Distance(targetPos, currentTargetPosition) > 0.1f)
            {
                currentTargetPosition = targetPos;

                foreach (var spotlight in spotlights)
                {
                    if (spotlight != null)
                    {
                        spotlight.SetTarget(targetPos);
                    }
                }
            }
        }

        /// <summary>
        /// 清除目标（聚光灯回到默认状态）
        /// </summary>
        public void ClearTarget()
        {
            currentTarget = null;

            // 可以在这里添加聚光灯回到初始方向的逻辑
            // 例如：指向屏幕中心
            foreach (var spotlight in spotlights)
            {
                if (spotlight != null)
                {
                    Vector2 centerDirection = (Vector2.zero - spotlight.GetPosition()).normalized;
                    spotlight.SetTargetDirection(centerDirection);
                }
            }

            if (showDebugInfo)
            {
                Debug.Log("[SpotlightManager] 目标已清除");
            }
        }

        /// <summary>
        /// 添加聚光灯
        /// </summary>
        public void AddSpotlight(SpotlightController spotlight)
        {
            if (spotlight != null && !spotlights.Contains(spotlight))
            {
                spotlights.Add(spotlight);

                // 应用颜色
                int index = spotlights.Count - 1;
                if (index < defaultColors.Length)
                {
                    spotlight.SetColor(defaultColors[index]);
                }
            }
        }

        /// <summary>
        /// 移除聚光灯
        /// </summary>
        public void RemoveSpotlight(SpotlightController spotlight)
        {
            if (spotlight != null)
            {
                spotlights.Remove(spotlight);
            }
        }

        /// <summary>
        /// 设置所有聚光灯强度
        /// </summary>
        public void SetAllIntensity(float intensity)
        {
            foreach (var spotlight in spotlights)
            {
                if (spotlight != null)
                {
                    spotlight.SetIntensity(intensity);
                }
            }
        }

        /// <summary>
        /// 设置特定聚光灯颜色
        /// </summary>
        public void SetSpotlightColor(int index, Color color)
        {
            if (index >= 0 && index < spotlights.Count && spotlights[index] != null)
            {
                spotlights[index].SetColor(color);
            }
        }

        /// <summary>
        /// 获取当前目标
        /// </summary>
        public FloatingMenuButton GetCurrentTarget()
        {
            return currentTarget;
        }

        /// <summary>
        /// 聚光灯数量
        /// </summary>
        public int GetSpotlightCount()
        {
            return spotlights.Count;
        }

#if UNITY_EDITOR
        // 编辑器辅助功能

        [ContextMenu("查找所有聚光灯")]
        private void FindAllSpotlights()
        {
            spotlights.Clear();
            var found = FindObjectsByType<SpotlightController>(FindObjectsSortMode.None);
            spotlights.AddRange(found);
            Debug.Log($"[SpotlightManager] 找到 {spotlights.Count} 个聚光灯");
        }

        [ContextMenu("测试：转向屏幕中心")]
        private void TestPointToCenter()
        {
            foreach (var spotlight in spotlights)
            {
                if (spotlight != null)
                {
                    spotlight.SetTarget(Vector2.zero);
                }
            }
            Debug.Log("[SpotlightManager] 测试：所有聚光灯指向中心");
        }

        [ContextMenu("应用默认颜色")]
        private void ApplyDefaultColorsEditor()
        {
            ApplyDefaultColors();
            Debug.Log("[SpotlightManager] 已应用默认颜色");
        }
#endif
    }
}

