// BossC1_PhaseSystem.cs
// 三阶段系统 - 负责BOSS的三阶段管理、阶段转换和遁入虚无逻辑
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using UnityEngine;

namespace FadedDreams.Boss
{
    /// <summary>
    /// BossC1三阶段系统 - 负责三阶段管理、阶段转换和遁入虚无逻辑
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC1_PhaseSystem : MonoBehaviour
    {
        [Header("== Phase Settings ==")]
        public int phase1Hits = 10;
        public int phase2Hits = 10;
        public int phase3Hits = 10;
        public float invulnerableAfterHit = 2f;

        [Header("== Phase Transition ==")]
        public float transformShowSeconds = 2.0f;
        public float phaseTransitionDuration = 1.5f;
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== Phase Out/In ==")]
        public float vanishFadeSeconds = 0.5f;
        public float appearFadeSeconds = 0.5f;
        public float phaseOutDuration = 3f;
        public float phaseInDuration = 2f;

        [Header("== VFX ==")]
        public GameObject vfxPhaseOut;
        public GameObject vfxPhaseIn;
        public GameObject vfxTransform12;
        public GameObject vfxTransform23;

        [Header("== Debug ==")]
        public bool verboseLogs = true;

        // 组件引用
        private BossC1_Core core;
        private BossC1_VisualSystem visualSystem;

        // 阶段状态
        private int _currentPhase = 1;
        private int _phase1HitCount = 0;
        private int _phase2HitCount = 0;
        private int _phase3HitCount = 0;
        private bool _isTransitioning = false;
        private bool _isPhaseOut = false;
        private Coroutine _phaseTransitionCR;
        private Coroutine _phaseOutCR;

        // 事件
        public event Action<int> OnPhaseChanged;
        public event Action OnPhaseTransitionStarted;
        public event Action OnPhaseTransitionCompleted;
        public event Action OnPhaseOutStarted;
        public event Action OnPhaseInCompleted;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<BossC1_Core>();
            visualSystem = GetComponent<BossC1_VisualSystem>();
        }

        private void Start()
        {
            // 订阅核心事件
            if (core != null)
            {
                core.OnDamageTaken += OnDamageTaken;
                core.OnPhaseChanged += OnCorePhaseChanged;
            }
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (core != null)
            {
                core.OnDamageTaken -= OnDamageTaken;
                core.OnPhaseChanged -= OnCorePhaseChanged;
            }

            // 停止协程
            if (_phaseTransitionCR != null)
            {
                StopCoroutine(_phaseTransitionCR);
            }

            if (_phaseOutCR != null)
            {
                StopCoroutine(_phaseOutCR);
            }
        }

        #endregion

        #region Phase Management

        /// <summary>
        /// 受到伤害时的处理
        /// </summary>
        private void OnDamageTaken(float damage)
        {
            // 记录击中
            RecordHit();
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
                        // 阶段3被击败，BOSS死亡
                        if (core != null)
                        {
                            core.TakeDamage(core.GetCurrentHealth());
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 推进到下一阶段
        /// </summary>
        private void AdvanceToPhase(int newPhase)
        {
            if (newPhase <= _currentPhase || _isTransitioning) return;

            if (verboseLogs)
                Debug.Log($"[BossC1_PhaseSystem] Advancing from phase {_currentPhase} to {newPhase}");

            _currentPhase = newPhase;
            OnPhaseChanged?.Invoke(_currentPhase);

            // 开始阶段转换
            StartPhaseTransition(newPhase);
        }

        /// <summary>
        /// 开始阶段转换
        /// </summary>
        private void StartPhaseTransition(int newPhase)
        {
            if (_phaseTransitionCR != null)
            {
                StopCoroutine(_phaseTransitionCR);
            }

            _phaseTransitionCR = StartCoroutine(PhaseTransitionCoroutine(newPhase));
        }

        /// <summary>
        /// 阶段转换协程
        /// </summary>
        private IEnumerator PhaseTransitionCoroutine(int newPhase)
        {
            _isTransitioning = true;
            OnPhaseTransitionStarted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC1_PhaseSystem] Starting phase transition to {newPhase}");

            // 播放转换特效
            PlayPhaseTransitionVfx(newPhase);

            // 阶段转换动画
            yield return StartCoroutine(TransformAnimation(newPhase));

            // 完成转换
            _isTransitioning = false;
            OnPhaseTransitionCompleted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC1_PhaseSystem] Phase transition completed");
        }

        /// <summary>
        /// 转换动画
        /// </summary>
        private IEnumerator TransformAnimation(int newPhase)
        {
            float elapsed = 0f;
            float duration = transformShowSeconds;

            // 获取当前和目标颜色
            Color currentColor = core.GetCurrentPhaseColor();
            Color targetColor = GetPhaseColor(newPhase);

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curveValue = transitionCurve.Evaluate(t);

                // 插值颜色
                Color lerpedColor = Color.Lerp(currentColor, targetColor, curveValue);
                core.SetPhaseColor(lerpedColor);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 确保最终颜色正确
            core.SetPhaseColor(targetColor);
        }

        #endregion

        #region Phase Out/In System

        /// <summary>
        /// 开始遁入虚无
        /// </summary>
        public void StartPhaseOut()
        {
            if (_isPhaseOut || _isTransitioning) return;

            if (verboseLogs)
                Debug.Log("[BossC1_PhaseSystem] Starting phase out");

            _isPhaseOut = true;
            OnPhaseOutStarted?.Invoke();

            if (_phaseOutCR != null)
            {
                StopCoroutine(_phaseOutCR);
            }

            _phaseOutCR = StartCoroutine(PhaseOutCoroutine());
        }

        /// <summary>
        /// 遁入虚无协程
        /// </summary>
        private IEnumerator PhaseOutCoroutine()
        {
            // 播放遁入特效
            if (vfxPhaseOut != null)
            {
                SpawnVfx(vfxPhaseOut, transform.position);
            }

            // 淡出
            if (visualSystem != null)
            {
                yield return StartCoroutine(visualSystem.FadeVisible(false, vanishFadeSeconds));
            }

            // 等待遁入时间
            yield return new WaitForSeconds(phaseOutDuration);

            // 回归
            yield return StartCoroutine(PhaseInCoroutine());
        }

        /// <summary>
        /// 回归协程
        /// </summary>
        private IEnumerator PhaseInCoroutine()
        {
            // 播放回归特效
            if (vfxPhaseIn != null)
            {
                SpawnVfx(vfxPhaseIn, transform.position);
            }

            // 淡入
            if (visualSystem != null)
            {
                yield return StartCoroutine(visualSystem.FadeVisible(true, appearFadeSeconds));
            }

            // 完成回归
            _isPhaseOut = false;
            OnPhaseInCompleted?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC1_PhaseSystem] Phase in completed");
        }

        #endregion

        #region VFX Management

        /// <summary>
        /// 播放阶段转换特效
        /// </summary>
        private void PlayPhaseTransitionVfx(int newPhase)
        {
            GameObject vfxPrefab = null;

            switch (newPhase)
            {
                case 2:
                    vfxPrefab = vfxTransform12;
                    break;
                case 3:
                    vfxPrefab = vfxTransform23;
                    break;
            }

            if (vfxPrefab != null)
            {
                SpawnVfx(vfxPrefab, transform.position);
            }
        }

        /// <summary>
        /// 生成特效
        /// </summary>
        private void SpawnVfx(GameObject prefab, Vector3 position)
        {
            if (prefab != null)
            {
                GameObject vfx = Instantiate(prefab, position, Quaternion.identity);
                Destroy(vfx, 5f); // 5秒后销毁
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 获取阶段颜色
        /// </summary>
        private Color GetPhaseColor(int phase)
        {
            switch (phase)
            {
                case 1: return core.phase1Color;
                case 2: return core.phase2Color;
                case 3: return core.phase3Color;
                default: return Color.white;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 核心阶段变化处理
        /// </summary>
        private void OnCorePhaseChanged(int newPhase)
        {
            _currentPhase = newPhase;
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
        /// 检查是否正在转换阶段
        /// </summary>
        public bool IsTransitioning() => _isTransitioning;

        /// <summary>
        /// 检查是否正在遁入虚无
        /// </summary>
        public bool IsPhaseOut() => _isPhaseOut;

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
        /// 获取下一阶段击中次数
        /// </summary>
        public int GetNextPhaseHits()
        {
            switch (_currentPhase)
            {
                case 1: return phase2Hits;
                case 2: return phase3Hits;
                case 3: return 0; // 最终阶段
                default: return 0;
            }
        }

        /// <summary>
        /// 强制推进到指定阶段
        /// </summary>
        public void ForceAdvanceToPhase(int phase)
        {
            if (phase < 1 || phase > 3) return;

            _currentPhase = phase;
            OnPhaseChanged?.Invoke(_currentPhase);

            if (verboseLogs)
                Debug.Log($"[BossC1_PhaseSystem] Force advanced to phase {phase}");
        }

        /// <summary>
        /// 重置阶段系统
        /// </summary>
        public void ResetPhaseSystem()
        {
            _currentPhase = 1;
            _phase1HitCount = 0;
            _phase2HitCount = 0;
            _phase3HitCount = 0;
            _isTransitioning = false;
            _isPhaseOut = false;

            // 停止协程
            if (_phaseTransitionCR != null)
            {
                StopCoroutine(_phaseTransitionCR);
                _phaseTransitionCR = null;
            }

            if (_phaseOutCR != null)
            {
                StopCoroutine(_phaseOutCR);
                _phaseOutCR = null;
            }

            if (verboseLogs)
                Debug.Log("[BossC1_PhaseSystem] Phase system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Phase: {_currentPhase}, P1: {_phase1HitCount}/{phase1Hits}, P2: {_phase2HitCount}/{phase2Hits}, P3: {_phase3HitCount}/{phase3Hits}, Transitioning: {_isTransitioning}, PhaseOut: {_isPhaseOut}";
        }

        #endregion
    }
}
