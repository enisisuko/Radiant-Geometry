// Assets/Scripts/Enemies/Chapter1/EnemyPseudoSlash2D.cs
// α�ռ�նС�֣�Hover-Over-Head �棩�������ͷ��������ͣ��ÿ���� 2 ���������ɱ���ҿռ�ն��ϣ�������ͷ�һ���ᴩ��Ļ�ġ�ն�ߡ���LaserBeamSegment2D����
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace FadedDreams.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class EnemyPseudoSlash2D : MonoBehaviour, IDamageable
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

        [Header("Pseudo Space-Slash")]
        public LaserBeamSegment2D slashPrefab;    // ��ͨ�ü��������ն�ߡ�
        public Camera cam;                        // �������Ĭ�� Camera.main��
        public float chargeSeconds = 2.0f;        // �������ɱ���ϣ�
        public float lethalSeconds = 0.15f;       // ����ִ�д���
        public float fadeOutSeconds = 0.35f;      // ����
        public float cooldownSeconds = 5.0f;
        public float thickness = 0.16f;
        public Color slashColor = Color.white;
        public float thickenMul = 1.8f;
        public float thickenLerp = 0.08f;
        public float energyDamage = 22f;
        public float knockupImpulse = 6f;

        [Header("Interrupt / Stun")]
        public bool interruptibleDuringCharge = true;
        public float stunOnInterrupted = 0.6f;
        public UnityEvent onChargeBegin;
        public UnityEvent onChargeInterrupted;
        public UnityEvent onSlashFire;

        [Header("Contact Damage (optional)")]
        public float contactEnergyDamage = 20f;
        public float contactDamageCooldown = 0.6f;
        public float contactKnockback = 8f;

        [Header("Death")]
        public GameObject explosionPrefab;
        public UnityEvent onDeath;
        public AudioClip explosionSFX;  // 爆炸音效（钢琴音）
        [Range(0f, 1f)] public float explosionVolume = 0.8f;
        [Range(0f, 0.5f)] public float pitchVariation = 0.15f;

        // runtime
        public bool IsDead { get; private set; }
        private enum State { Idle, Orbit, Charging, Firing, Recover, Stunned }
        private State _state;
        private Rigidbody2D _rb;
        private Transform _player;
        private float _nextSkillTime;
        private float _lastContactTime = -999f;
        private bool _inCharge;
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
            if (!cam) cam = Camera.main;

            _wanderPhase = Random.Range(0f, 1000f);
        }

        void Start()
        {
            _state = State.Idle;
            _nextSkillTime = Time.time + Random.Range(1.5f, cooldownSeconds);
        }

        void Update()
        {
            if (IsDead || !_player) return;

            float d = Vector2.Distance(transform.position, _player.position);
            switch (_state)
            {
                case State.Idle:
                    if (d <= warnRadius) _state = State.Orbit;
                    break;

                case State.Orbit:
                    HoverUpdate();
                    if (Time.time >= _nextSkillTime && d <= warnRadius + 2f)
                        StartCoroutine(CoChargeThenFire());
                    break;

                case State.Stunned:
                case State.Charging:
                case State.Firing:
                case State.Recover:
                    break;
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

#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, desiredVel, Time.deltaTime * 8f);
#else
            _rb.velocity = Vector2.Lerp(_rb.velocity, desiredVel, Time.deltaTime * 8f);
#endif

            // ������
            if (separationRadius > 0f && separationPush > 0f)
            {
                Vector2 sep = Vector2.zero;
                var hits = Physics2D.OverlapCircleAll(transform.position, separationRadius, separationMask);
                foreach (var h in hits)
                {
                    if (!h || !h.attachedRigidbody) continue;
                    if (h.attachedRigidbody == _rb) continue;
                    Vector2 away = (Vector2)transform.position - (Vector2)h.transform.position;
                    float dist = Mathf.Max(0.01f, away.magnitude);
                    sep += away.normalized * (separationPush / dist);
                }
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity += sep * Time.deltaTime * 8f;
#else
                _rb.velocity += sep * Time.deltaTime * 8f;
#endif
            }
        }

        private IEnumerator CoChargeThenFire()
        {
            _state = State.Charging;
            _inCharge = true;
            onChargeBegin?.Invoke();

            // ����������������ⲿVFX��
            float t = 0f;
            while (t < chargeSeconds && !IsDead)
            {
                t += Time.deltaTime;
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = Vector2.zero;
#else
                _rb.velocity = Vector2.zero;
#endif
                yield return null;
            }
            _inCharge = false;
            if (_state == State.Stunned || IsDead) yield break;

            // �ͷš�ն�ߡ�
            _state = State.Firing;
            onSlashFire?.Invoke();

            if (slashPrefab && cam && _player)
            {
                Vector3 origin = transform.position;
                Vector2 dir = (_player.position - origin).normalized;

                // ȡ����ɼ�����Խ��߳��ȣ���֤�ᴩ��Ļ
                GetWorldViewportRect(cam, origin.z, out Vector3 bl, out Vector3 br, out Vector3 tl, out Vector3 tr);
                float diag = Vector3.Distance(bl, tr) + 6f;
                Vector3 A = origin - (Vector3)dir * (diag * 0.5f);
                Vector3 B = origin + (Vector3)dir * (diag * 0.5f);

                var beam = Instantiate(slashPrefab);
                beam.name = "Enemy_PseudoSlash";
                // Initialize: (A, B, color, thickness, chargeSeconds, lethalSeconds, lifeSeconds, sweeping, velocity)
                beam.Initialize(A, B, slashColor, thickness, 0f, lethalSeconds, lethalSeconds + fadeOutSeconds, false, Vector2.zero);

                // ϸ�ڲ������ű����ã��ɱ��ռ�ն�п��������ݳ��ȣ�
                beam.useChargeColorLerp = false;
                beam.thickenOnLethal = true;
                beam.thickenMul = thickenMul;
                beam.thickenLerpSeconds = thickenLerp;
                beam.fadeOutSeconds = fadeOutSeconds;
                beam.energyDamage = energyDamage;
                beam.knockupImpulse = knockupImpulse;
                beam.sliceOnlyWhenLethal = true;
            }

            _state = State.Recover;
            yield return new WaitForSeconds(0.25f);
            _nextSkillTime = Time.time + cooldownSeconds;
            _state = State.Orbit;
        }

        // �ӿھ��Σ��������꣩
        private static void GetWorldViewportRect(Camera c, float z, out Vector3 bl, out Vector3 br, out Vector3 tl, out Vector3 tr)
        {
            if (c.orthographic)
            {
                bl = c.ViewportToWorldPoint(new Vector3(0, 0, 0));
                br = c.ViewportToWorldPoint(new Vector3(1, 0, 0));
                tl = c.ViewportToWorldPoint(new Vector3(0, 1, 0));
                tr = c.ViewportToWorldPoint(new Vector3(1, 1, 0));
                bl.z = br.z = tl.z = tr.z = z;
            }
            else
            {
                float dz = z - c.transform.position.z;
                bl = c.ViewportToWorldPoint(new Vector3(0, 0, dz));
                br = c.ViewportToWorldPoint(new Vector3(1, 0, dz));
                tl = c.ViewportToWorldPoint(new Vector3(0, 1, dz));
                tr = c.ViewportToWorldPoint(new Vector3(1, 1, dz));
            }
        }

        // ����ҿռ�ն�����У������� �� ��ϣ�����������ʱ�� �� ��������
        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            if (_inCharge && interruptibleDuringCharge)
            {
                StopAllCoroutines();
                _state = State.Stunned;
                _inCharge = false;
                onChargeInterrupted?.Invoke();
                StartCoroutine(CoStun(stunOnInterrupted));
                return;
            }

            Die();
        }

        private IEnumerator CoStun(float seconds)
        {
            float t = 0f;
            while (t < seconds && !IsDead)
            {
                t += Time.deltaTime;
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = Vector2.zero;
#else
                _rb.velocity = Vector2.zero;
#endif
                yield return null;
            }
            _nextSkillTime = Time.time + cooldownSeconds * 0.6f;
            _state = State.Orbit;
        }

        private void Die()
        {
            IsDead = true;
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
                Debug.Log($"🎹 空间斩敌人爆炸音效！音调：{randomPitch:F2}");
            }
            
            onDeath?.Invoke();
            Destroy(gameObject);
        }

        void OnCollisionEnter2D(Collision2D c) { TryContactDamage(c.collider); }
        void OnTriggerEnter2D(Collider2D other) { TryContactDamage(other); }
        private void TryContactDamage(Collider2D col)
        {
            if (!col) return;
            if (Time.time - _lastContactTime < contactDamageCooldown) return;

            var plc = col.GetComponentInParent<FadedDreams.Player.PlayerLightController>() ?? col.GetComponent<FadedDreams.Player.PlayerLightController>();
            if (plc != null)
            {
                plc.currentEnergy = Mathf.Max(0f, plc.currentEnergy - contactEnergyDamage);
                _lastContactTime = Time.time;

                var prb = plc.GetComponent<Rigidbody2D>();
                if (prb)
                {
                    Vector2 pushDir = (prb.position - (Vector2)transform.position).normalized;
                    prb.AddForce(pushDir * contactKnockback, ForceMode2D.Impulse);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, warnRadius);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, separationRadius);
        }
#endif
    }
}
