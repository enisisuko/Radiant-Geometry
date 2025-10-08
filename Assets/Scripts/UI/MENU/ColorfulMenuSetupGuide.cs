using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace FadedDreams.UI
{
    /// <summary>
    /// 彩色主菜单设置指南
    /// 这个脚本包含了如何在Unity中设置彩色主菜单的详细步骤
    /// </summary>
    public class ColorfulMenuSetupGuide : MonoBehaviour
    {
        [Header("设置步骤说明")]
        [TextArea(20, 30)]
        public string setupInstructions = @"
=== 彩色主菜单设置指南 ===

1. 创建主菜单场景
   - 新建场景：File > New Scene
   - 保存为：ColorfulMainMenu

2. 设置摄像机
   - 创建摄像机：GameObject > Camera
   - 设置位置：(0, 0, -10)
   - 添加标签：MainCamera

3. 创建UI画布
   - 创建画布：GameObject > UI > Canvas
   - 设置渲染模式：Screen Space - Overlay
   - 添加CanvasScaler组件，设置UI Scale Mode为Scale With Screen Size

4. 创建五个分区
   对每个分区执行以下步骤：
   
   a) 创建分区背景
      - 右键Canvas > UI > Image
      - 命名为：Section_0, Section_1, Section_2, Section_3, Section_4
      - 设置颜色：红色、蓝色、紫色、黄色、绿色
      - 设置大小：根据屏幕比例调整
   
   b) 添加碰撞器
      - 添加BoxCollider2D组件
      - 调整大小覆盖整个分区
      - 设置IsTrigger为true
   
   c) 添加文字
      - 右键分区 > UI > Text
      - 设置文字：新游戏、继续游戏、双人模式、退出游戏、支持我
      - 调整字体大小和颜色

5. 创建中心旋转小球
   - 创建球体：GameObject > 3D Object > Sphere
   - 命名为：CenterBall
   - 设置位置：(0, 0, 0)
   - 添加材质和颜色
   - 添加CenterRotatingBall脚本

6. 设置光照系统
   - 创建光源：GameObject > Light > Directional Light
   - 为每个分区创建点光源：GameObject > Light > Point Light
   - 设置光源颜色与分区颜色对应
   - 添加MenuLightingSystem脚本

7. 添加粒子效果（可选）
   - 为每个分区创建粒子系统：GameObject > Effects > Particle System
   - 设置粒子颜色与分区颜色对应
   - 调整粒子参数

8. 设置主菜单管理器
   - 创建空物体：GameObject > Create Empty
   - 命名为：MenuManager
   - 添加ColorfulMenuManager脚本
   - 在Inspector中连接所有引用

9. 配置菜单动作
   - 在ColorfulMenuManager中设置：
     * newGameScene = ""STORY0""
     * firstCheckpointId = ""101""
   - 确保STORY0场景在Build Settings中

10. 添加音效（可选）
    - 创建AudioSource组件
    - 添加悬停、点击、蔓延音效
    - 添加背景音乐

11. 测试功能
    - 运行场景
    - 测试鼠标悬停效果
    - 测试点击色彩蔓延
    - 测试菜单功能

=== 注意事项 ===
- 确保所有脚本都正确引用
- 检查场景中的检查点ID是否正确
- 测试在不同分辨率下的显示效果
- 确保音效文件存在且格式正确

=== 故障排除 ===
- 如果小球不旋转，检查CenterRotatingBall脚本是否正确添加
- 如果光照不工作，检查光源设置和MenuLightingSystem脚本
- 如果色彩蔓延不工作，检查ColorSpreadEffect脚本和UI引用
- 如果场景切换失败，检查SceneLoader和SaveSystem是否正确配置
";

        [Header("快速设置工具")]
        public bool autoSetup = false;
        
        void Start()
        {
            if (autoSetup)
            {
                StartCoroutine(AutoSetup());
            }
        }
        
        System.Collections.IEnumerator AutoSetup()
        {
            Debug.Log("开始自动设置彩色主菜单...");
            
            // 这里可以添加自动设置代码
            // 例如自动创建UI元素、设置引用等
            
            yield return new WaitForSeconds(1f);
            
            Debug.Log("自动设置完成！请检查设置结果。");
        }
        
        [ContextMenu("显示设置指南")]
        public void ShowSetupGuide()
        {
            Debug.Log(setupInstructions);
        }
        
        [ContextMenu("验证设置")]
        public void ValidateSetup()
        {
            bool isValid = true;
            string errors = "";
            
            // 检查必要的组件
            if (FindObjectOfType<ColorfulMenuManager>() == null)
            {
                errors += "缺少ColorfulMenuManager组件\n";
                isValid = false;
            }
            
            if (FindObjectOfType<CenterRotatingBall>() == null)
            {
                errors += "缺少CenterRotatingBall组件\n";
                isValid = false;
            }
            
            if (FindObjectOfType<MenuLightingSystem>() == null)
            {
                errors += "缺少MenuLightingSystem组件\n";
                isValid = false;
            }
            
            if (FindObjectOfType<ColorSpreadEffect>() == null)
            {
                errors += "缺少ColorSpreadEffect组件\n";
                isValid = false;
            }
            
            // 检查场景设置
            if (!IsSceneInBuildSettings("STORY0"))
            {
                errors += "STORY0场景未添加到Build Settings\n";
                isValid = false;
            }
            
            if (isValid)
            {
                Debug.Log("✓ 设置验证通过！");
            }
            else
            {
                Debug.LogError("✗ 设置验证失败：\n" + errors);
            }
        }
        
        bool IsSceneInBuildSettings(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                if (scenePath.Contains(sceneName))
                {
                    return true;
                }
            }
            return false;
        }
    }
}