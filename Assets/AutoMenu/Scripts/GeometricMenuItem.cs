using UnityEngine;

[DisallowMultipleComponent]
public class GeometricMenuItem : MonoBehaviour
{
    [Header("Visuals")]
    public Renderer targetRenderer;
    public Transform visualRoot;
    public float baseEmission = 1.2f;
    public float hoverEmissionMul = 2.2f;
    public float confirmEmissionMul = 3.5f;

    [Header("Breath (Idle)")]
    public bool idleBreath = true;
    public float breathSpeed = 0.7f;
    public float breathScaleAmp = 0.035f;
    public float hoverScale = 1.06f;
    public float confirmScale = 1.15f;

    [Header("Optional Availability")]
    public bool interactable = true;

    MaterialPropertyBlock _mpb;
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    Color _cachedEmissionColor = Color.white;
    float _hoverLerp;
    float _confirmPulse;
    Vector3 _initScale;

    void Reset()
    {
        targetRenderer = GetComponentInChildren<Renderer>();
        if (!visualRoot) visualRoot = transform;
    }

    void Awake()
    {
        if (!targetRenderer) targetRenderer = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _initScale = visualRoot ? visualRoot.localScale : Vector3.one;

        if (targetRenderer && targetRenderer.sharedMaterial && targetRenderer.sharedMaterial.HasProperty(EmissionColorId))
        {
            _cachedEmissionColor = targetRenderer.sharedMaterial.GetColor(EmissionColorId);
        }
    }

    public void SetHovered(bool hovered)
    {
        _hoverLerp = Mathf.MoveTowards(_hoverLerp, hovered ? 1f : 0f, Time.deltaTime * 6f);
    }

    public void TriggerConfirmPulse()
    {
        _confirmPulse = 1f;
    }

    public void SetInteractable(bool can)
    {
        interactable = can;
        if (!interactable) _hoverLerp = 0f;
    }

    void LateUpdate()
    {
        float emissionMul = Mathf.Lerp(1f, hoverEmissionMul, _hoverLerp);
        if (_confirmPulse > 0f)
        {
            float pulse = Mathf.SmoothStep(0f, 1f, _confirmPulse);
            emissionMul = Mathf.Lerp(emissionMul, confirmEmissionMul, pulse);
            _confirmPulse = Mathf.MoveTowards(_confirmPulse, 0f, Time.deltaTime * 2.5f);
        }

        Color finalEmission = _cachedEmissionColor * (baseEmission * emissionMul);
        if (!interactable) finalEmission *= 0.35f;

        if (targetRenderer)
        {
            _mpb.Clear();
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, finalEmission);
            targetRenderer.SetPropertyBlock(_mpb);
        }

        if (visualRoot)
        {
            float breath = idleBreath ? (1f + Mathf.Sin(Time.time * breathSpeed) * breathScaleAmp) : 1f;
            float scale = breath * Mathf.Lerp(1f, hoverScale, _hoverLerp);
            if (_confirmPulse > 0f) scale = Mathf.Lerp(scale, confirmScale, Mathf.SmoothStep(0, 1, _confirmPulse));
            visualRoot.localScale = _initScale * scale;
        }
    }
}
