// 📋 代码总览: 请先阅读 Assets/Scripts/CODE_OVERVIEW.md 了解完整项目结构
// 🚀 开发指南: 参考 Assets/Scripts/DEVELOPMENT_GUIDE.md 进行开发

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using FD.Bosses.C3; // 直接使用 BossColor / BossC3_AllInOne

namespace FadedDreams.Player
{
    public enum ColorMode { Red, Green }

    /// <summary>
    /// 模式切换控制器：双能量条 + 事件广播 + UI/特效通知
    /// </summary>
    public class PlayerColorModeController : MonoBehaviour, IColorState
    {
        [Header("Energies")]
        [SerializeField] private float redMax = 100f;
        [SerializeField] private float greenMax = 100f;
        [SerializeField] private float red = 100f;
        [SerializeField] private float green = 100f;

        [Header("Costs")]
        [SerializeField] private float switchCost = 5f;
        [SerializeField] private float redAttackCost = 8f;
        [SerializeField] private float greenAttackCost = 6f;

        [Header("Switch Rules")]
        public ColorMode Mode = ColorMode.Red;
        [SerializeField] private float minOtherEnergyToSwitch = 1f;
        [SerializeField] private float switchCooldown = 0.15f;
        private float _lastSwitchTime = -99f;

        [Header("Auto Behaviors")]
        public bool autoSwitchOnDeplete = true;       // 当前模式耗尽 → 自动切到另一色
        public bool autoRespawnOnBothEmpty = true;    // 两色耗尽 → 重生
        public float respawnDelay = 0.2f;             // 重生前的缓冲
        public UnityEvent OnBothEnergiesEmpty = new UnityEvent();

        [Tooltip("SendMessage 目标（为空则向自己发送，用于关卡系统接收重生广播）")]
        [SerializeField] private GameObject respawnTarget;
        [Tooltip("SendMessage 的方法名，关卡系统需要实现一个同名 public 方法即可")]
        [SerializeField] private string respawnMessage = "RespawnAtNearestTeleport";

        [Header("Optional Visual")]
        [SerializeField] private Light2D playerAuraLight;
        [SerializeField] private Color redLightColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color greenLightColor = new Color(0.25f, 1f, 0.25f, 1f);

        [System.Serializable] public class EnergyEvent : UnityEvent<float, float, float, float> { }
        public UnityEvent<ColorMode> OnModeChanged = new UnityEvent<ColorMode>();
        public EnergyEvent OnEnergyChanged = new EnergyEvent();

        public float Red01 => Mathf.Clamp01(red / redMax);
        public float Green01 => Mathf.Clamp01(green / greenMax);

        private bool _pendingRespawn;

        // === IColorState 实现：供 BossC3 获取玩家当前颜色 ===
        public BossColor GetColorMode()
        {
            return (Mode == ColorMode.Red) ? BossColor.Red : BossColor.Green;
        }

        private void Start()
        {
            PushEnergyEvent();
            ApplyAuraColor();
        }

        private void Update()
        {
            // 手动切换：右键切换
            if (Input.GetMouseButtonDown(1))
                TrySwitchMode();

            // 检查：即使外部调用导致某色 <=0，也能触发自动逻辑
            CheckAutoSwitchAndRespawn();
        }

        public bool CanSwitchTo(ColorMode target)
        {
            if (Time.unscaledTime - _lastSwitchTime < switchCooldown) return false;
            if (target == Mode) return false;

            bool targetHasEnergy = (target == ColorMode.Red) ? red > minOtherEnergyToSwitch
                                                             : green > minOtherEnergyToSwitch;
            if (!targetHasEnergy) return false;

            // 手动切换需要消耗当前模式的能量
            return HasEnergy(Mode, switchCost);
        }

        public bool TrySwitchMode()
        {
            ColorMode target = (Mode == ColorMode.Red) ? ColorMode.Green : ColorMode.Red;
            if (!CanSwitchTo(target)) return false;

            SpendEnergy(Mode, switchCost);
            ForceSwitch(target);
            return true;
        }

        public bool HasEnergy(ColorMode m, float amount) =>
            (m == ColorMode.Red) ? red >= amount : green >= amount;

        public bool SpendEnergy(ColorMode m, float amount)
        {
            // 如果当前模式能量不足，先清零当前能量，然后切换到另一能量条
            if (!HasEnergy(m, amount))
            {
                // 先清零当前能量
                if (m == ColorMode.Red) red = 0f; else green = 0f;
                ClampEnergies();
                PushEnergyEvent();
                
                // 切换到另一能量条继续扣除
                ColorMode otherMode = (m == ColorMode.Red) ? ColorMode.Green : ColorMode.Red;
                float remainingAmount = amount - ((m == ColorMode.Red) ? red : green);
                
                if (remainingAmount > 0f)
                {
                    // 切换到另一模式
                    if (autoSwitchOnDeplete) ForceSwitch(otherMode);
                    
                    // 从另一能量条扣除剩余量
                    if (otherMode == ColorMode.Red) 
                    {
                        red = Mathf.Max(0f, red - remainingAmount);
                    }
                    else 
                    {
                        green = Mathf.Max(0f, green - remainingAmount);
                    }
                    
                    ClampEnergies();
                    PushEnergyEvent();
                }
                
                CheckAutoSwitchAndRespawn();
                return true; // 总是返回true，因为已经处理了能量扣除
            }

            // 正常扣除当前模式能量
            if (m == ColorMode.Red) red -= amount; else green -= amount;
            ClampEnergies();
            PushEnergyEvent();

            CheckAutoSwitchAndRespawn();
            return true;
        }

        public void AddEnergy(ColorMode m, float amount)
        {
            if (m == ColorMode.Red) red = Mathf.Min(red + amount, redMax);
            else green = Mathf.Min(green + amount, greenMax);
            PushEnergyEvent();
        }

        public bool TrySpendAttackCost()
        {
            float c = (Mode == ColorMode.Red) ? redAttackCost : greenAttackCost;
            return SpendEnergy(Mode, c);
        }

        private void ClampEnergies()
        {
            red = Mathf.Clamp(red, 0f, redMax);
            green = Mathf.Clamp(green, 0f, greenMax);
        }

        private void PushEnergyEvent() =>
            OnEnergyChanged.Invoke(red, redMax, green, greenMax);

        private void ApplyAuraColor()
        {
            if (!playerAuraLight) return;
            playerAuraLight.color = (Mode == ColorMode.Red) ? redLightColor : greenLightColor;
            playerAuraLight.intensity = Mathf.Lerp(0.6f, 1.2f, (Mode == ColorMode.Red) ? Red01 : Green01);
        }

        private void ForceSwitch(ColorMode target)
        {
            Mode = target;
            _lastSwitchTime = Time.unscaledTime;
            ApplyAuraColor();
            OnModeChanged.Invoke(Mode);
        }

        private void CheckAutoSwitchAndRespawn()
        {
            bool redEmpty = red <= 0.01f;
            bool greenEmpty = green <= 0.01f;

            if (autoRespawnOnBothEmpty && redEmpty && greenEmpty && !_pendingRespawn)
            {
                _pendingRespawn = true;
                Invoke(nameof(DoRespawn), respawnDelay);
                return;
            }

            if (autoSwitchOnDeplete)
            {
                if (Mode == ColorMode.Red && redEmpty && green > minOtherEnergyToSwitch)
                    ForceSwitch(ColorMode.Green);
                else if (Mode == ColorMode.Green && greenEmpty && red > minOtherEnergyToSwitch)
                    ForceSwitch(ColorMode.Red);
            }
        }

        private void TryAutoSwitchIfEmpty()
        {
            bool redEmpty = red <= 0.01f;
            bool greenEmpty = green <= 0.01f;

            if (Mode == ColorMode.Red && redEmpty && green > minOtherEnergyToSwitch)
                ForceSwitch(ColorMode.Green);
            else if (Mode == ColorMode.Green && greenEmpty && red > minOtherEnergyToSwitch)
                ForceSwitch(ColorMode.Red);

            if (autoRespawnOnBothEmpty && redEmpty && greenEmpty && !_pendingRespawn)
            {
                _pendingRespawn = true;
                Invoke(nameof(DoRespawn), respawnDelay);
            }
        }

        private void DoRespawn()
        {
            _pendingRespawn = false;
            OnBothEnergiesEmpty.Invoke();

            var target = respawnTarget ? respawnTarget : gameObject;
            target.SendMessage(respawnMessage, transform.position, SendMessageOptions.DontRequireReceiver);
        }
    }
}