// FluidMainMenuBuilder.cs
// 华丽3D流体主菜单一键生成工具
// 功能：创建带有真实流体模拟、华丽光照、美丽渐变的3D主菜单场景
// 使用方法：Tools → Radiant Geometry → Build Fluid 3D Main Menu

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using FadedDreams.UI;
using FadedDreams.Audio;

namespace FadedDreams.Editor
{
    /// <summary>
    /// 华丽3D流体主菜单一键生成工具
    /// 自动创建完整的3D流体菜单场景
    /// </summary>
    public static class FluidMainMenuBuilder
    {
        // 路径配置
        const string MaterialsFolder = "Assets/Scripts/UI/FluidMenu/Materials";
        const string ShadersFolder = "Assets/Scripts/UI/FluidMenu/Shaders";
        
        [MenuItem("Tools/Radiant Geometry/Build Fluid 3D Main Menu")]
        public static void BuildFluidMenu()
        {
            if (!EditorSceneManager.GetActiveScene().isLoaded)
            {
                EditorUtility.DisplayDialog("提示", "请先打开一个场景", "确定");
                return;
            }

            // 开始生成
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            Debug.Log("[FluidMenuBuilder] 开始创建华丽3D流体主菜单...");

            // 1. 创建文件夹结构
            CreateFolderStructure();

            // 2. 创建3D相机系统
            Camera mainCam = CreateCameraSystem();

            // 3. 创建流体容器和平面
            GameObject fluidContainer = CreateFluidContainer();

            // 4. 创建平铺主面板（6分区）
            GameObject mainPanel = CreateMainPanel();

            // 5. 创建华丽光照系统
            CreateLightingSystem();

            // 6. 创建流体模拟组件
            CreateFluidSimulation(fluidContainer);

            // 7. 创建菜单管理器
            GameObject menuManager = CreateMenuManager(mainCam, mainPanel);

            // 8. 创建全局音量管理器
            CreateGlobalAudioManager();

            // 9. 创建后期处理
            CreatePostProcessing(mainCam);

            // 10. 创建EventSystem
            CreateEventSystem();

            Undo.CollapseUndoOperations(undoGroup);
            
            EditorUtility.DisplayDialog("完成", 
                "华丽3D流体主菜单已创建成功！\n\n" +
                "✨ 3D相机系统\n" +
                "✨ 真实流体模拟\n" +
                "✨ 平铺6分区主面板\n" +
                "✨ 华丽光照系统\n" +
                "✨ 美丽渐变效果\n\n" +
                "按播放键查看效果吧~ (◕‿◕✿)", 
                "太棒了！");
                
            Debug.Log("[FluidMenuBuilder] 创建完成！");
        }

        /// <summary>
        /// 创建文件夹结构
        /// </summary>
        static void CreateFolderStructure()
        {
            if (!Directory.Exists(MaterialsFolder))
                Directory.CreateDirectory(MaterialsFolder);
            if (!Directory.Exists(ShadersFolder))
                Directory.CreateDirectory(ShadersFolder);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 创建3D相机系统（俯视角度）
        /// </summary>
        static Camera CreateCameraSystem()
        {
            // 创建相机Rig
            GameObject camRig = new GameObject("MainMenu_CameraRig");
            Undo.RegisterCreatedObjectUndo(camRig, "Create Camera Rig");

            // 查找或创建主相机
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.transform.SetParent(camRig.transform);
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
            }
            else
            {
                cam.transform.SetParent(camRig.transform);
            }

            // 设置相机参数（俯视角度）
            cam.clearFlags = CameraClearFlags.Skybox; // 使用天空盒
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.15f); // 深蓝黑色
            
            // 俯视角度：从上方45-60度角看向平面
            cam.transform.position = new Vector3(0f, 12f, -8f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            
            // 透视投影，增强3D感
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            Debug.Log("[FluidMenuBuilder] 相机系统创建完成");
            return cam;
        }

        /// <summary>
        /// 创建流体容器（大型平面，作为液体载体）
        /// </summary>
        static GameObject CreateFluidContainer()
        {
            GameObject container = new GameObject("FluidContainer");
            Undo.RegisterCreatedObjectUndo(container, "Create Fluid Container");

            // 创建大型平面作为流体底部
            GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterPlane.name = "WaterSurface";
            waterPlane.transform.SetParent(container.transform);
            waterPlane.transform.localPosition = new Vector3(0f, 0f, 0f);
            waterPlane.transform.localScale = new Vector3(3f, 1f, 2f); // 扩大平面覆盖视野
            
            // 移除碰撞体（不需要物理碰撞）
            var collider = waterPlane.GetComponent<Collider>();
            if (collider) Object.DestroyImmediate(collider);

            // 创建水面材质
            Material waterMaterial = CreateWaterMaterial();
            waterPlane.GetComponent<Renderer>().sharedMaterial = waterMaterial;

            Debug.Log("[FluidMenuBuilder] 流体容器创建完成");
            return container;
        }

        /// <summary>
        /// 创建平铺主面板（6个分区，悬浮在流体上方）
        /// </summary>
        static GameObject CreateMainPanel()
        {
            GameObject panel = new GameObject("MainPanel");
            Undo.RegisterCreatedObjectUndo(panel, "Create Main Panel");
            panel.transform.position = new Vector3(0f, 0.5f, 0f); // 略微悬浮在水面上

            // 6个菜单选项的位置（网格布局）
            Vector3[] positions = new Vector3[]
            {
                new Vector3(-4f, 0f, 2f),   // 左上：新游戏
                new Vector3(0f, 0f, 2f),    // 中上：继续游戏
                new Vector3(4f, 0f, 2f),    // 右上：双人模式
                new Vector3(-2f, 0f, -2f),  // 左下：设置
                new Vector3(2f, 0f, -2f),   // 右下：支持我
                new Vector3(0f, 0f, -4f)    // 底部：退出游戏
            };

            // 6个选项的名称
            string[] optionNames = new string[]
            {
                "NewGame", "Continue", "Coop", "Settings", "Support", "Quit"
            };

            // 6个选项的颜色（HDR发光颜色）
            Color[] colors = new Color[]
            {
                new Color(1f, 0.2f, 0.2f) * 2f,     // 红色发光
                new Color(0.2f, 0.8f, 1f) * 2.5f,   // 蓝色发光（最亮）
                new Color(0.8f, 0.2f, 1f) * 2f,     // 紫色发光
                new Color(1f, 0.5f, 0f) * 2f,       // 橙色发光（设置）
                new Color(0.2f, 1f, 0.2f) * 2f,     // 绿色发光
                new Color(1f, 0.8f, 0.2f) * 1.8f    // 黄色发光
            };

            // 创建6个菜单选项
            for (int i = 0; i < 6; i++)
            {
                GameObject option = CreateMenuOption(
                    panel.transform,
                    $"Option_{optionNames[i]}",
                    positions[i],
                    colors[i]
                );
            }

            Debug.Log("[FluidMenuBuilder] 主面板创建完成（6分区）");
            return panel;
        }

        /// <summary>
        /// 创建单个菜单选项（发光几何体）
        /// </summary>
        static GameObject CreateMenuOption(Transform parent, string name, Vector3 position, Color emissionColor)
        {
            // 创建几何体（使用立方体或球体）
            GameObject option = GameObject.CreatePrimitive(PrimitiveType.Cube);
            option.name = name;
            option.transform.SetParent(parent);
            option.transform.localPosition = position;
            option.transform.localScale = Vector3.one * 1.2f;

            // 创建发光材质
            Material mat = CreateEmissionMaterial(name, emissionColor);
            option.GetComponent<Renderer>().sharedMaterial = mat;

            // 添加碰撞体用于射线检测
            var collider = option.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = false; // 用于射线检测
            }

            // 添加菜单项组件
            var menuItem = option.AddComponent<Fluid3DMenuItem>();
            menuItem.baseEmission = emissionColor;
            menuItem.targetRenderer = option.GetComponent<Renderer>();

            return option;
        }

        /// <summary>
        /// 创建华丽光照系统
        /// </summary>
        static void CreateLightingSystem()
        {
            GameObject lightingRoot = new GameObject("LightingSystem");
            Undo.RegisterCreatedObjectUndo(lightingRoot, "Create Lighting System");

            // 1. 主定向光（模拟天光）
            GameObject mainLight = new GameObject("MainLight");
            mainLight.transform.SetParent(lightingRoot.transform);
            mainLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            
            Light dirLight = mainLight.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.9f, 0.95f, 1f); // 冷白色
            dirLight.intensity = 0.8f;
            dirLight.shadows = LightShadows.Soft;

            // 2. 环境光（柔和背景光）
            GameObject ambientLight = new GameObject("AmbientLight");
            ambientLight.transform.SetParent(lightingRoot.transform);
            ambientLight.transform.position = new Vector3(0f, 5f, 0f);
            
            Light ambient = ambientLight.AddComponent<Light>();
            ambient.type = LightType.Point;
            ambient.color = new Color(0.3f, 0.4f, 0.6f); // 冷色调
            ambient.intensity = 1.5f;
            ambient.range = 20f;

            // 3. 6个分区光源（对应6个菜单项）
            Vector3[] lightPositions = new Vector3[]
            {
                new Vector3(-4f, 2f, 2f),   // 新游戏光源
                new Vector3(0f, 3f, 2f),    // 继续游戏光源（中心最亮）
                new Vector3(4f, 2f, 2f),    // 双人模式光源
                new Vector3(-2f, 2f, -2f),  // 设置光源
                new Vector3(2f, 2f, -2f),   // 支持我光源
                new Vector3(0f, 2f, -4f)    // 退出光源
            };

            Color[] lightColors = new Color[]
            {
                new Color(1f, 0.3f, 0.3f),   // 红光
                new Color(0.3f, 0.7f, 1f),   // 蓝光
                new Color(0.8f, 0.3f, 1f),   // 紫光
                new Color(1f, 0.6f, 0.2f),   // 橙光
                new Color(0.3f, 1f, 0.3f),   // 绿光
                new Color(1f, 0.9f, 0.3f)    // 黄光
            };

            for (int i = 0; i < 6; i++)
            {
                GameObject pointLight = new GameObject($"MenuLight_{i}");
                pointLight.transform.SetParent(lightingRoot.transform);
                pointLight.transform.position = lightPositions[i];

                Light light = pointLight.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lightColors[i];
                light.intensity = 2.5f; // 强烈发光
                light.range = 8f;
                light.shadows = LightShadows.None; // 性能优化
            }

            // 4. 添加光照增强组件
            var enhancedLighting = lightingRoot.AddComponent<MenuLightingEnhanced>();
            
            Debug.Log("[FluidMenuBuilder] 光照系统创建完成（6+2光源）");
        }

        /// <summary>
        /// 创建流体模拟系统
        /// </summary>
        static void CreateFluidSimulation(GameObject container)
        {
            // 添加流体模拟器组件
            var simulator = container.AddComponent<Fluid3DSimulator>();
            
            // 配置流体参数
            simulator.gridResolution = 64; // 分辨率（性能vs质量平衡）
            simulator.viscosity = 0.001f; // 粘度（类似水）
            simulator.diffusion = 0.0001f; // 扩散
            simulator.timeStep = 0.016f; // 时间步长
            
            Debug.Log("[FluidMenuBuilder] 流体模拟系统创建完成");
        }

        /// <summary>
        /// 创建菜单管理器
        /// </summary>
        static GameObject CreateMenuManager(Camera cam, GameObject panel)
        {
            GameObject manager = new GameObject("MenuManager");
            Undo.RegisterCreatedObjectUndo(manager, "Create Menu Manager");

            // 添加增强版菜单管理器
            var menuMgr = manager.AddComponent<FluidMenuManager>();
            
            // 查找并设置所有菜单选项
            var options = panel.GetComponentsInChildren<Fluid3DMenuItem>();
            if (options.Length >= 6)
            {
                // 这里可以设置引用
                Debug.Log($"[FluidMenuBuilder] 找到 {options.Length} 个菜单选项");
            }

            Debug.Log("[FluidMenuBuilder] 菜单管理器创建完成");
            return manager;
        }

        /// <summary>
        /// 创建全局音量管理器
        /// </summary>
        static void CreateGlobalAudioManager()
        {
            // 检查是否已存在
            var existing = Object.FindFirstObjectByType<GlobalAudioManager>();
            if (existing != null)
            {
                Debug.Log("[FluidMenuBuilder] GlobalAudioManager已存在，跳过创建");
                return;
            }

            GameObject audioMgr = new GameObject("GlobalAudioManager");
            Undo.RegisterCreatedObjectUndo(audioMgr, "Create Global Audio Manager");
            
            audioMgr.AddComponent<GlobalAudioManager>();
            
            Debug.Log("[FluidMenuBuilder] 全局音量管理器创建完成");
        }

        /// <summary>
        /// 创建后期处理（Bloom等）
        /// </summary>
        static void CreatePostProcessing(Camera cam)
        {
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            GameObject volGo = new GameObject("PostProcessVolume");
            Undo.RegisterCreatedObjectUndo(volGo, "Create Post Process Volume");
            
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1;

            // 创建VolumeProfile
            string profilePath = "Assets/Scripts/UI/FluidMenu/FluidMenuPostProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            vol.profile = profile;

            // 添加Bloom（超强发光效果）
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 2.5f; // 超强发光
            bloom.threshold.value = 0.6f; // 较低阈值，更多发光
            bloom.scatter.value = 0.8f; // 大范围扩散
            bloom.tint.value = new Color(0.9f, 0.95f, 1f); // 冷色调

            // 添加Color Adjustments（颜色增强）
            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.saturation.value = 15f; // 增加饱和度
            colorAdj.contrast.value = 10f; // 增加对比度

            // 添加Vignette（暗角）
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.25f;
            vignette.smoothness.value = 0.8f;
            vignette.color.value = new Color(0.05f, 0.1f, 0.2f); // 深蓝色暗角

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            
            Debug.Log("[FluidMenuBuilder] 后期处理创建完成");
#else
            Debug.LogWarning("[FluidMenuBuilder] 未检测到URP，跳过后期处理");
#endif
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
                
                Debug.Log("[FluidMenuBuilder] EventSystem创建完成");
            }
        }

        /// <summary>
        /// 创建水面材质（美丽的渐变和反射）
        /// </summary>
        static Material CreateWaterMaterial()
        {
            string path = $"{MaterialsFolder}/MAT_WaterSurface.mat";
            
            // 尝试加载现有材质
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (mat == null)
            {
                // 创建新材质
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                mat = new Material(shader);
                
                // 设置材质属性
                mat.EnableKeyword("_EMISSION");
                
                // 基础颜色：深蓝色
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.1f, 0.2f, 0.4f, 0.9f));
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", new Color(0.1f, 0.2f, 0.4f, 0.9f));
                
                // 发光颜色：青色光晕
                Color emissionColor = new Color(0.2f, 0.6f, 1f) * 1.5f;
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", emissionColor);
                
                // 金属度和光滑度（类似水面）
                if (mat.HasProperty("_Metallic"))
                    mat.SetFloat("_Metallic", 0.8f);
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", 0.95f);
                
                AssetDatabase.CreateAsset(mat, path);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            
            return mat;
        }

        /// <summary>
        /// 创建发光材质
        /// </summary>
        static Material CreateEmissionMaterial(string name, Color emissionColor)
        {
            string path = $"{MaterialsFolder}/MAT_{name}.mat";
            
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                mat = new Material(shader);
                
                // 启用发光
                mat.EnableKeyword("_EMISSION");
                
                // 基础颜色：黑色（只显示发光）
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.black);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", Color.black);
                
                // 发光颜色
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", emissionColor);
                
                AssetDatabase.CreateAsset(mat, path);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            
            return mat;
        }
    }
}
#endif

