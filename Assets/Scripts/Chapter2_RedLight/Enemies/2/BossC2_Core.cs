// BossC2_Core.cs
// 核心控制器 - 负责BOSS的基本生命周期、玩家检测、索敌和移动控制
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using FadedDreams.Enemies;
using System;
using System.Collections;
using UnityEngine;


namespace FadedDreams.Bosses
{
    /// <summary>
    /// BossC2核心控制器 - 负责基本生命周期、玩家检测、索敌和移动控制
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BossC2_Core : MonoBehaviour, IDamageable
    {
        [Header("== Core References ==")]
        public Transform player;
        public Rigidbody2D rb;
        public SpriteRenderer model;
        public UnityEngine.Rendering.Universal.Light2D auraLight;

        [Header("== Player Detection ==")]
        public LayerMask playerLayer = 1 << 8;
        public string playerTag = "Player";
        public float detectRadius = 15f;
        public float aggroRadius = 12f;
        public bool requireLineOfSight = true;
        public LayerMask groundMask = -1;

        [Header("== Movement ==")]
        public float moveSpeed = 8f;
        public float acceleration = 20f;
        public float deceleration = 15f;
        public float stopDistance = 2f;
        public float preferredDistance = 4f;

        [Header("== Combat ==")]
        public float defaultDamage = 10f;
        public float maxHP = 1000f;
        public float currentHP = 1000f;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 状态变量
        private bool _isPlayerDetected = false;
        private bool _isInAggro = false;
        private bool _isDead = false;
        private Vector2 _currentVelocity;
        private Vector2 _targetVelocity;
        private Coroutine _mainLoopCR;

        // 事件
        public event Action OnPlayerDetected;
        public event Action OnPlayerLost;
        public event Action OnAggroStarted;
        public event Action OnAggroEnded;
        public event Action<float> OnDamageTaken;
        public event Action OnDeath;

        #region Unity Lifecycle

        private void Awake()
        {
            // 获取组件引用
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (model == null) model = GetComponent<SpriteRenderer>();
            if (auraLight == null) auraLight = GetComponent<UnityEngine.Rendering.Universal.Light2D>();

            // 设置物理属性
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearDamping = 5f;
                rb.angularDamping = 5f;
            }
        }

        private void Start()
        {
            // 查找玩家
            ResolvePlayer();
            
            // 启动主循环
            _mainLoopCR = StartCoroutine(MainLoop());
        }

        private void Update()
        {
            UpdateMovement();
        }

        private void OnDestroy()
        {
            if (_mainLoopCR != null)
            {
                StopCoroutine(_mainLoopCR);
            }
        }

        #endregion

        #region Player Detection

        /// <summary>
        /// 解析玩家引用
        /// </summary>
        private bool ResolvePlayer()
        {
            if (player != null) return true;

            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                player = playerObj.transform;
                if (verboseLogs)
                    Debug.Log($"[BossC2_Core] Player resolved: {player.name}");
                return true;
            }

            if (verboseLogs)
                Debug.LogWarning("[BossC2_Core] Player not found!");
            return false;
        }

        /// <summary>
        /// 检查玩家是否在检测范围内
        /// </summary>
        private bool IsPlayerInDetectStrict()
        {
            if (player == null) return false;

            float distance = Vector2.Distance(transform.position, player.position);
            if (distance > detectRadius) return false;

            // 检查视线
            if (requireLineOfSight)
            {
                Vector2 direction = (player.position - transform.position).normalized;
                float distanceToPlayer = Vector2.Distance(transform.position, player.position);

                RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distanceToPlayer, groundMask);
                if (hit.collider != null) return false;
            }

            return true;
        }

        /// <summary>
        /// 检查玩家是否在激怒范围内
        /// </summary>
        private bool IsPlayerInAggro()
        {
            if (player == null) return false;

            float distance = Vector2.Distance(transform.position, player.position);
            return distance <= aggroRadius;
        }

        /// <summary>
        /// 检查是否是玩家
        /// </summary>
        private bool IsLikelyPlayer(Transform t, out string reasonFail)
        {
            reasonFail = "";

            if (t == null)
            {
                reasonFail = "null transform";
                return false;
            }

            if (!t.CompareTag(playerTag))
            {
                reasonFail = $"wrong tag: {t.tag}";
                return false;
            }

            if (!LayerMatch(playerLayer, t.gameObject.layer))
            {
                reasonFail = $"wrong layer: {t.gameObject.layer}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查层匹配
        /// </summary>
        private bool LayerMatch(LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;

        #endregion

        #region Movement System

        /// <summary>
        /// 更新移动逻辑
        /// </summary>
        private void UpdateMovement()
        {
            if (_isDead || !_isInAggro || player == null) return;

            Vector2 toPlayer = (player.position - transform.position);
            float distance = toPlayer.magnitude;

            // 计算目标速度
            if (distance > preferredDistance + stopDistance)
            {
                // 接近玩家
                _targetVelocity = toPlayer.normalized * moveSpeed;
            }
            else if (distance < preferredDistance - stopDistance)
            {
                // 远离玩家
                _targetVelocity = -toPlayer.normalized * moveSpeed;
            }
            else
            {
                // 保持距离
                _targetVelocity = Vector2.zero;
            }

            // 应用加速度
            Vector2 velocityDiff = _targetVelocity - _currentVelocity;
            float accel = _targetVelocity.magnitude > 0.1f ? acceleration : deceleration;
            _currentVelocity += velocityDiff.normalized * accel * Time.deltaTime;

            // 限制最大速度
            if (_currentVelocity.magnitude > moveSpeed)
            {
                _currentVelocity = _currentVelocity.normalized * moveSpeed;
            }

            // 应用速度
            if (rb != null)
            {
                rb.linearVelocity = _currentVelocity;
            }
            else
            {
                transform.position += (Vector3)_currentVelocity * Time.deltaTime;
            }
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public void StopMovement()
        {
            _targetVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 设置移动速度
        /// </summary>
        public void SetMoveSpeed(float speed)
        {
            moveSpeed = speed;
        }

        #endregion

        #region Main Loop

        /// <summary>
        /// 主循环协程
        /// </summary>
        private IEnumerator MainLoop()
        {
            while (!_isDead)
            {
                // 检测玩家
                bool wasDetected = _isPlayerDetected;
                _isPlayerDetected = IsPlayerInDetectStrict();

                if (_isPlayerDetected && !wasDetected)
                {
                    OnPlayerDetected?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC2_Core] Player detected!");
                }
                else if (!_isPlayerDetected && wasDetected)
                {
                    OnPlayerLost?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC2_Core] Player lost!");
                }

                // 检查激怒状态
                bool wasInAggro = _isInAggro;
                _isInAggro = _isPlayerDetected && IsPlayerInAggro();

                if (_isInAggro && !wasInAggro)
                {
                    OnAggroStarted?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC2_Core] Aggro started!");
                }
                else if (!_isInAggro && wasInAggro)
                {
                    OnAggroEnded?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC2_Core] Aggro ended!");
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        #endregion

        #region Combat System

        /// <summary>
        /// 受到伤害
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isDead) return;

            currentHP = Mathf.Max(0f, currentHP - damage);
            OnDamageTaken?.Invoke(damage);

            if (verboseLogs)
                Debug.Log($"[BossC2_Core] Took {damage} damage. HP: {currentHP}/{maxHP}");

            if (currentHP <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// 死亡处理
        /// </summary>
        private void Die()
        {
            if (_isDead) return;

            _isDead = true;
            currentHP = 0f;
            StopMovement();

            OnDeath?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC2_Core] Boss died!");

            // 停止主循环
            if (_mainLoopCR != null)
            {
                StopCoroutine(_mainLoopCR);
                _mainLoopCR = null;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取玩家是否被检测到
        /// </summary>
        public bool IsPlayerDetected() => _isPlayerDetected;

        /// <summary>
        /// 获取是否在激怒状态
        /// </summary>
        public bool IsInAggro() => _isInAggro;

        /// <summary>
        /// 获取是否死亡
        /// </summary>
        public bool IsDead => _isDead;

        /// <summary>
        /// 获取当前血量
        /// </summary>
        public float GetCurrentHealth() => currentHP;

        /// <summary>
        /// 获取最大血量
        /// </summary>
        public float GetMaxHealth() => maxHP;

        /// <summary>
        /// 获取血量百分比
        /// </summary>
        public float GetHealthPercentage() => maxHP > 0f ? currentHP / maxHP : 0f;

        /// <summary>
        /// 获取玩家引用
        /// </summary>
        public Transform GetPlayer() => player;

        /// <summary>
        /// 获取当前速度
        /// </summary>
        public Vector2 GetCurrentVelocity() => _currentVelocity;

        /// <summary>
        /// 获取目标速度
        /// </summary>
        public Vector2 GetTargetVelocity() => _targetVelocity;

        /// <summary>
        /// 设置血量
        /// </summary>
        public void SetHealth(float health)
        {
            currentHP = Mathf.Clamp(health, 0f, maxHP);
        }

        /// <summary>
        /// 设置最大血量
        /// </summary>
        public void SetMaxHealth(float maxHealth)
        {
            maxHP = Mathf.Max(1f, maxHealth);
            if (currentHP > maxHP) currentHP = maxHP;
        }

        /// <summary>
        /// 重置核心系统
        /// </summary>
        public void ResetCore()
        {
            _isPlayerDetected = false;
            _isInAggro = false;
            _isDead = false;
            _currentVelocity = Vector2.zero;
            _targetVelocity = Vector2.zero;
            currentHP = maxHP;

            // 重新启动主循环
            if (_mainLoopCR != null)
            {
                StopCoroutine(_mainLoopCR);
            }
            _mainLoopCR = StartCoroutine(MainLoop());

            if (verboseLogs)
                Debug.Log("[BossC2_Core] Core system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Detected: {_isPlayerDetected}, Aggro: {_isInAggro}, Dead: {_isDead}, HP: {currentHP:F1}/{maxHP:F1}, Vel: {_currentVelocity.magnitude:F1}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制检测范围
            Gizmos.color = _isPlayerDetected ? Color.green : Color.yellow;
            Gizmos.DrawWireCircle(transform.position, detectRadius);

            // 绘制激怒范围
            Gizmos.color = _isInAggro ? Color.red : Color.orange;
            Gizmos.DrawWireCircle(transform.position, aggroRadius);

            // 绘制到玩家的连线
            if (player != null)
            {
                Gizmos.color = _isPlayerDetected ? Color.green : Color.gray;
                Gizmos.DrawLine(transform.position, player.position);
            }

            // 绘制速度向量
            if (_currentVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, _currentVelocity);
            }
        }

        #endregion
    }
}
