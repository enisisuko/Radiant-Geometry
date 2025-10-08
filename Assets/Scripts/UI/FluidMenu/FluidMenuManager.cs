using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using FadedDreams.Core;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体色块菜单管理器
    /// 负责管理5个色块的交互、动画和功能执行
    /// </summary>
    public class FluidMenuManager : MonoBehaviour
    {
        [Header("色块配置")]
        public FluidColorBlock[] colorBlocks = new FluidColorBlock[5];
        public Transform[] blockTransforms = new Transform[5];
        
        [Header("布局设置")]
        public float blockSpacing = 200f;
        public float centerBlockSize = 1.2f;
        public float cornerBlockSize = 1.0f;
        
        [Header("动画设置")]
        public float hoverScale = 1.5f;
        public float squeezeScale = 0.7f;
        public float animationSpeed = 5f;
        public AnimationCurve squeezeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("场景配置")]
        public string newGameScene = "STORY0";
        public string firstCheckpointId = "101";
        
        [Header("音频")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;
        public AudioClip backgroundMusic;
        
        [Header("后期处理")]
        public Camera menuCamera;
        public bool enableBloom = true;
        
        // 菜单项配置
        private readonly MenuItemConfig[] menuItems = new MenuItemConfig[]
        {
            new MenuItemConfig("新游戏", new Color(0f, 0.85f, 1f), new Color(0f, 0.47f, 1f)), // 青蓝色
            new MenuItemConfig("继续游戏", new Color(0.73f, 0.4f, 1f), new Color(0.48f, 0.18f, 1f)), // 柔和紫
            new MenuItemConfig("双人模式", new Color(1f, 0.6f, 0.34f), new Color(1f, 0.42f, 0.21f)), // 活力橙
            new MenuItemConfig("退出游戏", new Color(1f, 0.42f, 0.42f), new Color(0.79f, 0.16f, 0.16f)), // 暗红色
            new MenuItemConfig("支持我", new Color(1f, 0.85f, 0.24f), new Color(1f, 0.7f, 0.1f)) // 温暖金
        };
        
        // 状态管理
        private int currentHoveredIndex = -1;
        private int currentSelectedIndex = -1;
        private bool isTransitioning = false;
        private bool isInitialized = false;
        
        // 动画状态
        private float[] targetScales = new float[5];
        private float[] currentScales = new float[5];
        private Vector3[] targetPositions = new Vector3[5];
        private Vector3[] currentPositions = new Vector3[5];
        private float[] scaleVelocities = new float[5];
        private Vector3[] positionVelocities = new Vector3[5];
        
        [System.Serializable]
        public class MenuItemConfig
        {
            public string name;
            public Color primaryColor;
            public Color secondaryColor;
            
            public MenuItemConfig(string name, Color primary, Color secondary)
            {
                this.name = name;
                this.primaryColor = primary;
                this.secondaryColor = secondary;
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
            UpdateAnimations();
        }
        
        void InitializeComponents()
        {
            // 自动获取组件引用
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            
            if (menuCamera == null) menuCamera = Camera.main;
            
            // 初始化动画状态
            for (int i = 0; i < 5; i++)
            {
                targetScales[i] = (i == 1) ? centerBlockSize : cornerBlockSize; // 中心块稍大
                currentScales[i] = targetScales[i];
                targetPositions[i] = GetBlockPosition(i);
                currentPositions[i] = targetPositions[i];
            }
        }
        
        IEnumerator InitializeMenu()
        {
            // 等待一帧确保所有组件都已初始化
            yield return null;
            
            // 设置色块
            SetupColorBlocks();
            
            // 初始化布局
            SetupLayout();
            
            // 淡入效果
            yield return StartCoroutine(FadeInBlocks());
            
            // 播放背景音乐
            PlayBackgroundMusic();
            
            isInitialized = true;
        }
        
        void SetupColorBlocks()
        {
            for (int i = 0; i < colorBlocks.Length && i < menuItems.Length; i++)
            {
                if (colorBlocks[i] != null)
                {
                    colorBlocks[i].Initialize(menuItems[i], i);
                }
            }
        }
        
        void SetupLayout()
        {
            for (int i = 0; i < blockTransforms.Length; i++)
            {
                if (blockTransforms[i] != null)
                {
                    Vector3 position = GetBlockPosition(i);
                    blockTransforms[i].position = position;
                    targetPositions[i] = position;
                    currentPositions[i] = position;
                }
            }
        }
        
        Vector3 GetBlockPosition(int index)
        {
            float halfSpacing = blockSpacing * 0.5f;
            
            switch (index)
            {
                case 0: return new Vector3(-halfSpacing, halfSpacing, 0); // 左上：新游戏
                case 1: return new Vector3(0, 0, 0); // 中心：继续游戏
                case 2: return new Vector3(halfSpacing, halfSpacing, 0); // 右上：双人模式
                case 3: return new Vector3(halfSpacing, -halfSpacing, 0); // 右下：退出游戏
                case 4: return new Vector3(-halfSpacing, -halfSpacing, 0); // 左下：支持我
                default: return Vector3.zero;
            }
        }
        
        void HandleInput()
        {
            HandleMouseInput();
            HandleKeyboardInput();
        }
        
        void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit))
                {
                    for (int i = 0; i < blockTransforms.Length; i++)
                    {
                        if (blockTransforms[i] != null && hit.collider.transform == blockTransforms[i])
                        {
                            OnBlockClicked(i);
                            break;
                        }
                    }
                }
            }
            else
            {
                // 处理悬停
                Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                int hoveredIndex = -1;
                if (Physics.Raycast(ray, out hit))
                {
                    for (int i = 0; i < blockTransforms.Length; i++)
                    {
                        if (blockTransforms[i] != null && hit.collider.transform == blockTransforms[i])
                        {
                            hoveredIndex = i;
                            break;
                        }
                    }
                }
                
                if (hoveredIndex != currentHoveredIndex)
                {
                    OnHoverChanged(hoveredIndex);
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
                    OnBlockClicked(i - 1);
                }
            }
            
            // ESC键退出
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnBlockClicked(3); // 退出游戏
            }
        }
        
        void OnHoverChanged(int newHoveredIndex)
        {
            if (currentHoveredIndex == newHoveredIndex) return;
            
            // 退出之前的悬停
            if (currentHoveredIndex >= 0)
            {
                OnBlockHoverExit(currentHoveredIndex);
            }
            
            currentHoveredIndex = newHoveredIndex;
            
            // 进入新的悬停
            if (currentHoveredIndex >= 0)
            {
                OnBlockHoverEnter(currentHoveredIndex);
            }
        }
        
        void OnBlockHoverEnter(int index)
        {
            // 播放悬停音效
            PlaySound(hoverSound);
            
            // 更新目标尺寸
            targetScales[index] = hoverScale;
            
            // 挤压其他色块
            for (int i = 0; i < 5; i++)
            {
                if (i != index)
                {
                    targetScales[i] = squeezeScale;
                }
            }
            
            // 更新色块状态
            if (colorBlocks[index] != null)
            {
                colorBlocks[index].SetHovered(true);
            }
        }
        
        void OnBlockHoverExit(int index)
        {
            // 重置所有色块尺寸
            for (int i = 0; i < 5; i++)
            {
                targetScales[i] = (i == 1) ? centerBlockSize : cornerBlockSize;
            }
            
            // 更新色块状态
            if (colorBlocks[index] != null)
            {
                colorBlocks[index].SetHovered(false);
            }
        }
        
        void OnBlockClicked(int index)
        {
            if (isTransitioning || index < 0 || index >= 5) return;
            
            currentSelectedIndex = index;
            isTransitioning = true;
            
            // 播放点击音效
            PlaySound(clickSound);
            
            // 开始选择动画
            StartCoroutine(ExecuteSelectionAnimation(index));
        }
        
        IEnumerator ExecuteSelectionAnimation(int index)
        {
            // 第一阶段：扩展选中的色块，挤压其他色块
            float animationTime = 0.5f;
            float elapsedTime = 0f;
            
            Vector3[] startPositions = new Vector3[5];
            Vector3[] startScales = new Vector3[5];
            
            for (int i = 0; i < 5; i++)
            {
                startPositions[i] = currentPositions[i];
                startScales[i] = new Vector3(currentScales[i], currentScales[i], 1f);
            }
            
            while (elapsedTime < animationTime)
            {
                float progress = elapsedTime / animationTime;
                float curveProgress = squeezeCurve.Evaluate(progress);
                
                // 选中的色块扩展
                targetScales[index] = Mathf.Lerp(hoverScale, 3f, curveProgress);
                
                // 其他色块被挤出
                for (int i = 0; i < 5; i++)
                {
                    if (i != index)
                    {
                        Vector3 direction = (currentPositions[i] - currentPositions[index]).normalized;
                        targetPositions[i] = startPositions[i] + direction * 1000f * curveProgress;
                        targetScales[i] = Mathf.Lerp(squeezeScale, 0.1f, curveProgress);
                    }
                }
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 等待一下让动画完成
            yield return new WaitForSeconds(0.3f);
            
            // 执行对应的菜单动作
            ExecuteMenuAction(index);
        }
        
        void ExecuteMenuAction(int index)
        {
            switch (index)
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
            
            // 加载新游戏场景
            SceneLoader.LoadScene(newGameScene, firstCheckpointId);
        }
        
        public void ContinueGame()
        {
            var scene = SaveSystem.Instance.LoadLastScene();
            if (string.IsNullOrEmpty(scene)) scene = newGameScene;
            var checkpoint = SaveSystem.Instance.LoadCheckpoint();
            SceneLoader.LoadScene(scene, checkpoint);
        }
        
        public void CoopMode()
        {
            Debug.Log("双人模式功能尚未开发 (´∀｀)♡");
            // TODO: 实现双人模式
            StartCoroutine(ResetMenuAfterDelay(1f));
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
            StartCoroutine(ResetMenuAfterDelay(1f));
        }
        
        IEnumerator ResetMenuAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ResetMenu();
        }
        
        void ResetMenu()
        {
            isTransitioning = false;
            currentSelectedIndex = -1;
            currentHoveredIndex = -1;
            
            // 重置所有色块状态
            for (int i = 0; i < 5; i++)
            {
                targetScales[i] = (i == 1) ? centerBlockSize : cornerBlockSize;
                targetPositions[i] = GetBlockPosition(i);
                
                if (colorBlocks[i] != null)
                {
                    colorBlocks[i].SetHovered(false);
                }
            }
        }
        
        void UpdateAnimations()
        {
            for (int i = 0; i < 5; i++)
            {
                // 平滑缩放
                currentScales[i] = Mathf.SmoothDamp(currentScales[i], targetScales[i], ref scaleVelocities[i], 0.2f);
                
                // 平滑位置
                currentPositions[i] = Vector3.SmoothDamp(currentPositions[i], targetPositions[i], ref positionVelocities[i], 0.2f);
                
                // 应用变换
                if (blockTransforms[i] != null)
                {
                    blockTransforms[i].localScale = Vector3.one * currentScales[i];
                    blockTransforms[i].position = currentPositions[i];
                }
                
                // 更新色块的Shader参数
                if (colorBlocks[i] != null)
                {
                    colorBlocks[i].UpdateShaderParameters(currentScales[i], currentPositions[i]);
                }
            }
        }
        
        IEnumerator FadeInBlocks()
        {
            // 初始状态：所有色块都是透明的
            for (int i = 0; i < colorBlocks.Length; i++)
            {
                if (colorBlocks[i] != null)
                {
                    colorBlocks[i].SetAlpha(0f);
                }
            }
            
            // 依次淡入色块
            for (int i = 0; i < colorBlocks.Length; i++)
            {
                if (colorBlocks[i] != null)
                {
                    yield return StartCoroutine(colorBlocks[i].FadeIn(0.5f));
                }
                yield return new WaitForSeconds(0.1f);
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
        
        public bool IsTransitioning()
        {
            return isTransitioning;
        }
        
        public int GetCurrentHoveredIndex()
        {
            return currentHoveredIndex;
        }
    }
}