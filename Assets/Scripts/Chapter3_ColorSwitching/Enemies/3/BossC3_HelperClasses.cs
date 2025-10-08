// BossC3_HelperClasses.cs
// BossC3辅助类 - DamageAdapter、KnockPreset等
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using UnityEngine;
using FadedDreams.Enemies;

namespace FD.Bosses.C3
{
    /// <summary>
    /// 伤害适配器 - 将无色伤害转换为Boss可处理的带色伤害
    /// </summary>
    public class DamageAdapter : MonoBehaviour, IDamageable
    {
        private BossC3_CombatSystem combatSystem;
        private BossC3_PhaseManager phaseManager;
        
        public bool IsDead => combatSystem != null && combatSystem.IsDead();
        
        private void Awake()
        {
            combatSystem = GetComponent<BossC3_CombatSystem>();
            phaseManager = GetComponent<BossC3_PhaseManager>();
        }
        
        /// <summary>
        /// 实现IDamageable接口 - 接收无色伤害
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (combatSystem == null) return;
            
            // 将无色伤害转换为Boss当前颜色的伤害
            BossColor sourceColor = phaseManager != null ? phaseManager.GetCurrentColor() : BossColor.None;
            
            // 调用Boss的伤害系统
            combatSystem.TakeDamage(damage, sourceColor);
        }
        
        /// <summary>
        /// 接收带颜色的伤害
        /// </summary>
        public void TakeDamage(float damage, BossColor sourcePlayerColor)
        {
            if (combatSystem == null) return;
            
            combatSystem.TakeDamage(damage, sourcePlayerColor);
        }
    }
    
    /// <summary>
    /// 击退预设结构
    /// </summary>
    [System.Serializable]
    public struct KnockPreset
    {
        public float baseSpeed;      // 基础击退速度
        public float duration;        // 持续时间
        public float verticalBoost;   // 垂直提升（用于3D）
        
        public KnockPreset(float speed, float dur, float vBoost = 0f)
        {
            baseSpeed = speed;
            duration = dur;
            verticalBoost = vBoost;
        }
    }
    
    /// <summary>
    /// 击退辅助方法扩展类
    /// </summary>
    public static class KnockbackHelper
    {
        /// <summary>
        /// 应用击退效果到目标
        /// </summary>
        public static void ApplyKnockbackTo(GameObject target, Vector3 direction, KnockPreset preset, bool use2D = true)
        {
            if (target == null) return;
            
            Vector3 knockDirection = direction.normalized;
            
            if (use2D)
            {
                // 2D击退
                Rigidbody2D rb2d = target.GetComponent<Rigidbody2D>();
                if (rb2d != null)
                {
                    Vector2 force = (Vector2)knockDirection * preset.baseSpeed;
                    rb2d.AddForce(force, ForceMode2D.Impulse);
                }
            }
            else
            {
                // 3D击退
                Rigidbody rb3d = target.GetComponent<Rigidbody>();
                if (rb3d != null)
                {
                    Vector3 force = knockDirection * preset.baseSpeed;
                    
                    // 添加垂直提升
                    if (preset.verticalBoost > 0f)
                    {
                        force.y += preset.verticalBoost;
                    }
                    
                    rb3d.AddForce(force, ForceMode.Impulse);
                }
            }
        }
        
        /// <summary>
        /// 应用击退效果（直接使用位置插值）
        /// </summary>
        public static void ApplyKnockbackToPosition(Transform target, Vector3 direction, KnockPreset preset, MonoBehaviour coroutineRunner)
        {
            if (target == null || coroutineRunner == null) return;
            
            coroutineRunner.StartCoroutine(KnockbackCoroutine(target, direction.normalized, preset));
        }
        
        private static System.Collections.IEnumerator KnockbackCoroutine(Transform target, Vector3 direction, KnockPreset preset)
        {
            Vector3 startPos = target.position;
            Vector3 endPos = startPos + direction * preset.baseSpeed;
            
            float elapsed = 0f;
            while (elapsed < preset.duration)
            {
                float t = elapsed / preset.duration;
                
                // 使用缓动曲线
                float easedT = 1f - (1f - t) * (1f - t); // EaseOut
                
                target.position = Vector3.Lerp(startPos, endPos, easedT);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            target.position = endPos;
        }
    }
    
    /// <summary>
    /// Boss移动指令基类（占位，供MovementDirector使用）
    /// </summary>
    public interface IMovementOrder
    {
        void Execute(float deltaTime);
        bool IsComplete();
    }
    
    /// <summary>
    /// 寻找目标指令
    /// </summary>
    public class OrderSeek : IMovementOrder
    {
        private Transform self;
        private Transform target;
        private float speed;
        
        public OrderSeek(Transform self, Transform target, float speed)
        {
            this.self = self;
            this.target = target;
            this.speed = speed;
        }
        
        public void Execute(float deltaTime)
        {
            if (self == null || target == null) return;
            
            Vector3 direction = (target.position - self.position).normalized;
            self.position += direction * speed * deltaTime;
        }
        
        public bool IsComplete()
        {
            if (self == null || target == null) return true;
            return Vector3.Distance(self.position, target.position) < 0.5f;
        }
    }
    
    /// <summary>
    /// 停止指令
    /// </summary>
    public class OrderHalt : IMovementOrder
    {
        public void Execute(float deltaTime) { }
        public bool IsComplete() => true;
    }
    
    /// <summary>
    /// 保持指令
    /// </summary>
    public class OrderHold : IMovementOrder
    {
        private float duration;
        private float elapsed;
        
        public OrderHold(float duration)
        {
            this.duration = duration;
            this.elapsed = 0f;
        }
        
        public void Execute(float deltaTime)
        {
            elapsed += deltaTime;
        }
        
        public bool IsComplete()
        {
            return elapsed >= duration;
        }
    }
}

