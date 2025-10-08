using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using FadedDreams.Core;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单系统集成脚本
    /// 负责将流体菜单与现有系统（SaveSystem、SceneLoader等）进行集成
    /// </summary>
    public class FluidMenuIntegration : MonoBehaviour
    {
        [Header("系统引用")]
        public FluidMenuManager menuManager;
        public SaveSystem saveSystem;
        
        [Header("场景配置")]
        public string newGameScene = "STORY0";
        public string firstCheckpointId = "101";
        public string coopModeScene = "CoopMode";
        public string supportScene = "SupportPage";
        
        [Header("过渡设置")]
        public float transitionDuration = 1.5f;
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("存档检查")]
        public bool checkSaveOnStart = true;
        public bool showNoSaveWarning = true;
        
        // 状态管理
        private bool isTransitioning = false;
        private Coroutine currentTransition;
        
        void Start()
        {
            InitializeIntegration();
        }
        
        void InitializeIntegration()
        {
            // 自动获取组件引用
            if (menuManager == null) menuManager = GetComponent<FluidMenuManager>();
            if (saveSystem == null) saveSystem = SaveSystem.Instance;
            
            // 检查存档状态
            if (checkSaveOnStart)
            {
                CheckSaveStatus();
            }
            
            // 设置菜单管理器的回调
            if (menuManager != null)
            {
                SetupMenuCallbacks();
            }
        }
        
        void CheckSaveStatus()
        {
            if (saveSystem == null) return;
            
            bool hasSave = saveSystem.HasSaveData();
            
            // 更新继续游戏按钮的状态
            if (menuManager != null && menuManager.colorBlocks != null && menuManager.colorBlocks.Length > 1)
            {
                FluidColorBlock continueBlock = menuManager.colorBlocks[1]; // 继续游戏是索引1
                if (continueBlock != null)
                {
                    // 如果没有存档，降低继续游戏按钮的亮度
                    if (!hasSave)
                    {
                        continueBlock.SetAlpha(0.5f);
                        Debug.Log("没有找到存档数据，继续游戏按钮已禁用");
                    }
                    else
                    {
                        continueBlock.SetAlpha(1f);
                        Debug.Log("找到存档数据，继续游戏按钮已启用");
                    }
                }
            }
        }
        
        void SetupMenuCallbacks()
        {
            // 这里可以设置菜单管理器的回调函数
            // 由于FluidMenuManager已经内置了功能，我们主要确保集成正确
        }
        
        public void OnNewGameSelected()
        {
            if (isTransitioning) return;
            
            Debug.Log("开始新游戏...");
            
            // 清空所有存档
            if (saveSystem != null)
            {
                saveSystem.ResetAll();
                saveSystem.SaveLastScene(newGameScene);
                saveSystem.SaveCheckpoint(firstCheckpointId);
            }
            
            // 开始过渡动画
            StartTransition(() => {
                LoadScene(newGameScene, firstCheckpointId);
            });
        }
        
        public void OnContinueGameSelected()
        {
            if (isTransitioning) return;
            
            // 检查是否有存档
            if (saveSystem == null || !saveSystem.HasSaveData())
            {
                if (showNoSaveWarning)
                {
                    Debug.LogWarning("没有找到存档数据！");
                    ShowNoSaveWarning();
                }
                return;
            }
            
            Debug.Log("继续游戏...");
            
            // 获取存档信息
            string lastScene = saveSystem.LoadLastScene();
            string checkpoint = saveSystem.LoadCheckpoint();
            
            if (string.IsNullOrEmpty(lastScene))
            {
                lastScene = newGameScene;
                checkpoint = firstCheckpointId;
            }
            
            // 开始过渡动画
            StartTransition(() => {
                LoadScene(lastScene, checkpoint);
            });
        }
        
        public void OnCoopModeSelected()
        {
            if (isTransitioning) return;
            
            Debug.Log("进入双人模式...");
            
            // 显示双人模式占位符界面
            StartCoroutine(ShowCoopModePlaceholder());
        }
        
        public void OnQuitGameSelected()
        {
            if (isTransitioning) return;
            
            Debug.Log("退出游戏...");
            
            // 开始退出动画
            StartTransition(() => {
                QuitApplication();
            });
        }
        
        public void OnSupportMeSelected()
        {
            if (isTransitioning) return;
            
            Debug.Log("显示支持页面...");
            
            // 显示支持页面
            StartCoroutine(ShowSupportPage());
        }
        
        void StartTransition(System.Action onComplete)
        {
            if (currentTransition != null)
            {
                StopCoroutine(currentTransition);
            }
            
            currentTransition = StartCoroutine(TransitionCoroutine(onComplete));
        }
        
        IEnumerator TransitionCoroutine(System.Action onComplete)
        {
            isTransitioning = true;
            
            // 这里可以添加过渡效果
            // 比如屏幕淡出、色块动画等
            
            float elapsedTime = 0f;
            while (elapsedTime < transitionDuration)
            {
                float progress = elapsedTime / transitionDuration;
                float curveProgress = transitionCurve.Evaluate(progress);
                
                // 更新过渡效果
                UpdateTransitionEffect(curveProgress);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 完成过渡
            UpdateTransitionEffect(1f);
            
            // 执行回调
            onComplete?.Invoke();
            
            isTransitioning = false;
            currentTransition = null;
        }
        
        void UpdateTransitionEffect(float progress)
        {
            // 这里可以更新过渡效果
            // 比如调整色块的透明度、位置等
            if (menuManager != null && menuManager.colorBlocks != null)
            {
                foreach (var block in menuManager.colorBlocks)
                {
                    if (block != null)
                    {
                        block.SetAlpha(1f - progress);
                    }
                }
            }
        }
        
        void LoadScene(string sceneName, string checkpointId)
        {
            // 使用静态的SceneLoader类
            SceneLoader.LoadScene(sceneName, checkpointId);
        }
        
        void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        void ShowNoSaveWarning()
        {
            // 这里可以显示一个警告UI
            Debug.LogWarning("没有找到存档数据，请先开始新游戏！");
            
            // 可以添加一个简单的UI提示
            StartCoroutine(ShowWarningMessage("没有找到存档数据！"));
        }
        
        IEnumerator ShowWarningMessage(string message)
        {
            // 简单的警告显示
            Debug.Log($"警告: {message}");
            
            // 等待2秒后自动关闭
            yield return new WaitForSeconds(2f);
            
            Debug.Log("警告已关闭");
        }
        
        IEnumerator ShowCoopModePlaceholder()
        {
            Debug.Log("双人模式功能正在开发中...");
            
            // 显示占位符信息
            yield return new WaitForSeconds(1f);
            
            Debug.Log("双人模式占位符已显示");
            
            // 可以在这里添加一个简单的双人模式界面
            // 或者直接返回主菜单
        }
        
        IEnumerator ShowSupportPage()
        {
            Debug.Log("感谢素素的支持！爱娘会继续努力的～(｡◕‿◕｡)");
            
            // 显示支持页面
            yield return new WaitForSeconds(1f);
            
            Debug.Log("支持页面已显示");
            
            // 可以在这里添加支持页面的UI
            // 或者打开外部链接
        }
        
        // 公共接口
        public bool IsTransitioning()
        {
            return isTransitioning;
        }
        
        public void SetNewGameScene(string sceneName)
        {
            newGameScene = sceneName;
        }
        
        public void SetFirstCheckpointId(string checkpointId)
        {
            firstCheckpointId = checkpointId;
        }
        
        public void SetCoopModeScene(string sceneName)
        {
            coopModeScene = sceneName;
        }
        
        public void SetSupportScene(string sceneName)
        {
            supportScene = sceneName;
        }
        
        public void ForceCheckSaveStatus()
        {
            CheckSaveStatus();
        }
        
        // 存档管理接口
        public void CreateTestSave()
        {
            if (saveSystem != null)
            {
                saveSystem.SaveLastScene(newGameScene);
                saveSystem.SaveCheckpoint(firstCheckpointId);
                Debug.Log("测试存档已创建");
                CheckSaveStatus();
            }
        }
        
        public void DeleteAllSaves()
        {
            if (saveSystem != null)
            {
                saveSystem.ResetAll();
                Debug.Log("所有存档已删除");
                CheckSaveStatus();
            }
        }
        
        // 调试接口
        [ContextMenu("Test New Game")]
        public void TestNewGame()
        {
            OnNewGameSelected();
        }
        
        [ContextMenu("Test Continue Game")]
        public void TestContinueGame()
        {
            OnContinueGameSelected();
        }
        
        [ContextMenu("Test Coop Mode")]
        public void TestCoopMode()
        {
            OnCoopModeSelected();
        }
        
        [ContextMenu("Test Quit Game")]
        public void TestQuitGame()
        {
            OnQuitGameSelected();
        }
        
        [ContextMenu("Test Support Me")]
        public void TestSupportMe()
        {
            OnSupportMeSelected();
        }
    }
}