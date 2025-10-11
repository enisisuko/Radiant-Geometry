// FixFluidMenuScales.cs
// 修复流体菜单选项缩放问题的工具
// 功能：一键修复所有菜单选项的缩放

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FadedDreams.Editor
{
    /// <summary>
    /// 修复流体菜单缩放问题
    /// </summary>
    public static class FixFluidMenuScales
    {
        [MenuItem("Tools/Radiant Geometry/Fix Fluid Menu Scales")]
        public static void FixMenuScales()
        {
            // 查找MainPanel
            GameObject mainPanel = GameObject.Find("MainPanel");
            if (mainPanel == null)
            {
                EditorUtility.DisplayDialog("错误", "找不到MainPanel对象", "确定");
                return;
            }

            // 修复所有子对象的缩放
            string[] optionNames = new string[]
            {
                "Option_NewGame",
                "Option_Continue", 
                "Option_Coop",
                "Option_Settings",
                "Option_Support",
                "Option_Quit"
            };

            int fixedCount = 0;
            foreach (string optionName in optionNames)
            {
                Transform option = mainPanel.transform.Find(optionName);
                if (option != null)
                {
                    option.localScale = Vector3.one * 1.2f;
                    fixedCount++;
                    Debug.Log($"[FixMenuScales] 修复了 {optionName} 的缩放");
                }
            }

            if (fixedCount > 0)
            {
                EditorUtility.DisplayDialog("成功", 
                    $"成功修复了 {fixedCount} 个菜单选项的缩放！\n\n" +
                    "现在应该能看到所有菜单按钮了~ ✨", 
                    "太好了！");
                    
                // 标记场景为已修改
                EditorSceneManagement.EditorSceneManager.MarkSceneDirty(
                    EditorSceneManagement.EditorSceneManager.GetActiveScene()
                );
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "没有找到需要修复的菜单选项", "确定");
            }
        }

        [MenuItem("Tools/Radiant Geometry/Enable Colorful Fluid")]
        public static void EnableColorfulFluid()
        {
            GameObject fluidContainer = GameObject.Find("FluidContainer");
            if (fluidContainer == null)
            {
                EditorUtility.DisplayDialog("错误", "找不到FluidContainer对象", "确定");
                return;
            }

            // 移除旧的流体模拟器（如果有）
            var oldSimulator = fluidContainer.GetComponent<FadedDreams.UI.Fluid3DSimulator>();
            if (oldSimulator != null)
            {
                Object.DestroyImmediate(oldSimulator);
                Debug.Log("[EnableColorfulFluid] 移除了旧的流体模拟器");
            }

            // 添加彩色流体模拟器
            var colorfulSimulator = fluidContainer.GetComponent<FadedDreams.UI.ColorfulFluid3DSimulator>();
            if (colorfulSimulator == null)
            {
                colorfulSimulator = fluidContainer.AddComponent<FadedDreams.UI.ColorfulFluid3DSimulator>();
                Debug.Log("[EnableColorfulFluid] 添加了彩色流体模拟器");
                
                // 配置参数
                colorfulSimulator.gridResolution = 64;
                colorfulSimulator.viscosity = 0.001f;
                colorfulSimulator.colorDiffusion = 0.001f;
                colorfulSimulator.colorInjectionStrength = 1.0f;
                colorfulSimulator.emissionIntensity = 2f;
                
                EditorUtility.DisplayDialog("成功", 
                    "彩色流体模拟器已启用！\n\n" +
                    "现在可以看到6种颜色相互侵染的效果了~ 🌈", 
                    "太棒了！");
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "彩色流体模拟器已经存在", "确定");
            }

            // 更新水面材质
            var waterSurface = fluidContainer.transform.Find("WaterSurface");
            if (waterSurface != null)
            {
                var renderer = waterSurface.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 尝试使用彩色流体Shader
                    Shader colorfulShader = Shader.Find("FadedDreams/ColorfulFluid3DWater");
                    if (colorfulShader != null)
                    {
                        renderer.sharedMaterial.shader = colorfulShader;
                        Debug.Log("[EnableColorfulFluid] 应用了彩色流体Shader");
                    }
                }
            }

            EditorSceneManagement.EditorSceneManager.MarkSceneDirty(
                EditorSceneManagement.EditorSceneManager.GetActiveScene()
            );
        }
    }
}
#endif
