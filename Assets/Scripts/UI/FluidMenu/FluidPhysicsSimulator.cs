using UnityEngine;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体物理模拟器
    /// 模拟液体的表面张力、流动和蔓延效果
    /// </summary>
    public class FluidPhysicsSimulator : MonoBehaviour
    {
        [Header("流体参数")]
        public float viscosity = 0.05f; // 粘度（降低）
        public float surfaceTension = 0.2f; // 表面张力（降低）
        public float flowSpeed = 3.0f; // 流动速度（增加）
        public float spreadRate = 2.0f; // 蔓延速度（增加）
        public float minDensity = 0.05f; // 最小密度（降低）
        public float maxDensity = 1.0f; // 最大密度
        
        [Header("网格参数")]
        public int gridWidth = 64;
        public int gridHeight = 64;
        public float cellSize = 1.0f;
        
        [Header("外部影响")]
        public Vector2 mouseInfluence = Vector2.zero;
        public float mouseInfluenceRadius = 10.0f;
        public float mouseInfluenceStrength = 1.0f;
        
        // 流体网格数据
        private float[,] densityGrid;
        private Vector2[,] velocityGrid;
        private Vector2[,] pressureGrid;
        
        // 渲染数据
        private Texture2D fluidTexture;
        private Color[] fluidPixels;
        
        // 时间累积
        private float timeAccumulator = 0f;
        private float simulationTimeStep = 0.016f; // 60 FPS
        
        void Start()
        {
            InitializeFluidGrid();
            CreateFluidTexture();
        }
        
        void Update()
        {
            timeAccumulator += Time.deltaTime;
            
            // 固定时间步长模拟
            while (timeAccumulator >= simulationTimeStep)
            {
                SimulateFluid();
                timeAccumulator -= simulationTimeStep;
            }
            
            UpdateFluidTexture();
        }
        
        void InitializeFluidGrid()
        {
            densityGrid = new float[gridWidth, gridHeight];
            velocityGrid = new Vector2[gridWidth, gridHeight];
            pressureGrid = new Vector2[gridWidth, gridHeight];
            
            // 初始化更大的中心区域为高密度
            int centerX = gridWidth / 2;
            int centerY = gridHeight / 2;
            int radius = 20; // 增大初始半径
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance <= radius)
                    {
                        // 使用更平滑的密度分布
                        float normalizedDistance = distance / radius;
                        densityGrid[x, y] = maxDensity * (1.0f - normalizedDistance * normalizedDistance);
                    }
                    else
                    {
                        densityGrid[x, y] = 0f;
                    }
                    
                    velocityGrid[x, y] = Vector2.zero;
                    pressureGrid[x, y] = Vector2.zero;
                }
            }
        }
        
        void CreateFluidTexture()
        {
            fluidTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false);
            fluidTexture.filterMode = FilterMode.Bilinear;
            fluidTexture.wrapMode = TextureWrapMode.Clamp;
            fluidPixels = new Color[gridWidth * gridHeight];
        }
        
        void SimulateFluid()
        {
            // 1. 计算压力梯度
            CalculatePressureGradient();
            
            // 2. 应用表面张力
            ApplySurfaceTension();
            
            // 3. 计算粘性力
            ApplyViscosity();
            
            // 4. 应用外部力（鼠标影响）
            ApplyExternalForces();
            
            // 5. 更新速度场
            UpdateVelocityField();
            
            // 6. 更新密度场
            UpdateDensityField();
            
            // 7. 边界处理
            ApplyBoundaryConditions();
        }
        
        void CalculatePressureGradient()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    float density = densityGrid[x, y];
                    if (density > minDensity)
                    {
                        // 计算密度梯度
                        float gradX = (densityGrid[x + 1, y] - densityGrid[x - 1, y]) * 0.5f;
                        float gradY = (densityGrid[x, y + 1] - densityGrid[x, y - 1]) * 0.5f;
                        
                        // 压力梯度与密度梯度相反
                        pressureGrid[x, y] = new Vector2(-gradX, -gradY) * surfaceTension;
                    }
                }
            }
        }
        
        void ApplySurfaceTension()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    float density = densityGrid[x, y];
                    if (density > minDensity)
                    {
                        // 计算拉普拉斯算子（曲率）
                        float laplacian = densityGrid[x + 1, y] + densityGrid[x - 1, y] +
                                        densityGrid[x, y + 1] + densityGrid[x, y - 1] - 4 * density;
                        
                        // 表面张力力
                        Vector2 tensionForce = new Vector2(laplacian, laplacian) * surfaceTension * 0.1f;
                        velocityGrid[x, y] += tensionForce;
                    }
                }
            }
        }
        
        void ApplyViscosity()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    float density = densityGrid[x, y];
                    if (density > minDensity)
                    {
                        // 计算速度的拉普拉斯算子
                        Vector2 laplacianV = velocityGrid[x + 1, y] + velocityGrid[x - 1, y] +
                                           velocityGrid[x, y + 1] + velocityGrid[x, y - 1] - 4 * velocityGrid[x, y];
                        
                        // 粘性力
                        velocityGrid[x, y] += laplacianV * viscosity * simulationTimeStep;
                    }
                }
            }
        }
        
        void ApplyExternalForces()
        {
            if (mouseInfluence != Vector2.zero)
            {
                Vector2 mousePos = mouseInfluence;
                int mouseX = Mathf.RoundToInt(mousePos.x * gridWidth);
                int mouseY = Mathf.RoundToInt(mousePos.y * gridHeight);
                
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(mouseX, mouseY));
                        if (distance <= mouseInfluenceRadius)
                        {
                            float influence = 1.0f - (distance / mouseInfluenceRadius);
                            Vector2 force = (new Vector2(mouseX, mouseY) - new Vector2(x, y)).normalized * 
                                          influence * mouseInfluenceStrength * simulationTimeStep;
                            
                            velocityGrid[x, y] += force;
                        }
                    }
                }
            }
        }
        
        void UpdateVelocityField()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    float density = densityGrid[x, y];
                    if (density > minDensity)
                    {
                        // 添加压力梯度
                        velocityGrid[x, y] += pressureGrid[x, y] * simulationTimeStep;
                        
                        // 限制最大速度
                        float maxSpeed = flowSpeed;
                        if (velocityGrid[x, y].magnitude > maxSpeed)
                        {
                            velocityGrid[x, y] = velocityGrid[x, y].normalized * maxSpeed;
                        }
                    }
                }
            }
        }
        
        void UpdateDensityField()
        {
            float[,] newDensityGrid = new float[gridWidth, gridHeight];
            
            // 先复制当前密度
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    newDensityGrid[x, y] = densityGrid[x, y];
                }
            }
            
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    float density = densityGrid[x, y];
                    if (density > minDensity)
                    {
                        // 对流传输
                        Vector2 velocity = velocityGrid[x, y];
                        float newX = x - velocity.x * simulationTimeStep * 10f; // 增加传输速度
                        float newY = y - velocity.y * simulationTimeStep * 10f;
                        
                        // 双线性插值
                        int x0 = Mathf.FloorToInt(newX);
                        int y0 = Mathf.FloorToInt(newY);
                        int x1 = x0 + 1;
                        int y1 = y0 + 1;
                        
                        if (x0 >= 0 && x1 < gridWidth && y0 >= 0 && y1 < gridHeight)
                        {
                            float fx = newX - x0;
                            float fy = newY - y0;
                            
                            float density00 = densityGrid[x0, y0];
                            float density10 = densityGrid[x1, y0];
                            float density01 = densityGrid[x0, y1];
                            float density11 = densityGrid[x1, y1];
                            
                            float interpolatedDensity = 
                                density00 * (1 - fx) * (1 - fy) +
                                density10 * fx * (1 - fy) +
                                density01 * (1 - fx) * fy +
                                density11 * fx * fy;
                            
                            // 保持最小密度，防止完全消失
                            newDensityGrid[x, y] = Mathf.Max(interpolatedDensity, density * 0.95f);
                        }
                        else
                        {
                            // 边界处保持密度
                            newDensityGrid[x, y] = density;
                        }
                    }
                }
            }
            
            densityGrid = newDensityGrid;
        }
        
        void ApplyBoundaryConditions()
        {
            // 边界处保持密度，不衰减
            for (int x = 0; x < gridWidth; x++)
            {
                if (densityGrid[x, 0] > minDensity)
                    densityGrid[x, 0] = Mathf.Max(densityGrid[x, 0], minDensity);
                if (densityGrid[x, gridHeight - 1] > minDensity)
                    densityGrid[x, gridHeight - 1] = Mathf.Max(densityGrid[x, gridHeight - 1], minDensity);
            }
            
            for (int y = 0; y < gridHeight; y++)
            {
                if (densityGrid[0, y] > minDensity)
                    densityGrid[0, y] = Mathf.Max(densityGrid[0, y], minDensity);
                if (densityGrid[gridWidth - 1, y] > minDensity)
                    densityGrid[gridWidth - 1, y] = Mathf.Max(densityGrid[gridWidth - 1, y], minDensity);
            }
        }
        
        void UpdateFluidTexture()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    float density = densityGrid[x, y];
                    float alpha = Mathf.Clamp01(density);
                    
                    // 创建白色纹理，让Image的颜色显示出来
                    Color fluidColor = new Color(1f, 1f, 1f, alpha);
                    fluidPixels[y * gridWidth + x] = fluidColor;
                }
            }
            
            fluidTexture.SetPixels(fluidPixels);
            fluidTexture.Apply();
        }
        
        public void SetMouseInfluence(Vector2 mousePos)
        {
            mouseInfluence = mousePos;
        }
        
        public void ClearMouseInfluence()
        {
            mouseInfluence = Vector2.zero;
        }
        
        public Texture2D GetFluidTexture()
        {
            return fluidTexture;
        }
        
        public float GetDensityAt(Vector2 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x * gridWidth);
            int y = Mathf.RoundToInt(worldPos.y * gridHeight);
            
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
            {
                return densityGrid[x, y];
            }
            
            return 0f;
        }
        
        void OnDestroy()
        {
            if (fluidTexture != null)
            {
                DestroyImmediate(fluidTexture);
            }
        }
    }
}
