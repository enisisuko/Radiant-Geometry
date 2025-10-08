// BossC3_CombatSystem.cs
// 战斗系统 - 负责BOSS的血量管理、伤害计算、颜色免疫和死亡处理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using FadedDreams.Enemies;
using System;
using System.Collections;
using UnityEngine;

namespace FD.Bosses.C3
{
    /// <summary>
    /// BossC3战斗系统 - 负责血量管理、伤害计算、颜色免疫和死亡处理
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC3_CombatSystem : MonoBehaviour
    {
        [Header("== Boss Health ==")]
        public float maxHP = 2200f;           // 总血量
        public float currentHP = 2200f;       // 当前血量
        public float phase2Threshold = 1100f; // P2阶段血量阈值

        [Header("== Combat Settings ==")]
        public bool playerColorImmunity = true; // 异色无效
        public float invincibilityDuration = 0.5f; // 无敌帧时长
        public float damageFlashDuration = 0.1f;   // 受伤闪烁时长

        [Header("== Color Immunity ==")]
        public bool enableColorImmunity = true;
        public float immunityFlashIntensity = 3f;
        public Color immunityFlashColor = Color.white;

        [Header("== Death Settings ==")]
        public float deathSequenceDuration = 3f;
        public GameObject deathVfx;
        public AudioClip deathSound;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool showHealthInConsole = true;

        // 组件引用
        private BossC3_PhaseManager phaseManager;
        private Renderer colorRenderer;
        private AudioSource audioSource;

        // 战斗状态
        private bool _isInvincible = false;
        private bool _isDead = false;
        private Coroutine _invincibilityCR;
        private Coroutine _damageFlashCR;
        private Coroutine _deathSequenceCR;

        // 原始材质颜色
        private Color _originalColor;
        private Color _originalEmissionColor;

        // 事件
        public event Action<float, float> OnHealthChanged; // currentHP, maxHP
        public event Action<float, BossColor> OnDamageTaken; // damage, sourceColor
        public event Action OnPhase2Triggered;
        public event Action OnDeath;
        public event Action OnInvincibilityStarted;
        public event Action OnInvincibilityEnded;

        #region Unity Lifecycle

        private void Awake()
        {
            phaseManager = GetComponent<BossC3_PhaseManager>();
            colorRenderer = GetComponent<Renderer>();
            audioSource = GetComponent<AudioSource>();

            // 保存原始颜色
            if (colorRenderer != null)
            {
                _originalColor = colorRenderer.material.color;
                if (colorRenderer.material.HasProperty("_EmissionColor"))
                {
                    _originalEmissionColor = colorRenderer.material.GetColor("_EmissionColor");
                }
            }
        }

        private void Start()
        {
            // 初始化血量
            currentHP = maxHP;
            OnHealthChanged?.Invoke(currentHP, maxHP);

            if (showHealthInConsole && verboseLogs)
            {
                Debug.Log($"[BossC3_CombatSystem] Initialized with {currentHP}/{maxHP} HP");
            }
        }

        #endregion

        #region Damage System

        /// <summary>
        /// 受到伤害
        /// </summary>
        public void TakeDamage(float damage, BossColor? sourcePlayerColor = null)
        {
            if (_isDead || _isInvincible) return;

            // 检查颜色免疫
            if (enableColorImmunity && playerColorImmunity && sourcePlayerColor.HasValue)
            {
                BossColor currentBossColor = phaseManager.GetCurrentColor();
                BossColor sourceColor = sourcePlayerColor.Value;

                // 如果颜色不匹配，免疫伤害
                if (currentBossColor != sourceColor)
                {
                    if (verboseLogs)
                        Debug.Log($"[BossC3_CombatSystem] Damage blocked by color immunity: {currentBossColor} vs {sourceColor}");

                    // 播放免疫特效
                    StartCoroutine(ImmunityFlashCoroutine());
                    return;
                }
            }

            // 应用伤害
            float actualDamage = Mathf.Max(0f, damage);
            currentHP = Mathf.Max(0f, currentHP - actualDamage);

            // 触发事件
            OnDamageTaken?.Invoke(actualDamage, sourcePlayerColor ?? BossColor.None);
            OnHealthChanged?.Invoke(currentHP, maxHP);

            if (verboseLogs)
                Debug.Log($"[BossC3_CombatSystem] Took {actualDamage} damage. HP: {currentHP}/{maxHP}");

            // 播放受伤特效
            StartCoroutine(DamageFlashCoroutine());

            // 检查阶段切换
            CheckPhaseTransition();

            // 检查死亡
            if (currentHP <= 0f)
            {
                Die();
            }
            else
            {
                // 启动无敌帧
                StartInvincibility();
            }
        }

        /// <summary>
        /// 检查阶段切换
        /// </summary>
        private void CheckPhaseTransition()
        {
            if (phaseManager.GetCurrentPhase() == Phase.P1 && currentHP <= phase2Threshold)
            {
                if (verboseLogs)
                    Debug.Log("[BossC3_CombatSystem] Triggering Phase 2 transition");

                phaseManager.ChangePhase(Phase.P2);
                OnPhase2Triggered?.Invoke();
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

            if (verboseLogs)
                Debug.Log("[BossC3_CombatSystem] Boss died!");

            OnDeath?.Invoke();
            OnHealthChanged?.Invoke(currentHP, maxHP);

            // 开始死亡序列
            if (_deathSequenceCR != null)
            {
                StopCoroutine(_deathSequenceCR);
            }
            _deathSequenceCR = StartCoroutine(DeathSequenceCoroutine());
        }

        #endregion

        #region Invincibility System

        /// <summary>
        /// 启动无敌帧
        /// </summary>
        private void StartInvincibility()
        {
            if (_invincibilityCR != null)
            {
                StopCoroutine(_invincibilityCR);
            }

            _isInvincible = true;
            OnInvincibilityStarted?.Invoke();

            _invincibilityCR = StartCoroutine(InvincibilityCoroutine());
        }

        /// <summary>
        /// 无敌帧协程
        /// </summary>
        private IEnumerator InvincibilityCoroutine()
        {
            yield return new WaitForSeconds(invincibilityDuration);

            _isInvincible = false;
            OnInvincibilityEnded?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC3_CombatSystem] Invincibility ended");
        }

        /// <summary>
        /// 检查是否无敌
        /// </summary>
        public bool IsInvincible()
        {
            return _isInvincible;
        }

        #endregion

        #region Visual Effects

        /// <summary>
        /// 受伤闪烁协程
        /// </summary>
        private IEnumerator DamageFlashCoroutine()
        {
            if (colorRenderer == null) yield break;

            // 闪烁为红色
            Color flashColor = Color.red;
            colorRenderer.material.color = flashColor;

            yield return new WaitForSeconds(damageFlashDuration);

            // 恢复原始颜色
            colorRenderer.material.color = _originalColor;
        }

        /// <summary>
        /// 免疫闪烁协程
        /// </summary>
        private IEnumerator ImmunityFlashCoroutine()
        {
            if (colorRenderer == null) yield break;

            // 闪烁为白色
            Color flashColor = immunityFlashColor * immunityFlashIntensity;
            
            if (colorRenderer.material.HasProperty("_EmissionColor"))
            {
                colorRenderer.material.SetColor("_EmissionColor", flashColor);
                colorRenderer.material.EnableKeyword("_EMISSION");
            }
            else
            {
                colorRenderer.material.color = flashColor;
            }

            yield return new WaitForSeconds(damageFlashDuration);

            // 恢复原始颜色
            if (colorRenderer.material.HasProperty("_EmissionColor"))
            {
                colorRenderer.material.SetColor("_EmissionColor", _originalEmissionColor);
            }
            else
            {
                colorRenderer.material.color = _originalColor;
            }
        }

        #endregion

        #region Death Sequence

        /// <summary>
        /// 死亡序列协程
        /// </summary>
        private IEnumerator DeathSequenceCoroutine()
        {
            if (verboseLogs)
                Debug.Log("[BossC3_CombatSystem] Starting death sequence");

            // 播放死亡音效
            if (audioSource != null && deathSound != null)
            {
                audioSource.PlayOneShot(deathSound);
            }

            // 播放死亡特效
            if (deathVfx != null)
            {
                GameObject vfx = Instantiate(deathVfx, transform.position, Quaternion.identity);
                Destroy(vfx, deathSequenceDuration);
            }

            // 淡出效果
            float elapsed = 0f;
            while (elapsed < deathSequenceDuration)
            {
                float alpha = 1f - (elapsed / deathSequenceDuration);
                
                if (colorRenderer != null)
                {
                    Color currentColor = colorRenderer.material.color;
                    currentColor.a = alpha;
                    colorRenderer.material.color = currentColor;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 完成死亡序列
            if (verboseLogs)
                Debug.Log("[BossC3_CombatSystem] Death sequence completed");

            // 这里可以添加游戏结束逻辑
            // 例如：加载下一关、显示胜利画面等
        }

        #endregion

        #region Health Management

        /// <summary>
        /// 治疗
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead) return;

            float oldHP = currentHP;
            currentHP = Mathf.Min(maxHP, currentHP + amount);

            if (currentHP != oldHP)
            {
                OnHealthChanged?.Invoke(currentHP, maxHP);

                if (verboseLogs)
                    Debug.Log($"[BossC3_CombatSystem] Healed {currentHP - oldHP}. HP: {currentHP}/{maxHP}");
            }
        }

        /// <summary>
        /// 设置血量
        /// </summary>
        public void SetHealth(float health)
        {
            if (_isDead) return;

            float oldHP = currentHP;
            currentHP = Mathf.Clamp(health, 0f, maxHP);

            if (currentHP != oldHP)
            {
                OnHealthChanged?.Invoke(currentHP, maxHP);

                // 检查阶段切换
                CheckPhaseTransition();

                // 检查死亡
                if (currentHP <= 0f)
                {
                    Die();
                }

                if (verboseLogs)
                    Debug.Log($"[BossC3_CombatSystem] Health set to {currentHP}/{maxHP}");
            }
        }

        /// <summary>
        /// 设置最大血量
        /// </summary>
        public void SetMaxHealth(float maxHealth)
        {
            maxHP = Mathf.Max(1f, maxHealth);
            
            // 如果当前血量超过新的最大血量，调整当前血量
            if (currentHP > maxHP)
            {
                currentHP = maxHP;
            }

            OnHealthChanged?.Invoke(currentHP, maxHP);

            if (verboseLogs)
                Debug.Log($"[BossC3_CombatSystem] Max health set to {maxHP}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取当前血量
        /// </summary>
        public float GetCurrentHealth()
        {
            return currentHP;
        }

        /// <summary>
        /// 获取最大血量
        /// </summary>
        public float GetMaxHealth()
        {
            return maxHP;
        }

        /// <summary>
        /// 获取血量百分比
        /// </summary>
        public float GetHealthPercentage()
        {
            return maxHP > 0f ? currentHP / maxHP : 0f;
        }

        /// <summary>
        /// 检查是否死亡
        /// </summary>
        public bool IsDead()
        {
            return _isDead;
        }

        /// <summary>
        /// 检查是否满血
        /// </summary>
        public bool IsFullHealth()
        {
            return currentHP >= maxHP;
        }

        /// <summary>
        /// 检查是否低血量
        /// </summary>
        public bool IsLowHealth(float threshold = 0.25f)
        {
            return GetHealthPercentage() <= threshold;
        }

        /// <summary>
        /// 检查是否在P2阶段
        /// </summary>
        public bool IsInPhase2()
        {
            return phaseManager.GetCurrentPhase() == Phase.P2;
        }

        /// <summary>
        /// 获取P2阶段血量阈值
        /// </summary>
        public float GetPhase2Threshold()
        {
            return phase2Threshold;
        }

        /// <summary>
        /// 设置P2阶段血量阈值
        /// </summary>
        public void SetPhase2Threshold(float threshold)
        {
            phase2Threshold = Mathf.Clamp(threshold, 0f, maxHP);
        }

        /// <summary>
        /// 重置战斗系统
        /// </summary>
        public void ResetCombatSystem()
        {
            _isDead = false;
            _isInvincible = false;
            currentHP = maxHP;

            // 停止所有协程
            if (_invincibilityCR != null)
            {
                StopCoroutine(_invincibilityCR);
                _invincibilityCR = null;
            }

            if (_damageFlashCR != null)
            {
                StopCoroutine(_damageFlashCR);
                _damageFlashCR = null;
            }

            if (_deathSequenceCR != null)
            {
                StopCoroutine(_deathSequenceCR);
                _deathSequenceCR = null;
            }

            // 恢复原始颜色
            if (colorRenderer != null)
            {
                colorRenderer.material.color = _originalColor;
                if (colorRenderer.material.HasProperty("_EmissionColor"))
                {
                    colorRenderer.material.SetColor("_EmissionColor", _originalEmissionColor);
                }
            }

            OnHealthChanged?.Invoke(currentHP, maxHP);

            if (verboseLogs)
                Debug.Log("[BossC3_CombatSystem] Combat system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"HP: {currentHP:F1}/{maxHP:F1} ({GetHealthPercentage():P1}), Dead: {_isDead}, Invincible: {_isInvincible}, Phase: {phaseManager.GetCurrentPhase()}";
        }

        /// <summary>
        /// 强制死亡（调试用）
        /// </summary>
        [ContextMenu("Force Death")]
        public void ForceDeath()
        {
            if (!_isDead)
            {
                TakeDamage(currentHP, BossColor.None);
            }
        }

        /// <summary>
        /// 完全治疗（调试用）
        /// </summary>
        [ContextMenu("Full Heal")]
        public void FullHeal()
        {
            if (!_isDead)
            {
                Heal(maxHP);
            }
        }

        #endregion
    }
}
