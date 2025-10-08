// FluidMenuCore.cs
// 核心管理器 - 负责色块引用和配置、初始化和生命周期、状态管理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using FadedDreams.Core;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单核心管理器 - 负责色块引用和配置、初始化和生命周期、状态管理
    /// </summary>
    public class FluidMenuCore : MonoBehaviour
    {
        [Header("== 色块配置 ==")]
        public FluidColorBlock[] colorBlocks = new FluidColorBlock[5];
        public Transform[] blockTransforms = new Transform[5];

        [Header("== 布局设置 ==")]
        public float blockSpacing = 200f;
        public float centerBlockSize = 1.2f;
        public float cornerBlockSize = 1.0f;

        [Header("== 动画设置 ==")]
        public float hoverScale = 1.5f;
        public float squeezeScale = 0.7f;
        public float animationSpeed = 5f;
        public AnimationCurve squeezeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== 场景配置 ==")]
        public string newGameScene = "STORY0";
        public string firstCheckpointId = "101";

        [Header("== 音频 ==")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;
        public AudioClip backgroundMusic;

        [Header("== 后期处理 ==")]
        public Camera menuCamera;
        public Canvas canvas;
        public bool enableBloom = true;

        [Header("== 调试 ==")]
        public bool verboseLogs = true;

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
        private bool isInitialized = false;
        private bool isMenuActive = true;

        // 事件
        public event System.Action OnMenuInitialized;
        public event System.Action OnMenuActivated;
        public event System.Action OnMenuDeactivated;

        #region Unity Lifecycle

        void Awake()
        {
            InitializeComponents();
        }

        void Start()
        {
            InitializeMenu();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 自动查找Canvas
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            // 自动查找相机
            if (menuCamera == null)
            {
                menuCamera = Camera.main;
                if (menuCamera == null)
                {
                    menuCamera = FindObjectOfType<Camera>();
                }
            }

            // 自动查找音频源
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (verboseLogs)
                Debug.Log("[FluidMenuCore] Components initialized");
        }

        /// <summary>
        /// 初始化菜单
        /// </summary>
        private void InitializeMenu()
        {
            if (isInitialized) return;

            // 初始化色块
            InitializeColorBlocks();

            // 设置初始布局
            SetupInitialLayout();

            // 播放背景音乐
            PlayBackgroundMusic();

            isInitialized = true;
            OnMenuInitialized?.Invoke();

            if (verboseLogs)
                Debug.Log("[FluidMenuCore] Menu initialized");
        }

        /// <summary>
        /// 初始化色块
        /// </summary>
        private void InitializeColorBlocks()
        {
            for (int i = 0; i < colorBlocks.Length; i++)
            {
                if (colorBlocks[i] != null)
                {
                    // 设置色块配置
                    MenuItemConfig config = menuItems[i];
                    colorBlocks[i].SetColors(config.primaryColor, config.secondaryColor);
                    colorBlocks[i].SetName(config.name);

                    // 设置初始缩放
                    float initialScale = (i == 2) ? centerBlockSize : cornerBlockSize; // 中心块更大
                    colorBlocks[i].SetScale(initialScale);
                }
            }
        }

        /// <summary>
        /// 设置初始布局
        /// </summary>
        private void SetupInitialLayout()
        {
            if (canvas == null) return;

            Vector3 centerPosition = canvas.transform.position;

            // 设置色块位置（十字形布局）
            Vector3[] positions = new Vector3[]
            {
                centerPosition + Vector3.left * blockSpacing,    // 左
                centerPosition + Vector3.up * blockSpacing,      // 上
                centerPosition,                                  // 中
                centerPosition + Vector3.down * blockSpacing,    // 下
                centerPosition + Vector3.right * blockSpacing    // 右
            };

            for (int i = 0; i < colorBlocks.Length; i++)
            {
                if (colorBlocks[i] != null)
                {
                    colorBlocks[i].SetPosition(positions[i]);
                }
            }
        }

        #endregion

        #region Audio Management

        /// <summary>
        /// 播放背景音乐
        /// </summary>
        private void PlayBackgroundMusic()
        {
            if (audioSource != null && backgroundMusic != null)
            {
                audioSource.clip = backgroundMusic;
                audioSource.loop = true;
                audioSource.volume = 0.5f;
                audioSource.Play();
            }
        }

        /// <summary>
        /// 播放悬停音效
        /// </summary>
        public void PlayHoverSound()
        {
            if (audioSource != null && hoverSound != null)
            {
                audioSource.PlayOneShot(hoverSound, 0.3f);
            }
        }

        /// <summary>
        /// 播放点击音效
        /// </summary>
        public void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound, 0.5f);
            }
        }

        /// <summary>
        /// 停止背景音乐
        /// </summary>
        public void StopBackgroundMusic()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }

        #endregion

        #region Menu State Management

        /// <summary>
        /// 激活菜单
        /// </summary>
        public void ActivateMenu()
        {
            if (isMenuActive) return;

            isMenuActive = true;
            OnMenuActivated?.Invoke();

            if (verboseLogs)
                Debug.Log("[FluidMenuCore] Menu activated");
        }

        /// <summary>
        /// 停用菜单
        /// </summary>
        public void DeactivateMenu()
        {
            if (!isMenuActive) return;

            isMenuActive = false;
            OnMenuDeactivated?.Invoke();

            if (verboseLogs)
                Debug.Log("[FluidMenuCore] Menu deactivated");
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取色块
        /// </summary>
        public FluidColorBlock GetColorBlock(int index)
        {
            if (index >= 0 && index < colorBlocks.Length)
            {
                return colorBlocks[index];
            }
            return null;
        }

        /// <summary>
        /// 获取色块变换
        /// </summary>
        public Transform GetBlockTransform(int index)
        {
            if (index >= 0 && index < blockTransforms.Length)
            {
                return blockTransforms[index];
            }
            return null;
        }

        /// <summary>
        /// 获取菜单项配置
        /// </summary>
        public MenuItemConfig GetMenuItemConfig(int index)
        {
            if (index >= 0 && index < menuItems.Length)
            {
                return menuItems[index];
            }
            return null;
        }

        /// <summary>
        /// 获取色块数量
        /// </summary>
        public int GetColorBlockCount()
        {
            return colorBlocks.Length;
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public bool IsInitialized()
        {
            return isInitialized;
        }

        /// <summary>
        /// 检查菜单是否激活
        /// </summary>
        public bool IsMenuActive()
        {
            return isMenuActive;
        }

        /// <summary>
        /// 获取新游戏场景
        /// </summary>
        public string GetNewGameScene()
        {
            return newGameScene;
        }

        /// <summary>
        /// 获取第一个检查点ID
        /// </summary>
        public string GetFirstCheckpointId()
        {
            return firstCheckpointId;
        }

        /// <summary>
        /// 设置色块
        /// </summary>
        public void SetColorBlock(int index, FluidColorBlock colorBlock)
        {
            if (index >= 0 && index < colorBlocks.Length)
            {
                colorBlocks[index] = colorBlock;
            }
        }

        /// <summary>
        /// 设置色块变换
        /// </summary>
        public void SetBlockTransform(int index, Transform transform)
        {
            if (index >= 0 && index < blockTransforms.Length)
            {
                blockTransforms[index] = transform;
            }
        }

        /// <summary>
        /// 重置菜单
        /// </summary>
        public void ResetMenu()
        {
            isInitialized = false;
            isMenuActive = true;

            // 重新初始化
            InitializeMenu();

            if (verboseLogs)
                Debug.Log("[FluidMenuCore] Menu reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Initialized: {isInitialized}, Active: {isMenuActive}, Blocks: {colorBlocks.Length}";
        }

        #endregion
    }

    /// <summary>
    /// 菜单项配置
    /// </summary>
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
}
