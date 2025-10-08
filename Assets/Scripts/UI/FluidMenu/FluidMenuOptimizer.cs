using UnityEngine;
using UnityEngine.Profiling;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单性能优化器
    /// 负责监控和优化菜单系统的性能
    /// </summary>
    public class FluidMenuOptimizer : MonoBehaviour
    {
        [Header("性能监控")]
        public bool enableProfiling = true;
        public bool showPerformanceUI = false;
        public float updateInterval = 0.5f;
        
        [Header("优化设置")]
        public bool enableLOD = true;
        public bool enableCulling = true;
        public bool enableBatching = true;
        public bool enablePooling = true;
        
        [Header("LOD设置")]
        public float[] lodDistances = { 5f, 10f, 20f };
        public int[] lodTriangles = { 100, 50, 25 };
        
        [Header("性能阈值")]
        public float targetFPS = 60f;
        public float lowFPSThreshold = 45f;
        public float highFPSThreshold = 75f;
        
        // 性能数据
        private float currentFPS;
        private float averageFPS;
        private float minFPS = float.MaxValue;
        private float maxFPS = 0f;
        private int frameCount = 0;
        private float timeAccumulator = 0f;
        
        // 优化状态
        private bool isOptimized = false;
        private int currentLODLevel = 0;
        private List<FluidColorBlock> colorBlocks = new List<FluidColorBlock>();
        
        // 对象池
        private Queue<GameObject> uiElementPool = new Queue<GameObject>();
        private int poolSize = 10;
        
        void Start()
        {
            InitializeOptimizer();
        }
        
        void Update()
        {
            if (enableProfiling)
            {
                UpdatePerformanceMetrics();
                CheckPerformanceThresholds();
            }
            
            if (showPerformanceUI)
            {
                UpdatePerformanceUI();
            }
        }
        
        void InitializeOptimizer()
        {
            // 收集所有色块
            colorBlocks.AddRange(FindObjectsOfType<FluidColorBlock>());
            
            // 初始化对象池
            if (enablePooling)
            {
                InitializeObjectPool();
            }
            
            // 设置初始LOD
            if (enableLOD)
            {
                SetLODLevel(0);
            }
            
            Debug.Log($"流体菜单优化器初始化完成，找到 {colorBlocks.Count} 个色块");
        }
        
        void UpdatePerformanceMetrics()
        {
            frameCount++;
            timeAccumulator += Time.unscaledDeltaTime;
            
            if (timeAccumulator >= updateInterval)
            {
                currentFPS = frameCount / timeAccumulator;
                averageFPS = (averageFPS + currentFPS) / 2f;
                
                if (currentFPS < minFPS) minFPS = currentFPS;
                if (currentFPS > maxFPS) maxFPS = currentFPS;
                
                frameCount = 0;
                timeAccumulator = 0f;
            }
        }
        
        void CheckPerformanceThresholds()
        {
            if (currentFPS < lowFPSThreshold && !isOptimized)
            {
                Debug.LogWarning($"FPS过低 ({currentFPS:F1})，开始性能优化");
                OptimizePerformance();
            }
            else if (currentFPS > highFPSThreshold && isOptimized)
            {
                Debug.Log($"FPS恢复正常 ({currentFPS:F1})，恢复高质量设置");
                RestoreQuality();
            }
        }
        
        void OptimizePerformance()
        {
            isOptimized = true;
            
            // 降低LOD级别
            if (enableLOD)
            {
                SetLODLevel(Mathf.Min(currentLODLevel + 1, lodDistances.Length - 1));
            }
            
            // 禁用非必要效果
            DisableNonEssentialEffects();
            
            // 减少更新频率
            ReduceUpdateFrequency();
            
            Debug.Log("性能优化已应用");
        }
        
        void RestoreQuality()
        {
            isOptimized = false;
            
            // 恢复LOD级别
            if (enableLOD)
            {
                SetLODLevel(0);
            }
            
            // 恢复所有效果
            EnableAllEffects();
            
            // 恢复更新频率
            RestoreUpdateFrequency();
            
            Debug.Log("高质量设置已恢复");
        }
        
        void SetLODLevel(int level)
        {
            currentLODLevel = level;
            
            foreach (var block in colorBlocks)
            {
                if (block != null)
                {
                    // 根据LOD级别调整Shader参数
                    AdjustBlockLOD(block, level);
                }
            }
        }
        
        void AdjustBlockLOD(FluidColorBlock block, int lodLevel)
        {
            // 根据LOD级别调整效果强度
            float lodMultiplier = 1f - (lodLevel * 0.3f);
            
            // 调整变形强度
            block.distortionStrength *= lodMultiplier;
            
            // 调整波纹频率
            // 这里可以通过MaterialPropertyBlock调整Shader参数
        }
        
        void DisableNonEssentialEffects()
        {
            foreach (var block in colorBlocks)
            {
                if (block != null)
                {
                    // 禁用呼吸动画
                    block.breathScale *= 0.5f;
                    block.breathSpeed *= 0.5f;
                }
            }
        }
        
        void EnableAllEffects()
        {
            foreach (var block in colorBlocks)
            {
                if (block != null)
                {
                    // 恢复呼吸动画
                    block.breathScale *= 2f;
                    block.breathSpeed *= 2f;
                }
            }
        }
        
        void ReduceUpdateFrequency()
        {
            // 减少动画更新频率
            Time.fixedDeltaTime = 1f / 30f; // 降低到30FPS
        }
        
        void RestoreUpdateFrequency()
        {
            // 恢复正常更新频率
            Time.fixedDeltaTime = 1f / 60f; // 恢复到60FPS
        }
        
        void InitializeObjectPool()
        {
            // 创建UI元素对象池
            for (int i = 0; i < poolSize; i++)
            {
                GameObject poolObject = new GameObject($"PooledUI_{i}");
                poolObject.SetActive(false);
                poolObject.transform.SetParent(transform);
                uiElementPool.Enqueue(poolObject);
            }
        }
        
        public GameObject GetPooledObject()
        {
            if (uiElementPool.Count > 0)
            {
                return uiElementPool.Dequeue();
            }
            
            // 如果池空了，创建新对象
            GameObject newObject = new GameObject("PooledUI_New");
            newObject.transform.SetParent(transform);
            return newObject;
        }
        
        public void ReturnToPool(GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                uiElementPool.Enqueue(obj);
            }
        }
        
        void UpdatePerformanceUI()
        {
            // 这里可以显示性能信息的UI
            // 比如FPS、内存使用等
        }
        
        // 性能分析接口
        public void StartProfiling()
        {
            if (enableProfiling)
            {
                Profiler.BeginSample("FluidMenu_Update");
            }
        }
        
        public void EndProfiling()
        {
            if (enableProfiling)
            {
                Profiler.EndSample();
            }
        }
        
        // 公共接口
        public float GetCurrentFPS()
        {
            return currentFPS;
        }
        
        public float GetAverageFPS()
        {
            return averageFPS;
        }
        
        public float GetMinFPS()
        {
            return minFPS;
        }
        
        public float GetMaxFPS()
        {
            return maxFPS;
        }
        
        public bool IsOptimized()
        {
            return isOptimized;
        }
        
        public int GetCurrentLODLevel()
        {
            return currentLODLevel;
        }
        
        public void ForceOptimize()
        {
            OptimizePerformance();
        }
        
        public void ForceRestoreQuality()
        {
            RestoreQuality();
        }
        
        public void SetTargetFPS(float fps)
        {
            targetFPS = fps;
        }
        
        public void SetLowFPSThreshold(float threshold)
        {
            lowFPSThreshold = threshold;
        }
        
        public void SetHighFPSThreshold(float threshold)
        {
            highFPSThreshold = threshold;
        }
        
        // 调试接口
        [ContextMenu("Show Performance Info")]
        public void ShowPerformanceInfo()
        {
            Debug.Log($"当前FPS: {currentFPS:F1}");
            Debug.Log($"平均FPS: {averageFPS:F1}");
            Debug.Log($"最低FPS: {minFPS:F1}");
            Debug.Log($"最高FPS: {maxFPS:F1}");
            Debug.Log($"当前LOD级别: {currentLODLevel}");
            Debug.Log($"是否优化: {isOptimized}");
        }
        
        [ContextMenu("Force Optimize")]
        public void DebugForceOptimize()
        {
            ForceOptimize();
        }
        
        [ContextMenu("Force Restore Quality")]
        public void DebugForceRestoreQuality()
        {
            ForceRestoreQuality();
        }
        
        void OnGUI()
        {
            if (showPerformanceUI)
            {
                GUILayout.BeginArea(new Rect(10, 10, 200, 150));
                GUILayout.Label($"FPS: {currentFPS:F1}");
                GUILayout.Label($"平均FPS: {averageFPS:F1}");
                GUILayout.Label($"最低FPS: {minFPS:F1}");
                GUILayout.Label($"最高FPS: {maxFPS:F1}");
                GUILayout.Label($"LOD级别: {currentLODLevel}");
                GUILayout.Label($"优化状态: {(isOptimized ? "已优化" : "正常")}");
                GUILayout.EndArea();
            }
        }
    }
}