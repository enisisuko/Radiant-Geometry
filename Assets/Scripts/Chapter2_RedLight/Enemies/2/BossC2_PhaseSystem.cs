// BossC2_PhaseSystem.cs
// 阶段系统 - 负责BOSS的阶段管理、攻击循环和特殊技能
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FadedDreams.Bosses;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// BossC2阶段系统 - 负责阶段管理、攻击循环和特殊技能
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC2_PhaseSystem : MonoBehaviour
    {
        [Header("== Phase Settings ==")]
        public int phase1HitsToAdvance = 3;
        public int phase2HitsToKill = 3;
        public float phaseTransitionDuration = 2f;

        [Header("== Phase 1 - Airborne Attacks ==")]
        public Vector2 attackIntervalRange = new Vector2(1.2f, 1.8f);
        public float bigBulletSpeed = 10f;
        public float bigBulletRedDamage = 20f;
        public int bigBulletVolley = 5;
        public float grenadeSpeed = 7f;

        [Header("== Phase 2 - Special Skills ==")]
        public bool enableLotusBloom = true;
        public bool enableOrbitalSparks = true;
        public bool enableStarfallArcs = true;
        public bool enableHomingLaserChase = true;
        public bool enableScytheSweep2 = true;

        [Header("== Phase 1 Drop Points ==")]
        public Transform[] phase1DropPoints = new Transform[4];

        [Header("== Phase 2 Dash Points ==")]
        public Transform[] phase2DashPoints = new Transform[4];

        [Header("== Debug ==")]
        public bool verboseLogs = true;

        // 组件引用
        private BossC2_Core core;
        private BossC2_AttackSystem attackSystem;

        // 阶段状态
        private int _currentPhase = 1;
        private int _phase1Hits = 0;
        private int _phase2Hits = 0;
        private bool _isTransitioning = false;
        private Coroutine _phase1CR;
        private Coroutine _phase2CR;
        private Coroutine _transitionCR;

        // 事件
        public event Action<int> OnPhaseChanged;
        public event Action OnPhase1Started;
        public event Action OnPhase2Started;
        public event Action OnPhaseTransitionStarted;
        public event Action OnPhaseTransitionCompleted;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<BossC2_Core>();
            attackSystem = GetComponent<BossC2_AttackSystem>();
        }

        private void Start()
        {
            // 订阅核心事件
            if (core != null)
            {
                core.OnAggroStarted += StartPhase1;
                core.OnDeath += StopAllPhases;
            }
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (core != null)
            {
                core.OnAggroStarted -= StartPhase1;
                core.OnDeath -= StopAllPhases;
            }
        }

        #endregion

        #region Phase Management

        /// <summary>
        /// 开始阶段1
        /// </summary>
        public void StartPhase1()
        {
            if (_currentPhase != 1 || _isTransitioning) return;

            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Starting Phase 1");

            _currentPhase = 1;
            _phase1Hits = 0;

            if (_phase1CR != null)
            {
                StopCoroutine(_phase1CR);
            }

            _phase1CR = StartCoroutine(CoPhase1());
            OnPhase1Started?.Invoke();
            OnPhaseChanged?.Invoke(1);
        }

        /// <summary>
        /// 开始阶段2
        /// </summary>
        public void StartPhase2()
        {
            if (_currentPhase != 2 || _isTransitioning) return;

            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Starting Phase 2");

            _currentPhase = 2;
            _phase2Hits = 0;

            if (_phase2CR != null)
            {
                StopCoroutine(_phase2CR);
            }

            _phase2CR = StartCoroutine(CoPhase2());
            OnPhase2Started?.Invoke();
            OnPhaseChanged?.Invoke(2);
        }

        /// <summary>
        /// 切换到下一阶段
        /// </summary>
        public void AdvanceToNextPhase()
        {
            if (_isTransitioning) return;

            if (_currentPhase == 1)
            {
                StartCoroutine(TransitionToPhase2());
            }
            else if (_currentPhase == 2)
            {
                // 阶段2是最终阶段，不需要切换
                if (verboseLogs)
                    Debug.Log("[BossC2_PhaseSystem] Phase 2 is final phase");
            }
        }

        /// <summary>
        /// 转换到阶段2
        /// </summary>
        private IEnumerator TransitionToPhase2()
        {
            if (_isTransitioning) yield break;

            _isTransitioning = true;
            OnPhaseTransitionStarted?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Transitioning to Phase 2");

            // 停止阶段1
            if (_phase1CR != null)
            {
                StopCoroutine(_phase1CR);
                _phase1CR = null;
            }

            // 等待转换时间
            yield return new WaitForSeconds(phaseTransitionDuration);

            // 开始阶段2
            StartPhase2();

            _isTransitioning = false;
            OnPhaseTransitionCompleted?.Invoke();
        }

        /// <summary>
        /// 停止所有阶段
        /// </summary>
        private void StopAllPhases()
        {
            if (_phase1CR != null)
            {
                StopCoroutine(_phase1CR);
                _phase1CR = null;
            }

            if (_phase2CR != null)
            {
                StopCoroutine(_phase2CR);
                _phase2CR = null;
            }

            if (_transitionCR != null)
            {
                StopCoroutine(_transitionCR);
                _transitionCR = null;
            }

            _isTransitioning = false;

            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] All phases stopped");
        }

        #endregion

        #region Phase 1 Implementation

        /// <summary>
        /// 阶段1主循环
        /// </summary>
        private IEnumerator CoPhase1()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Phase 1 loop started");

            while (_currentPhase == 1 && !core.IsDead())
            {
                // 等待攻击间隔
                float interval = UnityEngine.Random.Range(attackIntervalRange.x, attackIntervalRange.y);
                yield return new WaitForSeconds(interval);

                // 选择攻击方式
                int attackType = UnityEngine.Random.Range(0, 3);
                
                switch (attackType)
                {
                    case 0:
                        yield return StartCoroutine(DoAirVolley());
                        break;
                    case 1:
                        yield return StartCoroutine(DoAirGrenade());
                        break;
                    case 2:
                        yield return StartCoroutine(DoAirSummon());
                        break;
                }
            }

            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Phase 1 loop ended");
        }

        /// <summary>
        /// 空中子弹齐射
        /// </summary>
        private IEnumerator DoAirVolley()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Executing air volley attack");

            Transform player = core.GetPlayer();
            if (player == null) yield break;

            Vector2 direction = (player.position - transform.position).normalized;

            for (int i = 0; i < bigBulletVolley; i++)
            {
                if (attackSystem != null)
                {
                    attackSystem.SpawnBullet(transform.position, direction, bigBulletSpeed, bigBulletRedDamage);
                }

                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// 空中手榴弹
        /// </summary>
        private IEnumerator DoAirGrenade()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Executing air grenade attack");

            Transform player = core.GetPlayer();
            if (player == null) yield break;

            Vector2 direction = (player.position - transform.position).normalized;

            if (attackSystem != null)
            {
                attackSystem.SpawnGrenade(transform.position, direction, grenadeSpeed);
            }

            yield return null;
        }

        /// <summary>
        /// 空中召唤
        /// </summary>
        private IEnumerator DoAirSummon()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Executing air summon attack");

            // 这里可以召唤黑暗精灵或其他敌人
            // 具体实现取决于项目的召唤系统

            yield return null;
        }

        #endregion

        #region Phase 2 Implementation

        /// <summary>
        /// 阶段2主循环
        /// </summary>
        private IEnumerator CoPhase2()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Phase 2 loop started");

            while (_currentPhase == 2 && !core.IsDead())
            {
                // 旋转射击
                yield return StartCoroutine(CoSpinShoot(3f));

                // 等待间隔
                yield return new WaitForSeconds(1f);

                // 选择特殊技能
                yield return StartCoroutine(CoPickOneSpecial());
            }

            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Phase 2 loop ended");
        }

        /// <summary>
        /// 旋转射击
        /// </summary>
        private IEnumerator CoSpinShoot(float duration)
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Executing spin shoot");

            float elapsed = 0f;
            float rotationSpeed = 180f; // 度/秒

            while (elapsed < duration)
            {
                float angle = elapsed * rotationSpeed * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                if (attackSystem != null)
                {
                    attackSystem.SpawnBullet(transform.position, direction, bigBulletSpeed, bigBulletRedDamage);
                }

                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// 选择一个特殊技能
        /// </summary>
        private IEnumerator CoPickOneSpecial()
        {
            List<System.Func<IEnumerator>> availableSkills = new List<System.Func<IEnumerator>>();

            if (enableLotusBloom) availableSkills.Add(CoLotusBloom);
            if (enableOrbitalSparks) availableSkills.Add(CoOrbitalSpark);
            if (enableStarfallArcs) availableSkills.Add(CoStarfallArcs);

            if (availableSkills.Count == 0)
            {
                yield return null;
                return;
            }

            int randomIndex = UnityEngine.Random.Range(0, availableSkills.Count);
            yield return StartCoroutine(availableSkills[randomIndex]());
        }

        /// <summary>
        /// 莲花绽放技能
        /// </summary>
        private IEnumerator CoLotusBloom()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Executing lotus bloom");

            // 莲花绽放的具体实现
            // 这里可以创建莲花形状的攻击模式

            yield return new WaitForSeconds(2f);
        }

        /// <summary>
        /// 轨道火花技能
        /// </summary>
        private IEnumerator CoOrbitalSpark()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Executing orbital spark");

            // 轨道火花的具体实现
            // 这里可以创建围绕BOSS旋转的火花攻击

            yield return new WaitForSeconds(2f);
        }

        /// <summary>
        /// 星降弧技能
        /// </summary>
        private IEnumerator CoStarfallArcs()
        {
            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Executing starfall arcs");

            // 星降弧的具体实现
            // 这里可以创建从天而降的弧形攻击

            yield return new WaitForSeconds(2f);
        }

        #endregion

        #region Hit Management

        /// <summary>
        /// 记录击中
        /// </summary>
        public void RecordHit()
        {
            if (_currentPhase == 1)
            {
                _phase1Hits++;
                if (verboseLogs)
                    Debug.Log($"[BossC2_PhaseSystem] Phase 1 hit recorded: {_phase1Hits}/{phase1HitsToAdvance}");

                if (_phase1Hits >= phase1HitsToAdvance)
                {
                    AdvanceToNextPhase();
                }
            }
            else if (_currentPhase == 2)
            {
                _phase2Hits++;
                if (verboseLogs)
                    Debug.Log($"[BossC2_PhaseSystem] Phase 2 hit recorded: {_phase2Hits}/{phase2HitsToKill}");

                if (_phase2Hits >= phase2HitsToKill)
                {
                    // 阶段2被击败，BOSS死亡
                    if (core != null)
                    {
                        core.TakeDamage(core.GetCurrentHealth());
                    }
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取当前阶段
        /// </summary>
        public int GetCurrentPhase() => _currentPhase;

        /// <summary>
        /// 获取阶段1击中次数
        /// </summary>
        public int GetPhase1Hits() => _phase1Hits;

        /// <summary>
        /// 获取阶段2击中次数
        /// </summary>
        public int GetPhase2Hits() => _phase2Hits;

        /// <summary>
        /// 检查是否正在转换阶段
        /// </summary>
        public bool IsTransitioning() => _isTransitioning;

        /// <summary>
        /// 获取阶段1进度
        /// </summary>
        public float GetPhase1Progress() => (float)_phase1Hits / phase1HitsToAdvance;

        /// <summary>
        /// 获取阶段2进度
        /// </summary>
        public float GetPhase2Progress() => (float)_phase2Hits / phase2HitsToKill;

        /// <summary>
        /// 重置阶段系统
        /// </summary>
        public void ResetPhaseSystem()
        {
            StopAllPhases();
            _currentPhase = 1;
            _phase1Hits = 0;
            _phase2Hits = 0;
            _isTransitioning = false;

            if (verboseLogs)
                Debug.Log("[BossC2_PhaseSystem] Phase system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Phase: {_currentPhase}, P1 Hits: {_phase1Hits}/{phase1HitsToAdvance}, P2 Hits: {_phase2Hits}/{phase2HitsToKill}, Transitioning: {_isTransitioning}";
        }

        #endregion
    }
}
