using System.Collections;
using UnityEngine;

namespace FadedDreams.UI
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ProjectedMenuItem : MonoBehaviour
    {
        public enum ActionType { NewGame, Continue, Quit, None }

        [Header("Material Keys")]
        public string stretchKey = "_Stretch";
        public string skewKey = "_Skew";
        public string shatterKey = "_Shatter";
        public string intensityKey = "_Intensity";

        [Header("Hover (legacy)")]
        public float hoverStretch = 1.35f;
        public float hoverSkew = 0.25f;
        public float lerpSpeed = 8f;

        [Header("Selected")]
        public float selectedBoost = 0.5f;

        [Header("Primary Action")]
        public ActionType actionType = ActionType.None;
        public MainMenu mainMenu; // New/Continue/Quit

        [Header("Shatter Effect")]
        public ParticleSystem shatterVfxPrefab;

        [Header("Spotlight (optional)")]
        public Light spot;
        [Tooltip("常态强度")]
        public float spotBase = 2.5f;
        [Tooltip("悬停强度")]
        public float spotHover = 3.2f;
        [Tooltip("选中强度")]
        public float spotSelected = 3.8f;
        public float spotLerp = 8f;
        [Tooltip("聚光灯相对按钮的高度")]
        public float spotHeight = 2.2f;

        [Header("Spot Color/Shape")]
        [Tooltip("常态颜色")]
        public Color spotColorBase = Color.white;
        [Tooltip("悬停时缓慢变为这个绿色")]
        public Color spotColorHover = new Color(0.55f, 1.0f, 0.55f, 1f);
        [Tooltip("颜色渐变速度")]
        public float spotColorLerp = 5f;

        [Tooltip("常态锥角（°）")]
        public float spotAngleBase = 40f;
        [Tooltip("悬停时略微变小的锥角（°）")]
        public float spotAngleHover = 34f;
        [Tooltip("角度/距离插值速度")]
        public float spotShapeLerp = 6f;

        [Tooltip("常态照明范围（米）")]
        public float spotRangeBase = 12f;
        [Tooltip("悬停时略微变小的范围（米）")]
        public float spotRangeHover = 10f;

        [Header("VIS Root (recommended)")]
        [Tooltip("只对这个子物体做放大/颤抖，避免把 Collider 一起放大。为空则作用在自身。")]
        public Transform visualRoot;

        [Header("Hover Scale + Shake + Glow")]
        public float hoverScale = 1.08f;
        public float scaleLerp = 4f;
        public float shakePosAmp = 0.02f;
        public float shakeRotAmp = 0.8f;
        public float shakeFreq = 9f;
        public float shakeOnsetLerp = 3.0f;
        public float breathAmp = 0.4f;
        public float breathFreq = 0.8f;
        public float breathOnsetLerp = 2.0f;

        // —— 全局“区域调暗”乘数（由 Orchestrator 设置）——
        public static float GlobalSpotMul { get; private set; } = 1f;
        public static void SetGlobalSpotMul(float mul) => GlobalSpotMul = Mathf.Clamp(mul, 0f, 2f);

        // 状态
        [SerializeField] bool interactable = true;
        float visibleAlpha = 1f;
        float disabledMul = 0.45f;
        float skewSign = 1f;

        // runtime
        MeshRenderer mr;
        Material mat;
        ProjectedMenuController controller;

        float baseStretch = 1f;
        float baseSkew = 0f;
        float baseIntensity = 2f;
        float curStretch, curSkew, curIntensity, curShatter;

        // 悬停增强：缩放/颤抖/呼吸
        Transform vis;
        Vector3 visBaseLocalPos;
        Quaternion visBaseLocalRot;
        Vector3 visBaseLocalScale;
        float hoverWeight;   // 0~1
        float shakeWeight;   // 0~1
        float breathWeight;  // 0~1
        float nX, nY, nZ;    // Perlin 噪声种子

        // 基础位置
        Vector3 basePos;
        Coroutine slideCo;

        // 旧 push（保留）
        Vector3 vel;
        bool pushed;
        float pushDamp = 6f;
        float pushSpring = 12f;
        Vector3 targetPos;

        bool isHover = false;
        bool isSelected = false;

        // —— 冻结滑动 —— 
        bool freezeSlide;
        Vector3 freezeOffsetWorld;
        public float freezeFollowLerp = 20f; // 可在 Inspector 调

        void Awake()
        {
            mr = GetComponent<MeshRenderer>();
            mat = mr.material;

            curStretch = baseStretch = mat.HasProperty(stretchKey) ? mat.GetFloat(stretchKey) : 1f;
            curSkew = baseSkew = mat.HasProperty(skewKey) ? mat.GetFloat(skewKey) : 0f;
            curIntensity = baseIntensity = mat.HasProperty(intensityKey) ? mat.GetFloat(intensityKey) : 2f;

            basePos = transform.position;

            vis = visualRoot ? visualRoot : transform;
            visBaseLocalPos = vis.localPosition;
            visBaseLocalRot = vis.localRotation;
            visBaseLocalScale = vis.localScale;

            nX = Random.value * 1000f;
            nY = Random.value * 1000f;
            nZ = Random.value * 1000f;

            if (spot)
            {
                spot.type = LightType.Spot;
                spot.spotAngle = spotAngleBase;
                spot.range = spotRangeBase;
                Vector3 p = transform.position; p.y += spotHeight;
                spot.transform.position = p;
                spot.transform.rotation = Quaternion.LookRotation((transform.position - spot.transform.position).normalized, Vector3.up);
                spot.intensity = spotBase * GlobalSpotMul;
                spot.color = spotColorBase;
                spot.shadows = LightShadows.None;
            }
        }

        public void BindController(ProjectedMenuController c) => controller = c;

        public void SetHover(bool v) => isHover = v;
        public void EnterSelectedState() => isSelected = true;
        public void ExitSelectedState()
        {
            isSelected = false;
            curShatter = 0;
            if (mat && mat.HasProperty(shatterKey)) mat.SetFloat(shatterKey, 0);
        }

        public void SetInteractable(bool v) { interactable = v; }
        public void SetSkewSign(float s) { skewSign = Mathf.Sign(s == 0 ? 1 : s); }

        public void SetVisible(bool v, float fadeDuration = 0.3f)
        {
            StopCoroutineSafe(ref slideCo);
            StartCoroutine(FadeVisible(v ? 1f : 0f, fadeDuration));
        }
        IEnumerator FadeVisible(float target, float dur)
        {
            float start = visibleAlpha;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                visibleAlpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, t / dur));
                yield return null;
            }
            visibleAlpha = target;
        }

        // 冻结 API
        public void FreezeSlide(Vector3 worldOffsetFromBase)
        {
            freezeSlide = true;
            freezeOffsetWorld = worldOffsetFromBase;
        }
        public void UnfreezeSlide() { freezeSlide = false; }

        // Shatter
        public void PlayShatterBrief(float dur = 0.3f)
        {
            if (shatterVfxPrefab) Instantiate(shatterVfxPrefab, transform.position, transform.rotation);
            StartCoroutine(CoShatterOnce(dur));
        }
        IEnumerator CoShatterOnce(float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                curShatter = Mathf.SmoothStep(0, 1, t / dur);
                if (mat && mat.HasProperty(shatterKey)) mat.SetFloat(shatterKey, curShatter);
                if (mat && mat.HasProperty(intensityKey)) mat.SetFloat(intensityKey,
                    Mathf.Lerp(baseIntensity + (isSelected ? selectedBoost : 0), 0f, t / dur));
                yield return null;
            }
        }

        void Update()
        {
            // 悬停权重
            hoverWeight = Mathf.MoveTowards(hoverWeight, (isHover && interactable) ? 1f : 0f, Time.deltaTime * scaleLerp);
            shakeWeight = Mathf.MoveTowards(shakeWeight, (isHover && interactable) ? 1f : 0f, Time.deltaTime * shakeOnsetLerp);
            breathWeight = Mathf.MoveTowards(breathWeight, (isHover && interactable) ? 1f : 0f, Time.deltaTime * breathOnsetLerp);

            // 材质 + 呼吸
            float targetStretch = (isHover ? hoverStretch : baseStretch);
            float targetSkew = (isHover ? hoverSkew : baseSkew) * skewSign;

            float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * Mathf.Max(0.01f, breathFreq));
            float breathAdd = breathAmp * breath * breathWeight;

            float baseInt = baseIntensity + (isSelected ? selectedBoost : 0);
            float targetIntensity = baseInt + breathAdd;

            targetStretch = Mathf.Lerp(baseStretch, targetStretch, visibleAlpha) * (interactable ? 1f : disabledMul);
            targetSkew = Mathf.Lerp(baseSkew, targetSkew, visibleAlpha);
            targetIntensity = Mathf.Lerp(0f, targetIntensity, visibleAlpha) * (interactable ? 1f : disabledMul);

            curStretch = Mathf.Lerp(curStretch, targetStretch, Time.deltaTime * lerpSpeed);
            curSkew = Mathf.Lerp(curSkew, targetSkew, Time.deltaTime * lerpSpeed);
            curIntensity = Mathf.Lerp(curIntensity, targetIntensity, Time.deltaTime * lerpSpeed);

            if (mat)
            {
                if (mat.HasProperty(stretchKey)) mat.SetFloat(stretchKey, curStretch);
                if (mat.HasProperty(skewKey)) mat.SetFloat(skewKey, curSkew);
                if (mat.HasProperty(intensityKey)) mat.SetFloat(intensityKey, curIntensity);
            }

            // 悬停放大/颤抖
            if (vis)
            {
                Vector3 targetScale = visBaseLocalScale * Mathf.Lerp(1f, Mathf.Max(1.0f, hoverScale), hoverWeight);
                vis.localScale = Vector3.Lerp(vis.localScale, targetScale, Time.deltaTime * scaleLerp);

                float t = Time.time * Mathf.Max(0.01f, shakeFreq);
                float ax = (Mathf.PerlinNoise(nX, t) - 0.5f) * 2f;
                float ay = (Mathf.PerlinNoise(nY, t * 0.93f) - 0.5f) * 2f;
                float az = (Mathf.PerlinNoise(nZ, t * 1.07f) - 0.5f) * 2f;

                Vector3 posJit = new Vector3(ax, ay, 0f) * (shakePosAmp * shakeWeight);
                Vector3 rotJit = new Vector3(ay, az, ax) * (shakeRotAmp * shakeWeight);

                vis.localPosition = Vector3.Lerp(vis.localPosition, visBaseLocalPos + posJit, Time.deltaTime * (shakeOnsetLerp + 2f));
                vis.localRotation = Quaternion.Slerp(vis.localRotation, visBaseLocalRot * Quaternion.Euler(rotJit), Time.deltaTime * (shakeOnsetLerp + 2f));
            }

            // 顶灯：定位 + 强度 + 颜色 + 形状
            if (spot)
            {
                Vector3 p = transform.position; p.y += spotHeight;
                spot.transform.position = Vector3.Lerp(spot.transform.position, p, Time.deltaTime * 8f);
                spot.transform.rotation = Quaternion.Slerp(spot.transform.rotation,
                    Quaternion.LookRotation((transform.position - spot.transform.position).normalized, Vector3.up),
                    Time.deltaTime * 8f);

                float baseI = (isSelected ? spotSelected : (isHover ? spotHover : spotBase));
                float targetI = baseI * GlobalSpotMul;
                spot.intensity = Mathf.Lerp(spot.intensity, targetI, Time.deltaTime * spotLerp);

                Color targetC = isHover ? spotColorHover : spotColorBase;
                spot.color = Color.Lerp(spot.color, targetC, Time.deltaTime * spotColorLerp);

                float targetAngle = isHover ? spotAngleHover : spotAngleBase;
                spot.spotAngle = Mathf.Lerp(spot.spotAngle, targetAngle, Time.deltaTime * spotShapeLerp);

                float targetRange = isHover ? spotRangeHover : spotRangeBase;
                spot.range = Mathf.Lerp(spot.range, targetRange, Time.deltaTime * spotShapeLerp);
            }

            // 冻结位置纠正
            if (freezeSlide)
            {
                Vector3 want = basePos + freezeOffsetWorld;
                transform.position = Vector3.Lerp(transform.position, want, Time.deltaTime * freezeFollowLerp);
            }

            if (pushed) TickSpring();
        }

        // ======= 滑动 API =======
        public void SlideToOffset(Vector3 worldOffset, float dur, AnimationCurve ease)
        {
            StopCoroutineSafe(ref slideCo);
            Vector3 from = transform.position;
            Vector3 to = basePos + worldOffset;
            slideCo = StartCoroutine(CoSlide(from, to, dur, ease));
        }

        public void SlideBack(float dur, AnimationCurve ease)
        {
            if (freezeSlide) return; // 冻结时忽略回位
            StopCoroutineSafe(ref slideCo);
            Vector3 from = transform.position;
            Vector3 to = basePos;
            slideCo = StartCoroutine(CoSlide(from, to, dur, ease));
        }

        IEnumerator CoSlide(Vector3 from, Vector3 to, float dur, AnimationCurve ease)
        {
            float zeta = 0.55f;
            float omega = 9.5f;
            Vector3 v = Vector3.zero;
            float t = 0;

            transform.position = from;

            while (t < dur)
            {
                t += Time.deltaTime;
                Vector3 x = transform.position;
                Vector3 a = -2f * zeta * omega * v + (to - x) * (omega * omega);
                v += a * Time.deltaTime;
                x += v * Time.deltaTime;
                transform.position = x;
                yield return null;
            }
            transform.position = to;
        }

        void StopCoroutineSafe(ref Coroutine c)
        {
            if (c != null) { StopCoroutine(c); c = null; }
        }

        // ======= 旧 push（保留）=======
        public void PushFrom(Vector3 source, float force, float radius)
        {
            Vector3 dir = (transform.position - source);
            float dist = dir.magnitude;
            float amp = Mathf.Clamp01(1f - dist / radius);
            if (amp <= 0) return;
            dir = dir.normalized;

            targetPos = basePos + dir * (force * amp);
            pushed = true;
        }

        public void ReleasePush() { pushed = false; }

        public void TickSpring()
        {
            Vector3 dest = pushed ? targetPos : basePos;
            Vector3 delta = dest - transform.position;
            vel += delta * (pushSpring * Time.deltaTime);
            vel *= Mathf.Exp(-pushDamp * Time.deltaTime);
            transform.position += vel * Time.deltaTime;
        }
    }
}
