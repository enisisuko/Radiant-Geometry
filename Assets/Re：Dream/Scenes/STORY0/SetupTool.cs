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
        var bg = FindOrCreate("Background");
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
        SetupSprite(square, 0, 5);
        
        // 设置背景
        SetupSprite(bg, 0, 0, 50);
        
        // 设置Director脚本
        var script = director.GetComponent<Story0Director>();
        if (!script) script = director.AddComponent<Story0Director>();
        
        SetProp(script, "fallingSquare", square.transform);
        SetProp(script, "mainCamera", Camera.main);
        SetProp(script, "background", bg.GetComponent<SpriteRenderer>());
        SetProp(script, "titleGroup", titleGroup.GetComponent<CanvasGroup>());
        SetProp(script, "titleText", titleGroup.transform.Find("TitleText").GetComponent<TextMeshProUGUI>());
        SetProp(script, "authorGroup", authorGroup.GetComponent<CanvasGroup>());
        SetProp(script, "authorText", authorGroup.transform.Find("AuthorText").GetComponent<TextMeshProUGUI>());
        SetProp(script, "fadeScreen", fadeScreen.GetComponent<CanvasGroup>());
        
        // 保存
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("✅ 配置完成！别忘了设置特效预制体~");
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
        if (!canvas.GetComponent<Canvas>())
        {
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
    }
    
    static void SetupUIGroup(GameObject group, string textName, string content, float x, float y)
    {
        if (!group.GetComponent<CanvasGroup>())
            group.AddComponent<CanvasGroup>();
        
        var rt = group.GetComponent<RectTransform>();
        if (!rt) rt = group.AddComponent<RectTransform>();
        
        var textObj = FindOrCreate(textName, group.transform);
        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (!tmp) tmp = textObj.AddComponent<TextMeshProUGUI>();
        
        tmp.text = content;
        tmp.fontSize = textName.Contains("Title") ? 72 : 48;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(x, y);
        textRt.sizeDelta = new Vector2(800, 100);
    }
    
    static void SetupFadeScreen(GameObject obj)
    {
        if (!obj.GetComponent<CanvasGroup>())
            obj.AddComponent<CanvasGroup>();
        
        var img = obj.GetComponent<UnityEngine.UI.Image>();
        if (!img) img = obj.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
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

