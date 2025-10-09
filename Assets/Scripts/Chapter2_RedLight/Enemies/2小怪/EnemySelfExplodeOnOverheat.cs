// EnemySelfExplodeOnOverheat.cs �� ��ȼ���� + 3������� + ��ը��뾶����
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
        public ParticleSystem explosionVfxPrefab;     // ��ѭ����ը����
        public Light2D explosionLightPrefab;          // ������ Light2D �ĸɾ�Ԥ��
        [Tooltip("��ըǿ�����ʱ�����룩")]
        public float explosionLightDuration = 3f;

        [Header("Explosion Light Curve")]
        [Tooltip("0~1: ʱ���һ�� �� ǿ�ȱ���")]
        public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 2f, 1, 0f);
        [Tooltip("Light2D.intensity �Ļ�׼ֵ")]
        public float baseLightIntensity = 5f;
        [Tooltip("Light2D.pointLightOuterRadius �Ļ�׼ֵ")]
        public float baseLightRadius = 6f;
        [Tooltip("�뾶�����߷Ŵ������= baseRadius * (1 + radiusCurveMultiplier * curveEval))")]
        public float radiusCurveMultiplier = 0.25f;
        [Tooltip("�������ű�ը��뾶��0.5=��Сһ�룬1=ԭ����")]
        public float explosionLightRadiusScale = 0.6f;

        [Header("Explosion Gameplay")]
        public float radius = 6f;      // Ӱ��뾶
        public float damage = 60f;     // �˺�
        public float igniteRadius = 6f; // ��ȼ��Ѱ뾶
        public float knockbackForce = 9f;
        public LayerMask affectMask;

        [Header("Aftermath")]
        [Tooltip("�Ա���ɺ���ʱ�����������룩")]
        public float cleanupDelay = 3f;

        [Header("Screen Shake")]
        public float shakeIntensity = 0.4f;
        public float shakeDuration = 0.25f;

        [Header("Explosion Audio")]
        public AudioClip explosionSFX;                   // 爆炸音效（钢琴音）
        [Range(0f, 1f)] public float explosionVolume = 0.8f;
        [Range(0f, 0.5f)] public float pitchVariation = 0.15f; // 音调随机变化范围

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
                    // �������� �� �����Ա������ӳ��������У�
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

        /// <summary>���⹫���������ܻ�����/�ű����ã���</summary>
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

            // 1) ��ը����
            if (explosionVfxPrefab)
            {
                var vfx = Instantiate(explosionVfxPrefab, pos, Quaternion.identity);
                var main = vfx.main;
                main.loop = false;
                main.startDelay = 0f;
                vfx.Play(true);
                Destroy(vfx.gameObject, main.startLifetime.constantMax + 0.5f);
            }

            // 2) ��ȼǿ�⣨����+�뾶���ţ�
            Light2D runtimeLight = null;
            if (explosionLightPrefab)
            {
                runtimeLight = Instantiate(explosionLightPrefab, pos, Quaternion.identity);
                runtimeLight.enabled = true; // ǿ������
                runtimeLight.intensity = baseLightIntensity;
                runtimeLight.pointLightOuterRadius = baseLightRadius * Mathf.Max(0.01f, explosionLightRadiusScale);
            }

            // 3) ��ȼ��Χ���
            var colsTorch = Physics2D.OverlapCircleAll(pos, igniteRadius);
            foreach (var c in colsTorch)
            {
                var torch = c.GetComponentInParent<Torch>();
                if (torch) torch.OnLaserFirstHit();
            }

            // 4) ��Χ�˺� + ���ˣ��ɴ���������
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

            // 5) ��Ļ��
            if (Camera.main) StartCoroutine(DoShake(Camera.main.transform));

            // 5.5) 播放爆炸音效（带随机音调）
            if (explosionSFX)
            {
                GameObject tempGO = new GameObject("TempExplosionSFX");
                tempGO.transform.position = pos;
                AudioSource tempSource = tempGO.AddComponent<AudioSource>();
                tempSource.clip = explosionSFX;
                tempSource.volume = explosionVolume;
                tempSource.spatialBlend = 0f; // 2D音效
                tempSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                tempSource.Play();
                Destroy(tempGO, explosionSFX.length + 0.1f);
            }

            // 6) ��Ч���� + �ӳ�����
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
            Destroy(gameObject); // 3�루�� explosionLightDuration/cleanupDelay �ϴ��ߣ�������
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
