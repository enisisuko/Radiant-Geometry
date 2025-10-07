// SlashVFXBooster.cs  （可选）
using UnityEngine;
using UnityEngine.UI;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class SlashVFXBooster : MonoBehaviour
{
#if UNITY_RENDERING_UNIVERSAL
    public Volume postVolume;   // 拖有 Bloom/ChromaticAberration/Vignette 的 Volume
#endif
    public Image whiteFlash;    // 全屏 Image（白色），CanvasGroup 或直接改 Color.a

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

    // 绑定到 Chapter1SpaceSlashController.onShakeWhileHolding(strength)
    public void OnHoldShakeStrength(float s)
    {
#if UNITY_RENDERING_UNIVERSAL
        if (ca) ca.intensity.value = Mathf.Clamp01(s * 0.35f);
        if (vig) vig.intensity.value = Mathf.Clamp01(s * 0.2f);
#endif
    }

    // 绑定到 Chapter1SpaceSlashController.onSweepBlast()
    public void OnSweepBlast()
    {
#if UNITY_RENDERING_UNIVERSAL
        if (bloom) bloom.intensity.value = 18f;  // 先拉满，Update 里回落
        if (ca) ca.intensity.value = 0.6f;
        if (vig) vig.intensity.value = 0.35f;
#endif
        flashT = 1f; // 白闪开始
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
