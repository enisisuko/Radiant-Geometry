using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using FadedDreams.Core;

namespace FadedDreams.UI
{
    /// <summary>
    /// 五分区彩色主菜单系统
    /// 包含中心旋转小球、鼠标悬停光照增强、点击色彩蔓延效果
    /// </summary>
    public class ColorfulMainMenu : MonoBehaviour
    {
        [Header("UI References")]
        public Camera mainCamera;
        public Transform centerBall;
        public Transform[] menuSections = new Transform[5]; // 五个分区
        public Image[] sectionImages = new Image[5]; // 分区背景图片
        public Text[] sectionTexts = new Text[5]; // 分区文字
        
        [Header("Ball Animation")]
        public float ballRotationSpeed = 30f;
        public float ballPulseSpeed = 2f;
        public float ballPulseAmplitude = 0.1f;
        
        [Header("Lighting System")]
        public Light[] sectionLights = new Light[5]; // 每个分区的光源
        public float normalLightIntensity = 0.5f;
        public float hoverLightIntensity = 1.5f;
        public float lightTransitionSpeed = 5f;
        
        [Header("Color System")]
        public Color[] sectionColors = new Color[5] 
        {
            new Color(1f, 0.2f, 0.2f, 0.8f), // 新游戏 - 红色
            new Color(0.2f, 0.8f, 1f, 0.8f), // 继续游戏 - 蓝色
            new Color(0.8f, 0.2f, 1f, 0.8f), // 双人模式 - 紫色
            new Color(1f, 0.8f, 0.2f, 0.8f), // 退出游戏 - 黄色
            new Color(0.2f, 1f, 0.2f, 0.8f)  // 支持我 - 绿色
        };
        
        [Header("Color Spread Effect")]
        public float spreadSpeed = 2f;
        public AnimationCurve spreadCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Menu Actions")]
        public string newGameScene = "STORY0";
        public string firstCheckpointId = "101";
        
        private int currentHoveredSection = -1;
        private int currentSpreadSection = -1;
        private bool isSpreading = false;
        private Coroutine spreadCoroutine;
        
        // 菜单项名称
        private readonly string[] menuNames = { "新游戏", "继续游戏", "双人模式", "退出游戏", "支持我" };
        
        void Start()
        {
            InitializeMenu();
            SetupSectionColors();
        }
        
        void Update()
        {
            HandleMouseInput();
            UpdateBallAnimation();
            UpdateLighting();
        }
        
        void InitializeMenu()
        {
            if (!mainCamera) mainCamera = Camera.main;
            
            // 设置分区文字
            for (int i = 0; i < sectionTexts.Length && i < menuNames.Length; i++)
            {
                if (sectionTexts[i] != null)
                    sectionTexts[i].text = menuNames[i];
            }
            
            // 初始化光源强度
            for (int i = 0; i < sectionLights.Length; i++)
            {
                if (sectionLights[i] != null)
                    sectionLights[i].intensity = normalLightIntensity;
            }
        }
        
        void SetupSectionColors()
        {
            for (int i = 0; i < sectionImages.Length; i++)
            {
                if (sectionImages[i] != null && i < sectionColors.Length)
                {
                    sectionImages[i].color = sectionColors[i];
                }
            }
        }
        
        void HandleMouseInput()
        {
            if (isSpreading) return;
            
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            int hoveredSection = -1;
            
            if (Physics.Raycast(ray, out hit))
            {
                // 检查鼠标是否在某个分区内
                for (int i = 0; i < menuSections.Length; i++)
                {
                    if (menuSections[i] != null && hit.collider.transform == menuSections[i])
                    {
                        hoveredSection = i;
                        break;
                    }
                }
            }
            
            // 处理鼠标悬停
            if (hoveredSection != currentHoveredSection)
            {
                currentHoveredSection = hoveredSection;
            }
            
            // 处理鼠标点击
            if (Input.GetMouseButtonDown(0) && currentHoveredSection >= 0)
            {
                OnSectionClicked(currentHoveredSection);
            }
        }
        
        void UpdateBallAnimation()
        {
            if (centerBall != null)
            {
                // 旋转
                centerBall.Rotate(0, 0, ballRotationSpeed * Time.deltaTime);
                
                // 脉冲效果
                float pulse = 1f + Mathf.Sin(Time.time * ballPulseSpeed) * ballPulseAmplitude;
                centerBall.localScale = Vector3.one * pulse;
            }
        }
        
        void UpdateLighting()
        {
            for (int i = 0; i < sectionLights.Length; i++)
            {
                if (sectionLights[i] != null)
                {
                    float targetIntensity = (i == currentHoveredSection) ? hoverLightIntensity : normalLightIntensity;
                    sectionLights[i].intensity = Mathf.Lerp(sectionLights[i].intensity, targetIntensity, 
                        lightTransitionSpeed * Time.deltaTime);
                }
            }
        }
        
        void OnSectionClicked(int sectionIndex)
        {
            if (isSpreading) return;
            
            currentSpreadSection = sectionIndex;
            StartColorSpread();
        }
        
        void StartColorSpread()
        {
            if (spreadCoroutine != null)
                StopCoroutine(spreadCoroutine);
            
            spreadCoroutine = StartCoroutine(ColorSpreadCoroutine());
        }
        
        IEnumerator ColorSpreadCoroutine()
        {
            isSpreading = true;
            
            // 获取点击分区的颜色
            Color spreadColor = sectionColors[currentSpreadSection];
            
            // 开始蔓延效果
            float elapsedTime = 0f;
            float duration = 1f / spreadSpeed;
            
            while (elapsedTime < duration)
            {
                float progress = spreadCurve.Evaluate(elapsedTime / duration);
                
                // 将所有分区颜色向点击分区的颜色过渡
                for (int i = 0; i < sectionImages.Length; i++)
                {
                    if (sectionImages[i] != null)
                    {
                        Color currentColor = Color.Lerp(sectionColors[i], spreadColor, progress);
                        sectionImages[i].color = currentColor;
                    }
                }
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 确保最终颜色正确
            for (int i = 0; i < sectionImages.Length; i++)
            {
                if (sectionImages[i] != null)
                {
                    sectionImages[i].color = spreadColor;
                }
            }
            
            // 延迟一下再执行菜单动作
            yield return new WaitForSeconds(0.5f);
            
            // 执行对应的菜单动作
            ExecuteMenuAction(currentSpreadSection);
            
            isSpreading = false;
        }
        
        void ExecuteMenuAction(int sectionIndex)
        {
            switch (sectionIndex)
            {
                case 0: // 新游戏
                    NewGame();
                    break;
                case 1: // 继续游戏
                    ContinueGame();
                    break;
                case 2: // 双人模式
                    CoopMode();
                    break;
                case 3: // 退出游戏
                    QuitGame();
                    break;
                case 4: // 支持我
                    SupportMe();
                    break;
            }
        }
        
        public void NewGame()
        {
            // 清空存档
            SaveSystem.Instance.ResetAll();
            
            // 设置新游戏起点
            SaveSystem.Instance.SaveLastScene(newGameScene);
            SaveSystem.Instance.SaveCheckpoint(firstCheckpointId);
            
            // 加载STORY0场景
            SceneLoader.LoadScene(newGameScene, firstCheckpointId);
        }
        
        public void ContinueGame()
        {
            var scene = SaveSystem.Instance.LoadLastScene();
            if (string.IsNullOrEmpty(scene)) scene = newGameScene;
            SceneLoader.LoadScene(scene, SaveSystem.Instance.LoadCheckpoint());
        }
        
        public void CoopMode()
        {
            Debug.Log("双人模式功能尚未开发 (´∀｀)♡");
            // TODO: 实现双人模式
        }
        
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        public void SupportMe()
        {
            Debug.Log("感谢素素的支持！爱娘会继续努力的～(｡◕‿◕｡)");
            // TODO: 可以添加支持页面或链接
        }
    }
}