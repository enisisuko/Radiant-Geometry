// DynamicMenuBuilder.cs
// 2D动态菜单一键生成工具
// 功能：创建完整的2D动态菜单场景（6个按键 + 聚光灯系统）

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using FadedDreams.UI;
using FadedDreams.Audio;

namespace FadedDreams.Editor
{
    /// <summary>
    /// 2D动态菜单一键生成工具
    /// </summary>
    public static class DynamicMenuBuilder
    {
        // 路径配置
        const string ShadersFolder = "Assets/Scripts/UI/DynamicMenu/Shaders";

        [MenuItem("Tools/Radiant Geometry/Build Dynamic 2D Menu")]
        public static void BuildDynamicMenu()
        {
            if (!EditorSceneManager.GetActiveScene().isLoaded)
            {
                EditorUtility.DisplayDialog("提示", "请先打开一个场景", "确定");
                return;
            }

            // 开始生成
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Debug.Log("[DynamicMenuBuilder] 开始创建2D动态菜单...");

            // 1. 设置2D相机
            Setup2DCamera();

            // 2. 删除旧的3D系统
            RemoveOld3DSystem();

            // 3. 创建Canvas
            GameObject canvas = CreateCanvas();

            // 4. 创建背景
            CreateBackground(canvas.transform);

            // 5. 创建6个漂浮按键
            FloatingMenuButton[] buttons = CreateFloatingButtons(canvas.transform);

            // 6. 创建6个聚光灯
            SpotlightController[] spotlights = CreateSpotlights(canvas.transform);

            // 7. 创建聚光灯管理器
            SpotlightManager spotlightManager = CreateSpotlightManager(spotlights);

            // 8. 创建菜单管理器
            CreateMenuManager(buttons, spotlightManager);

            // 9. 创建全局音频管理器（如果不存在）
            EnsureGlobalAudioManager();

            // 10. 创建EventSystem
            CreateEventSystem();

            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.DisplayDialog("完成",
                "2D动态菜单已创建成功！\n\n" +
                "✨ 2D相机系统\n" +
                "✨ 6个自由漂浮按键\n" +
                "✨ 6个聚光灯\n" +
                "✨ 聚光灯转向系统\n\n" +
                "按播放键查看效果吧~ (◕‿◕✿)",
                "太棒了！");

            Debug.Log("[DynamicMenuBuilder] 创建完成！");
        }

        /// <summary>
        /// 设置2D相机
        /// </summary>
        static void Setup2DCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
            }

            // 设置为正交投影
            mainCam.orthographic = true;
            mainCam.orthographicSize = 5f;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.05f, 0.05f, 0.15f); // 深蓝黑色

            // 重置位置和旋转
            mainCam.transform.position = new Vector3(0f, 0f, -10f);
            mainCam.transform.rotation = Quaternion.identity;

            Debug.Log("[DynamicMenuBuilder] 相机已设置为2D模式");
        }

        /// <summary>
        /// 删除旧的3D系统
        /// </summary>
        static void RemoveOld3DSystem()
        {
            // 删除FluidContainer
            var fluidContainer = GameObject.Find("FluidContainer");
            if (fluidContainer != null)
            {
                Undo.DestroyObjectImmediate(fluidContainer);
                Debug.Log("[DynamicMenuBuilder] 已删除FluidContainer");
            }

            // 删除MainPanel（旧的3D菜单）
            var mainPanel = GameObject.Find("MainPanel");
            if (mainPanel != null)
            {
                Undo.DestroyObjectImmediate(mainPanel);
                Debug.Log("[DynamicMenuBuilder] 已删除MainPanel");
            }

            // 删除LightingSystem（旧的3D光照）
            var lightingSystem = GameObject.Find("LightingSystem");
            if (lightingSystem != null)
            {
                Undo.DestroyObjectImmediate(lightingSystem);
                Debug.Log("[DynamicMenuBuilder] 已删除LightingSystem");
            }

            // 删除CameraRig
            var cameraRig = GameObject.Find("MainMenu_CameraRig");
            if (cameraRig != null)
            {
                Undo.DestroyObjectImmediate(cameraRig);
                Debug.Log("[DynamicMenuBuilder] 已删除CameraRig");
            }
        }

        /// <summary>
        /// 创建Canvas
        /// </summary>
        static GameObject CreateCanvas()
        {
            GameObject canvasGo = new GameObject("MenuCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            Debug.Log("[DynamicMenuBuilder] Canvas已创建");
            return canvasGo;
        }

        /// <summary>
        /// 创建背景
        /// </summary>
        static void CreateBackground(Transform parent)
        {
            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(bgGo, "Create Background");

            RectTransform bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;

            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.15f); // 深蓝黑色

            Debug.Log("[DynamicMenuBuilder] 背景已创建");
        }

        /// <summary>
        /// 创建6个漂浮按键
        /// </summary>
        static FloatingMenuButton[] CreateFloatingButtons(Transform parent)
        {
            FloatingMenuButton[] buttons = new FloatingMenuButton[6];

            string[] buttonNames = new string[]
            {
                "Button_NewGame",
                "Button_Continue",
                "Button_Coop",
                "Button_Settings",
                "Button_Support",
                "Button_Quit"
            };

            FloatingMenuButton.MenuButtonType[] buttonTypes = new FloatingMenuButton.MenuButtonType[]
            {
                FloatingMenuButton.MenuButtonType.NewGame,
                FloatingMenuButton.MenuButtonType.Continue,
                FloatingMenuButton.MenuButtonType.Coop,
                FloatingMenuButton.MenuButtonType.Settings,
                FloatingMenuButton.MenuButtonType.Support,
                FloatingMenuButton.MenuButtonType.Quit
            };

            Color[] buttonColors = new Color[]
            {
                new Color(1f, 0.2f, 0.2f),    // 红色（新游戏）
                new Color(0.2f, 0.8f, 1f),    // 蓝色（继续）
                new Color(0.8f, 0.2f, 1f),    // 紫色（双人）
                new Color(1f, 0.5f, 0f),      // 橙色（设置）
                new Color(0.2f, 1f, 0.2f),    // 绿色（支持）
                new Color(1f, 0.8f, 0.2f)     // 黄色（退出）
            };

            for (int i = 0; i < 6; i++)
            {
                // 创建按键GameObject
                GameObject btnGo = new GameObject(buttonNames[i]);
                btnGo.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(btnGo, $"Create {buttonNames[i]}");

                // RectTransform
                RectTransform btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(120f, 120f);

                // Image
                Image btnImage = btnGo.AddComponent<Image>();
                btnImage.color = buttonColors[i];
                btnImage.raycastTarget = true; // 确保可以接收鼠标事件

                // FloatingMenuButton组件
                FloatingMenuButton button = btnGo.AddComponent<FloatingMenuButton>();
                button.buttonType = buttonTypes[i];
                button.buttonColor = buttonColors[i];
                button.moveSpeed = Random.Range(15f, 25f);
                button.minDistance = 150f;
                button.baseScale = 1f;
                button.hoverScale = 1.2f;
                button.boundaryMargin = 100f; // 增大边界边距

                // 注意：不在这里调用SetRandomPosition，让它在Start中自动调用
                
                buttons[i] = button;
            }

            Debug.Log("[DynamicMenuBuilder] 6个按键已创建");
            return buttons;
        }

        /// <summary>
        /// 创建6个聚光灯
        /// </summary>
        static SpotlightController[] CreateSpotlights(Transform parent)
        {
            SpotlightController[] spotlights = new SpotlightController[6];

            // 加载Spotlight Shader
            Shader spotlightShader = Shader.Find("FadedDreams/Spotlight2D");
            if (spotlightShader == null)
            {
                Debug.LogWarning("[DynamicMenuBuilder] 未找到Spotlight2D Shader，使用默认Shader");
            }

            SpotlightController.SpotlightPosition[] positions = new SpotlightController.SpotlightPosition[]
            {
                SpotlightController.SpotlightPosition.TopLeft,
                SpotlightController.SpotlightPosition.Top,
                SpotlightController.SpotlightPosition.TopRight,
                SpotlightController.SpotlightPosition.BottomLeft,
                SpotlightController.SpotlightPosition.Bottom,
                SpotlightController.SpotlightPosition.BottomRight
            };

            Color[] spotlightColors = new Color[]
            {
                new Color(1f, 0.2f, 0.2f),    // 红色
                new Color(0.2f, 0.8f, 1f),    // 蓝色
                new Color(0.8f, 0.2f, 1f),    // 紫色
                new Color(1f, 0.5f, 0f),      // 橙色
                new Color(0.2f, 1f, 0.2f),    // 绿色
                new Color(1f, 0.8f, 0.2f)     // 黄色
            };

            for (int i = 0; i < 6; i++)
            {
                // 创建聚光灯GameObject
                GameObject spotGo = new GameObject($"Spotlight_{i}");
                spotGo.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(spotGo, $"Create Spotlight_{i}");

                // RectTransform（全屏）
                RectTransform spotRect = spotGo.AddComponent<RectTransform>();
                spotRect.anchorMin = Vector2.zero;
                spotRect.anchorMax = Vector2.one;
                spotRect.sizeDelta = Vector2.zero;
                spotRect.anchoredPosition = Vector2.zero;

                // Image
                Image spotImage = spotGo.AddComponent<Image>();
                spotImage.raycastTarget = false; // 聚光灯不需要接收鼠标事件
                
                // 创建材质
                if (spotlightShader != null)
                {
                    Material mat = new Material(spotlightShader);
                    mat.name = $"SpotlightMaterial_{i}";
                    spotImage.material = mat;
                    // 设置默认参数
                    mat.SetColor("_SpotlightColor", spotlightColors[i]);
                    mat.SetFloat("_Intensity", 4f); // 增加强度
                    mat.SetFloat("_ConeAngle", 15f); // 减小光锥角度让光束更集中
                    mat.SetFloat("_MaxDistance", 1000f);
                    mat.SetFloat("_FalloffPower", 1.5f); // 设置衰减
                    mat.SetFloat("_BeamWidth", 0.3f); // 光束宽度
                    mat.SetFloat("_BeamIntensity", 2.0f); // 光束中心强度
                }

                // SpotlightController组件
                SpotlightController spotlight = spotGo.AddComponent<SpotlightController>();
                spotlight.spotlightColor = spotlightColors[i];
                spotlight.intensity = 4f; // 增加强度让光束更明显
                spotlight.coneAngle = 15f; // 减小角度让光束更集中（从30改为15）
                spotlight.maxDistance = 1000f; // 初始值，会被动态调整
                spotlight.rotationSpeed = 120f;
                spotlight.useEasing = true;
                spotlight.easingSpeed = 6f;
                spotlight.startPosition = positions[i];

                spotlights[i] = spotlight;
            }

            Debug.Log("[DynamicMenuBuilder] 6个聚光灯已创建");
            return spotlights;
        }

        /// <summary>
        /// 创建聚光灯管理器
        /// </summary>
        static SpotlightManager CreateSpotlightManager(SpotlightController[] spotlights)
        {
            GameObject managerGo = new GameObject("SpotlightManager");
            Undo.RegisterCreatedObjectUndo(managerGo, "Create SpotlightManager");

            SpotlightManager manager = managerGo.AddComponent<SpotlightManager>();
            manager.spotlights.AddRange(spotlights);
            manager.allSpotlightsFollowTarget = true;

            Debug.Log("[DynamicMenuBuilder] 聚光灯管理器已创建");
            return manager;
        }

        /// <summary>
        /// 创建菜单管理器
        /// </summary>
        static void CreateMenuManager(FloatingMenuButton[] buttons, SpotlightManager spotlightManager)
        {
            GameObject managerGo = new GameObject("MenuManager");
            Undo.RegisterCreatedObjectUndo(managerGo, "Create MenuManager");

            DynamicMenuManager manager = managerGo.AddComponent<DynamicMenuManager>();
            manager.menuButtons.AddRange(buttons);
            manager.spotlightManager = spotlightManager;
            manager.newGameScene = "STORY0";
            manager.firstCheckpointId = "101";
            manager.assignSpotlightsToButtons = true; // 开局时让聚光灯锁定对应按钮

            // 添加AudioSource
            AudioSource audioSource = managerGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            manager.audioSource = audioSource;

            Debug.Log("[DynamicMenuBuilder] 菜单管理器已创建");
        }

        /// <summary>
        /// 确保全局音频管理器存在
        /// </summary>
        static void EnsureGlobalAudioManager()
        {
            var existing = Object.FindFirstObjectByType<GlobalAudioManager>();
            if (existing == null)
            {
                GameObject audioMgr = new GameObject("GlobalAudioManager");
                Undo.RegisterCreatedObjectUndo(audioMgr, "Create Global Audio Manager");
                audioMgr.AddComponent<GlobalAudioManager>();
                Debug.Log("[DynamicMenuBuilder] 全局音频管理器已创建");
            }
        }

        /// <summary>
        /// 创建EventSystem
        /// </summary>
        static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
                Debug.Log("[DynamicMenuBuilder] EventSystem已创建");
            }
        }
    }
}
#endif

