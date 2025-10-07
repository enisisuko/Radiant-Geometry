// EnemySelfExplodeOnOverheat.cs — 爆燃曲线 + 3秒后销毁 + 爆炸光半径缩放
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using System.Collections;
using FadedDreams.Player;       // RedLightController
using FadedDreams.World.Light;  // Torch

namespace FadedDreams.Enemies
{
    [RequireComponent(typeof(RedLightController))]
    public class EnemySelfExplodeOnOverheat : MonoBehaviour
    {
        [Header("Explosion VFX & Light")]
        public ParticleSystem explosionVfxPrefab;     // 非循环爆炸粒子
        public Light2D explosionLightPrefab;          // 仅包含 Light2D 的干净预制
        [Tooltip("爆炸强光表现时长（秒）")]
        public float explosionLightDuration = 3f;

        [Header("Explosion Light Curve")]
        [Tooltip("0~1: 时间归一化 → 强度倍率")]
        public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 2f, 1, 0f);
        [Tooltip("Light2D.intensity 的基准值")]
        public float baseLightIntensity = 5f;
        [Tooltip("Light2D.pointLightOuterRadius 的基准值")]
        public float baseLightRadius = 6f;
        [Tooltip("半径随曲线放大比例（= baseRadius * (1 + radiusCurveMultiplier * curveEval))")]
        public float radiusCurveMultiplier = 0.25f;
        [Tooltip("整体缩放爆炸光半径（0.5=缩小一半，1=原样）")]
        public float explosionLightRadiusScale = 0.6f;

        [Header("Explosion Gameplay")]
        public float radius = 6f;      // 影响半径
        public float damage = 60f;     // 伤害
        public float igniteRadius = 6f; // 点燃火把半径
        public float knockbackForce = 9f;
        public LayerMask affectMask;

        [Header("Aftermath")]
        [Tooltip("自爆完成后延时销毁自身（秒）")]
        public float cleanupDelay = 3f;

        [Header("Screen Shake")]
        public float shakeIntensity = 0.4f;
        public float shakeDuration = 0.25f;

        private RedLightController _red;
        private bool _exploded;
        private UnityAction<float, float> _onRedChangedAction;
        private Coroutine _explodeCo;

        private void Awake()
        {
            _red = GetComponent<RedLightController>();
        }

        private void OnEnable()
        {
            if (_red != null)
            {
                if (_red.onChanged == null) _red.onChanged = new UnityEvent<float, float>();
                _onRedChangedAction = (cur, max) =>
                {
                    // 红量灌满 → 触发自爆（走延迟销毁序列）
                    if (!_exploded && max > 0f && cur >= max)
                        Explode();
                };
                _red.onChanged.RemoveListener(_onRedChangedAction);
                _red.onChanged.AddListener(_onRedChangedAction);
            }
        }

        private void OnDisable()
        {
            if (_red != null && _onRedChangedAction != null)
                _red.onChanged.RemoveListener(_onRedChangedAction);
        }

        /// <summary>对外公开触发（受击致死/脚本调用）。</summary>
        public void TriggerExplosion()
        {
            if (_exploded) return;
            Explode();
        }

        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            Vector3 pos = transform.position;

            // 1) 爆炸粒子
            if (explosionVfxPrefab)
            {
                var vfx = Instantiate(explosionVfxPrefab, pos, Quaternion.identity);
                var main = vfx.main;
                main.loop = false;
                main.startDelay = 0f;
                vfx.Play(true);
                Destroy(vfx.gameObject, main.startLifetime.constantMax + 0.5f);
            }

            // 2) 爆燃强光（曲线+半径缩放）
            Light2D runtimeLight = null;
            if (explosionLightPrefab)
            {
                runtimeLight = Instantiate(explosionLightPrefab, pos, Quaternion.identity);
                runtimeLight.enabled = true; // 强制启用
                runtimeLight.intensity = baseLightIntensity;
                runtimeLight.pointLightOuterRadius = baseLightRadius * Mathf.Max(0.01f, explosionLightRadiusScale);
            }

            // 3) 点燃周围火把
            var colsTorch = Physics2D.OverlapCircleAll(pos, igniteRadius);
            foreach (var c in colsTorch)
            {
                var torch = c.GetComponentInParent<Torch>();
                if (torch) torch.OnLaserFirstHit();
            }

            // 4) 范围伤害 + 击退（可触发连锁）
            var cols = Physics2D.OverlapCircleAll(pos, radius, affectMask);
            foreach (var c in cols)
            {
                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead) dmg.TakeDamage(damage);

                var rb = c.attachedRigidbody;
                if (rb)
                {
                    Vector2 dir = ((Vector2)rb.position - (Vector2)pos).normalized;
                    rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
                }
            }

            // 5) 屏幕震动
            if (Camera.main) StartCoroutine(DoShake(Camera.main.transform));

            // 6) 光效动画 + 延迟销毁
            if (_explodeCo != null) StopCoroutine(_explodeCo);
            _explodeCo = StartCoroutine(ExplodeSequence(runtimeLight));
        }

        private IEnumerator ExplodeSequence(Light2D l)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, Mathf.Max(explosionLightDuration, cleanupDelay));
            float baseIntensity = baseLightIntensity;
            float baseRadius = baseLightRadius * Mathf.Max(0.01f, explosionLightRadiusScale);

            while (t < dur)
            {
                t += Time.deltaTime;
                float nt = Mathf.Clamp01(t / dur);    // 0~1
                float k = intensityCurve.Evaluate(nt);
                if (l)
                {
                    float kk = Mathf.Max(0f, k);
                    l.intensity = baseIntensity * kk;
                    l.pointLightOuterRadius = baseRadius * (1f + radiusCurveMultiplier * kk);
                }
                yield return null;
            }

            if (l) Destroy(l.gameObject);
            Destroy(gameObject); // 3秒（或 explosionLightDuration/cleanupDelay 较大者）后销毁
        }

        private IEnumerator DoShake(Transform cam)
        {
            var original = cam.position;
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float ox = Random.Range(-1f, 1f) * shakeIntensity;
                float oy = Random.Range(-1f, 1f) * shakeIntensity;
                cam.position = original + new Vector3(ox, oy, 0f);
                yield return null;
            }
            cam.position = original;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.15f);
            Gizmos.DrawSphere(transform.position, igniteRadius);
        }
#endif
    }
}
