using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.Player
{
    public enum ColorMode { Red, Green }

    /// <summary>
    /// ģʽ�л������������¼��㲥��UI/��Ч����
    /// </summary>
    public class PlayerColorModeController : MonoBehaviour
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
        public bool autoSwitchOnDeplete = true;       // ��ǰģʽ���� �� �Զ��е���һɫ
        public bool autoRespawnOnBothEmpty = true;    // ��ɫ���� �� ����
        public float respawnDelay = 0.2f;             // ����ǰ�Ļ���
        public UnityEvent OnBothEnergiesEmpty = new UnityEvent();

        [Tooltip("SendMessage ��Ŀ�꣨��Ϊ�գ������������������㲥����")]
        [SerializeField] private GameObject respawnTarget;
        [Tooltip("SendMessage �ķ���������Ĺؿ�������ʵ��һ��ͬ�� public �������ɣ���")]
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

        private void Start()
        {
            PushEnergyEvent();
            ApplyAuraColor();
        }

        private void Update()
        {
            // �ֶ��л�
            if (Input.GetMouseButtonDown(1))
                TrySwitchMode();

            // ���ף�������˴��ⲿ�������ĳ� <=0��Ҳ�ܴ����Զ��߼�
            CheckAutoSwitchAndRespawn();
        }

        public bool CanSwitchTo(ColorMode target)
        {
            if (Time.unscaledTime - _lastSwitchTime < switchCooldown) return false;
            if (target == Mode) return false;

            bool targetHasEnergy = (target == ColorMode.Red) ? red > minOtherEnergyToSwitch : green > minOtherEnergyToSwitch;
            if (!targetHasEnergy) return false;

            // �ֶ��л���Ҫ������ǰģʽ���Ĵ���
            return HasEnergy(Mode, switchCost);
        }

        public bool TrySwitchMode()
        {
            ColorMode target = Mode == ColorMode.Red ? ColorMode.Green : ColorMode.Red;
            if (!CanSwitchTo(target)) return false;

            SpendEnergy(Mode, switchCost);
            ForceSwitch(target);
            return true;
        }

        public bool HasEnergy(ColorMode m, float amount) =>
            (m == ColorMode.Red) ? red >= amount : green >= amount;

        public bool SpendEnergy(ColorMode m, float amount)
        {
            if (!HasEnergy(m, amount))
            {
                // ��ǰģʽ�������������Զ��л��������е���һɫ
                if (autoSwitchOnDeplete) TryAutoSwitchIfEmpty();
                return false;
            }

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
            float c = Mode == ColorMode.Red ? redAttackCost : greenAttackCost;
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
            playerAuraLight.color = Mode == ColorMode.Red ? redLightColor : greenLightColor;
            playerAuraLight.intensity = Mathf.Lerp(0.6f, 1.2f, Mode == ColorMode.Red ? Red01 : Green01);
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
