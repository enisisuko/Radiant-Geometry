// BossC1_Core.cs
// 核心控制器 - 负责BOSS的基本生命周期、索敌、移动控制和受击处理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using FadedDreams.Enemies;
using System;
using System.Collections;
using UnityEngine;


namespace FadedDreams.Boss
{
    /// <summary>
    /// BossC1核心控制器 - 负责基本生命周期、索敌、移动控制和受击处理
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BossC1_Core : MonoBehaviour, IDamageable
    {
        [Header("== Core References ==")]
        public Transform player;
        public SpriteRenderer spriteRenderer;
        public UnityEngine.Rendering.Universal.Light2D selfLight;

        [Header("== Detection & Movement ==")]
        public float detectRadius = 20f;
        public float moveSpeed = 3.2f;
        public float keepDistance = 6f;
        public float strafeSpeed = 1.6f;

        [Header("== Health & Phases ==")]
        public int phase1Hits = 10;
        public int phase2Hits = 10;
        public int phase3Hits = 10;
        public float invulnerableAfterHit = 2f;

        [Header("== Visual Effects ==")]
        public float spawnLeadVfxSeconds = 1.0f;
        public float spawnFadeSeconds = 0.6f;
        public float vanishFadeSeconds = 0.5f;
        public float appearFadeSeconds = 0.5f;

        [Header("== Colors ==")]
        public Color phase1Color = Color.white;
        public Color phase2Color = new Color(1f, .55f, .1f, 1f);
        public Color phase3Color = Color.red;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 组件引用
        private Rigidbody2D _rb;
        private Collider2D _collider;

        // 状态变量
        private bool _isPlayerDetected = false;
        private bool _isInAggro = false;
        private bool _isDead = false;
        private bool _isInvulnerable = false;
        private int _currentPhase = 1;
        private int _phase1HitCount = 0;
        private int _phase2HitCount = 0;
        private int _phase3HitCount = 0;
        private Coroutine _mainLoopCR;
        private Coroutine _invulnerabilityCR;

        // 移动状态
        private Vector2 _currentVelocity;
        private Vector2 _targetVelocity;

        // 事件
        public event Action OnPlayerDetected;
        public event Action OnPlayerLost;
        public event Action OnAggroStarted;
        public event Action OnAggroEnded;
        public event Action<float> OnDamageTaken;
        public event Action<int> OnPhaseChanged;
        public event Action OnDeath;
        public event Action OnInvulnerabilityStarted;
        public event Action OnInvulnerabilityEnded;

        #region Unity Lifecycle

        private void Awake()
        {
            // 获取组件引用
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();

            // 设置物理属性
            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.linearDamping = 5f;
                _rb.angularDamping = 5f;
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

            if (_invulnerabilityCR != null)
            {
                StopCoroutine(_invulnerabilityCR);
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

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                if (verboseLogs)
                    Debug.Log($"[BossC1_Core] Player resolved: {player.name}");
                return true;
            }

            if (verboseLogs)
                Debug.LogWarning("[BossC1_Core] Player not found!");
            return false;
        }

        /// <summary>
        /// 检查玩家是否在检测范围内
        /// </summary>
        private bool IsPlayerInDetectRange()
        {
            if (player == null) return false;

            float distance = Vector2.Distance(transform.position, player.position);
            return distance <= detectRadius;
        }

        /// <summary>
        /// 检查玩家是否在激怒范围内
        /// </summary>
        private bool IsPlayerInAggroRange()
        {
            if (player == null) return false;

            float distance = Vector2.Distance(transform.position, player.position);
            return distance <= detectRadius * 0.8f; // 激怒范围稍小
        }

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
            if (distance > keepDistance + 1f)
            {
                // 接近玩家
                _targetVelocity = toPlayer.normalized * moveSpeed;
            }
            else if (distance < keepDistance - 1f)
            {
                // 远离玩家
                _targetVelocity = -toPlayer.normalized * moveSpeed;
            }
            else
            {
                // 侧向移动
                Vector2 right = Vector2.Perpendicular(toPlayer.normalized);
                _targetVelocity = right * strafeSpeed;
            }

            // 应用速度
            if (_rb != null)
            {
                _rb.linearVelocity = _targetVelocity;
            }
            else
            {
                transform.position += (Vector3)_targetVelocity * Time.deltaTime;
            }

            _currentVelocity = _targetVelocity;
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public void StopMovement()
        {
            _targetVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
            }
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
                _isPlayerDetected = IsPlayerInDetectRange();

                if (_isPlayerDetected && !wasDetected)
                {
                    OnPlayerDetected?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC1_Core] Player detected!");
                }
                else if (!_isPlayerDetected && wasDetected)
                {
                    OnPlayerLost?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC1_Core] Player lost!");
                }

                // 检查激怒状态
                bool wasInAggro = _isInAggro;
                _isInAggro = _isPlayerDetected && IsPlayerInAggroRange();

                if (_isInAggro && !wasInAggro)
                {
                    OnAggroStarted?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC1_Core] Aggro started!");
                }
                else if (!_isInAggro && wasInAggro)
                {
                    OnAggroEnded?.Invoke();
                    if (verboseLogs)
                        Debug.Log("[BossC1_Core] Aggro ended!");
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        #endregion

        #region Combat System

        /// <summary>
        /// 受到伤害
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_isDead || _isInvulnerable) return;

            // 记录击中
            RecordHit();

            OnDamageTaken?.Invoke(amount);

            if (verboseLogs)
                Debug.Log($"[BossC1_Core] Took {amount} damage. Phase: {_currentPhase}");

            // 启动无敌帧
            StartInvulnerability();
        }

        /// <summary>
        /// 记录击中
        /// </summary>
        private void RecordHit()
        {
            switch (_currentPhase)
            {
                case 1:
                    _phase1HitCount++;
                    if (_phase1HitCount >= phase1Hits)
                    {
                        AdvanceToPhase(2);
                    }
                    break;
                case 2:
                    _phase2HitCount++;
                    if (_phase2HitCount >= phase2Hits)
                    {
                        AdvanceToPhase(3);
                    }
                    break;
                case 3:
                    _phase3HitCount++;
                    if (_phase3HitCount >= phase3Hits)
                    {
                        Die();
                    }
                    break;
            }
        }

        /// <summary>
        /// 推进到下一阶段
        /// </summary>
        private void AdvanceToPhase(int newPhase)
        {
            if (newPhase <= _currentPhase) return;

            _currentPhase = newPhase;
            OnPhaseChanged?.Invoke(_currentPhase);

            if (verboseLogs)
                Debug.Log($"[BossC1_Core] Advanced to phase {_currentPhase}");
        }

        /// <summary>
        /// 死亡处理
        /// </summary>
        private void Die()
        {
            if (_isDead) return;

            _isDead = true;
            StopMovement();

            OnDeath?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC1_Core] Boss died!");

            // 停止主循环
            if (_mainLoopCR != null)
            {
                StopCoroutine(_mainLoopCR);
                _mainLoopCR = null;
            }
        }

        #endregion

        #region Invulnerability System

        /// <summary>
        /// 启动无敌帧
        /// </summary>
        private void StartInvulnerability()
        {
            if (_invulnerabilityCR != null)
            {
                StopCoroutine(_invulnerabilityCR);
            }

            _isInvulnerable = true;
            OnInvulnerabilityStarted?.Invoke();

            _invulnerabilityCR = StartCoroutine(InvulnerabilityCoroutine());
        }

        /// <summary>
        /// 无敌帧协程
        /// </summary>
        private IEnumerator InvulnerabilityCoroutine()
        {
            yield return new WaitForSeconds(invulnerableAfterHit);

            _isInvulnerable = false;
            OnInvulnerabilityEnded?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC1_Core] Invulnerability ended");
        }

        #endregion

        #region Visual Effects

        /// <summary>
        /// 设置阶段颜色
        /// </summary>
        public void SetPhaseColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// 获取当前阶段颜色
        /// </summary>
        public Color GetCurrentPhaseColor()
        {
            switch (_currentPhase)
            {
                case 1: return phase1Color;
                case 2: return phase2Color;
                case 3: return phase3Color;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 淡入淡出效果
        /// </summary>
        public IEnumerator FadeVisible(bool show, float seconds)
        {
            if (spriteRenderer == null) yield break;

            Color startColor = spriteRenderer.color;
            Color endColor = startColor;
            endColor.a = show ? 1f : 0f;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                float t = elapsed / seconds;
                Color currentColor = Color.Lerp(startColor, endColor, t);
                spriteRenderer.color = currentColor;

                // 同时调整光源
                if (selfLight != null)
                {
                    selfLight.intensity = show ? 1f : 0f;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            spriteRenderer.color = endColor;
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
        /// 获取当前生命值
        /// </summary>
        public float GetCurrentHealth()
        {
            switch (_currentPhase)
            {
                case 1:
                    return phase1Hits - _phase1HitCount;
                case 2:
                    return phase2Hits - _phase2HitCount;
                case 3:
                    return phase3Hits - _phase3HitCount;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 获取是否无敌
        /// </summary>
        public bool IsInvulnerable() => _isInvulnerable;

        /// <summary>
        /// 获取当前阶段
        /// </summary>
        public int GetCurrentPhase() => _currentPhase;

        /// <summary>
        /// 获取阶段1击中次数
        /// </summary>
        public int GetPhase1Hits() => _phase1HitCount;

        /// <summary>
        /// 获取阶段2击中次数
        /// </summary>
        public int GetPhase2Hits() => _phase2HitCount;

        /// <summary>
        /// 获取阶段3击中次数
        /// </summary>
        public int GetPhase3Hits() => _phase3HitCount;

        /// <summary>
        /// 获取玩家引用
        /// </summary>
        public Transform GetPlayer() => player;

        /// <summary>
        /// 获取当前速度
        /// </summary>
        public Vector2 GetCurrentVelocity() => _currentVelocity;

        /// <summary>
        /// 获取阶段进度
        /// </summary>
        public float GetPhaseProgress()
        {
            switch (_currentPhase)
            {
                case 1: return (float)_phase1HitCount / phase1Hits;
                case 2: return (float)_phase2HitCount / phase2Hits;
                case 3: return (float)_phase3HitCount / phase3Hits;
                default: return 0f;
            }
        }

        /// <summary>
        /// 重置核心系统
        /// </summary>
        public void ResetCore()
        {
            _isPlayerDetected = false;
            _isInAggro = false;
            _isDead = false;
            _isInvulnerable = false;
            _currentPhase = 1;
            _phase1HitCount = 0;
            _phase2HitCount = 0;
            _phase3HitCount = 0;
            _currentVelocity = Vector2.zero;
            _targetVelocity = Vector2.zero;

            // 重新启动主循环
            if (_mainLoopCR != null)
            {
                StopCoroutine(_mainLoopCR);
            }
            _mainLoopCR = StartCoroutine(MainLoop());

            if (verboseLogs)
                Debug.Log("[BossC1_Core] Core system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Phase: {_currentPhase}, Detected: {_isPlayerDetected}, Aggro: {_isInAggro}, Dead: {_isDead}, Invulnerable: {_isInvulnerable}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制检测范围
            Gizmos.color = _isPlayerDetected ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRadius);

            // 绘制激怒范围
            Gizmos.color = _isInAggro ? Color.red : Color.orange;
            Gizmos.DrawWireSphere(transform.position, detectRadius * 0.8f);

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
