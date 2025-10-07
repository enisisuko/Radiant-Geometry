
using System.Collections;
using UnityEngine;

namespace FadedDreams.Player
{
    /// <summary>
    /// 小光剑（伴随剑）—保持原有 Idle/Attach/Flourish 逻辑；
    /// 已与玩家颜色联动，不依赖 Light2D；用于衔接动作视觉上的“连贯”与“收招”。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class CompanionBlade : MonoBehaviour
    {
        [Header("Refs")]
        public Transform player;
        public PlayerColorModeController _pcm;

        [Header("Line Style")]
        public Material lineMaterial;
        public float lineWidth = 0.06f;
        public float bladeLength = 1.2f;

        [Header("Idle Orbit")]
        public float idleRadius = 0.85f;
        public float sideOffset = 0.10f;
        public float followLerp = 10f;
        public float attachLerp = 16f;
        public float tipFollowLerp = 22f;
        public float orbitAngularSpeed = 240f;
        public float idleSwayAmp = 0.07f;
        public float idleSwayFreq = 3.0f;

        [Header("Flourish（阶段切换小旋转）")]
        public float flourishAngle = 260f;
        public float flourishTime = 0.22f;

        [Header("Color (auto by Mode)")]
        public Color redCore = new Color(1.00f, 0.28f, 0.22f, 1f);
        public Color redGlow = new Color(1.00f, 0.48f, 0.18f, 0.95f);
        public Color greenCore = new Color(0.70f, 1.00f, 0.80f, 1f);
        public Color greenGlow = new Color(0.20f, 1.00f, 0.60f, 0.95f);

        [Header("Sorting")]
        public int sortingOrderCore = 25;
        public int sortingOrderGlow1 = 24;
        public int sortingOrderGlow2 = 23;

        // runtime
        private LineRenderer _core, _g1, _g2;
        private Transform _attachTarget;
        private bool _hasTipTarget;
        private Vector3 _tipTarget;
        private ColorMode _lastMode;
        private float _orbitAngle;

        private Coroutine _flourishCo;

        private void Awake()
        {
            if (!player) player = GetComponentInParent<Transform>();
            if (!_pcm) _pcm = GetComponentInParent<PlayerColorModeController>();

            _core = GetComponent<LineRenderer>();
            SetupLR(_core, sortingOrderCore);
            _g1 = CreateLR("_Glow1", sortingOrderGlow1);
            _g2 = CreateLR("_Glow2", sortingOrderGlow2);
        }

        private void OnEnable()
        {
            if (!_pcm) _pcm = GetComponentInParent<PlayerColorModeController>();
            if (_pcm != null)
            {
                _pcm.OnModeChanged.AddListener(OnModeChanged);
                OnModeChanged(_pcm.Mode);
            }
        }

        private void OnDisable()
        {
            if (_pcm != null) _pcm.OnModeChanged.RemoveListener(OnModeChanged);
        }

        private void OnModeChanged(ColorMode mode)
        {
            _lastMode = mode;
            ApplyPalette(mode);
        }

        private void ApplyPalette(ColorMode mode)
        {
            Color core = (mode == ColorMode.Red) ? redCore : greenCore;
            Color glow = (mode == ColorMode.Red) ? redGlow : greenGlow;

            _core.colorGradient = MakeGradient(core, 1f);
            _g1.colorGradient = MakeGradient(Color.Lerp(core, glow, 0.6f), 0.85f);
            _g2.colorGradient = MakeGradient(Color.Lerp(core, glow, 0.85f), 0.65f);
        }

        private void SetupLR(LineRenderer lr, int sorting)
        {
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 8;
            lr.numCornerVertices = 4;
            lr.widthMultiplier = 1f;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = sorting;
            if (lineMaterial) lr.sharedMaterial = lineMaterial;
        }

        private LineRenderer CreateLR(string name, int sorting)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            SetupLR(lr, sorting);
            if (lineMaterial) lr.sharedMaterial = lineMaterial;
            return lr;
        }

        private void Update()
        {
            if (_attachTarget)
            {
                Vector3 targetPos = _attachTarget.position;
                transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-attachLerp * Time.unscaledDeltaTime));
                if (_hasTipTarget)
                {
                    transform.position = Vector3.Lerp(transform.position, _tipTarget, 1f - Mathf.Exp(-tipFollowLerp * Time.unscaledDeltaTime));
                }
            }
            else
            {
                if (!player) return;
                _orbitAngle += orbitAngularSpeed * Mathf.Deg2Rad * Time.unscaledDeltaTime;

                Vector3 world = GetMouseWorld(player.position);
                Vector2 aim = ((Vector2)(world - player.position)).normalized;
                Vector2 right = new Vector2(-aim.y, aim.x);

                float sway = Mathf.Sin(Time.unscaledTime * idleSwayFreq) * idleSwayAmp;
                Vector2 orbitOffset = Quaternion.Euler(0, 0, _orbitAngle * Mathf.Rad2Deg) * right * (idleRadius * 0.15f);

                Vector3 idle = player.position + (Vector3)(aim * idleRadius + right * sideOffset + (Vector2)orbitOffset + new Vector2(0f, sway));
                transform.position = Vector3.Lerp(transform.position, idle, 1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime));
            }

            DrawMiniBlade();
        }

        private void DrawMiniBlade()
        {
            if (!player) return;

            Vector3 p0 = player.position;
            Vector3 target = _hasTipTarget ? _tipTarget : GetMouseWorld(player.position);
            Vector3 dir = (target - p0);
            if (dir.sqrMagnitude < 1e-4f) dir = transform.right;
            dir.Normalize();

            Vector3 p1 = p0 + dir * bladeLength;

            transform.position = (p0 + p1) * 0.5f;
            transform.right = dir;

            _core.positionCount = 2; _core.SetPosition(0, p0); _core.SetPosition(1, p1);
            _core.startWidth = lineWidth; _core.endWidth = lineWidth * 0.92f;

            _g1.positionCount = 2; _g1.SetPosition(0, p0); _g1.SetPosition(1, p1);
            _g1.startWidth = lineWidth * 1.6f; _g1.endWidth = lineWidth * 1.5f;

            _g2.positionCount = 2; _g2.SetPosition(0, p0); _g2.SetPosition(1, p1);
            _g2.startWidth = lineWidth * 2.2f; _g2.endWidth = lineWidth * 2.1f;
        }

        private Gradient MakeGradient(Color c, float a = 1f)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(a * 0.9f, 1f) }
            );
            return g;
        }

        private static Vector3 GetMouseWorld(Vector3 zRef)
        {
            var cam = Camera.main;
            Vector3 mp = Input.mousePosition;
            float depth = Mathf.Abs((cam ? cam.transform.position.z : 0f) - zRef.z);
            mp.z = depth <= 0.001f ? 10f : depth;
            Vector3 world = cam ? cam.ScreenToWorldPoint(mp) : zRef + Vector3.right;
            world.z = zRef.z;
            return world;
        }

        // ====== Public API ======
        public void AttachTo(Transform t)
        {
            _attachTarget = t;
            _hasTipTarget = false;
            if (_flourishCo != null) { StopCoroutine(_flourishCo); _flourishCo = null; }
        }

        public void FollowTip(Vector3 tipWorldPos)
        {
            _tipTarget = tipWorldPos;
            _hasTipTarget = true;
        }

        public void ReturnToOrbitDelayed(float delay)
        {
            StopAllCoroutines();
            StartCoroutine(CoReturn(delay));
        }

        public void TransitionFlourish(Vector2 hintDir)
        {
            if (_flourishCo != null) StopCoroutine(_flourishCo);
            _flourishCo = StartCoroutine(CoFlourish(hintDir));
        }

        private IEnumerator CoReturn(float delay)
        {
            yield return new WaitForSecondsRealtime(delay * 0.5f);
            _attachTarget = null;
            _hasTipTarget = false;
        }

        private IEnumerator CoFlourish(Vector2 hintDir)
        {
            if (!player) yield break;
            float t = 0f;
            float dur = Mathf.Max(0.12f, flourishTime);
            float startAng = Vector2.SignedAngle(Vector2.right, hintDir.sqrMagnitude < 1e-4f ? Vector2.right : hintDir.normalized);
            float endAng = startAng + Mathf.Sign(flourishAngle) * Mathf.Abs(flourishAngle);

            Vector3 center = player.position;
            float r = idleRadius * 0.6f;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float rot = Mathf.Lerp(startAng, endAng, Mathf.SmoothStep(0f, 1f, u));
                Vector2 pos = (Vector2)center + new Vector2(Mathf.Cos(rot * Mathf.Deg2Rad), Mathf.Sin(rot * Mathf.Deg2Rad)) * r;
                transform.position = pos;
                transform.right = (GetMouseWorld(center) - (Vector3)pos).normalized;
                yield return null;
            }
        }
    }
}
