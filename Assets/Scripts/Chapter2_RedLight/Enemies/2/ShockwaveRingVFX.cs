// Assets/Scripts/Bosses/Chapter2/ShockwaveRingVFX.cs
using System.Collections;
using UnityEngine;

namespace FadedDreams.Bosses
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShockwaveRingVFX : MonoBehaviour
    {
        public float startScale = 0.3f;
        public float endScale = 4.2f;
        public float life = 0.35f;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            StartCoroutine(CoPlay());
        }

        private IEnumerator CoPlay()
        {
            float t = 0f;
            Color baseC = _sr.color;
            while (t < life)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, life));
                float s = Mathf.Lerp(startScale, endScale, scaleCurve.Evaluate(k));
                float a = alphaCurve.Evaluate(k);
                transform.localScale = Vector3.one * s;
                _sr.color = new Color(baseC.r, baseC.g, baseC.b, a);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
