using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

namespace FadedDreams.UI.Editor
{
    /// <summary>
    /// 彩色主菜单自动构建器
    /// 在编辑器中自动创建完整的彩色主菜单场景
    /// </summary>
    public class ColorfulMenuAutoBuilder : EditorWindow
    {
        [MenuItem("Tools/彩色主菜单/一键创建菜单系统")]
        public static void ShowWindow()
        {
            GetWindow<ColorfulMenuAutoBuilder>("彩色主菜单构建器");
        }
        
        [MenuItem("Tools/彩色主菜单/快速构建")]
        public static void QuickBuild()
        {
            if (EditorUtility.DisplayDialog("确认创建", 
                "这将在当前场景中创建完整的彩色主菜单系统。是否继续？", 
                "是的，创建吧！", "取消"))
            {
                BuildColorfulMenu();
            }
        }
        
        void OnGUI()
        {
            GUILayout.Label("彩色主菜单自动构建器", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "点击下面的按钮将在当前场景中自动创建：\n" +
                "• UI画布和五个分区\n" +
                "• 中心旋转小球\n" +
                "• 光照系统\n" +
                "• 所有必需的脚本组件", 
                MessageType.Info);
            
            GUILayout.Space(20);
            
            if (GUILayout.Button("一键创建彩色主菜单", GUILayout.Height(40)))
            {
                BuildColorfulMenu();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("清理当前场景", GUILayout.Height(30)))
            {
                CleanScene();
            }
        }
        
        public static void BuildColorfulMenu()
        {
            Debug.Log("开始构建彩色主菜单系统...");
            
            // 创建主管理器
            GameObject manager = CreateMenuManager();
            
            // 创建UI画布
            GameObject canvas = CreateCanvas();
            
            // 创建五个分区
            GameObject[] sections = CreateSections(canvas.transform);
            
            // 创建中心小球
            GameObject ball = CreateCenterBall();
            
            // 创建光照系统
            CreateLightingSystem(manager);
            
            // 连接所有引用
            ConnectReferences(manager, canvas, sections, ball);
            
            // 保存场景
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            
            Debug.Log("✓ 彩色主菜单创建完成！");
            EditorUtility.DisplayDialog("成功", "彩色主菜单系统创建完成！", "太棒了！");
        }
        
        static GameObject CreateMenuManager()
        {
            GameObject manager = new GameObject("MenuManager");
            manager.AddComponent<FadedDreams.UI.ColorfulMenuManager>();
            return manager;
        }
        
        static GameObject CreateCanvas()
        {
            // 创建Canvas
            GameObject canvasObj = new GameObject("MenuCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            return canvasObj;
        }
        
        static GameObject[] CreateSections(Transform parent)
        {
            GameObject[] sections = new GameObject[5];
            string[] names = { "新游戏", "继续游戏", "双人模式", "退出游戏", "支持我" };
            Color[] colors = {
                new Color(1f, 0.2f, 0.2f, 0.8f), // 红色
                new Color(0.2f, 0.8f, 1f, 0.8f), // 蓝色
                new Color(0.8f, 0.2f, 1f, 0.8f), // 紫色
                new Color(1f, 0.8f, 0.2f, 0.8f), // 黄色
                new Color(0.2f, 1f, 0.2f, 0.8f)  // 绿色
            };
            
            float sectionWidth = 384; // 1920 / 5
            float sectionHeight = 1080;
            
            for (int i = 0; i < 5; i++)
            {
                // 创建分区
                GameObject section = new GameObject($"Section_{i}");
                section.transform.SetParent(parent);
                
                RectTransform rectTransform = section.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(i / 5f, 0);
                rectTransform.anchorMax = new Vector2((i + 1) / 5f, 1);
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                
                Image image = section.AddComponent<Image>();
                image.color = colors[i];
                
                Button button = section.AddComponent<Button>();
                
                // 创建文字
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(section.transform);
                
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                textRect.localScale = Vector3.one;
                
                Text text = textObj.AddComponent<Text>();
                text.text = names[i];
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 48;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                
                sections[i] = section;
            }
            
            return sections;
        }
        
        static GameObject CreateCenterBall()
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "CenterBall";
            ball.transform.position = new Vector3(0, 0, 5);
            ball.transform.localScale = Vector3.one * 0.5f;
            
            // 添加旋转脚本
            ball.AddComponent<FadedDreams.UI.CenterRotatingBall>();
            
            // 创建发光材质
            Material mat = new Material(Shader.Find("Standard"));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.white);
            ball.GetComponent<Renderer>().material = mat;
            
            return ball;
        }
        
        static void CreateLightingSystem(GameObject manager)
        {
            // 添加光照系统组件
            manager.AddComponent<FadedDreams.UI.MenuLightingSystem>();
            
            // 创建环境光
            GameObject ambientLight = new GameObject("AmbientLight");
            Light ambient = ambientLight.AddComponent<Light>();
            ambient.type = LightType.Directional;
            ambient.color = new Color(0.1f, 0.1f, 0.2f);
            ambient.intensity = 0.3f;
            
            // 创建五个分区光源
            Color[] lightColors = {
                new Color(1f, 0.3f, 0.3f),
                new Color(0.3f, 0.7f, 1f),
                new Color(0.8f, 0.3f, 1f),
                new Color(1f, 0.8f, 0.3f),
                new Color(0.3f, 1f, 0.3f)
            };
            
            for (int i = 0; i < 5; i++)
            {
                GameObject lightObj = new GameObject($"SectionLight_{i}");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lightColors[i];
                light.intensity = 0.5f;
                light.range = 10f;
                
                float xPos = -4f + (i * 2f); // 分布在-4到4的范围
                lightObj.transform.position = new Vector3(xPos, 0, 3);
            }
            
            // 创建中心聚光灯
            GameObject spotlight = new GameObject("CenterSpotlight");
            Light spot = spotlight.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = Color.white;
            spot.intensity = 2f;
            spot.range = 10f;
            spot.spotAngle = 45f;
            spotlight.transform.position = new Vector3(0, 3, 0);
            spotlight.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
        
        static void ConnectReferences(GameObject manager, GameObject canvas, GameObject[] sections, GameObject ball)
        {
            var menuManager = manager.GetComponent<FadedDreams.UI.ColorfulMenuManager>();
            if (menuManager != null)
            {
                // 连接UI引用
                menuManager.menuCanvas = canvas.GetComponent<Canvas>();
                menuManager.menuSections = new Transform[sections.Length];
                menuManager.sectionImages = new Image[sections.Length];
                menuManager.sectionTexts = new Text[sections.Length];
                menuManager.sectionButtons = new Button[sections.Length];
                
                for (int i = 0; i < sections.Length; i++)
                {
                    menuManager.menuSections[i] = sections[i].transform;
                    menuManager.sectionImages[i] = sections[i].GetComponent<Image>();
                    menuManager.sectionTexts[i] = sections[i].GetComponentInChildren<Text>();
                    menuManager.sectionButtons[i] = sections[i].GetComponent<Button>();
                }
                
                // 连接中心小球
                menuManager.centerBall = ball.GetComponent<FadedDreams.UI.CenterRotatingBall>();
                
                // 设置场景配置
                menuManager.newGameScene = "STORY0";
                menuManager.firstCheckpointId = "101";
            }
            
            // 连接光照系统
            var lightingSystem = manager.GetComponent<FadedDreams.UI.MenuLightingSystem>();
            if (lightingSystem != null)
            {
                var lights = GameObject.FindObjectsOfType<Light>();
                int sectionLightIndex = 0;
                
                foreach (var light in lights)
                {
                    if (light.name.StartsWith("SectionLight_") && sectionLightIndex < 5)
                    {
                        if (lightingSystem.sectionLights == null)
                            lightingSystem.sectionLights = new Light[5];
                        lightingSystem.sectionLights[sectionLightIndex++] = light;
                    }
                    else if (light.name == "AmbientLight")
                    {
                        lightingSystem.globalAmbientLight = light;
                    }
                    else if (light.name == "CenterSpotlight")
                    {
                        lightingSystem.centerSpotlight = light;
                    }
                }
            }
            
            Debug.Log("✓ 所有引用已连接");
        }
        
        static void CleanScene()
        {
            if (EditorUtility.DisplayDialog("确认清理", 
                "这将删除场景中所有菜单相关的物体。是否继续？", 
                "确认", "取消"))
            {
                GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name.Contains("Menu") || 
                        obj.name.Contains("Section") || 
                        obj.name.Contains("Ball") ||
                        obj.name.Contains("Light"))
                    {
                        DestroyImmediate(obj);
                    }
                }
                
                Debug.Log("✓ 场景已清理");
            }
        }
    }
}