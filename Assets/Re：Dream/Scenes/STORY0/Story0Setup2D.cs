using UnityEngine;
using TMPro;
using FadedDreams.Story;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// STORY0场景一键配置工具 - 2D版本
/// 帮助自动设置Director脚本的所有引用
/// </summary>
public class Story0Setup2D : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/STORY0/Auto Setup Director 2D")]
    public static void AutoSetupDirector2D()
    {
        // 打开STORY0场景
        var scene = EditorSceneManager.OpenScene("Assets/Re：Dream/Scenes/STORY0.unity");
        
        // 查找Director对象
        var director = GameObject.Find("Director");
        if (director == null)
        {
            Debug.LogError("找不到Director对象！");
            return;
        }
        
        // 确保有Story0Director2D组件
        var script = director.GetComponent<Story0Director2D>();
        if (script == null)
        {
            script = director.AddComponent<Story0Director2D>();
            Debug.Log("✓ 添加Story0Director2D组件");
        }
        
        // 查找并设置所有引用
        var fallingSquare = GameObject.Find("FallingSquare");
        var mainCamera = GameObject.Find("Main Camera");
        var background = GameObject.Find("Background");
        var canvas = GameObject.Find("Canvas");
        
        // 设置Transform引用
        if (fallingSquare != null)
            SetProperty(script, "fallingSquare", fallingSquare.transform);
        else
            Debug.LogWarning("找不到FallingSquare");
            
        // 设置Camera引用并确保是正交模式
        if (mainCamera != null)
        {
            var cam = mainCamera.GetComponent<Camera>();
            SetProperty(script, "mainCamera", cam);
            if (cam != null && !cam.orthographic)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                Debug.Log("✓ 设置相机为正交模式");
            }
        }
        else
            Debug.LogWarning("找不到Main Camera");
            
        // 设置Background的SpriteRenderer
        if (background != null)
            SetProperty(script, "backgroundSprite", background.GetComponent<SpriteRenderer>());
        else
            Debug.LogWarning("找不到Background");
        
        // 设置UI引用
        if (canvas != null)
        {
            var titleGroup = canvas.transform.Find("TitleGroup");
            var authorGroup = canvas.transform.Find("AuthorGroup");
            var fadeScreen = canvas.transform.Find("FadeScreen");
            
            if (titleGroup != null)
            {
                SetProperty(script, "titleGroup", titleGroup.GetComponent<CanvasGroup>());
                var titleText = titleGroup.Find("TitleText");
                if (titleText != null)
                    SetProperty(script, "titleText", titleText.GetComponent<TextMeshProUGUI>());
            }
            
            if (authorGroup != null)
            {
                SetProperty(script, "authorGroup", authorGroup.GetComponent<CanvasGroup>());
                var authorText = authorGroup.Find("AuthorText");
                if (authorText != null)
                    SetProperty(script, "authorText", authorText.GetComponent<TextMeshProUGUI>());
            }
            
            if (fadeScreen != null)
                SetProperty(script, "fadeScreen", fadeScreen.GetComponent<CanvasGroup>());
        }
        
        // 加载并设置渐变材质
        var gradientMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Re：Dream/Scenes/STORY0/GradientBackground.mat");
        if (gradientMat != null)
            SetProperty(script, "gradientMaterial", gradientMat);
        else
            Debug.LogWarning("找不到GradientBackground.mat");
        
        // 标记场景为已修改
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("✅ STORY0 Director 2D配置完成！所有引用已自动设置。");
        Debug.Log("⚠️ 请记得：");
        Debug.Log("   1. 手动设置特效预制体（Effect Prefab）字段");
        Debug.Log("   2. 为FallingSquare设置Sprite（白色正方形）");
        Debug.Log("   3. 为Background设置Sprite（或留空用颜色）");
    }
    
    private static void SetProperty(Object target, string propertyName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            Debug.Log($"✓ 设置 {propertyName} = {(value != null ? value.name : "null")}");
        }
        else
        {
            Debug.LogWarning($"找不到属性: {propertyName}");
        }
    }
    
    [MenuItem("Tools/STORY0/Create White Square Sprite")]
    public static void CreateWhiteSquareSprite()
    {
        // 创建一个白色正方形贴图
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        
        // 保存为PNG
        byte[] bytes = tex.EncodeToPNG();
        string path = "Assets/Re：Dream/Scenes/STORY0/WhiteSquare.png";
        System.IO.File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();
        
        // 设置为Sprite类型
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        
        Debug.Log($"✅ 白色正方形Sprite已创建：{path}");
        Debug.Log("   你可以将它拖拽到FallingSquare的SpriteRenderer上");
    }
#endif
}

