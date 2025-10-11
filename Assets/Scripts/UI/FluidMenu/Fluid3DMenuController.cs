// Fluid3DMenuController.cs
// 3D流体菜单控制器 - 专门为3D场景设计
// 功能：3D射线检测、6个菜单选项、流体交互、光照联动

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using FadedDreams.Core;
using FadedDreams.Audio;

namespace FadedDreams.UI
{
    /// <summary>
    /// 3D流体菜单控制器
    /// 管理6个3D菜单选项的交互和动画
    /// </summary>
    public class Fluid3DMenuController : MonoBehaviour
    {
        [Header("菜单项引用")]
        [Tooltip("6个菜单项（新游戏、继续、双人、设置、支持、退出）")]
        public Fluid3DMenuItem[] menuItems = new Fluid3DMenuItem[6];

        [Header("相机设置")]
        [Tooltip("主相机")]
        public Camera menuCamera;

        [Tooltip("射线检测层")]
        public LayerMask raycastMask = ~0;

        [Tooltip("射线最大距离")]
        public float raycastDistance = 100f;

        [Header("流体交互")]
        [Tooltip("流体模拟器")]
        public Fluid3DSimulator fluidSimulator;

        [Tooltip("悬停时的流体作用力")]
        public float hoverFluidForce = 50f;

        [Tooltip("点击时的流体作用力")]
        public float clickFluidForce = 150f;

        [Header("光照联动")]
        [Tooltip("光照系统")]
        public MenuLightingEnhanced lightingSystem;

        [Header("场景配置")]
        public string newGameScene = "STORY0";
        public string firstCheckpointId = "101";

        [Header("音频")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;
        public AudioClip backgroundMusic;

        [Header("设置面板")]
        [Tooltip("设置面板GameObject")]
        public GameObject settingsPanel;

        [Header("调试")]
        public bool showDebugRay = false;

        // 私有状态
        private int currentHoveredIndex = -1;
        private bool isTransitioning = false;
        private Vector3 lastMouseWorldPos;

        private void Start()
        {
            InitializeMenu();
        }

        /// <summary>
        /// 初始化菜单
        /// </summary>
        private void InitializeMenu()
        {
            // 自动查找相机
            if (menuCamera == null)
            {
                menuCamera = Camera.main;
            }

            // 自动查找组件
            if (fluidSimulator == null)
            {
                fluidSimulator = FindFirstObjectByType<Fluid3DSimulator>();
            }

            if (lightingSystem == null)
            {
                lightingSystem = FindFirstObjectByType<MenuLightingEnhanced>();
            }

            // 自动查找音频源
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

            // 检查存档状态，设置"继续游戏"可用性
            CheckSaveState();

            // 隐藏设置面板
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            Debug.Log("[Fluid3DMenuController] 菜单初始化完成");
        }

        private void Update()
        {
            if (isTransitioning) return;

            // 处理3D射线检测
            Handle3DRaycast();
        }

        /// <summary>
        /// 处理3D射线检测（鼠标交互）
        /// </summary>
        private void Handle3DRaycast()
        {
            if (menuCamera == null) return;

            // 从鼠标位置发射射线
            Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            int newHoveredIndex = -1;

            // 检测射线碰撞
            if (Physics.Raycast(ray, out hit, raycastDistance, raycastMask))
            {
                // 检查是否击中菜单项
                for (int i = 0; i < menuItems.Length; i++)
                {
                    if (menuItems[i] != null && hit.collider.gameObject == menuItems[i].gameObject)
                    {
                        newHoveredIndex = i;
                        lastMouseWorldPos = hit.point;
                        break;
                    }
                }

                // 调试：显示射线
                if (showDebugRay)
                {
                    Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green);
                }
            }

            // 处理悬停变化
            if (newHoveredIndex != currentHoveredIndex)
            {
                OnHoverChanged(newHoveredIndex);
            }

            // 处理点击
            if (Input.GetMouseButtonDown(0) && newHoveredIndex >= 0)
            {
                OnMenuItemClicked(newHoveredIndex);
            }
        }

        /// <summary>
        /// 悬停变化处理
        /// </summary>
        private void OnHoverChanged(int newIndex)
        {
            // 取消之前的悬停
            if (currentHoveredIndex >= 0 && currentHoveredIndex < menuItems.Length)
            {
                if (menuItems[currentHoveredIndex] != null)
                {
                    menuItems[currentHoveredIndex].SetHovered(false);
                }

                // 取消光照悬停
                if (lightingSystem != null)
                {
                    lightingSystem.SetMenuItemHovered(currentHoveredIndex, false);
                }
            }

            currentHoveredIndex = newIndex;

            // 应用新的悬停
            if (currentHoveredIndex >= 0 && currentHoveredIndex < menuItems.Length)
            {
                if (menuItems[currentHoveredIndex] != null)
                {
                    menuItems[currentHoveredIndex].SetHovered(true);
                }

                // 应用光照悬停
                if (lightingSystem != null)
                {
                    lightingSystem.SetMenuItemHovered(currentHoveredIndex, true);
                }

                // 播放悬停音效
                PlaySound(hoverSound);

                // 触发流体交互
                TriggerFluidInteraction(lastMouseWorldPos, hoverFluidForce);
            }
        }

        /// <summary>
        /// 菜单项点击处理
        /// </summary>
        private void OnMenuItemClicked(int index)
        {
            if (index < 0 || index >= menuItems.Length) return;
            if (menuItems[index] != null && !menuItems[index].interactable) return;

            isTransitioning = true;

            // 触发选中效果
            if (menuItems[index] != null)
            {
                menuItems[index].TriggerSelectedPulse();
            }

            // 触发光照选中
            if (lightingSystem != null)
            {
                lightingSystem.SetMenuItemSelected(index);
            }

            // 播放点击音效
            PlaySound(clickSound);

            // 触发强力流体交互
            TriggerFluidInteraction(lastMouseWorldPos, clickFluidForce);

            // 延迟执行动作（让动画播放）
            StartCoroutine(ExecuteMenuActionDelayed(index, 0.5f));
        }

        /// <summary>
        /// 触发流体交互
        /// </summary>
        private void TriggerFluidInteraction(Vector3 worldPos, float force)
        {
            if (fluidSimulator == null) return;

            // 在点击位置添加向下的力（造成波纹）
            Vector3 forceDir = Vector3.down + Vector3.forward * 0.3f;
            fluidSimulator.AddForceAtPosition(worldPos, forceDir.normalized * force);
            fluidSimulator.AddDensityAtPosition(worldPos, 0.5f);
        }

        /// <summary>
        /// 延迟执行菜单动作
        /// </summary>
        private IEnumerator ExecuteMenuActionDelayed(int index, float delay)
        {
            yield return new WaitForSeconds(delay);
            ExecuteMenuAction(index);
        }

        /// <summary>
        /// 执行菜单动作
        /// </summary>
        private void ExecuteMenuAction(int index)
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
                case 3: // 设置
                    OpenSettings();
                    break;
                case 4: // 支持我
                    SupportMe();
                    break;
                case 5: // 退出游戏
                    QuitGame();
                    break;
            }
        }

        /// <summary>
        /// 新游戏
        /// </summary>
        public void NewGame()
        {
            // 清空存档
            SaveSystem.Instance.ResetAll();
            
            // 设置新游戏起点
            SaveSystem.Instance.SaveLastScene(newGameScene);
            SaveSystem.Instance.SaveCheckpoint(firstCheckpointId);
            
            // 加载场景
            SceneLoader.LoadScene(newGameScene, firstCheckpointId);
        }

        /// <summary>
        /// 继续游戏
        /// </summary>
        public void ContinueGame()
        {
            var scene = SaveSystem.Instance.LoadLastScene();
            if (string.IsNullOrEmpty(scene)) scene = newGameScene;
            var checkpoint = SaveSystem.Instance.LoadCheckpoint();
            SceneLoader.LoadScene(scene, checkpoint);
        }

        /// <summary>
        /// 双人模式
        /// </summary>
        public void CoopMode()
        {
            Debug.Log("双人模式功能尚未开发 (´∀｀)♡");
            isTransitioning = false;
        }

        /// <summary>
        /// 打开设置
        /// </summary>
        public void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
                Debug.Log("[Fluid3DMenuController] 设置面板已打开");
            }
            else
            {
                Debug.LogWarning("[Fluid3DMenuController] 未找到设置面板");
            }
            
            isTransitioning = false;
        }

        /// <summary>
        /// 支持我
        /// </summary>
        public void SupportMe()
        {
            Debug.Log("感谢素素的支持！爱娘会继续努力的～(｡◕‿◕｡)");
            isTransitioning = false;
        }

        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

            // 设置"继续游戏"的可用性
            if (menuItems.Length > 1 && menuItems[1] != null)
            {
                menuItems[1].SetInteractable(hasSave);
            }

            Debug.Log($"[Fluid3DMenuController] 存档检测: {(hasSave ? "有存档" : "无存档")}");
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

#if UNITY_EDITOR
        // 编辑器调试
        [ContextMenu("测试：触发新游戏")]
        private void TestNewGame()
        {
            OnMenuItemClicked(0);
        }

        [ContextMenu("测试：打开设置")]
        private void TestSettings()
        {
            OnMenuItemClicked(3);
        }
#endif
    }
}

