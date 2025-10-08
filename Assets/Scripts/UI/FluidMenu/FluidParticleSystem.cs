using UnityEngine;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体粒子系统
    /// 负责管理流体粒子的创建、更新和状态
    /// </summary>
    public class FluidParticleSystem : MonoBehaviour
    {
        [Header("粒子参数")]
        public float particleMass = 1f;
        public int particleCount = 5;
        
        // 粒子数据
        private List<FluidParticle> particles = new List<FluidParticle>();
        
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
            InitializeParticles();
        }
        
        void InitializeParticles()
        {
            particles.Clear();
            
            for (int i = 0; i < particleCount; i++)
            {
                Vector2 position = GetInitialPosition(i);
                FluidParticle particle = new FluidParticle(position, i);
                particle.mass = particleMass;
                particles.Add(particle);
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
        
        public void UpdateParticle(int index, Vector2 newPosition, Vector2 newVelocity, Vector2 newAcceleration)
        {
            if (index >= 0 && index < particles.Count)
            {
                FluidParticle particle = particles[index];
                particle.position = newPosition;
                particle.velocity = newVelocity;
                particle.acceleration = newAcceleration;
            }
        }
        
        public void SetParticlePressure(int index, float pressure)
        {
            if (index >= 0 && index < particles.Count)
            {
                particles[index].pressure = pressure;
            }
        }
        
        public void SetParticleVelocity(int index, Vector2 velocity)
        {
            if (index >= 0 && index < particles.Count)
            {
                particles[index].velocity = velocity;
            }
        }
        
        public Vector2 GetParticlePosition(int index)
        {
            if (index >= 0 && index < particles.Count)
            {
                return particles[index].position;
            }
            return Vector2.zero;
        }
        
        public Vector2 GetParticleVelocity(int index)
        {
            if (index >= 0 && index < particles.Count)
            {
                return particles[index].velocity;
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
        
        public FluidParticle GetParticle(int index)
        {
            if (index >= 0 && index < particles.Count)
            {
                return particles[index];
            }
            return null;
        }
        
        public List<FluidParticle> GetAllParticles()
        {
            return new List<FluidParticle>(particles);
        }
        
        public int GetParticleCount()
        {
            return particles.Count;
        }
        
        public void ResetParticles()
        {
            for (int i = 0; i < particles.Count; i++)
            {
                particles[i].position = GetInitialPosition(i);
                particles[i].velocity = Vector2.zero;
                particles[i].acceleration = Vector2.zero;
                particles[i].pressure = 0f;
            }
        }
        
        public void SetSqueezeTargets(int hoveredIndex)
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
        
        public void SetExpandTargets(int selectedIndex)
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
    }
}
