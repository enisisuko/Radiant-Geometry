// ColorfulFluid3DSimulator.cs
// 彩色流体模拟器 - 支持多种颜色相互侵染
// 功能：彩色流体混合、颜色扩散、动态调色、美丽渐变

using UnityEngine;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 彩色3D流体模拟器
    /// 支持多种颜色流体相互侵染的美丽效果
    /// </summary>
    public class ColorfulFluid3DSimulator : MonoBehaviour
    {
        [Header("流体网格设置")]
        [Tooltip("网格分辨率")]
        [Range(16, 128)]
        public int gridResolution = 64;

        [Header("物理参数")]
        [Tooltip("粘度")]
        [Range(0.0001f, 0.1f)]
        public float viscosity = 0.001f;

        [Tooltip("颜色扩散系数")]
        [Range(0.00001f, 0.01f)]
        public float colorDiffusion = 0.001f;

        [Tooltip("时间步长")]
        [Range(0.001f, 0.1f)]
        public float timeStep = 0.016f;

        [Header("颜色设置")]
        [Tooltip("6个菜单项的颜色")]
        public Color[] menuColors = new Color[6]
        {
            new Color(1f, 0.2f, 0.2f, 1f),    // 红色（新游戏）
            new Color(0.2f, 0.8f, 1f, 1f),    // 蓝色（继续）
            new Color(0.8f, 0.2f, 1f, 1f),    // 紫色（双人）
            new Color(1f, 0.5f, 0f, 1f),      // 橙色（设置）
            new Color(0.2f, 1f, 0.2f, 1f),    // 绿色（支持）
            new Color(1f, 0.8f, 0.2f, 1f)     // 黄色（退出）
        };

        [Header("交互参数")]
        [Tooltip("颜色注入强度")]
        public float colorInjectionStrength = 1.0f;

        [Tooltip("注入半径")]
        public float injectionRadius = 2f;

        [Header("渲染设置")]
        [Tooltip("流体可视化材质")]
        public Material fluidVisualizeMaterial;

        [Tooltip("颜色混合模式")]
        public enum ColorBlendMode { Additive, Average, Max }
        public ColorBlendMode blendMode = ColorBlendMode.Average;

        [Header("视觉效果")]
        [Tooltip("颜色饱和度增强")]
        [Range(1f, 3f)]
        public float saturationBoost = 1.5f;

        [Tooltip("发光强度")]
        [Range(0f, 5f)]
        public float emissionIntensity = 2f;

        // 颜色场（RGB向量场）
        private Vector3[,,] colorField;
        private Vector3[,,] colorField0;

        // 速度场
        private Vector3[,,] velocity;
        private Vector3[,,] velocity0;

        // 密度场
        private float[,,] density;
        private float[,,] density0;

        // 渲染相关
        private Texture3D colorTexture;
        private ComputeBuffer colorBuffer;

        // 网格尺寸
        private int N;
        private float cellSize = 0.5f;

        // 颜色源
        private class ColorSource
        {
            public Vector3 position;
            public Color color;
            public float strength;
            public float lifeTime;
        }
        private List<ColorSource> activeColorSources = new List<ColorSource>();

        private void Start()
        {
            InitializeFluidGrid();
            InitializeRendering();
            StartColorfulSimulation();
        }

        /// <summary>
        /// 初始化流体网格
        /// </summary>
        private void InitializeFluidGrid()
        {
            N = gridResolution;

            // 分配内存
            velocity = new Vector3[N, N, N];
            velocity0 = new Vector3[N, N, N];
            density = new float[N, N, N];
            density0 = new float[N, N, N];
            colorField = new Vector3[N, N, N];
            colorField0 = new Vector3[N, N, N];

            // 初始化基础流体
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    for (int k = 0; k < N; k++)
                    {
                        // 底部有初始液体
                        if (j < N / 4)
                        {
                            density[i, j, k] = 1f;
                            // 初始颜色：深蓝色基底
                            colorField[i, j, k] = new Vector3(0.1f, 0.2f, 0.4f);
                        }
                    }
                }
            }

            Debug.Log($"[ColorfulFluid3D] 彩色流体网格初始化完成: {N}x{N}x{N}");
        }

        /// <summary>
        /// 初始化渲染系统
        /// </summary>
        private void InitializeRendering()
        {
            // 创建3D纹理用于GPU渲染
            colorTexture = new Texture3D(N, N, N, TextureFormat.RGBAFloat, false);
            colorTexture.wrapMode = TextureWrapMode.Clamp;
            colorTexture.filterMode = FilterMode.Trilinear;

            // 设置材质属性
            if (fluidVisualizeMaterial != null)
            {
                fluidVisualizeMaterial.SetTexture("_ColorField", colorTexture);
                fluidVisualizeMaterial.SetFloat("_GridSize", N);
                fluidVisualizeMaterial.SetFloat("_EmissionIntensity", emissionIntensity);
            }

            Debug.Log("[ColorfulFluid3D] 渲染系统初始化完成");
        }

        /// <summary>
        /// 开始彩色流体模拟
        /// </summary>
        public void StartColorfulSimulation()
        {
            // 初始化6个颜色源（对应6个菜单项）
            Vector3[] colorSourcePositions = new Vector3[]
            {
                new Vector3(-4f, 0.5f, 2f),   // 新游戏
                new Vector3(0f, 0.5f, 2f),    // 继续
                new Vector3(4f, 0.5f, 2f),    // 双人
                new Vector3(-2f, 0.5f, -2f),  // 设置
                new Vector3(2f, 0.5f, -2f),   // 支持
                new Vector3(0f, 0.5f, -4f)    // 退出
            };

            // 为每个位置添加初始颜色源
            for (int i = 0; i < 6; i++)
            {
                AddColorSource(colorSourcePositions[i], menuColors[i], 0.5f);
            }

            Debug.Log("[ColorfulFluid3D] 彩色流体模拟已启动");
        }

        private void Update()
        {
            // 执行流体模拟步骤
            SimulateColorfulFluidStep();

            // 更新颜色源
            UpdateColorSources();

            // 更新渲染纹理
            UpdateColorTexture();

            // 处理交互
            HandleColorfulInteraction();
        }

        /// <summary>
        /// 执行一步彩色流体模拟
        /// </summary>
        private void SimulateColorfulFluidStep()
        {
            float dt = timeStep;

            // 1. 速度场扩散
            DiffuseVelocity(dt);

            // 2. 投影（确保不可压缩性）
            ProjectVelocity();

            // 3. 平流（速度场自我传输）
            AdvectVelocity(dt);

            // 4. 再次投影
            ProjectVelocity();

            // 5. 密度场扩散
            DiffuseDensity(dt);

            // 6. 密度场平流
            AdvectDensity(dt);

            // 7. 颜色场扩散（颜色相互侵染的关键）
            DiffuseColor(dt);

            // 8. 颜色场平流
            AdvectColor(dt);

            // 9. 颜色混合增强
            EnhanceColorMixing();
        }

        /// <summary>
        /// 颜色场扩散（实现颜色侵染效果）
        /// </summary>
        private void DiffuseColor(float dt)
        {
            float a = dt * colorDiffusion * N * N * N;

            // 复制当前颜色场
            System.Array.Copy(colorField, colorField0, colorField.Length);

            // 扩散迭代（使用Gauss-Seidel）
            for (int iter = 0; iter < 6; iter++) // 多次迭代以获得更好的扩散
            {
                for (int i = 1; i < N - 1; i++)
                {
                    for (int j = 1; j < N - 1; j++)
                    {
                        for (int k = 1; k < N - 1; k++)
                        {
                            // 6邻域平均
                            Vector3 avgColor = (
                                colorField[i - 1, j, k] + colorField[i + 1, j, k] +
                                colorField[i, j - 1, k] + colorField[i, j + 1, k] +
                                colorField[i, j, k - 1] + colorField[i, j, k + 1]
                            ) / 6f;

                            // 根据密度加权混合
                            float localDensity = density[i, j, k];
                            if (localDensity > 0.01f)
                            {
                                colorField[i, j, k] = (colorField0[i, j, k] + a * avgColor) / (1 + 6 * a);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 颜色场平流（被速度场带动）
        /// </summary>
        private void AdvectColor(float dt)
        {
            System.Array.Copy(colorField, colorField0, colorField.Length);

            for (int i = 1; i < N - 1; i++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int k = 1; k < N - 1; k++)
                    {
                        Vector3 pos = new Vector3(i, j, k);
                        Vector3 vel = velocity[i, j, k];
                        Vector3 prevPos = pos - vel * dt * N;

                        prevPos.x = Mathf.Clamp(prevPos.x, 0.5f, N - 1.5f);
                        prevPos.y = Mathf.Clamp(prevPos.y, 0.5f, N - 1.5f);
                        prevPos.z = Mathf.Clamp(prevPos.z, 0.5f, N - 1.5f);

                        // 三线性插值（获得平滑的颜色过渡）
                        colorField[i, j, k] = TrilinearInterpolateColor(prevPos);
                    }
                }
            }
        }

        /// <summary>
        /// 增强颜色混合效果
        /// </summary>
        private void EnhanceColorMixing()
        {
            // 在流体边界处增强颜色混合
            for (int i = 1; i < N - 1; i++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int k = 1; k < N - 1; k++)
                    {
                        if (density[i, j, k] > 0.1f)
                        {
                            Vector3 currentColor = colorField[i, j, k];
                            
                            // 检测颜色梯度（颜色变化剧烈的地方）
                            Vector3 colorGradient = CalculateColorGradient(i, j, k);
                            float gradientMagnitude = colorGradient.magnitude;

                            // 在颜色变化剧烈的地方增加混合
                            if (gradientMagnitude > 0.1f)
                            {
                                // 收集周围的颜色
                                Vector3 mixedColor = CollectSurroundingColors(i, j, k);
                                
                                // 根据梯度强度混合
                                float mixFactor = Mathf.Min(gradientMagnitude * 0.2f, 0.5f);
                                colorField[i, j, k] = Vector3.Lerp(currentColor, mixedColor, mixFactor);
                            }

                            // 应用饱和度增强
                            colorField[i, j, k] = EnhanceSaturation(colorField[i, j, k]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 计算颜色梯度
        /// </summary>
        private Vector3 CalculateColorGradient(int i, int j, int k)
        {
            Vector3 dx = (colorField[i + 1, j, k] - colorField[i - 1, j, k]) * 0.5f;
            Vector3 dy = (colorField[i, j + 1, k] - colorField[i, j - 1, k]) * 0.5f;
            Vector3 dz = (colorField[i, j, k + 1] - colorField[i, j, k - 1]) * 0.5f;
            
            return dx + dy + dz;
        }

        /// <summary>
        /// 收集周围颜色
        /// </summary>
        private Vector3 CollectSurroundingColors(int i, int j, int k)
        {
            Vector3 mixedColor = Vector3.zero;
            int count = 0;

            // 收集3x3x3邻域的颜色
            for (int di = -1; di <= 1; di++)
            {
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int dk = -1; dk <= 1; dk++)
                    {
                        int ni = i + di;
                        int nj = j + dj;
                        int nk = k + dk;

                        if (ni >= 0 && ni < N && nj >= 0 && nj < N && nk >= 0 && nk < N)
                        {
                            if (density[ni, nj, nk] > 0.1f)
                            {
                                mixedColor += colorField[ni, nj, nk];
                                count++;
                            }
                        }
                    }
                }
            }

            return count > 0 ? mixedColor / count : colorField[i, j, k];
        }

        /// <summary>
        /// 增强饱和度
        /// </summary>
        private Vector3 EnhanceSaturation(Vector3 color)
        {
            // RGB转HSV
            Color.RGBToHSV(new Color(color.x, color.y, color.z), out float h, out float s, out float v);
            
            // 增强饱和度
            s = Mathf.Min(s * saturationBoost, 1f);
            
            // HSV转回RGB
            Color enhanced = Color.HSVToRGB(h, s, v);
            return new Vector3(enhanced.r, enhanced.g, enhanced.b);
        }

        /// <summary>
        /// 三线性插值颜色
        /// </summary>
        private Vector3 TrilinearInterpolateColor(Vector3 pos)
        {
            int i0 = Mathf.FloorToInt(pos.x);
            int j0 = Mathf.FloorToInt(pos.y);
            int k0 = Mathf.FloorToInt(pos.z);
            
            int i1 = Mathf.Min(i0 + 1, N - 1);
            int j1 = Mathf.Min(j0 + 1, N - 1);
            int k1 = Mathf.Min(k0 + 1, N - 1);
            
            float fx = pos.x - i0;
            float fy = pos.y - j0;
            float fz = pos.z - k0;

            // 8个角的颜色值
            Vector3 c000 = colorField0[i0, j0, k0];
            Vector3 c100 = colorField0[i1, j0, k0];
            Vector3 c010 = colorField0[i0, j1, k0];
            Vector3 c110 = colorField0[i1, j1, k0];
            Vector3 c001 = colorField0[i0, j0, k1];
            Vector3 c101 = colorField0[i1, j0, k1];
            Vector3 c011 = colorField0[i0, j1, k1];
            Vector3 c111 = colorField0[i1, j1, k1];

            // 三线性插值
            Vector3 c00 = Vector3.Lerp(c000, c100, fx);
            Vector3 c10 = Vector3.Lerp(c010, c110, fx);
            Vector3 c01 = Vector3.Lerp(c001, c101, fx);
            Vector3 c11 = Vector3.Lerp(c011, c111, fx);
            
            Vector3 c0 = Vector3.Lerp(c00, c10, fy);
            Vector3 c1 = Vector3.Lerp(c01, c11, fy);
            
            return Vector3.Lerp(c0, c1, fz);
        }

        /// <summary>
        /// 添加颜色源
        /// </summary>
        public void AddColorSource(Vector3 worldPos, Color color, float strength)
        {
            ColorSource source = new ColorSource
            {
                position = worldPos,
                color = color,
                strength = strength,
                lifeTime = 2f // 持续2秒
            };
            
            activeColorSources.Add(source);
            
            // 立即注入颜色
            InjectColorAtPosition(worldPos, color, strength);
        }

        /// <summary>
        /// 在指定位置注入颜色
        /// </summary>
        private void InjectColorAtPosition(Vector3 worldPos, Color color, float strength)
        {
            Vector3 gridPos = WorldToGrid(worldPos);
            int x = Mathf.RoundToInt(gridPos.x);
            int y = Mathf.RoundToInt(gridPos.y);
            int z = Mathf.RoundToInt(gridPos.z);

            int r = Mathf.CeilToInt(injectionRadius);

            for (int i = -r; i <= r; i++)
            {
                for (int j = -r; j <= r; j++)
                {
                    for (int k = -r; k <= r; k++)
                    {
                        int xi = x + i;
                        int yj = y + j;
                        int zk = z + k;

                        if (xi >= 0 && xi < N && yj >= 0 && yj < N && zk >= 0 && zk < N)
                        {
                            float dist = Vector3.Distance(new Vector3(i, j, k), Vector3.zero);
                            if (dist < injectionRadius)
                            {
                                // 高斯分布
                                float gaussian = Mathf.Exp(-dist * dist / (injectionRadius * injectionRadius));
                                
                                // 注入颜色
                                Vector3 targetColor = new Vector3(color.r, color.g, color.b);
                                colorField[xi, yj, zk] = Vector3.Lerp(
                                    colorField[xi, yj, zk],
                                    targetColor,
                                    gaussian * strength * colorInjectionStrength
                                );
                                
                                // 同时增加密度
                                density[xi, yj, zk] = Mathf.Min(density[xi, yj, zk] + gaussian * 0.5f, 1f);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 更新颜色源
        /// </summary>
        private void UpdateColorSources()
        {
            for (int i = activeColorSources.Count - 1; i >= 0; i--)
            {
                ColorSource source = activeColorSources[i];
                source.lifeTime -= Time.deltaTime;

                if (source.lifeTime > 0)
                {
                    // 持续注入颜色（但强度逐渐减弱）
                    float fadeStrength = source.strength * (source.lifeTime / 2f);
                    InjectColorAtPosition(source.position, source.color, fadeStrength * Time.deltaTime);
                }
                else
                {
                    activeColorSources.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 处理彩色交互
        /// </summary>
        private void HandleColorfulInteraction()
        {
            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100f))
                {
                    // 检测击中的菜单项，注入对应颜色
                    for (int i = 0; i < 6; i++)
                    {
                        string optionName = $"Option_{GetOptionName(i)}";
                        if (hit.collider.name == optionName)
                        {
                            AddColorSource(hit.point, menuColors[i], 1f);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取选项名称
        /// </summary>
        private string GetOptionName(int index)
        {
            string[] names = { "NewGame", "Continue", "Coop", "Settings", "Support", "Quit" };
            return names[index];
        }

        /// <summary>
        /// 更新3D纹理（用于GPU渲染）
        /// </summary>
        private void UpdateColorTexture()
        {
            if (colorTexture == null) return;

            Color[] colors = new Color[N * N * N];
            int idx = 0;

            for (int k = 0; k < N; k++)
            {
                for (int j = 0; j < N; j++)
                {
                    for (int i = 0; i < N; i++)
                    {
                        Vector3 col = colorField[i, j, k];
                        float alpha = density[i, j, k];
                        
                        // 应用发光效果
                        col *= (1f + emissionIntensity * alpha);
                        
                        colors[idx] = new Color(col.x, col.y, col.z, alpha);
                        idx++;
                    }
                }
            }

            colorTexture.SetPixels(colors);
            colorTexture.Apply();
        }

        /// <summary>
        /// 当菜单项被悬停时注入颜色
        /// </summary>
        public void OnMenuItemHovered(int index, Vector3 worldPos)
        {
            if (index >= 0 && index < menuColors.Length)
            {
                AddColorSource(worldPos, menuColors[index], 0.3f);
            }
        }

        /// <summary>
        /// 当菜单项被点击时注入大量颜色
        /// </summary>
        public void OnMenuItemClicked(int index, Vector3 worldPos)
        {
            if (index >= 0 && index < menuColors.Length)
            {
                AddColorSource(worldPos, menuColors[index], 1.5f);
                
                // 添加额外的力
                Vector3 gridPos = WorldToGrid(worldPos);
                AddForceAtPosition(gridPos, Vector3.down * 200f);
            }
        }

        // ========== 以下是基础流体模拟方法（从原版继承）==========

        private void DiffuseVelocity(float dt)
        {
            float a = dt * viscosity * N * N * N;

            for (int iter = 0; iter < 4; iter++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    for (int j = 1; j < N - 1; j++)
                    {
                        for (int k = 1; k < N - 1; k++)
                        {
                            Vector3 avg = (
                                velocity[i - 1, j, k] + velocity[i + 1, j, k] +
                                velocity[i, j - 1, k] + velocity[i, j + 1, k] +
                                velocity[i, j, k - 1] + velocity[i, j, k + 1]
                            ) / 6f;

                            velocity[i, j, k] = (velocity0[i, j, k] + a * avg) / (1 + 6 * a);
                        }
                    }
                }
            }
        }

        private void ProjectVelocity()
        {
            int surfaceLevel = N / 4;

            for (int i = 1; i < N - 1; i++)
            {
                for (int k = 1; k < N - 1; k++)
                {
                    float div = (
                        velocity[i + 1, surfaceLevel, k].x - velocity[i - 1, surfaceLevel, k].x +
                        velocity[i, surfaceLevel + 1, k].y - velocity[i, surfaceLevel - 1, k].y +
                        velocity[i, surfaceLevel, k + 1].z - velocity[i, surfaceLevel, k - 1].z
                    ) * 0.5f / N;

                    velocity[i, surfaceLevel, k] -= new Vector3(div, div, div) * 0.5f;
                }
            }
        }

        private void AdvectVelocity(float dt)
        {
            System.Array.Copy(velocity, velocity0, velocity.Length);

            for (int i = 1; i < N - 1; i++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int k = 1; k < N - 1; k++)
                    {
                        Vector3 pos = new Vector3(i, j, k);
                        Vector3 vel = velocity0[i, j, k];
                        Vector3 prevPos = pos - vel * dt * N;
                        
                        prevPos.x = Mathf.Clamp(prevPos.x, 0.5f, N - 1.5f);
                        prevPos.y = Mathf.Clamp(prevPos.y, 0.5f, N - 1.5f);
                        prevPos.z = Mathf.Clamp(prevPos.z, 0.5f, N - 1.5f);
                        
                        int i0 = Mathf.FloorToInt(prevPos.x);
                        int j0 = Mathf.FloorToInt(prevPos.y);
                        int k0 = Mathf.FloorToInt(prevPos.z);
                        
                        if (i0 >= 0 && i0 < N - 1 && j0 >= 0 && j0 < N - 1 && k0 >= 0 && k0 < N - 1)
                        {
                            velocity[i, j, k] = velocity0[i0, j0, k0];
                        }
                    }
                }
            }
        }

        private void DiffuseDensity(float dt)
        {
            float a = dt * colorDiffusion * N * N * N;

            for (int iter = 0; iter < 4; iter++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    for (int j = 1; j < N - 1; j++)
                    {
                        for (int k = 1; k < N - 1; k++)
                        {
                            float avg = (
                                density[i - 1, j, k] + density[i + 1, j, k] +
                                density[i, j - 1, k] + density[i, j + 1, k] +
                                density[i, j, k - 1] + density[i, j, k + 1]
                            ) / 6f;

                            density[i, j, k] = (density0[i, j, k] + a * avg) / (1 + 6 * a);
                        }
                    }
                }
            }
        }

        private void AdvectDensity(float dt)
        {
            System.Array.Copy(density, density0, density.Length);

            for (int i = 1; i < N - 1; i++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int k = 1; k < N - 1; k++)
                    {
                        Vector3 pos = new Vector3(i, j, k);
                        Vector3 vel = velocity[i, j, k];
                        Vector3 prevPos = pos - vel * dt * N;
                        
                        prevPos.x = Mathf.Clamp(prevPos.x, 0.5f, N - 1.5f);
                        prevPos.y = Mathf.Clamp(prevPos.y, 0.5f, N - 1.5f);
                        prevPos.z = Mathf.Clamp(prevPos.z, 0.5f, N - 1.5f);
                        
                        int i0 = Mathf.FloorToInt(prevPos.x);
                        int j0 = Mathf.FloorToInt(prevPos.y);
                        int k0 = Mathf.FloorToInt(prevPos.z);
                        
                        if (i0 >= 0 && i0 < N - 1 && j0 >= 0 && j0 < N - 1 && k0 >= 0 && k0 < N - 1)
                        {
                            density[i, j, k] = density0[i0, j0, k0];
                        }
                    }
                }
            }
        }

        public void AddForceAtPosition(Vector3 gridPos, Vector3 force)
        {
            int x = Mathf.RoundToInt(gridPos.x);
            int y = Mathf.RoundToInt(gridPos.y);
            int z = Mathf.RoundToInt(gridPos.z);

            int r = Mathf.CeilToInt(injectionRadius);

            for (int i = -r; i <= r; i++)
            {
                for (int j = -r; j <= r; j++)
                {
                    for (int k = -r; k <= r; k++)
                    {
                        int xi = x + i;
                        int yj = y + j;
                        int zk = z + k;

                        if (xi >= 0 && xi < N && yj >= 0 && yj < N && zk >= 0 && zk < N)
                        {
                            float dist = Vector3.Distance(new Vector3(i, j, k), Vector3.zero);
                            if (dist < injectionRadius)
                            {
                                float strength = Mathf.Exp(-dist * dist / (injectionRadius * injectionRadius));
                                velocity[xi, yj, zk] += force * strength;
                            }
                        }
                    }
                }
            }
        }

        private Vector3 WorldToGrid(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            
            float x = (localPos.x / cellSize + N * 0.5f);
            float y = (localPos.y / cellSize + N * 0.5f);
            float z = (localPos.z / cellSize + N * 0.5f);

            return new Vector3(
                Mathf.Clamp(x, 0, N - 1),
                Mathf.Clamp(y, 0, N - 1),
                Mathf.Clamp(z, 0, N - 1)
            );
        }

        /// <summary>
        /// 获取指定位置的颜色（用于渲染）
        /// </summary>
        public Color GetColorAt(int x, int y, int z)
        {
            if (x < 0 || x >= N || y < 0 || y >= N || z < 0 || z >= N)
                return Color.clear;

            Vector3 col = colorField[x, y, z];
            float alpha = density[x, y, z];
            
            return new Color(col.x, col.y, col.z, alpha);
        }

        private void OnDestroy()
        {
            // 清理资源
            if (colorTexture != null)
                DestroyImmediate(colorTexture);
            if (colorBuffer != null)
                colorBuffer.Release();
        }
    }
}
