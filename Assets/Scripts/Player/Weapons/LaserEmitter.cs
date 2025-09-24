// LaserEmitter.cs
using FadedDreams.Enemies;
using FadedDreams.Player;
using FadedDreams.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.Player.Weapons
{
    [RequireComponent(typeof(RedLightController))]
    public class LaserEmitter : MonoBehaviour
    {
        [Header("Refs")]
        public Camera cam;
        public Transform muzzle;
        public LineRenderer beamRenderer;
        public LayerRefs layers;

        [Header("Scatter (Cone)")]
        [Range(5f, 80f)] public float scatterAngle = 30f;
        public int scatterRays = 17;
        public float scatterMaxDist = 1000f;
        public float scatterTickDamage = 0f;

        [Header("Converge (Beam)")]
        public float dps = 40f;
        public float redPerSec = 20f;
        public float beamWidth = 0.14f;
        public float hitRadius = 0.12f;

        [Header("Transitions")]
        public float collapseDuration = 1.0f;
        public float recoverScatterTime = 0.35f;

        [Header("VFX")]
        public ParticleSystem chargeVfx;          // 蓄力特效（建议短粒子+向前喷射）
        public ParticleSystem beamVfx;            // 激光持续特效（建议循环）
        public ParticleSystem hitVfxPrefab;       // 命中特效（由脚本复用为单例）
        [Tooltip("根据你粒子贴图朝向微调，默认粒子‘向上’代表0度，所以通常设 -90")]
        public float vfxAngleOffsetDeg = -90f;
        [Tooltip("VFX是否每帧跟随muzzle位置")]
        public bool vfxAttachToMuzzle = true;

        [Header("Scatter Range Guides")]
        public LineRenderer leftEdge;
        public LineRenderer rightEdge;
        public float edgeLineLength = 3f;

        [Header("Scatter (Lighting)")]
        public Light2D scatterLight;
        public float scatterInnerRadius = 50f;
        public float scatterOuterRadius = 60f;
        public float scatterInnerAngleDeg = 29f;
        public bool scatterAutoShortenByObstacle = false;
        public float scatterRadiusLerpSpeed = 20f;

        [Header("Beam (Lighting)")]
        public Light2D beamLightCore;
        public Light2D beamLightAmbient;
        public Light2D hitHaloLight;
        public float visualBleed = 0.25f;

        [Header("Scatter (Halo)")]
        public Light2D scatterHaloPrefab;
        public float scatterHaloIntensityScale = 0.33f;
        public float scatterHaloRadius = 0.8f;
        public float scatterHaloLifetime = 0.25f;
        public int scatterHaloEvery = 3;
        public float scatterHaloBleed = 0.12f;

        [Header("Red & Reflection")]
        public float beamRedPerSec = 30f;          // 命中对象每秒增加的红光（=热量）
        [Range(0, 8)] public int maxBounces = 0;   // 0=不反射
        public LayerMask mirrorMask;               // 认作“镜子”的图层
        public float bounceStepEpsilon = 0.01f;

        // runtime
        private RedLightController red;
        private bool firingBeam;
        private float _currentScatterAngle;
        private readonly Collider2D[] hitBuffer = new Collider2D[16];
        private bool _collapsing;
        private float _collapseTimer;
        private float _collapseStartAngle;

        private static Vector3[] _quad = new Vector3[4];

        // 命中粒子“单例”（复用，避免每帧 Instantiate）
        private ParticleSystem activeHitVfx;

        private void Awake()
        {
            red = GetComponent<RedLightController>();
            if (!cam) cam = Camera.main;

            if (beamRenderer)
            {
                beamRenderer.positionCount = 2;
                beamRenderer.startWidth = beamWidth;
                beamRenderer.endWidth = beamWidth;
                beamRenderer.enabled = false;
            }

            if (leftEdge)
            {
                leftEdge.useWorldSpace = true;
                leftEdge.positionCount = 2;
                leftEdge.startWidth = leftEdge.endWidth = 0.05f;
                leftEdge.enabled = false;
            }
            if (rightEdge)
            {
                rightEdge.useWorldSpace = true;
                rightEdge.positionCount = 2;
                rightEdge.startWidth = rightEdge.endWidth = 0.05f;
                rightEdge.enabled = false;
            }

            if (scatterLight)
            {
                scatterLight.enabled = true;
                scatterLight.pointLightOuterRadius = scatterOuterRadius;
                scatterLight.pointLightInnerRadius = Mathf.Clamp(scatterInnerRadius, 0f, scatterOuterRadius);
            }

            if (beamLightCore) beamLightCore.enabled = false;
            if (beamLightAmbient) beamLightAmbient.enabled = false;
            if (hitHaloLight) hitHaloLight.enabled = false;

            if (beamVfx) beamVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (chargeVfx) chargeVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _currentScatterAngle = Mathf.Max(0f, scatterAngle);
        }

        private void Update()
        {
            if (!cam) cam = Camera.main;

            Vector3 sp = cam.WorldToScreenPoint(muzzle ? muzzle.position : transform.position);
            Vector3 mw = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, sp.z));
            Vector2 dir = ((Vector2)mw - (Vector2)(muzzle ? muzzle.position : transform.position)).normalized;

            bool lmb = Input.GetMouseButton(0);

            // 收拢 → 固定 1 秒进入激光
            if (!firingBeam)
            {
                if (lmb && red.CanConverge)
                {
                    if (!_collapsing)
                    {
                        _collapsing = true;
                        _collapseTimer = 0f;
                        _collapseStartAngle = _currentScatterAngle;
                        if (chargeVfx) { PrepareAndPlay(chargeVfx, dir); }
                    }

                    _collapseTimer += Time.deltaTime;
                    float t = Mathf.Clamp01(_collapseTimer / Mathf.Max(0.0001f, collapseDuration));
                    _currentScatterAngle = Mathf.Lerp(_collapseStartAngle, 0f, t);

                    if (chargeVfx && chargeVfx.isPlaying)
                        OrientVfx(chargeVfx.transform, dir);

                    if (t >= 1f)
                    {
                        _currentScatterAngle = 0f;
                        _collapsing = false;
                        StartBeam(dir); // 进入Beam
                        return;
                    }
                }
                else
                {
                    if (_collapsing)
                    {
                        _collapsing = false;
                        _collapseTimer = 0f;
                        if (chargeVfx && chargeVfx.isPlaying)
                            chargeVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }

                    float recoverTime = Mathf.Max(0.001f, recoverScatterTime);
                    float recoverSpeed = scatterAngle / recoverTime;
                    _currentScatterAngle = Mathf.MoveTowards(_currentScatterAngle, Mathf.Max(0f, scatterAngle), recoverSpeed * Time.deltaTime);
                }

                if (red.CanScatter)
                {
                    DoScatter(dir);

                    if (scatterLight)
                    {
                        scatterLight.enabled = true;
                        scatterLight.transform.position = muzzle.position;

                        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        scatterLight.transform.rotation = Quaternion.Euler(0, 0, ang - 90f);

                        float outerAng = _currentScatterAngle;
                        float innerAng = Mathf.Clamp(scatterInnerAngleDeg, 0f, outerAng);
                        scatterLight.pointLightOuterAngle = outerAng;
                        scatterLight.pointLightInnerAngle = innerAng;

                        if (scatterAutoShortenByObstacle)
                        {
                            RaycastHit2D centerHit = Physics2D.Raycast(muzzle.position, dir, scatterMaxDist, layers.worldObstacles);
                            float desiredOuter = centerHit ? Mathf.Max(0.5f, centerHit.distance) : scatterOuterRadius;
                            float currentOuter = scatterLight.pointLightOuterRadius;
                            float nextOuter = Mathf.MoveTowards(currentOuter, Mathf.Min(desiredOuter, scatterOuterRadius), scatterRadiusLerpSpeed * Time.deltaTime);
                            scatterLight.pointLightOuterRadius = nextOuter;
                            scatterLight.pointLightInnerRadius = Mathf.Clamp(scatterInnerRadius, 0f, nextOuter);
                        }
                        else
                        {
                            scatterLight.pointLightOuterRadius = scatterOuterRadius;
                            scatterLight.pointLightInnerRadius = Mathf.Clamp(scatterInnerRadius, 0f, scatterOuterRadius);
                        }
                    }

                    DrawScatterEdges(dir);
                }
            }

            // 汇聚
            if (firingBeam)
            {
                if (leftEdge && leftEdge.enabled) leftEdge.enabled = false;
                if (rightEdge && rightEdge.enabled) rightEdge.enabled = false;

                bool hasEnergy = red.TryConsume(Time.deltaTime * redPerSec);
                if (!Input.GetMouseButton(0) || !hasEnergy)
                {
                    StopBeam();
                    return;
                }

                if (beamVfx && beamVfx.isPlaying)
                {
                    if (vfxAttachToMuzzle) beamVfx.transform.position = muzzle.position;
                    OrientVfx(beamVfx.transform, dir);
                }

                DoBeam(dir);
            }
        }

        private void StartBeam(Vector2 dirAtStart)
        {
            firingBeam = true;

            if (chargeVfx && chargeVfx.isPlaying)
                chargeVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (scatterLight) scatterLight.enabled = false;
            if (beamLightCore) beamLightCore.enabled = true;
            if (beamLightAmbient) beamLightAmbient.enabled = true;
            if (hitHaloLight) hitHaloLight.enabled = true;

            if (leftEdge) leftEdge.enabled = false;
            if (rightEdge) rightEdge.enabled = false;

            if (beamRenderer) beamRenderer.enabled = false;

            if (beamVfx) { PrepareAndPlay(beamVfx, dirAtStart); }
        }

        private void StopBeam()
        {
            firingBeam = false;

            if (beamRenderer) beamRenderer.enabled = false;

            if (scatterLight) scatterLight.enabled = true;
            if (beamLightCore) beamLightCore.enabled = false;
            if (beamLightAmbient) beamLightAmbient.enabled = false;
            if (hitHaloLight) hitHaloLight.enabled = false;

            if (beamVfx && beamVfx.isPlaying)
                beamVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            _currentScatterAngle = 0f;

            if (leftEdge) leftEdge.enabled = false;
            if (rightEdge) rightEdge.enabled = false;

            _collapsing = false;
            _collapseTimer = 0f;

            if (activeHitVfx && activeHitVfx.isPlaying)
                activeHitVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void DoScatter(Vector2 dir)
        {
            float half = _currentScatterAngle * 0.5f * Mathf.Deg2Rad;
            Vector2 right = new Vector2(-dir.y, dir.x);
            Vector3 origin = muzzle ? muzzle.position : transform.position;

            for (int i = 0; i < scatterRays; i++)
            {
                float t = (scatterRays == 1) ? 0.5f : (i / (scatterRays - 1f));
                float ang = Mathf.Lerp(-half, half, t);
                Vector2 rdir = (dir * Mathf.Cos(ang) + right * Mathf.Sin(ang)).normalized;

                RaycastHit2D hit = Physics2D.Raycast(origin, rdir, scatterMaxDist, layers.worldObstacles);
                Vector2 hitPoint = hit.collider ? hit.point : (Vector2)(origin + (Vector3)(rdir * scatterMaxDist));

                if (scatterTickDamage > 0f)
                {
                    int n = Physics2D.OverlapCircleNonAlloc(hitPoint, 0.2f, hitBuffer, layers.enemy);
                    for (int k = 0; k < n; k++)
                    {
                        var dmg = hitBuffer[k].GetComponentInParent<IDamageable>();
                        if (dmg != null && !dmg.IsDead) dmg.TakeDamage(scatterTickDamage * Time.deltaTime);
                    }
                }
                if (scatterHaloPrefab && (i % Mathf.Max(1, scatterHaloEvery) == 0) && hit.collider)
                {
                    Vector2 posInside = hitPoint + rdir * scatterHaloBleed;
                    SpawnScatterHalo(posInside);
                }
            }
        }

        private void SpawnScatterHalo(Vector2 pos)
        {
            var halo = Instantiate(scatterHaloPrefab, pos, Quaternion.identity);
            halo.intensity = halo.intensity * scatterHaloIntensityScale;
            halo.pointLightOuterRadius = scatterHaloRadius;
            halo.shadowsEnabled = false;
            Destroy(halo.gameObject, scatterHaloLifetime);
        }

        private void DrawScatterEdges(Vector2 dir)
        {
            float half = _currentScatterAngle * 0.5f * Mathf.Deg2Rad;
            Vector2 right = new Vector2(-dir.y, dir.x);
            Vector3 origin = muzzle ? muzzle.position : transform.position;

            Vector2 ldir = (dir * Mathf.Cos(-half) + right * Mathf.Sin(-half)).normalized;
            var lhit = Physics2D.Raycast(origin, ldir, scatterMaxDist, layers.worldObstacles);
            float ldist = lhit ? Mathf.Min(lhit.distance, edgeLineLength) : edgeLineLength;
            if (leftEdge)
            {
                leftEdge.enabled = true;
                leftEdge.SetPosition(0, origin);
                leftEdge.SetPosition(1, origin + (Vector3)(ldir * ldist));
            }

            Vector2 rdir = (dir * Mathf.Cos(half) + right * Mathf.Sin(half)).normalized;
            var rhit = Physics2D.Raycast(origin, rdir, scatterMaxDist, layers.worldObstacles);
            float rdist = rhit ? Mathf.Min(rhit.distance, edgeLineLength) : edgeLineLength;
            if (rightEdge)
            {
                rightEdge.enabled = true;
                rightEdge.SetPosition(0, origin);
                rightEdge.SetPosition(1, origin + (Vector3)(rdir * rdist));
            }
        }

        private struct BeamCastResult
        {
            public List<Vector3> points;
            public Vector3 finalEnd;
            public Collider2D finalCollider;
            public bool reflected;
        }

        private BeamCastResult CastBeamPath(Vector3 origin, Vector2 dir, int maxBounce, int layerMask, float stepEps, float maxDist)
        {
            BeamCastResult r = new BeamCastResult { points = new List<Vector3>(maxBounce + 2) };
            r.points.Add(origin);
            Vector3 o = origin;
            Vector2 d = dir.normalized;

            for (int i = 0; i <= maxBounce; i++)
            {
                var hit = Physics2D.Raycast(o, d, maxDist, layerMask);
                if (!hit.collider)
                {
                    var far = o + (Vector3)(d * maxDist);
                    r.points.Add(far);
                    r.finalEnd = far;
                    r.finalCollider = null;
                    return r;
                }

                r.points.Add(hit.point);
                r.finalEnd = hit.point;
                r.finalCollider = hit.collider;

                bool isMirror = ((1 << hit.collider.gameObject.layer) & mirrorMask.value) != 0;

                if (i < maxBounce && isMirror)
                {
                    Vector2 refl = Vector2.Reflect(d, hit.normal).normalized;
                    o = (Vector2)hit.point + refl * stepEps;
                    d = refl;
                    r.reflected = true;
                    continue;
                }
                break;
            }
            return r;
        }

        /// <summary>
        /// 汇聚束体：反射 + 折线；末端交互：伤害/加红光/点火把（兼容旧 Torch）。
        /// </summary>
        private void DoBeam(Vector2 dir)
        {
            int rayMask = layers.worldObstacles | layers.enemy | layers.torch;
            if (mirrorMask.value != 0) rayMask |= mirrorMask.value;

            var res = CastBeamPath(muzzle.position, dir, maxBounces, rayMask, bounceStepEpsilon, 1000f);

            if (beamRenderer)
            {
                beamRenderer.enabled = true;
                beamRenderer.positionCount = res.points.Count;
                for (int i = 0; i < res.points.Count; i++)
                    beamRenderer.SetPosition(i, res.points[i]);
                beamRenderer.startWidth = beamWidth;
                beamRenderer.endWidth = beamWidth;
            }

            // 直线段绘制光带；若发生反射则关闭面光
            if (res.reflected)
            {
                if (beamLightCore) beamLightCore.enabled = false;
                if (beamLightAmbient) beamLightAmbient.enabled = false;
            }
            else
            {
                Vector3 o = res.points[0];
                Vector3 e = res.points.Count > 1 ? res.points[1] : o + (Vector3)(dir * 0.01f);
                Vector3 vEnd = e + (Vector3)(dir * visualBleed);
                Vector2 n = new Vector2(-dir.y, dir.x);
                float coreHalf = Mathf.Max(0.03f, beamWidth * 0.5f);
                float ambHalf = coreHalf * 2.5f;
                static Vector3 W2L(Light2D l, Vector3 wp) => l.transform.InverseTransformPoint(wp);

                if (beamLightCore)
                {
                    beamLightCore.enabled = true;
                    EnsureQuadSize(4);
                    _quad[0] = W2L(beamLightCore, o + (Vector3)(n * coreHalf));
                    _quad[1] = W2L(beamLightCore, vEnd + (Vector3)(n * coreHalf));
                    _quad[2] = W2L(beamLightCore, vEnd + (Vector3)(-n * coreHalf));
                    _quad[3] = W2L(beamLightCore, o + (Vector3)(-n * coreHalf));
                    beamLightCore.SetShapePath(_quad);
                }
                if (beamLightAmbient)
                {
                    beamLightAmbient.enabled = true;
                    EnsureQuadSize(4);
                    _quad[0] = W2L(beamLightAmbient, o + (Vector3)(n * ambHalf));
                    _quad[1] = W2L(beamLightAmbient, vEnd + (Vector3)(n * ambHalf));
                    _quad[2] = W2L(beamLightAmbient, vEnd + (Vector3)(-n * ambHalf));
                    _quad[3] = W2L(beamLightAmbient, o + (Vector3)(-n * ambHalf));
                    beamLightAmbient.SetShapePath(_quad);
                }
            }

            if (hitHaloLight)
            {
                hitHaloLight.enabled = true;
                Vector2 finalDir = (res.points.Count >= 2)
                    ? (Vector2)(res.points[^1] - res.points[^2]).normalized
                    : dir;
                hitHaloLight.transform.position = res.finalEnd - (Vector3)(finalDir * 0.1f);
            }

            int mask = layers.enemy | layers.torch;
            int nHit = Physics2D.OverlapCircleNonAlloc(res.finalEnd, hitRadius, hitBuffer, mask);
            for (int i = 0; i < nHit; i++)
            {
                var go = hitBuffer[i].gameObject;

                var dmg = go.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead) dmg.TakeDamage(dps * Time.deltaTime);

                // 兼容新旧 Torch：优先 OnLaserFirstHit，找不到则 Ignite
                var targetGO = go.GetComponentInParent<Transform>()?.gameObject;
                if (targetGO)
                {
                    targetGO.SendMessage("OnLaserFirstHit", SendMessageOptions.DontRequireReceiver);
                    targetGO.SendMessage("Ignite", SendMessageOptions.DontRequireReceiver);
                }

                var redOther = go.GetComponentInParent<FadedDreams.Player.RedLightController>();
                if (redOther) redOther.Add(beamRedPerSec * Time.deltaTime);
            }

            if (hitVfxPrefab)
            {
                if (!activeHitVfx)
                {
                    activeHitVfx = Instantiate(hitVfxPrefab, res.finalEnd, Quaternion.identity);

                    var main = activeHitVfx.main;
                    main.loop = true;
                    main.startLifetime = 0.5f;
                    main.startDelay = 0f;
                    main.maxParticles = Mathf.Max(main.maxParticles, 64);

                    var emission = activeHitVfx.emission;
                    if (emission.rateOverTime.constant > 6f)
                        emission.rateOverTime = 6f;
                }

                activeHitVfx.transform.position = res.finalEnd;
                Vector2 finalDir = (res.points.Count >= 2)
                    ? (Vector2)(res.points[^1] - res.points[^2]).normalized
                    : dir;
                float ang = Mathf.Atan2(finalDir.y, finalDir.x) * Mathf.Rad2Deg + vfxAngleOffsetDeg;
                activeHitVfx.transform.rotation = Quaternion.Euler(0, 0, ang);

                if (!activeHitVfx.isPlaying) activeHitVfx.Play();
            }
        }

        private static void EnsureQuadSize(int count)
        {
            if (_quad == null || _quad.Length != count)
                _quad = new Vector3[count];
        }

        // ---------- VFX helpers ----------
        private void PrepareAndPlay(ParticleSystem ps, Vector2 dir)
        {
            if (!ps) return;
            if (vfxAttachToMuzzle && muzzle) ps.transform.position = muzzle.position;
            OrientVfx(ps.transform, dir);
            var main = ps.main;
            if (!main.loop) { main.loop = true; }
            ps.Play();
        }

        private void OrientVfx(Transform t, Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + vfxAngleOffsetDeg;
            t.rotation = Quaternion.Euler(0, 0, ang);
        }
    }
}
