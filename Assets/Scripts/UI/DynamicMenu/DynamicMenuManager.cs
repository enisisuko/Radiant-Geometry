// DynamicMenuManager.cs
// 动态菜单管理器
// 功能：管理6个按键、聚光灯系统、菜单功能执行

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using FadedDreams.Core;
using FadedDreams.Audio;

namespace FadedDreams.UI
{
    /// <summary>
    /// 动态菜单管理器
    /// 整合按键、聚光灯和菜单功能
    /// </summary>
    public class DynamicMenuManager : MonoBehaviour
    {
        [Header("按键列表")]
        [Tooltip("6个菜单按键")]
        public List<FloatingMenuButton> menuButtons = new List<FloatingMenuButton>();

        [Header("聚光灯系统")]
        [Tooltip("聚光灯管理器")]
        public SpotlightManager spotlightManager;

        [Header("场景配置")]
        [Tooltip("新游戏场景")]
        public string newGameScene = "STORY0";

        [Tooltip("首个检查点ID")]
        public string firstCheckpointId = "101";

        [Header("设置面板")]
        [Tooltip("设置面板GameObject")]
        public GameObject settingsPanel;

        [Header("音频")]
        [Tooltip("音频源")]
        public AudioSource audioSource;

        [Tooltip("悬停音效")]
        public AudioClip hoverSound;

        [Tooltip("点击音效")]
        public AudioClip clickSound;

        [Tooltip("背景音乐")]
        public AudioClip backgroundMusic;

        [Header("开局设置")]
        [Tooltip("开局时让每个聚光灯锁定对应的按钮")]
        public bool assignSpotlightsToButtons = true;

        [Header("调试")]
        [Tooltip("显示调试信息")]
        public bool showDebugInfo = false;

        // 私有状态
        private bool isTransitioning = false;
        private bool isHoveringAnyButton = false;

        private void Start()
        {
            InitializeMenu();
        }

        private void Update()
        {
            // 如果开局分配了聚光灯，且没有悬停任何按键，持续更新聚光灯位置（因为按键会移动）
            if (assignSpotlightsToButtons && !isHoveringAnyButton && spotlightManager != null)
            {
                UpdateSpotlightsToButtons();
            }
        }

        /// <summary>
        /// 更新聚光灯位置（跟随移动的按键）
        /// </summary>
        private void UpdateSpotlightsToButtons()
        {
            int count = Mathf.Min(menuButtons.Count, spotlightManager.GetSpotlightCount());
            
            for (int i = 0; i < count; i++)
            {
                if (menuButtons[i] != null)
                {
                    Vector2 buttonPos = menuButtons[i].GetPosition();
                    spotlightManager.SetSpotlightTarget(i, buttonPos);
                }
            }
        }

        /// <summary>
        /// 初始化菜单
        /// </summary>
        private void InitializeMenu()
        {
            // 注册按键事件
            foreach (var button in menuButtons)
            {
                if (button != null)
                {
                    button.OnHoverEnter += OnButtonHoverEnter;
                    button.OnHoverExit += OnButtonHoverExit;
                    button.OnClick += OnButtonClick;
                }
            }

            // 设置音频
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D音效
            }

            // 播放背景音乐
            if (backgroundMusic != null && audioSource != null)
            {
                audioSource.clip = backgroundMusic;
                audioSource.loop = true;
                audioSource.Play();
            }

            // 隐藏设置面板
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            // 检查存档状态
            CheckSaveState();

            // 开局时让每个聚光灯锁定对应的按钮
            if (assignSpotlightsToButtons)
            {
                AssignSpotlightsToButtons();
            }

            if (showDebugInfo)
            {
                Debug.Log($"[DynamicMenuManager] 菜单初始化完成，{menuButtons.Count} 个按键");
            }
        }

        /// <summary>
        /// 开局时让每个聚光灯锁定对应的按钮（一对一）
        /// </summary>
        private void AssignSpotlightsToButtons()
        {
            if (spotlightManager == null) return;

            int count = Mathf.Min(menuButtons.Count, spotlightManager.GetSpotlightCount());
            
            for (int i = 0; i < count; i++)
            {
                if (menuButtons[i] != null)
                {
                    Vector2 buttonPos = menuButtons[i].GetPosition();
                    spotlightManager.SetSpotlightTarget(i, buttonPos);
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"[DynamicMenuManager] 已分配 {count} 个聚光灯到对应按键");
            }
        }

        /// <summary>
        /// 按键悬停进入事件
        /// </summary>
        private void OnButtonHoverEnter(FloatingMenuButton button)
        {
            isHoveringAnyButton = true;

            // 通知聚光灯管理器（所有聚光灯转向这个按键）
            if (spotlightManager != null)
            {
                spotlightManager.SetTarget(button);
            }

            // 播放悬停音效
            PlaySound(hoverSound);

            if (showDebugInfo)
            {
                Debug.Log($"[DynamicMenuManager] 悬停: {button.buttonType}");
            }
        }

        /// <summary>
        /// 按键悬停退出事件
        /// </summary>
        private void OnButtonHoverExit(FloatingMenuButton button)
        {
            isHoveringAnyButton = false;

            // 退出悬停后，恢复聚光灯到对应的按键（一对一）
            if (assignSpotlightsToButtons && spotlightManager != null)
            {
                AssignSpotlightsToButtons();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[DynamicMenuManager] 退出悬停: {button.buttonType}");
            }
        }

        /// <summary>
        /// 按键点击事件
        /// </summary>
        private void OnButtonClick(FloatingMenuButton button)
        {
            if (isTransitioning) return;

            // 播放点击音效
            PlaySound(clickSound);

            // 根据按键类型执行对应功能
            switch (button.buttonType)
            {
                case FloatingMenuButton.MenuButtonType.NewGame:
                    NewGame();
                    break;
                case FloatingMenuButton.MenuButtonType.Continue:
                    ContinueGame();
                    break;
                case FloatingMenuButton.MenuButtonType.Coop:
                    CoopMode();
                    break;
                case FloatingMenuButton.MenuButtonType.Settings:
                    OpenSettings();
                    break;
                case FloatingMenuButton.MenuButtonType.Support:
                    SupportMe();
                    break;
                case FloatingMenuButton.MenuButtonType.Quit:
                    QuitGame();
                    break;
            }

            if (showDebugInfo)
            {
                Debug.Log($"[DynamicMenuManager] 点击: {button.buttonType}");
            }
        }

        /// <summary>
        /// 新游戏
        /// </summary>
        private void NewGame()
        {
            isTransitioning = true;

            // 清空存档
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.ResetAll();
                SaveSystem.Instance.SaveLastScene(newGameScene);
                SaveSystem.Instance.SaveCheckpoint(firstCheckpointId);
            }

            // 加载场景
            SceneLoader.LoadScene(newGameScene, firstCheckpointId);

            Debug.Log("[DynamicMenuManager] 开始新游戏");
        }

        /// <summary>
        /// 继续游戏
        /// </summary>
        private void ContinueGame()
        {
            isTransitioning = true;

            string scene = newGameScene;
            string checkpoint = firstCheckpointId;

            if (SaveSystem.Instance != null)
            {
                string savedScene = SaveSystem.Instance.LoadLastScene();
                if (!string.IsNullOrEmpty(savedScene))
                {
                    scene = savedScene;
                }
                
                string savedCheckpoint = SaveSystem.Instance.LoadCheckpoint();
                if (!string.IsNullOrEmpty(savedCheckpoint))
                {
                    checkpoint = savedCheckpoint;
                }
            }

            // 加载场景
            SceneLoader.LoadScene(scene, checkpoint);

            Debug.Log($"[DynamicMenuManager] 继续游戏: {scene} @ {checkpoint}");
        }

        /// <summary>
        /// 双人模式
        /// </summary>
        private void CoopMode()
        {
            Debug.Log("[DynamicMenuManager] 双人模式功能尚未开发 (´∀｀)♡");
            isTransitioning = false;
        }

        /// <summary>
        /// 打开设置
        /// </summary>
        private void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
                Debug.Log("[DynamicMenuManager] 设置面板已打开");
            }
            else
            {
                Debug.LogWarning("[DynamicMenuManager] 未找到设置面板");
            }

            isTransitioning = false;
        }

        /// <summary>
        /// 支持我
        /// </summary>
        private void SupportMe()
        {
            Debug.Log("[DynamicMenuManager] 感谢素素的支持！爱娘会继续努力的～(｡◕‿◕｡)");
            isTransitioning = false;
        }

        /// <summary>
        /// 退出游戏
        /// </summary>
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            Debug.Log("[DynamicMenuManager] 退出游戏");
        }

        /// <summary>
        /// 检查存档状态
        /// </summary>
        private void CheckSaveState()
        {
            bool hasSave = false;

            if (SaveSystem.Instance != null)
            {
                string lastScene = SaveSystem.Instance.LoadLastScene();
                hasSave = !string.IsNullOrEmpty(lastScene);
            }

            // 设置"继续游戏"按键的可用性
            var continueButton = menuButtons.Find(b => b.buttonType == FloatingMenuButton.MenuButtonType.Continue);
            if (continueButton != null)
            {
                // 如果没有存档，可以改变按键的视觉状态（例如变暗）
                // 这里简单地保持可交互，但可以扩展
            }

            if (showDebugInfo)
            {
                Debug.Log($"[DynamicMenuManager] 存档检测: {(hasSave ? "有存档" : "无存档")}");
            }
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// 查找并添加所有按键
        /// </summary>
        public void FindAllButtons()
        {
            menuButtons.Clear();
            var found = FindObjectsByType<FloatingMenuButton>(FindObjectsSortMode.None);
            menuButtons.AddRange(found);

            // 重新注册事件
            InitializeMenu();

            Debug.Log($"[DynamicMenuManager] 找到 {menuButtons.Count} 个按键");
        }

#if UNITY_EDITOR
        // 编辑器辅助功能

        [ContextMenu("查找所有按键")]
        private void FindAllButtonsEditor()
        {
            FindAllButtons();
        }

        [ContextMenu("测试：新游戏")]
        private void TestNewGame()
        {
            NewGame();
        }

        [ContextMenu("测试：打开设置")]
        private void TestSettings()
        {
            OpenSettings();
        }
#endif
    }
}

