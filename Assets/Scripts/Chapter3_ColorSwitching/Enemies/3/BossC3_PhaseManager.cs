// BossC3_PhaseManager.cs
// 阶段管理器 - 负责BOSS的阶段切换、颜色管理和环绕体数量控制
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using UnityEngine;

namespace FD.Bosses.C3
{
    /// <summary>
    /// BossC3阶段管理器 - 负责阶段切换、颜色管理和环绕体数量控制
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC3_PhaseManager : MonoBehaviour
    {
        [Header("== Phase / Color ==")]
        public Phase phase = Phase.P1;
        public BossColor color = BossColor.Red;
        [Tooltip("P1=4, P2=6")]
        public int p1Orbs = 4;
        public int p2Orbs = 6;
        public float baseRadius = 2.8f;

        [Header("== Color & Emission ==")]
        public bool tintUseEmission = true;
        public float emissionIntensity = 2.2f;

        [Header("== Phase Transition ==")]
        public float phaseTransitionDuration = 1.0f;
        public AnimationCurve phaseTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== Color Switching ==")]
        public float colorSwitchCooldown = 3.0f;
        public bool autoColorSwitch = true;
        public float colorSwitchInterval = 8.0f;

        [Header("== Debug ==")]
        public bool verboseLogs = true;

        // 组件引用
        private Renderer colorRenderer;
        private BossC3_OrbSystem orbSystem;

        // 状态变量
        private bool isTransitioning = false;
        private float lastColorSwitchTime = 0f;
        private Coroutine phaseTransitionCR;

        // 事件
        public event Action<Phase> OnPhaseChanged;
        public event Action<BossColor> OnColorChanged;
        public event Action OnPhaseTransitionStarted;
        public event Action OnPhaseTransitionCompleted;

        #region Unity Lifecycle

        private void Awake()
        {
            colorRenderer = GetComponent<Renderer>();
            orbSystem = GetComponent<BossC3_OrbSystem>();
        }

        private void Start()
        {
            // 初始化阶段设置
            ApplyPhaseSettings(phase, true);
        }

        private void Update()
        {
            // 自动颜色切换
            if (autoColorSwitch && Time.time - lastColorSwitchTime > colorSwitchInterval)
            {
                SwitchColor();
            }
        }

        #endregion

        #region Phase Management

        /// <summary>
        /// 切换到指定阶段
        /// </summary>
        public void ChangePhase(Phase newPhase, bool force = false)
        {
            if (phase == newPhase && !force) return;
            if (isTransitioning && !force) return;

            if (verboseLogs)
                Debug.Log($"[BossC3_PhaseManager] Changing phase from {phase} to {newPhase}");

            Phase oldPhase = phase;
            phase = newPhase;

            if (phaseTransitionCR != null)
            {
                StopCoroutine(phaseTransitionCR);
            }

            phaseTransitionCR = StartCoroutine(PhaseTransitionCoroutine(oldPhase, newPhase));
        }

        /// <summary>
        /// 阶段转换协程
        /// </summary>
        private IEnumerator PhaseTransitionCoroutine(Phase fromPhase, Phase toPhase)
        {
            isTransitioning = true;
            OnPhaseTransitionStarted?.Invoke();

            float elapsed = 0f;
            float duration = phaseTransitionDuration;

            // 获取转换前的设置
            int fromOrbCount = GetOrbCountForPhase(fromPhase);
            int toOrbCount = GetOrbCountForPhase(toPhase);

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curveValue = phaseTransitionCurve.Evaluate(t);

                // 插值环绕体数量
                int currentOrbCount = Mathf.RoundToInt(Mathf.Lerp(fromOrbCount, toOrbCount, curveValue));
                
                // 应用环绕体数量变化
                if (orbSystem != null)
                {
                    orbSystem.SetOrbCount(currentOrbCount);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 确保最终设置正确
            ApplyPhaseSettings(toPhase, true);

            isTransitioning = false;
            OnPhaseChanged?.Invoke(toPhase);
            OnPhaseTransitionCompleted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC3_PhaseManager] Phase transition completed: {toPhase}");
        }

        /// <summary>
        /// 应用阶段设置
        /// </summary>
        private void ApplyPhaseSettings(Phase targetPhase, bool force = false)
        {
            if (isTransitioning && !force) return;

            // 设置环绕体数量
            int orbCount = GetOrbCountForPhase(targetPhase);
            if (orbSystem != null)
            {
                orbSystem.SetOrbCount(orbCount);
            }

            // 应用颜色设置
            ApplyColorSettings();

            if (verboseLogs)
                Debug.Log($"[BossC3_PhaseManager] Applied phase settings for {targetPhase}: {orbCount} orbs");
        }

        /// <summary>
        /// 获取指定阶段的环绕体数量
        /// </summary>
        private int GetOrbCountForPhase(Phase targetPhase)
        {
            return targetPhase == Phase.P1 ? p1Orbs : p2Orbs;
        }

        /// <summary>
        /// 检查是否可以切换到下一阶段
        /// </summary>
        public bool CanChangePhase()
        {
            return !isTransitioning;
        }

        /// <summary>
        /// 获取当前阶段的环绕体数量
        /// </summary>
        public int GetCurrentOrbCount()
        {
            return GetOrbCountForPhase(phase);
        }

        /// <summary>
        /// 获取下一阶段的环绕体数量
        /// </summary>
        public int GetNextPhaseOrbCount()
        {
            Phase nextPhase = (phase == Phase.P1) ? Phase.P2 : Phase.P1;
            return GetOrbCountForPhase(nextPhase);
        }

        #endregion

        #region Color Management

        /// <summary>
        /// 切换颜色
        /// </summary>
        public void SwitchColor()
        {
            if (Time.time - lastColorSwitchTime < colorSwitchCooldown) return;

            BossColor newColor = (color == BossColor.Red) ? BossColor.Green : BossColor.Red;
            SetColor(newColor);
        }

        /// <summary>
        /// 设置颜色
        /// </summary>
        public void SetColor(BossColor newColor)
        {
            if (color == newColor) return;

            BossColor oldColor = color;
            color = newColor;
            lastColorSwitchTime = Time.time;

            ApplyColorSettings();
            OnColorChanged?.Invoke(newColor);

            if (verboseLogs)
                Debug.Log($"[BossC3_PhaseManager] Color changed from {oldColor} to {newColor}");
        }

        /// <summary>
        /// 应用颜色设置
        /// </summary>
        private void ApplyColorSettings()
        {
            if (colorRenderer == null) return;

            Color targetColor = GetColorForBossColor(color);
            
            if (tintUseEmission)
            {
                // 使用发光材质
                colorRenderer.material.SetColor("_EmissionColor", targetColor * emissionIntensity);
                colorRenderer.material.EnableKeyword("_EMISSION");
            }
            else
            {
                // 使用基础颜色
                colorRenderer.material.color = targetColor;
            }
        }

        /// <summary>
        /// 获取BOSS颜色对应的Unity颜色
        /// </summary>
        private Color GetColorForBossColor(BossColor bossColor)
        {
            switch (bossColor)
            {
                case BossColor.Red:
                    return Color.red;
                case BossColor.Green:
                    return Color.green;
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 获取相反颜色
        /// </summary>
        public BossColor GetOppositeColor()
        {
            return (color == BossColor.Red) ? BossColor.Green : BossColor.Red;
        }

        /// <summary>
        /// 检查颜色是否匹配
        /// </summary>
        public bool IsColorMatch(BossColor otherColor)
        {
            return color == otherColor;
        }

        /// <summary>
        /// 检查颜色是否相反
        /// </summary>
        public bool IsColorOpposite(BossColor otherColor)
        {
            return color != otherColor && otherColor != BossColor.None;
        }

        #endregion

        #region Phase-Specific Behavior

        /// <summary>
        /// 获取阶段特定的行为参数
        /// </summary>
        public T GetPhaseParameter<T>(T p1Value, T p2Value)
        {
            return (phase == Phase.P1) ? p1Value : p2Value;
        }

        /// <summary>
        /// 获取阶段特定的浮点参数
        /// </summary>
        public float GetPhaseFloat(float p1Value, float p2Value)
        {
            return GetPhaseParameter(p1Value, p2Value);
        }

        /// <summary>
        /// 获取阶段特定的整数参数
        /// </summary>
        public int GetPhaseInt(int p1Value, int p2Value)
        {
            return GetPhaseParameter(p1Value, p2Value);
        }

        /// <summary>
        /// 获取阶段特定的布尔参数
        /// </summary>
        public bool GetPhaseBool(bool p1Value, bool p2Value)
        {
            return GetPhaseParameter(p1Value, p2Value);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取当前阶段
        /// </summary>
        public Phase GetCurrentPhase() => phase;

        /// <summary>
        /// 获取当前颜色
        /// </summary>
        public BossColor GetCurrentColor() => color;

        /// <summary>
        /// 检查是否正在转换阶段
        /// </summary>
        public bool IsTransitioning() => isTransitioning;

        /// <summary>
        /// 强制完成当前阶段转换
        /// </summary>
        public void ForceCompleteTransition()
        {
            if (phaseTransitionCR != null)
            {
                StopCoroutine(phaseTransitionCR);
                phaseTransitionCR = null;
            }

            isTransitioning = false;
            ApplyPhaseSettings(phase, true);
            OnPhaseTransitionCompleted?.Invoke();
        }

        /// <summary>
        /// 重置到初始状态
        /// </summary>
        public void ResetToInitialState()
        {
            phase = Phase.P1;
            color = BossColor.Red;
            isTransitioning = false;
            lastColorSwitchTime = 0f;

            if (phaseTransitionCR != null)
            {
                StopCoroutine(phaseTransitionCR);
                phaseTransitionCR = null;
            }

            ApplyPhaseSettings(phase, true);
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Phase: {phase}, Color: {color}, Orbs: {GetCurrentOrbCount()}, Transitioning: {isTransitioning}";
        }

        #endregion
    }
}
