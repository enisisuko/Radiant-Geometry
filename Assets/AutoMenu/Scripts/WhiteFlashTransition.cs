using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class WhiteFlashTransition : MonoBehaviour
{
    public Image flashImage;
    public float riseTime = 0.15f;
    public float holdTime = 0.05f;
    public float fallTime = 0.35f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0,0, 1,1);

    CanvasGroup _cg;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        _cg.alpha = 0f;
        if (!flashImage) flashImage = GetComponentInChildren<Image>(true);
    }

    public void Blast(Action onPeak = null)
    {
        StopAllCoroutines();
        StartCoroutine(Co_Blast(onPeak));
    }

    IEnumerator Co_Blast(Action onPeak)
    {
        float t = 0;
        while (t < riseTime)
        {
            t += Time.unscaledDeltaTime;
            float a = curve.Evaluate(Mathf.Clamp01(t / riseTime));
            _cg.alpha = a;
            yield return null;
        }
        _cg.alpha = 1f;

        if (holdTime > 0f) yield return new WaitForSecondsRealtime(holdTime);

        onPeak?.Invoke();

        t = 0;
        while (t < fallTime)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - curve.Evaluate(Mathf.Clamp01(t / fallTime));
            _cg.alpha = a;
            yield return null;
        }
        _cg.alpha = 0f;
    }
}
