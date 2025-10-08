using UnityEngine;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 真实物理流体模拟器
    /// 基于Navier-Stokes方程实现真实的流体物理
    /// </summary>
    public class RealFluidPhysics : MonoBehaviour
    {
        [Header("物理参数")]
        public float gravity = 9.81f; // 重力加速度
        public float viscosity = 0.01f; // 粘度
        public float surfaceTension = 0.5f; // 表面张力
        public float density = 1.0f; // 流体密度
        public float pressure = 1.0f; // 压力
        public float timeStep = 0.016f; // 时间步长
        
        [Header("网格设置")]
        public int gridWidth = 128;
        public int gridHeight = 128;
        public float cellSize = 1.0f;
        
        [Header("流体状态")]
        public float minDensity = 0.01f;
        public float maxDensity = 1.0f;
        public float fluidThreshold = 0.1f;
        
        // 流体场数据
        private float[,] densityField;
        private Vector2[,] velocityField;
        private float[,] pressureField;
        private bool[,] solidField; // 固体边界
        
        // 临时数组用于计算
        private float[,] tempDensity;
        private Vector2[,] tempVelocity;
        private float[,] tempPressure;
        
        // 渲染数据
        private Texture2D fluidTexture;
        private Color[] fluidPixels;
        
        // 时间累积
        private float timeAccumulator = 0f;
        
        void Start()
        {
            InitializeFluidField();
            CreateFluidTexture();
        }
        
        void Update()
        {
            timeAccumulator += Time.deltaTime;
            
            // 固定时间步长模拟
            while (timeAccumulator >= timeStep)
            {
                SimulateFluidStep();
                timeAccumulator -= timeStep;
            }
            
            UpdateFluidTexture();
        }
        
        void InitializeFluidField()
        {
            // 初始化数组
            densityField = new float[gridWidth, gridHeight];
            velocityField = new Vector2[gridWidth, gridHeight];
            pressureField = new float[gridWidth, gridHeight];
            solidField = new bool[gridWidth, gridHeight];
            
            tempDensity = new float[gridWidth, gridHeight];
            tempVelocity = new Vector2[gridWidth, gridHeight];
            tempPressure = new float[gridWidth, gridHeight];
            
            // 初始化流体区域
            int centerX = gridWidth / 2;
            int centerY = gridHeight / 2;
            int radius = 25; // 更大的初始半径
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    // 设置边界为固体
                    if (x == 0 || x == gridWidth - 1 || y == 0 || y == gridHeight - 1)
                    {
                        solidField[x, y] = true;
                        densityField[x, y] = 0f;
                    }
                    else
                    {
                        solidField[x, y] = false;
                        
                        // 初始化中心区域的流体
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                        if (distance <= radius)
                        {
                            float normalizedDistance = distance / radius;
                            densityField[x, y] = maxDensity * (1.0f - normalizedDistance * normalizedDistance);
                        }
                        else
                        {
                            densityField[x, y] = 0f;
                        }
                    }
                    
                    velocityField[x, y] = Vector2.zero;
                    pressureField[x, y] = 0f;
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
        
        void SimulateFluidStep()
        {
            // 1. 应用重力
            ApplyGravity();
            
            // 2. 计算压力
            CalculatePressure();
            
            // 3. 应用压力梯度
            ApplyPressureGradient();
            
            // 4. 应用表面张力
            ApplySurfaceTension();
            
            // 5. 应用粘度
            ApplyViscosity();
            
            // 6. 对流传输
            AdvectDensity();
            AdvectVelocity();
            
            // 7. 边界处理
            ApplyBoundaryConditions();
        }
        
        void ApplyGravity()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!solidField[x, y] && densityField[x, y] > fluidThreshold)
                    {
                        // 重力向下
                        velocityField[x, y].y -= gravity * timeStep;
                    }
                }
            }
        }
        
        void CalculatePressure()
        {
            // 使用迭代方法求解压力场
            int iterations = 20;
            
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int x = 1; x < gridWidth - 1; x++)
                {
                    for (int y = 1; y < gridHeight - 1; y++)
                    {
                        if (!solidField[x, y])
                        {
                            // 计算散度
                            float divergence = 
                                (velocityField[x + 1, y].x - velocityField[x - 1, y].x) * 0.5f +
                                (velocityField[x, y + 1].y - velocityField[x, y - 1].y) * 0.5f;
                            
                            // 更新压力
                            float newPressure = 
                                (pressureField[x + 1, y] + pressureField[x - 1, y] +
                                 pressureField[x, y + 1] + pressureField[x, y - 1] - divergence) * 0.25f;
                            
                            pressureField[x, y] = newPressure;
                        }
                    }
                }
            }
        }
        
        void ApplyPressureGradient()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!solidField[x, y])
                    {
                        // 计算压力梯度
                        float pressureGradX = (pressureField[x + 1, y] - pressureField[x - 1, y]) * 0.5f;
                        float pressureGradY = (pressureField[x, y + 1] - pressureField[x, y - 1]) * 0.5f;
                        
                        // 应用压力梯度到速度场
                        velocityField[x, y].x -= pressureGradX * timeStep;
                        velocityField[x, y].y -= pressureGradY * timeStep;
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
                    if (!solidField[x, y] && densityField[x, y] > fluidThreshold)
                    {
                        // 计算密度梯度
                        float densityGradX = (densityField[x + 1, y] - densityField[x - 1, y]) * 0.5f;
                        float densityGradY = (densityField[x, y + 1] - densityField[x, y - 1]) * 0.5f;
                        
                        // 计算拉普拉斯算子
                        float laplacian = densityField[x + 1, y] + densityField[x - 1, y] +
                                        densityField[x, y + 1] + densityField[x, y - 1] - 4 * densityField[x, y];
                        
                        // 表面张力力
                        Vector2 tensionForce = new Vector2(densityGradX, densityGradY) * surfaceTension * laplacian;
                        velocityField[x, y] += tensionForce * timeStep;
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
                    if (!solidField[x, y])
                    {
                        // 计算速度的拉普拉斯算子
                        Vector2 laplacianV = velocityField[x + 1, y] + velocityField[x - 1, y] +
                                           velocityField[x, y + 1] + velocityField[x, y - 1] - 4 * velocityField[x, y];
                        
                        // 应用粘度
                        velocityField[x, y] += laplacianV * viscosity * timeStep;
                    }
                }
            }
        }
        
        void AdvectDensity()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!solidField[x, y])
                    {
                        // 计算新位置
                        Vector2 velocity = velocityField[x, y];
                        float newX = x - velocity.x * timeStep;
                        float newY = y - velocity.y * timeStep;
                        
                        // 双线性插值
                        tempDensity[x, y] = InterpolateDensity(newX, newY);
                    }
                }
            }
            
            // 交换数组
            var temp = densityField;
            densityField = tempDensity;
            tempDensity = temp;
        }
        
        void AdvectVelocity()
        {
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!solidField[x, y])
                    {
                        // 计算新位置
                        Vector2 velocity = velocityField[x, y];
                        float newX = x - velocity.x * timeStep;
                        float newY = y - velocity.y * timeStep;
                        
                        // 双线性插值
                        tempVelocity[x, y] = InterpolateVelocity(newX, newY);
                    }
                }
            }
            
            // 交换数组
            var temp = velocityField;
            velocityField = tempVelocity;
            tempVelocity = temp;
        }
        
        float InterpolateDensity(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            
            if (x0 < 0 || x1 >= gridWidth || y0 < 0 || y1 >= gridHeight)
                return 0f;
            
            float fx = x - x0;
            float fy = y - y0;
            
            float d00 = densityField[x0, y0];
            float d10 = densityField[x1, y0];
            float d01 = densityField[x0, y1];
            float d11 = densityField[x1, y1];
            
            return d00 * (1 - fx) * (1 - fy) +
                   d10 * fx * (1 - fy) +
                   d01 * (1 - fx) * fy +
                   d11 * fx * fy;
        }
        
        Vector2 InterpolateVelocity(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            
            if (x0 < 0 || x1 >= gridWidth || y0 < 0 || y1 >= gridHeight)
                return Vector2.zero;
            
            float fx = x - x0;
            float fy = y - y0;
            
            Vector2 v00 = velocityField[x0, y0];
            Vector2 v10 = velocityField[x1, y0];
            Vector2 v01 = velocityField[x0, y1];
            Vector2 v11 = velocityField[x1, y1];
            
            return v00 * (1 - fx) * (1 - fy) +
                   v10 * fx * (1 - fy) +
                   v01 * (1 - fx) * fy +
                   v11 * fx * fy;
        }
        
        void ApplyBoundaryConditions()
        {
            // 处理边界
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (solidField[x, y])
                    {
                        velocityField[x, y] = Vector2.zero;
                        densityField[x, y] = 0f;
                    }
                }
            }
            
            // 限制最大速度
            float maxSpeed = 10f;
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (velocityField[x, y].magnitude > maxSpeed)
                    {
                        velocityField[x, y] = velocityField[x, y].normalized * maxSpeed;
                    }
                }
            }
        }
        
        void UpdateFluidTexture()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    float density = densityField[x, y];
                    float alpha = Mathf.Clamp01(density);
                    
                    // 创建白色纹理，让Image的颜色显示
                    Color fluidColor = new Color(1f, 1f, 1f, alpha);
                    fluidPixels[y * gridWidth + x] = fluidColor;
                }
            }
            
            fluidTexture.SetPixels(fluidPixels);
            fluidTexture.Apply();
        }
        
        public void AddFluid(Vector2 position, float amount)
        {
            int x = Mathf.RoundToInt(position.x * gridWidth);
            int y = Mathf.RoundToInt(position.y * gridHeight);
            
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && !solidField[x, y])
            {
                densityField[x, y] = Mathf.Min(densityField[x, y] + amount, maxDensity);
            }
        }
        
        public void AddForce(Vector2 position, Vector2 force)
        {
            int x = Mathf.RoundToInt(position.x * gridWidth);
            int y = Mathf.RoundToInt(position.y * gridHeight);
            
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && !solidField[x, y])
            {
                velocityField[x, y] += force * timeStep;
            }
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
                return densityField[x, y];
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
