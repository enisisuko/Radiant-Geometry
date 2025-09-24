using FadedDreams.Player;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;                 // UnityAction
using UnityEngine.Rendering;              // Volume
using UnityEngine.Rendering.Universal;    // Bloom, Light2D

// ������������ռ䣬�ѱ������ȥ����
[DisallowMultipleComponent]
public class PlayerLightVisual : MonoBehaviour
{
    [Header("Refs (�ڼ�������Ϻ�)")]
    public PlayerLightController plc;   // �ṩ onEnergyChanged / onModeChanged���޲� UnityEvent��
    public Light2D playerLight;         // ��� Light2D
    public SpriteRenderer sr;           // ��ѡ����ɫ�Է���

    [Header("Light Mapping")]
    [Range(0f, 1f)] public float minIntensity = 0.2f;
    [Range(0f, 5f)] public float maxIntensity = 2.2f;
    [Range(0f, 0.5f)] public float minOuterRadius = 1.8f;
    [Range(0.5f, 10f)] public float maxOuterRadius = 6.5f;

    [Header("Bloom Mapping (Global Volume)")]
    public Volume globalVolume;
    [Range(0f, 50f)] public float minBloom = 2f;
    [Range(0f, 50f)] public float maxBloom = 18f;

    [Header("Color (RGB/�׹�)")]
    public Color colorRed = new Color(1f, 0.2f, 0.2f);
    public Color colorGreen = new Color(0.2f, 1f, 0.2f);
    public Color colorBlue = new Color(0.2f, 0.5f, 1f);
    public Color colorWhite = Color.white;

    // ״̬
    private Bloom bloom;
    private MaterialPropertyBlock mpb;
    private float energy01 = 1f;

    private PlayerLightController.LightMode currentMode =
        PlayerLightController.LightMode.White;

    // ���� �ؼ����޲μ�������ƥ�� UnityEvent������
    private UnityAction _energyListener;
    private UnityAction _modeListener;

    // ���仺�棨���׶�ȡ����/ģʽ��
    private PropertyInfo _propEnergy01, _propCurrentEnergy, _propMaxEnergy, _propMode;
    private FieldInfo _fieldCurrentEnergy, _fieldMaxEnergy;

    void Awake()
    {
        if (!plc) plc = GetComponentInChildren<PlayerLightController>() ?? GetComponent<PlayerLightController>();
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>() ?? GetComponent<SpriteRenderer>();
        if (!playerLight) playerLight = GetComponentInChildren<Light2D>();

        if (globalVolume && globalVolume.profile) globalVolume.profile.TryGet(out bloom);
        mpb = new MaterialPropertyBlock();

        // ׼�����Ƴ��ļ��������޲Σ�
        _energyListener = OnEnergyDirty;
        _modeListener = OnModeDirty;

        // ����׼����Ϊ�˼��������� PLC ���ֶ�/����������
        if (plc != null)
        {
            var t = plc.GetType();
            _propEnergy01 = t.GetProperty("Energy01") ?? t.GetProperty("NormalizedEnergy") ?? t.GetProperty("EnergyNormalized");
            _propCurrentEnergy = t.GetProperty("currentEnergy") ?? t.GetProperty("CurrentEnergy");
            _propMaxEnergy = t.GetProperty("maxEnergy") ?? t.GetProperty("MaxEnergy");
            _propMode = t.GetProperty("CurrentMode") ?? t.GetProperty("Mode");

            _fieldCurrentEnergy = t.GetField("currentEnergy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _fieldMaxEnergy = t.GetField("maxEnergy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    void OnEnable()
    {
        if (plc != null)
        {
            plc.onEnergyChanged.AddListener(_energyListener);  // �޲� UnityEvent
            plc.onModeChanged.AddListener(_modeListener);      // �޲� UnityEvent
        }

        // ��ʼ��һ��
        PullEnergyAndMode();
        ApplyColorByMode();
        ApplyVisuals();
    }

    void OnDisable()
    {
        if (plc != null)
        {
            plc.onEnergyChanged.RemoveListener(_energyListener);
            plc.onModeChanged.RemoveListener(_modeListener);
        }
    }

    // ���� �޲λص����¼�����ʱ����ȡ����ǰֵ ����
    private void OnEnergyDirty() { PullEnergy(); ApplyVisuals(); }
    private void OnModeDirty() { PullMode(); ApplyColorByMode(); }

    private void PullEnergyAndMode()
    {
        PullEnergy();
        PullMode();
    }

    private void PullEnergy()
    {
        // 1) ���ȶ� Energy01 ���ԣ�0..1��
        if (plc == null) { energy01 = 1f; return; }
        if (_propEnergy01 != null)
        {
            object v = _propEnergy01.GetValue(plc, null);
            if (v is float f) { energy01 = Mathf.Clamp01(f); return; }
        }

        // 2) �˶�����Σ�currentEnergy / maxEnergy
        float cur = TryGetFloatPropOrField(_propCurrentEnergy, _fieldCurrentEnergy, plc, 1f);
        float max = TryGetFloatPropOrField(_propMaxEnergy, _fieldMaxEnergy, plc, 1f);
        energy01 = max > 0.0001f ? Mathf.Clamp01(cur / max) : 1f;
    }

    private void PullMode()
    {
        if (plc == null) return;

        // 1) ���ȶ� CurrentMode / Mode ���ԣ�ö�٣�
        if (_propMode != null)
        {
            object v = _propMode.GetValue(plc, null);
            if (v != null && v is Enum)
            {
                try { currentMode = (PlayerLightController.LightMode)v; return; }
                catch { /* ��������ռ䲻ͬҲ����� */ }
            }
        }
        // 2) ʵ��û�У��Ͳ��ģ������ϴεģ�
    }

    private float TryGetFloatPropOrField(PropertyInfo p, FieldInfo f, object o, float fallback)
    {
        try
        {
            if (p != null)
            {
                object v = p.GetValue(o, null);
                if (v is float pf) return pf;
            }
            if (f != null)
            {
                object v = f.GetValue(o);
                if (v is float ff) return ff;
            }
        }
        catch { }
        return fallback;
    }

    private void ApplyVisuals()
    {
        if (playerLight)
        {
            playerLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, energy01);
            playerLight.pointLightOuterRadius = Mathf.Lerp(minOuterRadius, maxOuterRadius, energy01);
            playerLight.pointLightInnerRadius = playerLight.pointLightOuterRadius * 0.35f;
        }

        if (sr)
        {
            sr.GetPropertyBlock(mpb);
            var baseCol = sr.color;
            var emis = baseCol * Mathf.Lerp(0.0f, 1.5f, energy01);
            mpb.SetColor("_EmissionColor", emis);
            sr.SetPropertyBlock(mpb);
        }

        if (bloom != null)
        {
            bloom.intensity.overrideState = true;
            bloom.intensity.value = Mathf.Lerp(minBloom, maxBloom, energy01);
        }
    }

    private void ApplyColorByMode()
    {
        Color target = colorWhite;
        switch (currentMode)
        {
            case PlayerLightController.LightMode.Red: target = colorRed; break;
            case PlayerLightController.LightMode.Green: target = colorGreen; break;
            case PlayerLightController.LightMode.Blue: target = colorBlue; break;
            case PlayerLightController.LightMode.White: target = colorWhite; break;
        }

        if (playerLight) playerLight.color = target;

        if (sr)
        {
            sr.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", target);
            sr.SetPropertyBlock(mpb);
        }
    }
}
