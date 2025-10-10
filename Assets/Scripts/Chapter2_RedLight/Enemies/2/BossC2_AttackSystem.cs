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
        private void Awake()
        {
            // 获取或添加音频组件
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // 2D音效
                _audioSource.volume = soundVolume;
            }
        }
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

        [Header("== Audio ==")]
        [Tooltip("激光发射音效")]
        public AudioClip laserShootSound;
        [Tooltip("扫射音效")]
        public AudioClip sweepSound;
        [Tooltip("子弹发射音效")]
        public AudioClip bulletShootSound;
        [Tooltip("手榴弹投掷音效")]
        public AudioClip grenadeThrowSound;
        [Tooltip("音效音量")]
        [Range(0f, 1f)] public float soundVolume = 0.8f;

        // 音频组件
        private AudioSource _audioSource;

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
            
            // 播放激光音效
            if (laserShootSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(laserShootSound, soundVolume);
            }
            
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
            
            // 播放扫射音效
            if (sweepSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(sweepSound, soundVolume);
            }
            
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

        /// <summary>
        /// 生成子弹
        /// </summary>
        public void SpawnBullet(Vector3 position, Vector3 direction, float speed, float damage)
        {
            // 播放子弹发射音效
            if (bulletShootSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(bulletShootSound, soundVolume * 0.7f); // 子弹音效稍微小一点
            }
            
            if (homingLaserPrefab != null)
            {
                GameObject bullet = Instantiate(homingLaserPrefab, position, Quaternion.LookRotation(Vector3.forward, direction));
                // 设置子弹属性
                var bulletScript = bullet.GetComponent<FadedDreams.Enemies.IDamageable>();
                if (bulletScript != null)
                {
                    // 如果子弹有伤害组件，设置伤害值
                    // 这里需要根据实际的伤害系统来设置
                }
                
                var rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = direction.normalized * speed;
                }
            }
        }

        /// <summary>
        /// 生成手榴弹
        /// </summary>
        public void SpawnGrenade(Vector3 position, Vector3 direction, float speed)
        {
            // 播放手榴弹投掷音效
            if (grenadeThrowSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(grenadeThrowSound, soundVolume);
            }
            
            if (scytheSweepPrefab != null)
            {
                GameObject grenade = Instantiate(scytheSweepPrefab, position, Quaternion.LookRotation(Vector3.forward, direction));
                
                var rb = grenade.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = direction.normalized * speed;
                }
            }
        }
    }
}
