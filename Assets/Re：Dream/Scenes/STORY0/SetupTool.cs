using UnityEngine;
using TMPro;
using FadedDreams.Story;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
#endif

/// <summary>
/// STORY0一键配置工具
/// </summary>
public class SetupTool : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/STORY0/一键配置")]
    public static void AutoSetup()
    {
        Debug.Log("=== 开始配置STORY0场景 ===");
        
        // 打开场景
        var scene = EditorSceneManager.OpenScene("Assets/Re：Dream/Scenes/STORY0.unity");
        
        // 创建/查找对象
        var director = FindOrCreate("Director");
        var square = FindOrCreate("FallingSquare");
        var canvas = FindOrCreate("Canvas");
        
        // 设置Canvas
        SetupCanvas(canvas);
        
        // 创建UI
        var titleGroup = FindOrCreate("TitleGroup", canvas.transform);
        var authorGroup = FindOrCreate("AuthorGroup", canvas.transform);
        var fadeScreen = FindOrCreate("FadeScreen", canvas.transform);
        
        SetupUIGroup(titleGroup, "TitleText", "Radiant Geometry", 0, 100);
        SetupUIGroup(authorGroup, "AuthorText", "EnishiEuko", 0, 50);
        SetupFadeScreen(fadeScreen);
        
        // 设置正方形
        SetupSprite(square, 0, 100);  // 起始高度100，确保11秒才撞地
        var squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Re：Dream/Scenes/STORY0/WhiteSquare.png");
        if (squareSprite)
        {
            square.GetComponent<SpriteRenderer>().sprite = squareSprite;
            Debug.Log("✓ 设置FallingSquare的Sprite");
        }
        
        // 设置Director脚本
        var script = director.GetComponent<Story0Director>();
        if (!script) script = director.AddComponent<Story0Director>();
        
        SetProp(script, "fallingSquare", square.transform);
        SetProp(script, "mainCamera", Camera.main);
        SetProp(script, "titleGroup", titleGroup.GetComponent<CanvasGroup>());
        SetProp(script, "titleText", titleGroup.transform.Find("TitleText").GetComponent<TextMeshProUGUI>());
        SetProp(script, "authorGroup", authorGroup.GetComponent<CanvasGroup>());
        SetProp(script, "authorText", authorGroup.transform.Find("AuthorText").GetComponent<TextMeshProUGUI>());
        SetProp(script, "fadeScreen", fadeScreen.GetComponent<CanvasGroup>());
        
        // 设置地面Sprite（使用白色方块）
        SetProp(script, "groundSprite", squareSprite);
        
        // 尝试自动设置特效预制体
        var effect1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/大气摩擦.prefab");
        if (effect1)
        {
            SetProp(script, "firstEffectPrefab", effect1);
            Debug.Log("✓ 自动设置第一特效（0秒）: 大气摩擦");
        }
        
        // 素素需要手动设置的特效
        Debug.Log("⚠️ 请手动设置：");
        Debug.Log("   - secondEffectPrefab（7秒，第二特效）");
        Debug.Log("   - explosionEffectPrefab（11秒，坠地爆炸）");
        
        // 保存
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("✅ 自动配置完成！");
        Debug.Log("📝 素素还需要手动设置特效：");
        Debug.Log("   1. secondEffectPrefab - 第7秒播放的特效");
        Debug.Log("   2. explosionEffectPrefab - 第11秒坠地爆炸");
    }
    
    [MenuItem("Tools/STORY0/创建白色方块")]
    public static void CreateSprite()
    {
        var tex = new Texture2D(64, 64);
        for (int i = 0; i < 64 * 64; i++)
            tex.SetPixel(i % 64, i / 64, Color.white);
        tex.Apply();
        
        string path = "Assets/Re：Dream/Scenes/STORY0/WhiteSquare.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.Refresh();
        
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = 64;
        imp.filterMode = FilterMode.Point;
        imp.SaveAndReimport();
        
        Debug.Log($"✅ 白色方块已创建: {path}");
    }
    
    static GameObject FindOrCreate(string name, Transform parent = null)
    {
        var obj = GameObject.Find(name);
        if (!obj)
        {
            obj = new GameObject(name);
            if (parent) obj.transform.SetParent(parent);
            Debug.Log($"✓ 创建 {name}");
        }
        return obj;
    }
    
    static void SetupCanvas(GameObject canvas)
    {
        var c = canvas.GetComponent<Canvas>();
        if (!c)
        {
            c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        // 确保Canvas的RectTransform设置正确
        var rt = canvas.GetComponent<RectTransform>();
        if (rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }
    
    static void SetupUIGroup(GameObject group, string textName, string content, float x, float y)
    {
        if (!group.GetComponent<CanvasGroup>())
            group.AddComponent<CanvasGroup>();
        
        var rt = group.GetComponent<RectTransform>();
        if (!rt) rt = group.AddComponent<RectTransform>();
        
        // Group在屏幕中心
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(800, 100);
        rt.pivot = new Vector2(0.5f, 0.5f);
        
        var textObj = FindOrCreate(textName, group.transform);
        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (!tmp) tmp = textObj.AddComponent<TextMeshProUGUI>();
        
        tmp.text = content;
        tmp.fontSize = textName.Contains("Title") ? 150 : 100;
        tmp.alignment = TextAlignmentOptions.Center;
        
        // 设置暖色调渐变
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new TMPro.VertexGradient(
            new Color(1f, 1f, 0.9f),      // 上左：浅暖白
            new Color(1f, 0.95f, 0.85f),   // 上右
            new Color(1f, 0.9f, 0.7f),     // 下左：金黄
            new Color(1f, 0.85f, 0.65f)    // 下右
        );
        
        // 彩色边框
        tmp.outlineWidth = 0.4f;
        tmp.outlineColor = new Color32(150, 80, 255, 255);
        
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(x, y);
        textRt.sizeDelta = new Vector2(800, 100);
        textRt.pivot = new Vector2(0.5f, 0.5f);
    }
    
    static void SetupFadeScreen(GameObject obj)
    {
        if (!obj.GetComponent<CanvasGroup>())
            obj.AddComponent<CanvasGroup>();
        
        var img = obj.GetComponent<UnityEngine.UI.Image>();
        if (!img) img = obj.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        
        var rt = obj.GetComponent<RectTransform>();
        if (!rt) rt = obj.AddComponent<RectTransform>();
        
        // 铺满整个屏幕
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
    
    static void SetupSprite(GameObject obj, float x, float y, float scale = 1)
    {
        var sr = obj.GetComponent<SpriteRenderer>();
        if (!sr) sr = obj.AddComponent<SpriteRenderer>();
        
        obj.transform.position = new Vector3(x, y, 0);
        obj.transform.localScale = Vector3.one * scale;
    }
    
    static void SetProp(Object target, string name, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(name);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
#endif
}

