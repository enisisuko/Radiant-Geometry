using UnityEngine;
using UnityEngine.UI;

namespace FadedDreams.UI
{
    /// <summary>
    /// 简单的菜单构建器
    /// 在运行时或编辑器中按下按钮时创建菜单系统
    /// </summary>
    public class SimpleMenuBuilder : MonoBehaviour
    {
        [ContextMenu("创建彩色主菜单")]
        public void BuildMenu()
        {
            Debug.Log("开始创建彩色主菜单系统...");
            
            // 查找Canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("未找到Canvas，请先创建Canvas");
                return;
            }
            
            // 创建MenuManager
            GameObject manager = new GameObject("MenuManager");
            var menuManager = manager.AddComponent<ColorfulMenuManager>();
            menuManager.menuCanvas = canvas;
            
            // 创建五个分区
            string[] names = { "新游戏", "继续游戏", "双人模式", "退出游戏", "支持我" };
            Color[] colors = {
                new Color(1f, 0.2f, 0.2f, 0.8f), // 红色
                new Color(0.2f, 0.8f, 1f, 0.8f), // 蓝色
                new Color(0.8f, 0.2f, 1f, 0.8f), // 紫色
                new Color(1f, 0.8f, 0.2f, 0.8f), // 黄色
                new Color(0.2f, 1f, 0.2f, 0.8f)  // 绿色
            };
            
            menuManager.menuSections = new Transform[5];
            menuManager.sectionImages = new Image[5];
            menuManager.sectionTexts = new Text[5];
            menuManager.sectionButtons = new Button[5];
            
            for (int i = 0; i < 5; i++)
            {
                // 创建分区
                GameObject section = new GameObject($"Section_{i}");
                section.transform.SetParent(canvas.transform);
                
                RectTransform rectTransform = section.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(i / 5f, 0);
                rectTransform.anchorMax = new Vector2((i + 1) / 5f, 1);
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                rectTransform.localPosition = Vector3.zero;
                
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
                textRect.localPosition = Vector3.zero;
                
                Text text = textObj.AddComponent<Text>();
                text.text = names[i];
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 48;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.fontStyle = FontStyle.Bold;
                
                // 保存引用
                menuManager.menuSections[i] = section.transform;
                menuManager.sectionImages[i] = image;
                menuManager.sectionTexts[i] = text;
                menuManager.sectionButtons[i] = button;
            }
            
            // 创建中心小球
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "CenterBall";
            ball.transform.position = new Vector3(0, 0, 5);
            ball.transform.localScale = Vector3.one * 0.5f;
            
            var centerBall = ball.AddComponent<CenterRotatingBall>();
            menuManager.centerBall = centerBall;
            
            // 创建发光材质
            Material mat = new Material(Shader.Find("Standard"));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.white);
            mat.SetColor("_Color", Color.white);
            ball.GetComponent<Renderer>().material = mat;
            
            // 添加光照系统
            var lightingSystem = manager.AddComponent<MenuLightingSystem>();
            menuManager.lightingSystem = lightingSystem;
            
            // 创建分区光源
            lightingSystem.sectionLights = new Light[5];
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
                
                float xPos = -8f + (i * 4f); // 分布在-8到8的范围
                lightObj.transform.position = new Vector3(xPos, 0, 3);
                
                lightingSystem.sectionLights[i] = light;
            }
            
            // 创建环境光
            GameObject ambientLight = new GameObject("AmbientLight");
            Light ambient = ambientLight.AddComponent<Light>();
            ambient.type = LightType.Directional;
            ambient.color = new Color(0.1f, 0.1f, 0.2f);
            ambient.intensity = 0.3f;
            lightingSystem.globalAmbientLight = ambient;
            
            // 创建中心聚光灯
            GameObject spotlight = new GameObject("CenterSpotlight");
            Light spot = spotlight.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = Color.white;
            spot.intensity = 2f;
            spot.range = 10f;
            spot.spotAngle = 45f;
            spotlight.transform.position = new Vector3(0, 5, 0);
            spotlight.transform.rotation = Quaternion.Euler(90, 0, 0);
            lightingSystem.centerSpotlight = spot;
            
            // 添加色彩蔓延效果
            var colorSpreadEffect = manager.AddComponent<ColorSpreadEffect>();
            menuManager.colorSpreadEffect = colorSpreadEffect;
            colorSpreadEffect.sectionImages = menuManager.sectionImages;
            colorSpreadEffect.sectionTexts = menuManager.sectionTexts;
            colorSpreadEffect.originalColors = colors;
            colorSpreadEffect.spreadColors = colors;
            
            // 设置场景配置
            menuManager.newGameScene = "STORY0";
            menuManager.firstCheckpointId = "101";
            
            Debug.Log("✓ 彩色主菜单创建完成！");
        }
    }
}