// Assets/Scripts/Enemies/Chapter1/EnemyRangedShooter2D.cs
// 远程射手（Hover-Over-Head 版）：在玩家头顶附近悬停，每 2 秒发射一枚子弹。子弹可被玩家“空间斩”切掉（请把子弹放在可被斩的层里）。
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
    public class EnemyRangedShooter2D : MonoBehaviour, IDamageable
    {
        [Header("Awareness")]
        public float warnRadius = 25f;

        [Header("Hover-Over-Head")]
        public float hoverHeight = 3.5f;          // 头顶高度
        public float hoverMaxSpeed = 8f;          // 最大巡航速度
        public float hoverAccel = 18f;            // 朝锚点加速度
        public float hoverDeadzone = 0.25f;       // 到达死区
        [Header("Hover Lateral Wander")]
        public float wanderAmpX = 1.2f;           // 左右摆幅
        public float wanderFreq = 0.7f;           // 摆动频率
        [Header("Anti-crowding / Separation")]
        public float separationRadius = 1.6f;     // 分离半径
        public float separationPush = 9f;         // 分离强度
        public LayerMask separationMask;          // 勾选敌人层

        [Header("Ranged Attack")]
        public BulletProjectile bulletPrefab;      // 你项目里的子弹预制体（可被空间斩切）
        public float bulletSpeed = 12f;
        public float fireCooldown = 2.0f;
        public float aimLeadSeconds = 0.06f;       // 轻微预判
        public float windupSeconds = 0.2f;         // 开火前提示
        public float recoilBack = 0.75f;           // 发射后坐
        public UnityEvent onWindup;
        public UnityEvent onShoot;

        [Header("Contact Damage")]
        public float energyDamage = 25f;
        public float damageCooldown = 0.6f;
        public float knockbackForce = 10f;

        [Header("Visuals")]
        public SpriteRenderer spriteRenderer;
        public Color baseColor = Color.white;
        public Color windupGlowColor = Color.white;
        [Min(1f)] public float windupGlowMultiplier = 1.6f;
        public AnimationCurve windupGlowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public Light2D glowLight;
        public float glowLightBaseIntensity = 0f;
        public float glowLightAddOnPeak = 1.0f;

        [Header("Death")]
        public GameObject explosionPrefab;
        public UnityEvent onDeath;

        // runtime
        public bool IsDead { get; private set; }
        private enum State { Idle, Orbit, Windup, Firing, Recover }
        private State _state;
        private Rigidbody2D _rb;
        private Transform _player;
        private float _nextFireTime;
        private float _lastDamageTime = -999f;
        private Color _baseMem;
        private bool _hasBase;
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

            if (spriteRenderer)
            {
                _baseMem = baseColor.a > 0 ? baseColor : spriteRenderer.color;
                _hasBase = true;
                spriteRenderer.color = _baseMem;
            }
            if (glowLight) glowLight.intensity = glowLightBaseIntensity;

            _wanderPhase = Random.Range(0f, 1000f);
        }

        void Start()
        {
            _state = State.Idle;
            _nextFireTime = Time.time + fireCooldown * Random.Range(.5f, 1f);
        }

        void Update()
        {
            if (!_player || IsDead) return;

            float d = Vector2.Distance(transform.position, _player.position);
            switch (_state)
            {
                case State.Idle:
                    if (d <= warnRadius) _state = State.Orbit;
                    break;

                case State.Orbit:
                    HoverUpdate();
                    if (Time.time >= _nextFireTime && d <= warnRadius + 2f)
                        StartCoroutine(CoWindupThenShoot());
                    break;

                case State.Windup:
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
            FaceTo(_player.position);
        }

        private IEnumerator CoWindupThenShoot()
        {
            _state = State.Windup;
            onWindup?.Invoke();
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector2.zero;
#else
            _rb.velocity = Vector2.zero;
#endif
            float t = 0f;
            while (t < windupSeconds)
            {
                t += Time.deltaTime;
                if (_player) FaceTo(_player.position);
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, windupSeconds));
                SetGlow(k);
                yield return null;
            }
            SetGlow(0f);

            if (bulletPrefab && _player)
            {
                _state = State.Firing;
                onShoot?.Invoke();

                Vector3 aimPos = _player.position;
                var prb = _player.GetComponent<Rigidbody2D>();
                if (prb) aimPos += (Vector3)(prb.linearVelocity * aimLeadSeconds);

                Vector2 dir = (aimPos - transform.position).normalized;
                var b = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                b.speed = bulletSpeed;
                b.Fire(dir);

#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = -dir * recoilBack;
#else
                _rb.velocity = -dir * recoilBack;
#endif
            }

            _state = State.Recover;
            yield return new WaitForSeconds(0.2f);

            _nextFireTime = Time.time + fireCooldown;
            _state = State.Orbit;
        }

        private void FaceTo(Vector3 worldTarget)
        {
            Vector2 dir = (worldTarget - transform.position).normalized;
            if (dir.sqrMagnitude > 1e-6f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        void OnCollisionEnter2D(Collision2D c) { TryHitPlayer(c.collider, c.GetContact(0).point); }
        void OnTriggerEnter2D(Collider2D other) { TryHitPlayer(other, other.ClosestPoint(transform.position)); }

        private void TryHitPlayer(Collider2D col, Vector2 hitPoint)
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
                    Vector2 pushDir = (prb.position - (Vector2)transform.position).normalized;
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
            if (explosionPrefab) Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            onDeath?.Invoke();
            Destroy(gameObject);
        }

        private void SetGlow(float w01)
        {
            if (spriteRenderer && _hasBase)
            {
                float mul = Mathf.Lerp(1f, windupGlowMultiplier, Mathf.Clamp01(w01));
                Color tc = Color.Lerp(Color.white, windupGlowColor, 0.5f);
                var c = new Color(_baseMem.r * tc.r * mul, _baseMem.g * tc.g * mul, _baseMem.b * tc.b * mul, _baseMem.a);
                spriteRenderer.color = c;
            }
            if (glowLight)
                glowLight.intensity = glowLightBaseIntensity + glowLightAddOnPeak * Mathf.Clamp01(w01);
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
