using UnityEngine;

namespace FadedDreams.UI
{
    /// <summary>
    /// Navier-Stokes方程求解器
    /// 负责求解流体动力学方程
    /// </summary>
    public class NavierStokesSolver : MonoBehaviour
    {
        [Header("物理参数")]
        public float gravity = 9.81f; // 重力加速度
        public float viscosity = 0.01f; // 粘度
        public float surfaceTension = 0.5f; // 表面张力
        public float density = 1.0f; // 流体密度
        public float pressure = 1.0f; // 压力
        public float timeStep = 0.016f; // 时间步长
        
        [Header("求解器设置")]
        public int pressureIterations = 20;
        public float maxSpeed = 10f;
        public float fluidThreshold = 0.1f;
        
        // 引用流体场管理器
        private FluidFieldManager fieldManager;
        
        void Start()
        {
            fieldManager = GetComponent<FluidFieldManager>();
        }
        
        public void SolveStep()
        {
            if (fieldManager == null) return;
            
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
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!fieldManager.IsSolid(x, y) && fieldManager.GetDensity(x, y) > fluidThreshold)
                    {
                        // 重力向下
                        Vector2 currentVelocity = fieldManager.GetVelocity(x, y);
                        currentVelocity.y -= gravity * timeStep;
                        fieldManager.SetVelocity(x, y, currentVelocity);
                    }
                }
            }
        }
        
        void CalculatePressure()
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            // 使用迭代方法求解压力场
            for (int iter = 0; iter < pressureIterations; iter++)
            {
                for (int x = 1; x < gridWidth - 1; x++)
                {
                    for (int y = 1; y < gridHeight - 1; y++)
                    {
                        if (!fieldManager.IsSolid(x, y))
                        {
                            // 计算散度
                            Vector2 velRight = fieldManager.GetVelocity(x + 1, y);
                            Vector2 velLeft = fieldManager.GetVelocity(x - 1, y);
                            Vector2 velUp = fieldManager.GetVelocity(x, y + 1);
                            Vector2 velDown = fieldManager.GetVelocity(x, y - 1);
                            
                            float divergence = 
                                (velRight.x - velLeft.x) * 0.5f +
                                (velUp.y - velDown.y) * 0.5f;
                            
                            // 更新压力
                            float pressureRight = fieldManager.GetPressure(x + 1, y);
                            float pressureLeft = fieldManager.GetPressure(x - 1, y);
                            float pressureUp = fieldManager.GetPressure(x, y + 1);
                            float pressureDown = fieldManager.GetPressure(x, y - 1);
                            
                            float newPressure = 
                                (pressureRight + pressureLeft + pressureUp + pressureDown - divergence) * 0.25f;
                            
                            fieldManager.SetPressure(x, y, newPressure);
                        }
                    }
                }
            }
        }
        
        void ApplyPressureGradient()
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!fieldManager.IsSolid(x, y))
                    {
                        // 计算压力梯度
                        float pressureGradX = (fieldManager.GetPressure(x + 1, y) - fieldManager.GetPressure(x - 1, y)) * 0.5f;
                        float pressureGradY = (fieldManager.GetPressure(x, y + 1) - fieldManager.GetPressure(x, y - 1)) * 0.5f;
                        
                        // 应用压力梯度到速度场
                        Vector2 currentVelocity = fieldManager.GetVelocity(x, y);
                        currentVelocity.x -= pressureGradX * timeStep;
                        currentVelocity.y -= pressureGradY * timeStep;
                        fieldManager.SetVelocity(x, y, currentVelocity);
                    }
                }
            }
        }
        
        void ApplySurfaceTension()
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!fieldManager.IsSolid(x, y) && fieldManager.GetDensity(x, y) > fluidThreshold)
                    {
                        // 计算密度梯度
                        float densityGradX = (fieldManager.GetDensity(x + 1, y) - fieldManager.GetDensity(x - 1, y)) * 0.5f;
                        float densityGradY = (fieldManager.GetDensity(x, y + 1) - fieldManager.GetDensity(x, y - 1)) * 0.5f;
                        
                        // 计算拉普拉斯算子
                        float laplacian = fieldManager.GetDensity(x + 1, y) + fieldManager.GetDensity(x - 1, y) +
                                        fieldManager.GetDensity(x, y + 1) + fieldManager.GetDensity(x, y - 1) - 4 * fieldManager.GetDensity(x, y);
                        
                        // 表面张力力
                        Vector2 tensionForce = new Vector2(densityGradX, densityGradY) * surfaceTension * laplacian;
                        Vector2 currentVelocity = fieldManager.GetVelocity(x, y);
                        currentVelocity += tensionForce * timeStep;
                        fieldManager.SetVelocity(x, y, currentVelocity);
                    }
                }
            }
        }
        
        void ApplyViscosity()
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!fieldManager.IsSolid(x, y))
                    {
                        // 计算速度的拉普拉斯算子
                        Vector2 laplacianV = fieldManager.GetVelocity(x + 1, y) + fieldManager.GetVelocity(x - 1, y) +
                                           fieldManager.GetVelocity(x, y + 1) + fieldManager.GetVelocity(x, y - 1) - 4 * fieldManager.GetVelocity(x, y);
                        
                        // 应用粘度
                        Vector2 currentVelocity = fieldManager.GetVelocity(x, y);
                        currentVelocity += laplacianV * viscosity * timeStep;
                        fieldManager.SetVelocity(x, y, currentVelocity);
                    }
                }
            }
        }
        
        void AdvectDensity()
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!fieldManager.IsSolid(x, y))
                    {
                        // 计算新位置
                        Vector2 velocity = fieldManager.GetVelocity(x, y);
                        float newX = x - velocity.x * timeStep;
                        float newY = y - velocity.y * timeStep;
                        
                        // 双线性插值
                        float newDensity = InterpolateDensity(newX, newY);
                        fieldManager.SetTempDensity(x, y, newDensity);
                    }
                }
            }
            
            fieldManager.SwapDensityFields();
        }
        
        void AdvectVelocity()
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            for (int x = 1; x < gridWidth - 1; x++)
            {
                for (int y = 1; y < gridHeight - 1; y++)
                {
                    if (!fieldManager.IsSolid(x, y))
                    {
                        // 计算新位置
                        Vector2 velocity = fieldManager.GetVelocity(x, y);
                        float newX = x - velocity.x * timeStep;
                        float newY = y - velocity.y * timeStep;
                        
                        // 双线性插值
                        Vector2 newVelocity = InterpolateVelocity(newX, newY);
                        fieldManager.SetTempVelocity(x, y, newVelocity);
                    }
                }
            }
            
            fieldManager.SwapVelocityFields();
        }
        
        float InterpolateDensity(float x, float y)
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            
            if (x0 < 0 || x1 >= gridWidth || y0 < 0 || y1 >= gridHeight)
                return 0f;
            
            float fx = x - x0;
            float fy = y - y0;
            
            float d00 = fieldManager.GetDensity(x0, y0);
            float d10 = fieldManager.GetDensity(x1, y0);
            float d01 = fieldManager.GetDensity(x0, y1);
            float d11 = fieldManager.GetDensity(x1, y1);
            
            return d00 * (1 - fx) * (1 - fy) +
                   d10 * fx * (1 - fy) +
                   d01 * (1 - fx) * fy +
                   d11 * fx * fy;
        }
        
        Vector2 InterpolateVelocity(float x, float y)
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            
            if (x0 < 0 || x1 >= gridWidth || y0 < 0 || y1 >= gridHeight)
                return Vector2.zero;
            
            float fx = x - x0;
            float fy = y - y0;
            
            Vector2 v00 = fieldManager.GetVelocity(x0, y0);
            Vector2 v10 = fieldManager.GetVelocity(x1, y0);
            Vector2 v01 = fieldManager.GetVelocity(x0, y1);
            Vector2 v11 = fieldManager.GetVelocity(x1, y1);
            
            return v00 * (1 - fx) * (1 - fy) +
                   v10 * fx * (1 - fy) +
                   v01 * (1 - fx) * fy +
                   v11 * fx * fy;
        }
        
        void ApplyBoundaryConditions()
        {
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            // 处理边界
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (fieldManager.IsSolid(x, y))
                    {
                        fieldManager.SetVelocity(x, y, Vector2.zero);
                        fieldManager.SetDensity(x, y, 0f);
                    }
                }
            }
            
            // 限制最大速度
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector2 velocity = fieldManager.GetVelocity(x, y);
                    if (velocity.magnitude > maxSpeed)
                    {
                        fieldManager.SetVelocity(x, y, velocity.normalized * maxSpeed);
                    }
                }
            }
        }
        
        public void SetTimeStep(float newTimeStep)
        {
            timeStep = newTimeStep;
        }
        
        public void SetGravity(float newGravity)
        {
            gravity = newGravity;
        }
        
        public void SetViscosity(float newViscosity)
        {
            viscosity = newViscosity;
        }
        
        public void SetSurfaceTension(float newSurfaceTension)
        {
            surfaceTension = newSurfaceTension;
        }
        
        public float GetTimeStep()
        {
            return timeStep;
        }
        
        public float GetGravity()
        {
            return gravity;
        }
        
        public float GetViscosity()
        {
            return viscosity;
        }
        
        public float GetSurfaceTension()
        {
            return surfaceTension;
        }
    }
}
