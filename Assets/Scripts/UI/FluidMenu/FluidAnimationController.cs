using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体动画控制器
    /// 负责计算色块间的空间分配和流体式动画效果
    /// </summary>
    public class FluidAnimationController : MonoBehaviour
    {
        [Header("流体参数")]
        public float fluidDensity = 1.0f;
        public float fluidViscosity = 0.5f;
        public float pressureThreshold = 0.1f;
        
        [Header("动画曲线")]
        public AnimationCurve fluidEaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve squeezeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve expandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("弹簧参数")]
        public float springConstant = 10f;
        public float damping = 0.8f;
        public float mass = 1f;
        
        [Header("性能设置")]
        public int maxIterations = 10;
        public float convergenceThreshold = 0.01f;
        
        // 流体模拟数据
        private List<FluidParticle> particles = new List<FluidParticle>();
        private Vector2[] pressureField;
        private float[,] densityField;
        private Vector2[,] velocityField;
        
        // 动画状态
        private bool isAnimating = false;
        private float animationTime = 0f;
        private float animationDuration = 1f;
        
        [System.Serializable]
        public class FluidParticle
        {
            public Vector2 position;
            public Vector2 velocity;
            public Vector2 acceleration;
            public float density;
            public float pressure;
            public float mass;
            public int blockIndex;
            
            public FluidParticle(Vector2 pos, int index)
            {
                position = pos;
                velocity = Vector2.zero;
                acceleration = Vector2.zero;
                density = 1f;
                pressure = 0f;
                mass = 1f;
                blockIndex = index;
            }
        }
        
        void Start()
        {
            InitializeFluidSimulation();
        }
        
        void Update()
        {
            if (isAnimating)
            {
                UpdateFluidSimulation();
                UpdateAnimation();
            }
        }
        
        void InitializeFluidSimulation()
        {
            // 初始化流体粒子（对应5个色块）
            particles.Clear();
            
            for (int i = 0; i < 5; i++)
            {
                Vector2 position = GetInitialPosition(i);
                FluidParticle particle = new FluidParticle(position, i);
                particles.Add(particle);
            }
            
            // 初始化压力场和密度场
            InitializeFields();
        }
        
        void InitializeFields()
        {
            int gridSize = 32;
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
        
        Vector2 GetInitialPosition(int index)
        {
            float spacing = 2f;
            float halfSpacing = spacing * 0.5f;
            
            switch (index)
            {
                case 0: return new Vector2(-halfSpacing, halfSpacing); // 左上
                case 1: return new Vector2(0, 0); // 中心
                case 2: return new Vector2(halfSpacing, halfSpacing); // 右上
                case 3: return new Vector2(halfSpacing, -halfSpacing); // 右下
                case 4: return new Vector2(-halfSpacing, -halfSpacing); // 左下
                default: return Vector2.zero;
            }
        }
        
        public void StartSqueezeAnimation(int hoveredIndex, float duration = 1f)
        {
            if (isAnimating) return;
            
            animationTime = 0f;
            animationDuration = duration;
            isAnimating = true;
            
            // 设置挤压目标
            SetSqueezeTargets(hoveredIndex);
        }
        
        public void StartExpandAnimation(int selectedIndex, float duration = 1.5f)
        {
            if (isAnimating) return;
            
            animationTime = 0f;
            animationDuration = duration;
            isAnimating = true;
            
            // 设置扩展目标
            SetExpandTargets(selectedIndex);
        }
        
        void SetSqueezeTargets(int hoveredIndex)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                FluidParticle particle = particles[i];
                
                if (i == hoveredIndex)
                {
                    // 悬停的粒子保持原位，但增加压力
                    particle.pressure = 1f;
                }
                else
                {
                    // 其他粒子被挤压，远离悬停粒子
                    Vector2 direction = (particle.position - particles[hoveredIndex].position).normalized;
                    particle.pressure = 0.5f;
                    
                    // 计算挤压后的目标位置
                    float squeezeDistance = 1.5f;
                    Vector2 targetPosition = particles[hoveredIndex].position + direction * squeezeDistance;
                    particle.velocity = (targetPosition - particle.position) * 0.5f;
                }
            }
        }
        
        void SetExpandTargets(int selectedIndex)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                FluidParticle particle = particles[i];
                
                if (i == selectedIndex)
                {
                    // 选中的粒子扩展
                    particle.pressure = 2f;
                    particle.velocity = Vector2.zero; // 保持在中心
                }
                else
                {
                    // 其他粒子被挤出屏幕
                    Vector2 direction = (particle.position - particles[selectedIndex].position).normalized;
                    Vector2 targetPosition = particle.position + direction * 10f; // 挤出屏幕
                    particle.velocity = (targetPosition - particle.position) * 2f;
                    particle.pressure = 0f;
                }
            }
        }
        
        void UpdateFluidSimulation()
        {
            // 计算密度场
            CalculateDensityField();
            
            // 计算压力场
            CalculatePressureField();
            
            // 更新粒子
            UpdateParticles();
        }
        
        void CalculateDensityField()
        {
            int gridSize = 32;
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
            foreach (FluidParticle particle in particles)
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
            int gridSize = 32;
            float cellSize = 4f / gridSize;
            
            // 计算压力梯度
            for (int x = 1; x < gridSize - 1; x++)
            {
                for (int y = 1; y < gridSize - 1; y++)
                {
                    float density = densityField[x, y];
                    float pressure = CalculatePressure(density);
                    
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
        
        void UpdateParticles()
        {
            float dt = Time.deltaTime;
            
            foreach (FluidParticle particle in particles)
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
            }
        }
        
        Vector2 CalculatePressureForce(FluidParticle particle)
        {
            int gridSize = 32;
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
        
        Vector2 CalculateViscosityForce(FluidParticle particle)
        {
            Vector2 viscosityForce = Vector2.zero;
            
            foreach (FluidParticle other in particles)
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
        
        void UpdateAnimation()
        {
            animationTime += Time.deltaTime;
            float progress = Mathf.Clamp01(animationTime / animationDuration);
            
            // 检查动画是否完成
            if (progress >= 1f)
            {
                isAnimating = false;
                OnAnimationComplete();
            }
        }
        
        void OnAnimationComplete()
        {
            // 动画完成后的处理
            Debug.Log("流体动画完成");
        }
        
        public Vector2 GetParticlePosition(int index)
        {
            if (index >= 0 && index < particles.Count)
            {
                return particles[index].position;
            }
            return Vector2.zero;
        }
        
        public float GetParticlePressure(int index)
        {
            if (index >= 0 && index < particles.Count)
            {
                return particles[index].pressure;
            }
            return 0f;
        }
        
        public bool IsAnimating()
        {
            return isAnimating;
        }
        
        public float GetAnimationProgress()
        {
            return Mathf.Clamp01(animationTime / animationDuration);
        }
        
        public void ResetAnimation()
        {
            isAnimating = false;
            animationTime = 0f;
            
            // 重置所有粒子到初始位置
            for (int i = 0; i < particles.Count; i++)
            {
                particles[i].position = GetInitialPosition(i);
                particles[i].velocity = Vector2.zero;
                particles[i].acceleration = Vector2.zero;
                particles[i].pressure = 0f;
            }
        }
    }
}