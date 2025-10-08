using UnityEngine;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体物理引擎
    /// 负责计算流体物理场和粒子间的相互作用
    /// </summary>
    public class FluidPhysicsEngine : MonoBehaviour
    {
        [Header("流体参数")]
        public float fluidDensity = 1.0f;
        public float fluidViscosity = 0.5f;
        public float pressureThreshold = 0.1f;
        
        [Header("性能设置")]
        public int maxIterations = 10;
        public float convergenceThreshold = 0.01f;
        public int gridSize = 32;
        
        // 物理场数据
        private Vector2[] pressureField;
        private float[,] densityField;
        private Vector2[,] velocityField;
        
        // 引用粒子系统
        private new FluidParticleSystem particleSystem;
        
        void Start()
        {
            particleSystem = GetComponent<FluidParticleSystem>();
            InitializeFields();
        }
        
        void InitializeFields()
        {
            pressureField = new Vector2[gridSize * gridSize];
            densityField = new float[gridSize, gridSize];
            velocityField = new Vector2[gridSize, gridSize];
            
            // 初始化所有场为0
            for (int i = 0; i < gridSize * gridSize; i++)
            {
                pressureField[i] = Vector2.zero;
            }
            
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    densityField[x, y] = 0f;
                    velocityField[x, y] = Vector2.zero;
                }
            }
        }
        
        public void UpdatePhysics(float dt, float damping)
        {
            if (particleSystem == null) return;
            
            // 计算密度场
            CalculateDensityField();
            
            // 计算压力场
            CalculatePressureField();
            
            // 更新粒子物理
            UpdateParticlePhysics(dt, damping);
        }
        
        void CalculateDensityField()
        {
            float cellSize = 4f / gridSize;
            
            // 重置密度场
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    densityField[x, y] = 0f;
                }
            }
            
            // 计算每个粒子对密度场的贡献
            List<FluidParticleSystem.FluidParticle> particles = particleSystem.GetAllParticles();
            foreach (FluidParticleSystem.FluidParticle particle in particles)
            {
                Vector2 gridPos = (particle.position + Vector2.one * 2f) / cellSize;
                int gridX = Mathf.FloorToInt(gridPos.x);
                int gridY = Mathf.FloorToInt(gridPos.y);
                
                // 使用平滑核函数
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int x = gridX + dx;
                        int y = gridY + dy;
                        
                        if (x >= 0 && x < gridSize && y >= 0 && y < gridSize)
                        {
                            Vector2 cellCenter = new Vector2(x + 0.5f, y + 0.5f) * cellSize - Vector2.one * 2f;
                            float distance = Vector2.Distance(particle.position, cellCenter);
                            float weight = SmoothingKernel(distance, 0.5f);
                            
                            densityField[x, y] += particle.mass * weight;
                        }
                    }
                }
            }
        }
        
        void CalculatePressureField()
        {
            float cellSize = 4f / gridSize;
            
            // 计算压力梯度
            for (int x = 1; x < gridSize - 1; x++)
            {
                for (int y = 1; y < gridSize - 1; y++)
                {
                    float density = densityField[x, y];
                    
                    // 计算压力梯度
                    float pressureX = (CalculatePressure(densityField[x + 1, y]) - 
                                     CalculatePressure(densityField[x - 1, y])) / (2f * cellSize);
                    float pressureY = (CalculatePressure(densityField[x, y + 1]) - 
                                     CalculatePressure(densityField[x, y - 1])) / (2f * cellSize);
                    
                    int index = y * gridSize + x;
                    pressureField[index] = new Vector2(-pressureX, -pressureY);
                }
            }
        }
        
        float CalculatePressure(float density)
        {
            // 理想气体状态方程：P = k * ρ
            float k = 1f;
            return k * density;
        }
        
        void UpdateParticlePhysics(float dt, float damping)
        {
            List<FluidParticleSystem.FluidParticle> particles = particleSystem.GetAllParticles();
            
            foreach (FluidParticleSystem.FluidParticle particle in particles)
            {
                // 计算压力力
                Vector2 pressureForce = CalculatePressureForce(particle);
                
                // 计算粘性力
                Vector2 viscosityForce = CalculateViscosityForce(particle);
                
                // 计算总力
                Vector2 totalForce = pressureForce + viscosityForce;
                
                // 更新加速度
                particle.acceleration = totalForce / particle.mass;
                
                // 更新速度
                particle.velocity += particle.acceleration * dt;
                
                // 应用阻尼
                particle.velocity *= damping;
                
                // 更新位置
                particle.position += particle.velocity * dt;
                
                // 更新粒子系统中的粒子
                particleSystem.UpdateParticle(particle.blockIndex, particle.position, particle.velocity, particle.acceleration);
            }
        }
        
        Vector2 CalculatePressureForce(FluidParticleSystem.FluidParticle particle)
        {
            float cellSize = 4f / gridSize;
            
            Vector2 gridPos = (particle.position + Vector2.one * 2f) / cellSize;
            int gridX = Mathf.FloorToInt(gridPos.x);
            int gridY = Mathf.FloorToInt(gridPos.y);
            
            if (gridX < 1 || gridX >= gridSize - 1 || gridY < 1 || gridY >= gridSize - 1)
            {
                return Vector2.zero;
            }
            
            int index = gridY * gridSize + gridX;
            return pressureField[index] * particle.mass;
        }
        
        Vector2 CalculateViscosityForce(FluidParticleSystem.FluidParticle particle)
        {
            Vector2 viscosityForce = Vector2.zero;
            List<FluidParticleSystem.FluidParticle> particles = particleSystem.GetAllParticles();
            
            foreach (FluidParticleSystem.FluidParticle other in particles)
            {
                if (other == particle) continue;
                
                Vector2 distance = particle.position - other.position;
                float distanceMagnitude = distance.magnitude;
                
                if (distanceMagnitude < 0.5f)
                {
                    Vector2 velocityDiff = other.velocity - particle.velocity;
                    float weight = SmoothingKernel(distanceMagnitude, 0.5f);
                    viscosityForce += velocityDiff * weight * fluidViscosity;
                }
            }
            
            return viscosityForce;
        }
        
        float SmoothingKernel(float distance, float radius)
        {
            if (distance >= radius) return 0f;
            
            float q = distance / radius;
            return (1f - q * q) * (1f - q * q);
        }
        
        public float GetDensityAtPosition(Vector2 position)
        {
            float cellSize = 4f / gridSize;
            Vector2 gridPos = (position + Vector2.one * 2f) / cellSize;
            int gridX = Mathf.FloorToInt(gridPos.x);
            int gridY = Mathf.FloorToInt(gridPos.y);
            
            if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
            {
                return densityField[gridX, gridY];
            }
            
            return 0f;
        }
        
        public Vector2 GetPressureAtPosition(Vector2 position)
        {
            float cellSize = 4f / gridSize;
            Vector2 gridPos = (position + Vector2.one * 2f) / cellSize;
            int gridX = Mathf.FloorToInt(gridPos.x);
            int gridY = Mathf.FloorToInt(gridPos.y);
            
            if (gridX >= 0 && gridX < gridSize && gridY >= 0 && gridY < gridSize)
            {
                int index = gridY * gridSize + gridX;
                return pressureField[index];
            }
            
            return Vector2.zero;
        }
        
        public void ResetFields()
        {
            InitializeFields();
        }
    }
}
