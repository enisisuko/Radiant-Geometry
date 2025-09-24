using FadedDreams.Player;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloomBreathByEnergy : MonoBehaviour
{
    public PlayerLightController plc;   // 无参 UnityEvent
    public Volume volume;

    [Range(0f, 2f)] public float pulseSpeedAtLowEnergy = 1.2f;
    [Range(0f, 0.8f)] public float pulseAmplitude = 0.4f;

    private Bloom bloom;
    private float energy01 = 1f;

    private UnityAction _energyListener;   // 无参
    private PropertyInfo _propEnergy01, _propCur, _propMax;
    private FieldInfo _fieldCur, _fieldMax;

    void Awake()
    {
        if (!volume) volume = GetComponent<Volume>();
        if (volume && volume.profile) volume.profile.TryGet(out bloom);

        _energyListener = PullEnergy;   // 无参监听器

        if (plc != null)
        {
            var t = plc.GetType();
            _propEnergy01 = t.GetProperty("Energy01") ?? t.GetProperty("NormalizedEnergy") ?? t.GetProperty("EnergyNormalized");
            _propCur = t.GetProperty("currentEnergy") ?? t.GetProperty("CurrentEnergy");
            _propMax = t.GetProperty("maxEnergy") ?? t.GetProperty("MaxEnergy");
            _fieldCur = t.GetField("currentEnergy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _fieldMax = t.GetField("maxEnergy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    void OnEnable()
    {
        if (plc != null) plc.onEnergyChanged.AddListener(_energyListener);
        PullEnergy(); // 初始化
    }

    void OnDisable()
    {
        if (plc != null) plc.onEnergyChanged.RemoveListener(_energyListener);
    }

    private void PullEnergy()
    {
        if (plc == null) { energy01 = 1f; return; }

        if (_propEnergy01 != null)
        {
            object v = _propEnergy01.GetValue(plc, null);
            if (v is float f) { energy01 = Mathf.Clamp01(f); return; }
        }

        float cur = TryGetFloatPropOrField(_propCur, _fieldCur, plc, 1f);
        float max = TryGetFloatPropOrField(_propMax, _fieldMax, plc, 1f);
        energy01 = max > 0.0001f ? Mathf.Clamp01(cur / max) : 1f;
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

    void Update()
    {
        if (bloom == null) return;

        // 低能更明显/更快，高能几乎无脉动
        float t = Time.time * Mathf.Lerp(0.15f, pulseSpeedAtLowEnergy, 1f - energy01);
        float add = Mathf.Sin(t) * pulseAmplitude * (1f - energy01);

        // 在当前 Bloom 上做微小比例波动
        bloom.intensity.value = Mathf.Max(0f, bloom.intensity.value * (1f + add));
    }
}
