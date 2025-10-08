// BossC3_Core.cs
// 核心控制器 - 负责BOSS的基本生命周期、移动、安全点检测和传送逻辑
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using FadedDreams.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace FD.Bosses.C3
{
    /// <summary>
    /// BossC3核心控制器 - 负责基本生命周期、移动、安全点检测和传送逻辑
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC3_Core : MonoBehaviour
    {
        [Header("== Core Refs ==")]
        public Transform orbAnchor;
        public Transform player;
        public Renderer colorRenderer;

        [Header("== Movement & Safety ==")]
        public bool use2DPhysics = true;
        public float moveSpeed = 5.0f;
        public float preferRange = 6.0f;
        public float stopDistance = 3.0f;
        public float farTeleportDistance = 24f;
        public float teleportNearPlayerRadius = 3.5f;
        public LayerMask groundMask = -1;
        public float safeProbeRadius = 0.35f;

        [Header("== Player & Combat ==")]
        public LayerMask playerMask = 1 << 8;
        public string playerTag = "Player";
        public float defaultDamage = 6f;
        public bool playerColorImmunity = true; // 异色无效

        [Header("== Smart Obstacle/Teleport ==")]
        [Tooltip("使用全新智能移动与避障（建议开启）")]
        public bool useSmartMovement = true;

        [Tooltip("前向试探长度")]
        public float smartProbeAhead = 1.2f;
        [Tooltip("侧向试探长度")]
        public float smartSideProbe = 1.0f;
        [Tooltip("左右各试探档位数（越大越稳，但更耗）")]
        [Range(1, 4)] public int smartSideSamples = 3;

        [Tooltip("远距保持压迫：希望距离下限/上限")]
        public float smartDesiredMin = 2.8f;
        public float smartDesiredMax = 4.2f;
        public float smartApproachSpeed = 6.0f;
        public float smartStrafeSpeed = 2.8f;
        public float smartMaxAccel = 18f;

        [Tooltip("视线检测是否要求无遮挡（多层地形建议开）")]
        public bool requireLineOfSightForAggro = true;

        [Tooltip("传送：只在满足条件时触发（远+无视线+卡住+冷却好）")]
        public bool smartTeleportEnabled = true;
        [Tooltip("认为卡住的最小速度（m/s）")]
        public float stuckSpeedThreshold = 0.2f;
        [Tooltip("连续多久判定为卡住（秒）")]
        public float stuckTimeToTeleport = 1.25f;
        [Tooltip("触发一次传送后的冷却（秒）")]
        public float teleportCooldown = 4.0f;
        [Tooltip("仅在距玩家超过此距离才考虑传送")]
        public float teleportMinDistance = 18f;

        [Header("== Debug ==")]
        public bool autoRun = true;
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // === Aggro / Battle State ===
        [Header("== Aggro / Battle ==")]
        [SerializeField] private bool battleStarted = false;         // 一旦为 true 就不回退
        [SerializeField] private float aggroRadius = 0f;             // 0=使用 detectRadius
        [SerializeField] private bool lockBattleOnceStarted = true;  // 开战后不退出
        private Coroutine _mainLoopCR;

        // 组件引用
        private Rigidbody _rb3;
        private Rigidbody2D _rb2;

        // 移动状态
        private Vector3 _lastPosition;
        private float _stuckTimer = 0f;
        private float _lastTeleportTime = 0f;

        // 事件
        public event Action OnBattleStarted;
        public event Action<Vector3> OnTeleport;

        #region Unity Lifecycle

        private void Awake()
        {
            // 获取物理组件
            if (use2DPhysics)
            {
                _rb2 = GetComponent<Rigidbody2D>();
                if (_rb2 == null)
                {
                    _rb2 = gameObject.AddComponent<Rigidbody2D>();
                    _rb2.gravityScale = 0f;
                    _rb2.linearDamping = 5f;
                }
            }
            else
            {
                _rb3 = GetComponent<Rigidbody>();
                if (_rb3 == null)
                {
                    _rb3 = gameObject.AddComponent<Rigidbody>();
                    _rb3.useGravity = false;
                    _rb3.linearDamping = 5f;
                }
            }

            // 初始化位置记录
            _lastPosition = transform.position;
        }

        private void Start()
        {
            if (autoRun)
            {
                StartBattle();
            }
        }

        private void Update()
        {
            if (battleStarted)
            {
                UpdateMovement();
                UpdateStuckDetection();
            }
        }

        private void LateUpdate()
        {
            _lastPosition = transform.position;
        }

        #endregion

        #region Battle Management

        /// <summary>
        /// 开始战斗
        /// </summary>
        public void StartBattle()
        {
            if (battleStarted) return;

            battleStarted = true;
            OnBattleStarted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC3_Core] Battle started!");

            // 启动主循环
            if (_mainLoopCR != null)
                StopCoroutine(_mainLoopCR);
            _mainLoopCR = StartCoroutine(MainLoop());
        }

        /// <summary>
        /// 停止战斗
        /// </summary>
        public void StopBattle()
        {
            if (!battleStarted) return;

            battleStarted = false;

            if (_mainLoopCR != null)
            {
                StopCoroutine(_mainLoopCR);
                _mainLoopCR = null;
            }

            if (verboseLogs)
                Debug.Log($"[BossC3_Core] Battle stopped!");
        }

        /// <summary>
        /// 检查玩家是否在激怒范围内
        /// </summary>
        private bool IsPlayerInAggro()
        {
            if (player == null) return false;

            float distance = Vector3.Distance(transform.position, player.position);
            float effectiveRadius = (aggroRadius > 0) ? aggroRadius : preferRange;

            if (distance > effectiveRadius) return false;

            // 如果需要视线检测
            if (requireLineOfSightForAggro)
            {
                Vector3 direction = (player.position - transform.position).normalized;
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);

                if (use2DPhysics)
                {
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distanceToPlayer, groundMask);
                    if (hit.collider != null) return false;
                }
                else
                {
                    if (Physics.Raycast(transform.position, direction, distanceToPlayer, groundMask))
                        return false;
                }
            }

            return true;
        }

        #endregion

        #region Movement System

        /// <summary>
        /// 更新移动逻辑
        /// </summary>
        private void UpdateMovement()
        {
            if (player == null) return;

            if (useSmartMovement)
            {
                UpdateSmartMovement();
            }
            else
            {
                UpdateLegacyMovement();
            }
        }

        /// <summary>
        /// 智能移动系统
        /// </summary>
        private void UpdateSmartMovement()
        {
            Vector3 toPlayer = player.position - transform.position;
            float distance = toPlayer.magnitude;

            // 检查是否需要传送
            if (smartTeleportEnabled && ShouldTeleport(distance))
            {
                TryTeleport();
                return;
            }

            // 计算目标位置
            Vector3 targetPosition = CalculateTargetPosition(toPlayer, distance);
            
            // 应用移动
            ApplyMovement(targetPosition);
        }

        /// <summary>
        /// 传统移动系统（保留兼容性）
        /// </summary>
        private void UpdateLegacyMovement()
        {
            Vector3 toPlayer = player.position - transform.position;
            float distance = toPlayer.magnitude;

            if (distance > preferRange)
            {
                // 接近玩家
                Vector3 direction = toPlayer.normalized;
                Vector3 targetPosition = transform.position + direction * moveSpeed * Time.deltaTime;
                
                // 检查安全点
                if (IsSafePosition(targetPosition))
                {
                    ApplyMovement(targetPosition);
                }
            }
            else if (distance < stopDistance)
            {
                // 远离玩家
                Vector3 direction = -toPlayer.normalized;
                Vector3 targetPosition = transform.position + direction * moveSpeed * Time.deltaTime;
                
                if (IsSafePosition(targetPosition))
                {
                    ApplyMovement(targetPosition);
                }
            }
        }

        /// <summary>
        /// 计算目标位置
        /// </summary>
        private Vector3 CalculateTargetPosition(Vector3 toPlayer, float distance)
        {
            Vector3 targetPosition = transform.position;

            if (distance > smartDesiredMax)
            {
                // 接近玩家
                Vector3 direction = toPlayer.normalized;
                targetPosition = transform.position + direction * smartApproachSpeed * Time.deltaTime;
            }
            else if (distance < smartDesiredMin)
            {
                // 远离玩家
                Vector3 direction = -toPlayer.normalized;
                targetPosition = transform.position + direction * smartStrafeSpeed * Time.deltaTime;
            }
            else
            {
                // 侧向移动
                Vector3 right = Vector3.Cross(toPlayer.normalized, Vector3.forward);
                targetPosition = transform.position + right * smartStrafeSpeed * Time.deltaTime;
            }

            return targetPosition;
        }

        /// <summary>
        /// 应用移动
        /// </summary>
        private void ApplyMovement(Vector3 targetPosition)
        {
            if (use2DPhysics && _rb2 != null)
            {
                Vector2 velocity = (targetPosition - transform.position) / Time.deltaTime;
                _rb2.linearVelocity = Vector2.ClampMagnitude(velocity, smartMaxAccel);
            }
            else if (!use2DPhysics && _rb3 != null)
            {
                Vector3 velocity = (targetPosition - transform.position) / Time.deltaTime;
                _rb3.linearVelocity = Vector3.ClampMagnitude(velocity, smartMaxAccel);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        #endregion

        #region Teleport System

        /// <summary>
        /// 检查是否应该传送
        /// </summary>
        private bool ShouldTeleport(float distanceToPlayer)
        {
            // 距离检查
            if (distanceToPlayer < teleportMinDistance) return false;

            // 冷却检查
            if (Time.time - _lastTeleportTime < teleportCooldown) return false;

            // 卡住检查
            if (_stuckTimer < stuckTimeToTeleport) return false;

            return true;
        }

        /// <summary>
        /// 尝试传送
        /// </summary>
        private void TryTeleport()
        {
            Vector3 teleportPosition = FindTeleportPosition();
            if (teleportPosition != Vector3.zero)
            {
                transform.position = teleportPosition;
                _lastTeleportTime = Time.time;
                _stuckTimer = 0f;
                OnTeleport?.Invoke(teleportPosition);

                if (verboseLogs)
                    Debug.Log($"[BossC3_Core] Teleported to {teleportPosition}");
            }
        }

        /// <summary>
        /// 寻找传送位置
        /// </summary>
        private Vector3 FindTeleportPosition()
        {
            // 尝试在玩家附近找安全点
            Vector3 nearPlayerPos = SampleSafeNearPlayer_Global(teleportNearPlayerRadius, 1.0f, false);
            if (nearPlayerPos != Vector3.zero) return nearPlayerPos;

            // 尝试远距离传送
            Vector3 farPos = SampleSafeNearPlayer_Global(farTeleportDistance, 2.0f, false);
            if (farPos != Vector3.zero) return farPos;

            return Vector3.zero;
        }

        /// <summary>
        /// 在玩家附近采样安全点
        /// </summary>
        private Vector3 SampleSafeNearPlayer_Global(float nearRadius, float minClear, bool requireLoS)
        {
            if (player == null) return Vector3.zero;

            Vector3 playerPos = player.position;
            int attempts = 8;
            float angleStep = 360f / attempts;

            for (int i = 0; i < attempts; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * nearRadius;
                Vector3 testPos = playerPos + offset;

                if (IsSafePosition(testPos, minClear))
                {
                    if (requireLoS)
                    {
                        Vector3 direction = (playerPos - testPos).normalized;
                        float distance = Vector3.Distance(testPos, playerPos);

                        if (use2DPhysics)
                        {
                            RaycastHit2D hit = Physics2D.Raycast(testPos, direction, distance, groundMask);
                            if (hit.collider != null) continue;
                        }
                        else
                        {
                            if (Physics.Raycast(testPos, direction, distance, groundMask)) continue;
                        }
                    }

                    return testPos;
                }
            }

            return Vector3.zero;
        }

        #endregion

        #region Safety Detection

        /// <summary>
        /// 检查位置是否安全
        /// </summary>
        private bool IsSafePosition(Vector3 position, float clearance = 0f)
        {
            float radius = safeProbeRadius + clearance;

            if (use2DPhysics)
            {
                Collider2D hit = Physics2D.OverlapCircle(position, radius, groundMask);
                return hit == null;
            }
            else
            {
                return !Physics.CheckSphere(position, radius, groundMask);
            }
        }

        /// <summary>
        /// 更新卡住检测
        /// </summary>
        private void UpdateStuckDetection()
        {
            float currentSpeed = (transform.position - _lastPosition).magnitude / Time.deltaTime;

            if (currentSpeed < stuckSpeedThreshold)
            {
                _stuckTimer += Time.deltaTime;
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        #endregion

        #region Main Loop

        /// <summary>
        /// 主循环协程
        /// </summary>
        private IEnumerator MainLoop()
        {
            while (battleStarted)
            {
                // 检查玩家激怒状态
                if (IsPlayerInAggro())
                {
                    if (!battleStarted)
                    {
                        StartBattle();
                    }
                }
                else if (!lockBattleOnceStarted)
                {
                    // 可以退出战斗（如果需要的话）
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取激怒倍率
        /// </summary>
        public float GetAggroRateMul()
        {
            if (!battleStarted) return 0f;
            if (player == null) return 0f;

            float distance = Vector3.Distance(transform.position, player.position);
            float effectiveRadius = (aggroRadius > 0) ? aggroRadius : preferRange;

            if (distance > effectiveRadius) return 0f;

            return Mathf.Clamp01(1f - (distance / effectiveRadius));
        }

        /// <summary>
        /// 强制传送到指定位置
        /// </summary>
        public void ForceTeleport(Vector3 position)
        {
            transform.position = position;
            _lastTeleportTime = Time.time;
            _stuckTimer = 0f;
            OnTeleport?.Invoke(position);
        }

        /// <summary>
        /// 获取战斗状态
        /// </summary>
        public bool IsBattleStarted() => battleStarted;
        
        /// <summary>
        /// 广播停止所有小技（用于大招期间）
        /// </summary>
        public void BroadcastStopMicros()
        {
            // 通知所有OrbUnit停止
            OrbUnit[] units = GetComponentsInChildren<OrbUnit>();
            foreach (var unit in units)
            {
                if (unit != null)
                    unit.StopNow();
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制激怒范围
            Gizmos.color = battleStarted ? Color.red : Color.yellow;
            float effectiveRadius = (aggroRadius > 0) ? aggroRadius : preferRange;
            Gizmos.DrawWireSphere(transform.position, effectiveRadius);

            // 绘制安全探测半径
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, safeProbeRadius);

            // 绘制智能移动范围
            if (useSmartMovement)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, smartDesiredMin);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, smartDesiredMax);
            }
        }

        #endregion
    }
}
