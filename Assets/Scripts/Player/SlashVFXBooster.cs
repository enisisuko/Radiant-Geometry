// SlashVFXBooster.cs  ����ѡ��
using UnityEngine;
using UnityEngine.UI;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class SlashVFXBooster : MonoBehaviour
{
#if UNITY_RENDERING_UNIVERSAL
    public Volume postVolume;   // ���� Bloom/ChromaticAberration/Vignette �� Volume
#endif
    public Image whiteFlash;    // ȫ�� Image����ɫ����CanvasGroup ��ֱ�Ӹ� Color.a

#if UNITY_RENDERING_UNIVERSAL
    Bloom bloom; ChromaticAberration ca; Vignette vig;
#endif
    float flashT;

    void Start()
    {
#if UNITY_RENDERING_UNIVERSAL
        if (postVolume && postVolume.profile)
        {
            postVolume.profile.TryGet(out bloom);
            postVolume.profile.TryGet(out ca);
            postVolume.profile.TryGet(out vig);
        }
#endif
    }

    // �󶨵� Chapter1SpaceSlashController.onShakeWhileHolding(strength)
    public void OnHoldShakeStrength(float s)
    {
#if UNITY_RENDERING_UNIVERSAL
        if (ca) ca.intensity.value = Mathf.Clamp01(s * 0.35f);
        if (vig) vig.intensity.value = Mathf.Clamp01(s * 0.2f);
#endif
    }

    // �󶨵� Chapter1SpaceSlashController.onSweepBlast()
    public void OnSweepBlast()
    {
#if UNITY_RENDERING_UNIVERSAL
        if (bloom) bloom.intensity.value = 18f;  // ��������Update �����
        if (ca) ca.intensity.value = 0.6f;
        if (vig) vig.intensity.value = 0.35f;
#endif
        flashT = 1f; // ������ʼ
    }

    void Update()
    {
#if UNITY_RENDERING_UNIVERSAL
        if (bloom) bloom.intensity.value = Mathf.Lerp(bloom.intensity.value, 0.8f, Time.deltaTime * 6f);
        if (ca) ca.intensity.value = Mathf.Lerp(ca.intensity.value, 0.05f, Time.deltaTime * 4f);
        if (vig) vig.intensity.value = Mathf.Lerp(vig.intensity.value, 0.12f, Time.deltaTime * 4f);
#endif
        if (whiteFlash)
        {
            if (flashT > 0f) flashT -= Time.unscaledDeltaTime * 2.2f;
            var c = whiteFlash.color; c.a = Mathf.Max(0f, flashT);
            whiteFlash.color = c;
        }
    }
}
