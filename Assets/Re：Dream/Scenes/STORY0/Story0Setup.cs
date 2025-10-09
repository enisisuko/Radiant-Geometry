using UnityEngine;
using TMPro;
using FadedDreams.Story;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// STORY0场景一键配置工具
/// 帮助自动设置Director脚本的所有引用
/// </summary>
public class Story0Setup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/STORY0/Auto Setup Director")]
    public static void AutoSetupDirector()
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
        
        var script = director.GetComponent<Story0Director>();
        if (script == null)
        {
            Debug.LogError("Director上没有Story0Director脚本！");
            return;
        }
        
        // 查找并设置所有引用
        var fallingCube = GameObject.Find("FallingCube");
        var mainCamera = GameObject.Find("Main Camera");
        var backgroundPlane = GameObject.Find("BackgroundPlane");
        var canvas = GameObject.Find("Canvas");
        
        // 设置Transform引用
        if (fallingCube != null)
            SetProperty(script, "fallingCube", fallingCube.transform);
        else
            Debug.LogWarning("找不到FallingCube");
            
        // 设置Camera引用
        if (mainCamera != null)
            SetProperty(script, "mainCamera", mainCamera.GetComponent<Camera>());
        else
            Debug.LogWarning("找不到Main Camera");
            
        // 设置BackgroundPlane的Renderer
        if (backgroundPlane != null)
            SetProperty(script, "backgroundPlane", backgroundPlane.GetComponent<Renderer>());
        else
            Debug.LogWarning("找不到BackgroundPlane");
        
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
        
        Debug.Log("✅ STORY0 Director配置完成！所有引用已自动设置。");
        Debug.Log("⚠️ 请记得手动设置特效预制体（Effect Prefab）字段哦~");
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
#endif
}

