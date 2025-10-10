using UnityEngine;
using UnityEditor;
using System.IO;

namespace FadedDreams.Editor
{
    /// <summary>
    /// 自动设置游戏图标的编辑器工具
    /// 为素素准备的一键设置工具 (◕‿◕✿)
    /// </summary>
    public class IconSetupTool : EditorWindow
    {
        private Texture2D iconTexture;
        private string iconPath = "Assets/Branding/radiant_geometry_icon.ico";
        private bool autoSetupComplete = false;
        
        [MenuItem("Tools/Radiant Geometry/设置游戏图标 ✨")]
        public static void ShowWindow()
        {
            IconSetupTool window = GetWindow<IconSetupTool>("图标设置工具");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }
        
        [MenuItem("Tools/Radiant Geometry/一键设置所有图标 🚀")]
        public static void QuickSetupIcon()
        {
            SetupDefaultIcon();
        }
        
        void OnGUI()
        {
            GUILayout.Label("🎨 Radiant Geometry - 图标设置工具", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox("这个工具会帮你自动设置游戏图标～(◕‿◕✿)", MessageType.Info);
            GUILayout.Space(10);
            
            // 显示当前图标路径
            GUILayout.Label("图标文件:", EditorStyles.boldLabel);
            iconPath = EditorGUILayout.TextField("路径:", iconPath);
            
            GUILayout.Space(10);
            
            // 检查文件是否存在
            bool fileExists = File.Exists(iconPath);
            if (fileExists)
            {
                EditorGUILayout.HelpBox("✅ 找到图标文件!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("❌ 找不到图标文件，请检查路径", MessageType.Warning);
            }
            
            GUILayout.Space(10);
            
            // 一键设置按钮
            GUI.enabled = fileExists;
            if (GUILayout.Button("🚀 一键设置图标", GUILayout.Height(40)))
            {
                SetupDefaultIcon();
                autoSetupComplete = true;
            }
            GUI.enabled = true;
            
            GUILayout.Space(10);
            
            if (autoSetupComplete)
            {
                EditorGUILayout.HelpBox("✨ 图标设置完成！\n" +
                    "现在可以构建游戏测试效果啦～\n" +
                    "爱娘建议：File → Build Settings → Build", MessageType.Info);
            }
            
            GUILayout.Space(20);
            
            // 手动设置区域
            GUILayout.Label("手动设置:", EditorStyles.boldLabel);
            if (GUILayout.Button("打开 Player Settings"))
            {
                SettingsService.OpenProjectSettings("Project/Player");
            }
            
            GUILayout.Space(10);
            
            // 显示当前设置的图标
            GUILayout.Label("当前默认图标:", EditorStyles.boldLabel);
            var currentIcons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
            if (currentIcons != null && currentIcons.Length > 0 && currentIcons[0] != null)
            {
                GUILayout.Label(currentIcons[0], GUILayout.Width(128), GUILayout.Height(128));
            }
            else
            {
                EditorGUILayout.HelpBox("未设置图标", MessageType.Warning);
            }
            
            GUILayout.FlexibleSpace();
            
            // 底部说明
            EditorGUILayout.HelpBox(
                "💡 提示：\n" +
                "1. 图标文件应该在 Assets/Branding/ 文件夹中\n" +
                "2. 支持 .ico 和 .png 格式\n" +
                "3. 推荐尺寸：256x256 或更大\n" +
                "4. 构建游戏后可以在.exe文件上看到图标", 
                MessageType.Info
            );
        }
        
        /// <summary>
        /// 自动设置默认图标
        /// </summary>
        static void SetupDefaultIcon()
        {
            string iconPath = "Assets/Branding/radiant_geometry_icon.ico";
            
            // 检查文件是否存在
            if (!File.Exists(iconPath))
            {
                Debug.LogWarning($"[IconSetupTool] 找不到图标文件: {iconPath}");
                EditorUtility.DisplayDialog("错误", 
                    "找不到图标文件！\n请确保文件在：\n" + iconPath, 
                    "好的");
                return;
            }
            
            // 加载图标资源
            Object iconAsset = AssetDatabase.LoadAssetAtPath<Object>(iconPath);
            if (iconAsset == null)
            {
                Debug.LogWarning("[IconSetupTool] 无法加载图标资源");
                return;
            }
            
            Debug.Log($"[IconSetupTool] 成功加载图标: {iconPath}");
            
            // 尝试设置为默认图标
            // 注意：.ico文件在Unity中需要特殊处理
            // 我们尝试多种方法
            
            // 方法1: 尝试作为Texture2D加载
            Texture2D iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            
            if (iconTexture != null)
            {
                // 设置默认图标
                Texture2D[] icons = new Texture2D[] { iconTexture };
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, icons);
                
                Debug.Log("[IconSetupTool] ✅ 默认图标设置成功!");
            }
            else
            {
                Debug.LogWarning("[IconSetupTool] 无法将.ico文件作为Texture2D加载");
                Debug.Log("[IconSetupTool] 建议：将.ico文件转换为PNG格式");
                
                EditorUtility.DisplayDialog("提示", 
                    "检测到.ico格式文件。\n\n" +
                    "Unity推荐使用PNG格式的图标。\n" +
                    "爱娘建议：\n" +
                    "1. 将.ico转换为PNG\n" +
                    "2. 或者手动在Player Settings中设置\n\n" +
                    "要打开Player Settings吗？", 
                    "是的", "取消");
                
                SettingsService.OpenProjectSettings("Project/Player");
            }
            
            // 同时设置Windows Standalone平台的图标
            try
            {
                if (iconTexture != null)
                {
                    Texture2D[] standalonIcons = new Texture2D[] { iconTexture };
                    PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, standalonIcons);
                    Debug.Log("[IconSetupTool] ✅ Windows平台图标设置成功!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IconSetupTool] 设置Windows图标时出错: {e.Message}");
            }
            
            // 保存资源
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("[IconSetupTool] 图标设置流程完成！✨");
        }
        
        /// <summary>
        /// 检查并创建PNG版本的图标
        /// </summary>
        [MenuItem("Tools/Radiant Geometry/检查图标格式 🔍")]
        public static void CheckIconFormat()
        {
            string iconPath = "Assets/Branding/radiant_geometry_icon.ico";
            
            if (!File.Exists(iconPath))
            {
                EditorUtility.DisplayDialog("提示", 
                    "找不到图标文件！\n路径: " + iconPath, 
                    "好的");
                return;
            }
            
            // 检查是否可以作为Texture加载
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            
            if (tex != null)
            {
                EditorUtility.DisplayDialog("图标格式检查", 
                    $"✅ 图标格式正常!\n\n" +
                    $"尺寸: {tex.width}x{tex.height}\n" +
                    $"格式: {tex.format}\n\n" +
                    "可以使用一键设置功能。", 
                    "好的");
            }
            else
            {
                EditorUtility.DisplayDialog("图标格式检查", 
                    "⚠️ 图标格式不兼容\n\n" +
                    ".ico文件可能需要转换为PNG格式。\n\n" +
                    "建议步骤：\n" +
                    "1. 使用在线工具将.ico转为PNG\n" +
                    "2. 保存为 icon_256.png\n" +
                    "3. 放入 Assets/Branding/ 文件夹\n" +
                    "4. 重新运行一键设置", 
                    "知道了");
            }
        }
    }
}

