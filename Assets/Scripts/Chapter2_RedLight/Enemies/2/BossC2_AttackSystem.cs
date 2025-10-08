// BossC2_AttackSystem.cs
// 激光技能系统 - 负责追踪激光、旋镰扫射和激光效果管理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using UnityEngine;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// BossC2激光技能系统 - 负责追踪激光、旋镰扫射和激光效果管理
    /// </summary>
    public class BossC2_AttackSystem : MonoBehaviour
    {
        [Header("== Laser Settings ==")]
        [Tooltip("追踪激光预制体")]
        public GameObject homingLaserPrefab;
        [Tooltip("旋镰扫射预制体")]
        public GameObject scytheSweepPrefab;
        [Tooltip("激光速度")]
        public float laserSpeed = 10f;
        [Tooltip("激光持续时间")]
        public float laserDuration = 3f;
        [Tooltip("激光淡出时间")]
        public float laserFadeTime = 1f;

        [Header("== Attack Cooldowns ==")]
        [Tooltip("追踪激光冷却时间")]
        public float homingLaserCooldown = 2f;
        [Tooltip("旋镰扫射冷却时间")]
        public float scytheSweepCooldown = 4f;

        // 内部状态
        private float _lastHomingLaserTime = 0f;
        private float _lastScytheSweepTime = 0f;

        /// <summary>
        /// 执行追踪激光攻击
        /// </summary>
        public void ExecuteHomingLaser(Vector3 targetPosition)
        {
            if (Time.time - _lastHomingLaserTime < homingLaserCooldown)
                return;

            _lastHomingLaserTime = Time.time;
            
            if (homingLaserPrefab != null)
            {
                GameObject laser = Instantiate(homingLaserPrefab, transform.position, Quaternion.identity);
                // 设置激光朝向目标
                Vector3 direction = (targetPosition - transform.position).normalized;
                laser.transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
                
                // 销毁激光
                Destroy(laser, laserDuration);
            }
        }

        /// <summary>
        /// 执行旋镰扫射攻击
        /// </summary>
        public void ExecuteScytheSweep()
        {
            if (Time.time - _lastScytheSweepTime < scytheSweepCooldown)
                return;

            _lastScytheSweepTime = Time.time;
            
            if (scytheSweepPrefab != null)
            {
                GameObject scythe = Instantiate(scytheSweepPrefab, transform.position, transform.rotation);
                
                // 销毁旋镰
                Destroy(scythe, laserDuration);
            }
        }

        /// <summary>
        /// 检查是否可以执行追踪激光
        /// </summary>
        public bool CanExecuteHomingLaser()
        {
            return Time.time - _lastHomingLaserTime >= homingLaserCooldown;
        }

        /// <summary>
        /// 检查是否可以执行旋镰扫射
        /// </summary>
        public bool CanExecuteScytheSweep()
        {
            return Time.time - _lastScytheSweepTime >= scytheSweepCooldown;
        }
    }
}
