using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using FadedDreams.Core;

namespace FadedDreams.UI
{
    /// <summary>
    /// 彩色主菜单管理器
    /// 整合所有菜单系统，提供统一的接口和完整的用户体验
    /// </summary>
    public class ColorfulMenuManager : MonoBehaviour
    {
        [Header("System References")]
        public ColorfulMainMenu mainMenu;
        public CenterRotatingBall centerBall;
        public MenuLightingSystem lightingSystem;
        public ColorSpreadEffect colorSpreadEffect;
        
        [Header("UI Canvas")]
        public Canvas menuCanvas;
        public CanvasGroup fadeCanvasGroup;
        
        [Header("Menu Sections")]
        public Transform[] menuSections = new Transform[5];
        public Image[] sectionImages = new Image[5];
        public Text[] sectionTexts = new Text[5];
        public Button[] sectionButtons = new Button[5];
        
        [Header("Animation Settings")]
        public float fadeInDuration = 1f;
        public float fadeOutDuration = 0.8f;
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;
        public AudioClip spreadSound;
        public AudioClip backgroundMusic;
        
        [Header("Menu Configuration")]
        public string newGameScene = "STORY0";
        public string firstCheckpointId = "101";
        public bool enableAutoSave = true;
        
        private bool isInitialized = false;
        private bool isTransitioning = false;
        private int currentHoveredSection = -1;
        
        // 菜单项配置
        private readonly MenuItemConfig[] menuItems = new MenuItemConfig[]
        {
            new MenuItemConfig("新游戏", "开始全新的冒险之旅", "NewGame"),
            new MenuItemConfig("继续游戏", "从上次保存的地方继续", "ContinueGame"),
            new MenuItemConfig("双人模式", "与朋友一起游玩", "CoopMode"),
            new MenuItemConfig("退出游戏", "离开游戏", "QuitGame"),
            new MenuItemConfig("支持我", "支持开发者", "SupportMe")
        };
        
        [System.Serializable]
        public class MenuItemConfig
        {
            public string name;
            public string description;
            public string action;
            
            public MenuItemConfig(string name, string description, string action)
            {
                this.name = name;
                this.description = description;
                this.action = action;
            }
        }
        
        void Awake()
        {
            InitializeComponents();
        }
        
        void Start()
        {
            StartCoroutine(InitializeMenu());
        }
        
        void Update()
        {
            if (!isInitialized || isTransitioning) return;
            
            HandleInput();
            UpdateHoverEffects();
        }
        
        void InitializeComponents()
        {
            // 自动获取组件引用
            if (mainMenu == null) mainMenu = GetComponent<ColorfulMainMenu>();
            if (centerBall == null) centerBall = FindObjectOfType<CenterRotatingBall>();
            if (lightingSystem == null) lightingSystem = GetComponent<MenuLightingSystem>();
            if (colorSpreadEffect == null) colorSpreadEffect = GetComponent<ColorSpreadEffect>();
            
            // 设置音频源
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            
            // 设置淡入淡出画布组
            if (fadeCanvasGroup == null)
            {
                GameObject fadeObj = new GameObject("FadeCanvasGroup");
                fadeObj.transform.SetParent(transform);
                fadeCanvasGroup = fadeObj.AddComponent<CanvasGroup>();
                fadeCanvasGroup.alpha = 0f;
            }
        }
        
        IEnumerator InitializeMenu()
        {
            // 等待一帧确保所有组件都已初始化
            yield return null;
            
            // 设置菜单项
            SetupMenuItems();
            
            // 初始化各个系统
            InitializeSystems();
            
            // 淡入效果
            yield return StartCoroutine(FadeIn());
            
            // 播放背景音乐
            PlayBackgroundMusic();
            
            isInitialized = true;
        }
        
        void SetupMenuItems()
        {
            for (int i = 0; i < menuItems.Length && i < sectionTexts.Length; i++)
            {
                if (sectionTexts[i] != null)
                {
                    sectionTexts[i].text = menuItems[i].name;
                }
                
                if (sectionButtons[i] != null)
                {
                    int index = i; // 闭包变量
                    sectionButtons[i].onClick.AddListener(() => OnSectionClicked(index));
                }
            }
        }
        
        void InitializeSystems()
        {
            // 初始化光照系统
            if (lightingSystem != null)
            {
                lightingSystem.ResetToDefault();
            }
            
            // 初始化色彩蔓延系统
            if (colorSpreadEffect != null)
            {
                colorSpreadEffect.ResetToOriginal();
            }
            
            // 初始化中心小球
            if (centerBall != null)
            {
                centerBall.ResetToBase();
            }
        }
        
        void HandleInput()
        {
            // 处理鼠标悬停
            HandleMouseHover();
            
            // 处理键盘输入
            HandleKeyboardInput();
        }
        
        void HandleMouseHover()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            int hoveredSection = -1;
            
            if (Physics.Raycast(ray, out hit))
            {
                for (int i = 0; i < menuSections.Length; i++)
                {
                    if (menuSections[i] != null && hit.collider.transform == menuSections[i])
                    {
                        hoveredSection = i;
                        break;
                    }
                }
            }
            
            if (hoveredSection != currentHoveredSection)
            {
                if (currentHoveredSection >= 0)
                {
                    OnSectionHoverExit(currentHoveredSection);
                }
                
                currentHoveredSection = hoveredSection;
                
                if (currentHoveredSection >= 0)
                {
                    OnSectionHoverEnter(currentHoveredSection);
                }
            }
        }
        
        void HandleKeyboardInput()
        {
            // 数字键选择
            for (int i = 1; i <= 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    OnSectionClicked(i - 1);
                }
            }
            
            // ESC键退出
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnSectionClicked(3); // 退出游戏
            }
        }
        
        void OnSectionHoverEnter(int sectionIndex)
        {
            // 更新光照系统
            if (lightingSystem != null)
            {
                lightingSystem.SetHoveredSection(sectionIndex);
            }
            
            // 更新中心小球
            if (centerBall != null)
            {
                centerBall.SetHovered(true);
            }
            
            // 播放悬停音效
            PlaySound(hoverSound);
            
            // 显示描述文字（如果有的话）
            ShowDescription(sectionIndex);
        }
        
        void OnSectionHoverExit(int sectionIndex)
        {
            // 重置光照系统
            if (lightingSystem != null)
            {
                lightingSystem.SetHoveredSection(-1);
            }
            
            // 重置中心小球
            if (centerBall != null)
            {
                centerBall.SetHovered(false);
            }
        }
        
        void OnSectionClicked(int sectionIndex)
        {
            if (isTransitioning || sectionIndex < 0 || sectionIndex >= menuItems.Length)
                return;
            
            // 播放点击音效
            PlaySound(clickSound);
            
            // 开始色彩蔓延效果
            if (colorSpreadEffect != null)
            {
                colorSpreadEffect.StartColorSpread(sectionIndex);
            }
            
            // 播放蔓延音效
            PlaySound(spreadSound);
            
            // 延迟执行菜单动作
            StartCoroutine(ExecuteMenuActionDelayed(sectionIndex));
        }
        
        IEnumerator ExecuteMenuActionDelayed(int sectionIndex)
        {
            // 等待色彩蔓延效果完成
            if (colorSpreadEffect != null)
            {
                while (colorSpreadEffect.IsSpreading())
                {
                    yield return null;
                }
            }
            
            // 额外延迟
            yield return new WaitForSeconds(0.5f);
            
            // 执行对应的菜单动作
            ExecuteMenuAction(sectionIndex);
        }
        
        void ExecuteMenuAction(int sectionIndex)
        {
            string action = menuItems[sectionIndex].action;
            
            switch (action)
            {
                case "NewGame":
                    NewGame();
                    break;
                case "ContinueGame":
                    ContinueGame();
                    break;
                case "CoopMode":
                    CoopMode();
                    break;
                case "QuitGame":
                    QuitGame();
                    break;
                case "SupportMe":
                    SupportMe();
                    break;
            }
        }
        
        public void NewGame()
        {
            StartCoroutine(TransitionToScene(newGameScene, firstCheckpointId));
        }
        
        public void ContinueGame()
        {
            var scene = SaveSystem.Instance.LoadLastScene();
            if (string.IsNullOrEmpty(scene)) scene = newGameScene;
            var checkpoint = SaveSystem.Instance.LoadCheckpoint();
            StartCoroutine(TransitionToScene(scene, checkpoint));
        }
        
        public void CoopMode()
        {
            Debug.Log("双人模式功能尚未开发 (´∀｀)♡");
            // TODO: 实现双人模式
        }
        
        public void QuitGame()
        {
            StartCoroutine(QuitGameCoroutine());
        }
        
        public void SupportMe()
        {
            Debug.Log("感谢素素的支持！爱娘会继续努力的～(｡◕‿◕｡)");
            // TODO: 可以添加支持页面或链接
        }
        
        IEnumerator TransitionToScene(string sceneName, string checkpointId)
        {
            isTransitioning = true;
            
            // 淡出效果
            yield return StartCoroutine(FadeOut());
            
            // 加载场景
            SceneLoader.LoadScene(sceneName, checkpointId);
        }
        
        IEnumerator QuitGameCoroutine()
        {
            isTransitioning = true;
            
            // 淡出效果
            yield return StartCoroutine(FadeOut());
            
            // 退出游戏
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        IEnumerator FadeIn()
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < fadeInDuration)
            {
                float progress = fadeCurve.Evaluate(elapsedTime / fadeInDuration);
                fadeCanvasGroup.alpha = 1f - progress;
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            fadeCanvasGroup.alpha = 0f;
        }
        
        IEnumerator FadeOut()
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < fadeOutDuration)
            {
                float progress = fadeCurve.Evaluate(elapsedTime / fadeOutDuration);
                fadeCanvasGroup.alpha = progress;
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            fadeCanvasGroup.alpha = 1f;
        }
        
        void UpdateHoverEffects()
        {
            // 更新各个系统的悬停效果
            if (lightingSystem != null)
            {
                lightingSystem.SetHoveredSection(currentHoveredSection);
            }
        }
        
        void ShowDescription(int sectionIndex)
        {
            // 可以在这里显示菜单项的描述
            if (sectionIndex >= 0 && sectionIndex < menuItems.Length)
            {
                Debug.Log($"悬停: {menuItems[sectionIndex].description}");
            }
        }
        
        void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        
        void PlayBackgroundMusic()
        {
            if (audioSource != null && backgroundMusic != null)
            {
                audioSource.clip = backgroundMusic;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        
        public void SetMenuActive(bool active)
        {
            if (menuCanvas != null)
            {
                menuCanvas.gameObject.SetActive(active);
            }
        }
        
        public bool IsTransitioning()
        {
            return isTransitioning;
        }
    }
}