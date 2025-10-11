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
        
        // 每个聚光灯负责的按钮（初始分配）
        private FloatingMenuButton[] assignedButtons;
        // 记录每个按钮的上一帧位置，用于检测移动
        private Vector2[] lastAssignedButtonPositions;
        // 标记是否已经完成初始分配
        private bool hasInitializedAssignments = false;

        private void Start()
        {
            // 初始化按钮分配数组
            assignedButtons = new FloatingMenuButton[spotlights.Count];
            lastAssignedButtonPositions = new Vector2[spotlights.Count];
            
            // 应用默认颜色
            ApplyDefaultColors();
        }

        private void Update()
        {
            // 根据是否有悬停目标，更新聚光灯指向
            if (currentTarget != null)
            {
                // 有悬停目标：所有聚光灯指向悬停的按钮
                UpdateSpotlightsToTarget();
            }
            else
            {
                // 无悬停目标：每个聚光灯跟踪自己负责的按钮
                UpdateSpotlightsToAssignedButtons();
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
        /// 更新聚光灯到悬停目标位置（处理移动的按键）
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
        /// 更新聚光灯到各自负责的按钮（只在按钮位置变化时才更新）
        /// </summary>
        private void UpdateSpotlightsToAssignedButtons()
        {
            if (assignedButtons == null || lastAssignedButtonPositions == null) return;

            // 每个聚光灯跟踪自己负责的按钮
            // 只在按钮位置变化时才调用SetTarget，避免频繁重置
            for (int i = 0; i < spotlights.Count; i++)
            {
                if (i < assignedButtons.Length && spotlights[i] != null && assignedButtons[i] != null)
                {
                    Vector2 buttonPos = assignedButtons[i].GetPosition();
                    
                    // 第一次更新或者位置变化超过阈值时才更新
                    if (!hasInitializedAssignments || Vector2.Distance(buttonPos, lastAssignedButtonPositions[i]) > 0.1f)
                    {
                        spotlights[i].SetTarget(buttonPos);
                        lastAssignedButtonPositions[i] = buttonPos;
                        
                        if (showDebugInfo && !hasInitializedAssignments)
                        {
                            Debug.Log($"[SpotlightManager] 第一帧更新：聚光灯 {i} 指向按钮位置: {buttonPos}");
                        }
                    }
                }
            }
            
            // 标记已完成初始化
            if (!hasInitializedAssignments)
            {
                hasInitializedAssignments = true;
            }
        }

        /// <summary>
        /// 清除目标（聚光灯回到各自负责的按钮）
        /// </summary>
        public void ClearTarget()
        {
            currentTarget = null;

            // 每个聚光灯回到自己负责的按钮位置
            // 这样光束始终在按钮之间平滑切换，不会收拢或闪烁
            for (int i = 0; i < spotlights.Count; i++)
            {
                if (spotlights[i] != null && assignedButtons != null && i < assignedButtons.Length)
                {
                    FloatingMenuButton assignedButton = assignedButtons[i];
                    if (assignedButton != null)
                    {
                        // 回到自己负责的按钮位置
                        spotlights[i].SetTarget(assignedButton.GetPosition());
                    }
                }
            }

            if (showDebugInfo)
            {
                Debug.Log("[SpotlightManager] 目标已清除，聚光灯回到各自负责的按钮");
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

        /// <summary>
        /// 设置单个聚光灯的目标（用于开局时一对一分配）
        /// </summary>
        /// <param name="spotlightIndex">聚光灯索引</param>
        /// <param name="targetPosition">目标位置</param>
        public void SetSpotlightTarget(int spotlightIndex, Vector2 targetPosition)
        {
            if (spotlightIndex >= 0 && spotlightIndex < spotlights.Count && spotlights[spotlightIndex] != null)
            {
                spotlights[spotlightIndex].SetTarget(targetPosition);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SpotlightManager] 聚光灯 {spotlightIndex} 锁定位置: {targetPosition}");
                }
            }
        }
        
        /// <summary>
        /// 分配聚光灯到按钮（记住分配关系，用于回正）
        /// </summary>
        /// <param name="spotlightIndex">聚光灯索引</param>
        /// <param name="button">负责的按钮</param>
        public void AssignSpotlightToButton(int spotlightIndex, FloatingMenuButton button)
        {
            if (spotlightIndex >= 0 && spotlightIndex < spotlights.Count && button != null)
            {
                // 记住分配关系
                if (assignedButtons == null)
                {
                    assignedButtons = new FloatingMenuButton[spotlights.Count];
                }
                if (lastAssignedButtonPositions == null)
                {
                    lastAssignedButtonPositions = new Vector2[spotlights.Count];
                }
                
                assignedButtons[spotlightIndex] = button;
                
                // 记录按钮初始位置
                Vector2 buttonPos = button.GetPosition();
                lastAssignedButtonPositions[spotlightIndex] = buttonPos;
                
                // 设置初始目标
                if (spotlights[spotlightIndex] != null)
                {
                    spotlights[spotlightIndex].SetTarget(buttonPos);
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SpotlightManager] 聚光灯 {spotlightIndex} 分配给按钮: {button.buttonType}，位置: {buttonPos}");
                }
            }
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

