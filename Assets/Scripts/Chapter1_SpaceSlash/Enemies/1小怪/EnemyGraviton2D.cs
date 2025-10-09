// Assets/Scripts/Enemies/Chapter1/EnemyGraviton2D.cs
// ����С�֣�Hover-Over-Head �棩�������ͷ��������ͣ���ͷ� 3 �뷶Χ����������Χ���壨�����/���ߣ���
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using FadedDreams.Player;

namespace FadedDreams.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class EnemyGraviton2D : MonoBehaviour, IDamageable
    {
        [Header("Awareness")]
        public float warnRadius = 25f;

        [Header("Hover-Over-Head")]
        public float hoverHeight = 3.5f;
        public float hoverMaxSpeed = 8f;
        public float hoverAccel = 18f;
        public float hoverDeadzone = 0.25f;
        [Header("Hover Lateral Wander")]
        public float wanderAmpX = 1.2f;
        public float wanderFreq = 0.7f;
        [Header("Anti-crowding / Separation")]
        public float separationRadius = 1.6f;
        public float separationPush = 9f;
        public LayerMask separationMask;

        [Header("Gravity Skill")]
        public float gravityRadius = 8f;
        public float pullForcePerSecond = 40f;    // ÿ��������
        public float attractDuration = 3.0f;
        public float skillCooldown = 5.0f;
        public LayerMask affectMask = ~0;         // ��������Ĳ㣨����ң�
        public UnityEvent onGravityStart;
        public UnityEvent onGravityStop;
        public GameObject vfxAttractLoop;         // ѭ����Ч����ѡ��
        public GameObject vfxAttractBurst;        // ����������Ч����ѡ��
        public Light2D pulseLight;                // ����⣨URP ��ѡ��

        [Header("Contact Damage")]
        public float energyDamage = 25f;
        public float damageCooldown = 0.6f;
        public float knockbackForce = 8f;

        [Header("Death")]
        public GameObject explosionPrefab;
        public UnityEvent onDeath;

        // runtime
        public bool IsDead { get; private set; }
        private enum State { Idle, Orbit, Attracting, Recover }
        private State _state;
        private Rigidbody2D _rb;
        private Transform _player;
        private float _nextSkillTime;
        private float _lastDamageTime = -999f;
        private GameObject _loopFxInst;
        private float _wanderPhase;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector2.zero;
#endif
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) _player = p.transform;

            _wanderPhase = Random.Range(0f, 1000f);
        }

        void Start()
        {
            _state = State.Idle;
            _nextSkillTime = Time.time + Random.Range(1.5f, skillCooldown);
        }

        void Update()
        {
            if (IsDead) return;
            if (!_player) return;

            float d = Vector2.Distance(transform.position, _player.position);
            switch (_state)
            {
                case State.Idle:
                    if (d <= warnRadius) _state = State.Orbit;
                    break;

                case State.Orbit:
                    HoverUpdate();
                    if (Time.time >= _nextSkillTime && d <= warnRadius + 2f)
                        StartCoroutine(CoAttract());
                    break;

                case State.Attracting:
                case State.Recover:
                    break;
            }

            if (_state == State.Attracting && pulseLight)
            {
                pulseLight.intensity = 0.6f + Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 1.2f;
            }
        }

        private void HoverUpdate()
        {
            if (_player == null) return;

            float xOffset = Mathf.Sin(Time.time * wanderFreq + _wanderPhase) * wanderAmpX;
            Vector2 anchor = (Vector2)_player.position + new Vector2(xOffset, hoverHeight);

            Vector2 to = anchor - (Vector2)transform.position;
            Vector2 desiredVel = Vector2.zero;
            if (to.sqrMagnitude > hoverDeadzone * hoverDeadzone)
            {
                desiredVel = Vector2.ClampMagnitude(to * hoverAccel, hoverMaxSpeed);
            }

            Vector2 sep = Vector2.zero;
            if (separationRadius > 0f && separationPush > 0f)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, separationRadius, separationMask);
                foreach (var h in hits)
                {
                    if (!h || !h.attachedRigidbody) continue;
                    if (h.attachedRigidbody == _rb) continue;
                    Vector2 away = (Vector2)transform.position - (Vector2)h.transform.position;
                    float dist = Mathf.Max(0.01f, away.magnitude);
                    sep += away.normalized * (separationPush / dist);
                }
            }

#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, desiredVel + sep, Time.deltaTime * 8f);
#else
            _rb.velocity = Vector2.Lerp(_rb.velocity, desiredVel + sep, Time.deltaTime * 8f);
#endif
        }

        private IEnumerator CoAttract()
        {
            _state = State.Attracting;
            onGravityStart?.Invoke();
            if (vfxAttractBurst) Instantiate(vfxAttractBurst, transform.position, Quaternion.identity);
            if (vfxAttractLoop) _loopFxInst = Instantiate(vfxAttractLoop, transform);

            float t = 0f;
            while (t < attractDuration && !IsDead)
            {
                t += Time.deltaTime;
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = Vector2.zero;
#else
                _rb.velocity = Vector2.zero;
#endif
                DoPull(Time.deltaTime);
                yield return null;
            }

            if (_loopFxInst) Destroy(_loopFxInst);
            onGravityStop?.Invoke();

            _state = State.Recover;
            yield return new WaitForSeconds(0.35f);

            _nextSkillTime = Time.time + skillCooldown;
            _state = State.Orbit;
        }

        private void DoPull(float dt)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, gravityRadius, affectMask);
            foreach (var h in hits)
            {
                if (!h || !h.attachedRigidbody) continue;
                if (h.attachedRigidbody == _rb) continue;

                Vector2 toCenter = (Vector2)transform.position - h.attachedRigidbody.position;
                float dist = Mathf.Max(0.1f, toCenter.magnitude);
                Vector2 dir = toCenter / dist;

                float force = pullForcePerSecond * dt;
                h.attachedRigidbody.AddForce(dir * force, ForceMode2D.Force);
            }
        }

        void OnCollisionEnter2D(Collision2D c) { TryHitPlayer(c.collider); }
        void OnTriggerEnter2D(Collider2D other) { TryHitPlayer(other); }
        private void TryHitPlayer(Collider2D col)
        {
            if (!col) return;
            if (Time.time - _lastDamageTime < damageCooldown) return;

            var plc = col.GetComponentInParent<PlayerLightController>() ?? col.GetComponent<PlayerLightController>();
            if (plc != null)
            {
                plc.currentEnergy = Mathf.Max(0f, plc.currentEnergy - energyDamage);
                _lastDamageTime = Time.time;

                var prb = plc.GetComponent<Rigidbody2D>();
                if (prb)
                {
                    Vector2 pushDir = ((Vector2)prb.position - (Vector2)transform.position).normalized;
                    prb.AddForce(pushDir * knockbackForce, ForceMode2D.Impulse);
                }
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            Die();
        }

        private void Die()
        {
            IsDead = true;
            if (_loopFxInst) Destroy(_loopFxInst);
            if (explosionPrefab) Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            
            // 播放爆炸音效（带随机音调）
            if (explosionSFX)
            {
                GameObject tempGO = new GameObject("TempExplosionSFX");
                tempGO.transform.position = transform.position;
                AudioSource tempSource = tempGO.AddComponent<AudioSource>();
                tempSource.clip = explosionSFX;
                tempSource.volume = explosionVolume;
                tempSource.spatialBlend = 0f;
                float randomPitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                tempSource.pitch = randomPitch;
                tempSource.Play();
                Destroy(tempGO, explosionSFX.length + 0.1f);
                Debug.Log($"🎹 引力体敌人爆炸音效！音调：{randomPitch:F2}");
            }
            
            onDeath?.Invoke();
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, gravityRadius);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, warnRadius);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, separationRadius);
        }
#endif
    }
}
