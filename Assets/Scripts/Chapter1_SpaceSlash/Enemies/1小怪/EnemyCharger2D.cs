// EnemyCharger2D.cs — 极简性能版（仅逻辑 + 发光 + 爆炸）
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal; // 如果不用URP 2D，可移除此行
using FadedDreams.Player;

namespace FadedDreams.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class EnemyCharger2D : MonoBehaviour, IDamageable
    {
        [Header("Awareness / Orbit")]
        public float warnRadius = 25f;
        public float orbitRadius = 5f;
        public float approachSpeed = 6f;
        public float orbitTangentSpeed = 6f;

        [Header("Charge")]
        public float chargeWindup = 0.35f;
        public float chargeSpeed = 18f;
        public float chargeDuration = 0.5f;
        public Vector2 chargeCooldownRange = new Vector2(1.25f, 2.0f);

        [Header("Damage to Player")]
        public float energyDamage = 25f;
        public float damageCooldown = 0.6f;
        public float knockbackForce = 12f;

        [Header("Death FX")]
        public GameObject explosionPrefab;
        public UnityEvent onDeath;

        [Header("Explosion Audio")]
        public AudioClip explosionSFX;  // 爆炸音效（钢琴音）
        [Range(0f, 1f)] public float explosionVolume = 0.8f;
        [Range(0f, 0.5f)] public float pitchVariation = 0.15f;

        [Header("Telegraph / Ram Events")]
        public UnityEvent onWindup;
        public UnityEvent onCharge;
        public UnityEvent onHitPlayer;

        // ====== 仅保留：预备阶段发光（颜色乘法 + 可选Light2D） ======
        [Header("Visuals / Glow (Windup Only)")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color baseColor = Color.clear;
        [SerializeField] private Color windupGlowColor = Color.white;
        [SerializeField, Min(1f)] private float windupGlowMultiplier = 2.0f;
        [SerializeField] private AnimationCurve windupGlowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("可选：URP 2D Light2D 增强发光（不需要可留空或关闭自动创建）")]
        [SerializeField] private Light2D glowLight;
        [SerializeField, Min(0f)] private float glowLightBaseIntensity = 0f;
        [SerializeField, Min(0f)] private float glowLightAddOnPeak = 1.2f;

        [Header("Auto Setup")]
        [Tooltip("找不到 SpriteRenderer 时，自动创建一个1×1白色占位Sprite")]
        public bool autoCreatePlaceholderSprite = true;
        [Tooltip("没有 Light2D 时是否自动创建（若你不用URP 2D，请关掉以免报错）")]
        public bool autoCreateGlowLight2D = true;

        // ====== 运行态 ======
        public bool IsDead { get; private set; }
        private enum State { Idle, Orbit, Windup, Charge, Recover }
        private State _state;
        private Rigidbody2D _rb;
        private Transform _player;
        private float _nextChargeTime;
        private float _lastDamageTime = -999f;
        private Vector2 _chargeDir;
        private bool _dying, _quitting;

        private Color _memorizedBaseColor;
        private bool _hasMemorizedBase;

        // ---------- Unity ----------
        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector2.zero;
#endif
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (!_player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) _player = p.transform;
            }

            EnsureSpriteRenderer();
            EnsureGlowLight2D();
        }

        void OnApplicationQuit() { _quitting = true; }

        void Start()
        {
            _state = State.Idle;
            _nextChargeTime = Time.time + Random.Range(chargeCooldownRange.x, chargeCooldownRange.y);

            if (spriteRenderer)
            {
                if (baseColor.a <= 0f) baseColor = spriteRenderer.color;
                _memorizedBaseColor = baseColor;
                _hasMemorizedBase = true;
                ApplyColor(_memorizedBaseColor);
            }

            if (glowLight) glowLight.intensity = glowLightBaseIntensity;
        }

#if UNITY_EDITOR
        void Reset()
        {
            EnsureSpriteRenderer();
            EnsureGlowLight2D();
        }

        void OnValidate()
        {
            if (!Application.isPlaying && spriteRenderer && baseColor.a > 0f)
                spriteRenderer.color = baseColor;
            if (glowLight && !Application.isPlaying)
                glowLight.intensity = glowLightBaseIntensity;
        }
#endif

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
                    OrbitUpdate(d);
                    if (Time.time >= _nextChargeTime && d <= warnRadius + 2f)
                        StartCoroutine(CoWindupThenCharge());
                    break;

                case State.Windup:
                case State.Charge:
                case State.Recover:
                    // 协程驱动
                    break;
            }
        }

        private void OrbitUpdate(float distToPlayer)
        {
            Vector2 toPlayer = ((Vector2)_player.position - (Vector2)transform.position);
            Vector2 dir = toPlayer.sqrMagnitude > 1e-6f ? toPlayer.normalized : Vector2.right;

            Vector2 ringTarget = (Vector2)_player.position - dir * orbitRadius;
            Vector2 radial = (ringTarget - (Vector2)transform.position).normalized * approachSpeed;
            Vector2 tangent = new Vector2(dir.y, -dir.x) * orbitTangentSpeed;

#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = radial + tangent;
#else
            _rb.velocity = radial + tangent;
#endif
        }

        private IEnumerator CoWindupThenCharge()
        {
            _state = State.Windup;
            onWindup?.Invoke();

#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector2.zero;
#else
            _rb.velocity = Vector2.zero;
#endif

            // 预备阶段：面向玩家 + 发光渐强
            float t = 0f;
            while (t < chargeWindup)
            {
                t += Time.deltaTime;
                FaceTo(_player.position);

                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, chargeWindup));
                float w = windupGlowCurve != null ? windupGlowCurve.Evaluate(k) : k;
                SetGlowWeight(w);
                yield return null;
            }

            _state = State.Charge;
            onCharge?.Invoke();

            Vector2 aim = ((Vector2)_player.position - (Vector2)transform.position);
            _chargeDir = aim.sqrMagnitude < 1e-4f ? Vector2.right : aim.normalized;

            float dur = 0f;
            while (dur < chargeDuration)
            {
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = _chargeDir * chargeSpeed;
#else
                _rb.velocity = _chargeDir * chargeSpeed;
#endif
                dur += Time.deltaTime;
                yield return null;
            }

            // 收尾 + 冷却
            _state = State.Recover;
            float rec = 0.25f;
            while (rec > 0f)
            {
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, 0.15f);
#else
                _rb.velocity = Vector2.Lerp(_rb.velocity, Vector2.zero, 0.15f);
#endif
                rec -= Time.deltaTime;
                yield return null;
            }

            // 发光回到基础
            SetGlowWeight(0f);

            _nextChargeTime = Time.time + Random.Range(chargeCooldownRange.x, chargeCooldownRange.y);
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

        // ---------- 碰撞伤害（保持不变） ----------
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
                onHitPlayer?.Invoke();

                var prb = plc.GetComponent<Rigidbody2D>();
                if (prb)
                {
                    Vector2 pushDir = (prb.position - (Vector2)transform.position).normalized;
                    prb.AddForce(pushDir * knockbackForce, ForceMode2D.Impulse);
                }
            }
        }

        // ---------- 受伤/死亡（仅保留爆炸） ----------
        public void TakeDamage(float amount)
        {
            if (IsDead || _dying) return;
            Die();
        }

        private void Die()
        {
            if (_dying) return;
            _dying = true; IsDead = true;

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
                tempSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                tempSource.Play();
                Destroy(tempGO, explosionSFX.length + 0.1f);
            }
            
            onDeath?.Invoke();

            Destroy(gameObject);
        }

        void OnDestroy()
        {
            IsDead = true;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, warnRadius);
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, orbitRadius);
        }
#endif

        // ---------- Glow / Color ----------
        private void SetGlowWeight(float w)
        {
            if (spriteRenderer)
            {
                float mul = Mathf.Lerp(1f, windupGlowMultiplier, Mathf.Clamp01(w));
                Color c = MultiplyColor(_memorizedBaseColor, windupGlowColor, mul);
                ApplyColor(c);
            }
            if (glowLight)
                glowLight.intensity = glowLightBaseIntensity + glowLightAddOnPeak * Mathf.Clamp01(w);
        }

        private void ApplyColor(Color c)
        {
            if (spriteRenderer) spriteRenderer.color = c;
        }

        private static Color MultiplyColor(Color baseC, Color tint, float mul)
        {
            var t = new Color(
                Mathf.Lerp(1f, Mathf.Max(0.0001f, tint.r), 0.5f),
                Mathf.Lerp(1f, Mathf.Max(0.0001f, tint.g), 0.5f),
                Mathf.Lerp(1f, Mathf.Max(0.0001f, tint.b), 0.5f),
                1f);
            return new Color(baseC.r * t.r * mul, baseC.g * t.g * mul, baseC.b * t.b * mul, baseC.a);
        }

        // ---------- 自动创建（仅保留 SR 与 Glow Light） ----------
        private void EnsureSpriteRenderer()
        {
            if (spriteRenderer) return;

            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            if (srs != null && srs.Length > 0)
            {
                SpriteRenderer pick = srs[0];
                float best = -1f;
                foreach (var sr in srs)
                {
                    float score = sr.sprite ? sr.sprite.rect.size.sqrMagnitude : 0.1f;
                    score += sr.sortingOrder * 0.01f;
                    if (score > best) { best = score; pick = sr; }
                }
                spriteRenderer = pick;
                return;
            }

            if (!autoCreatePlaceholderSprite) return;

            var spriteGO = new GameObject("Sprite");
            spriteGO.transform.SetParent(transform, false);
            spriteGO.transform.localPosition = Vector3.zero;

            var srNew = spriteGO.AddComponent<SpriteRenderer>();
            srNew.sprite = CreateWhitePixelSprite();
            srNew.color = Color.white;
            spriteRenderer = srNew;

            if (baseColor.a <= 0f) baseColor = srNew.color;
        }

        private void EnsureGlowLight2D()
        {
            if (glowLight || !autoCreateGlowLight2D) return;

            var light = GetComponentInChildren<Light2D>(true);
            if (!light)
            {
                var lightGO = new GameObject("GlowLight2D");
                lightGO.transform.SetParent(transform, false);
                lightGO.transform.localPosition = Vector3.zero;
                light = lightGO.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.pointLightInnerRadius = 0.2f;
                light.pointLightOuterRadius = Mathf.Max(orbitRadius * 0.75f, 2.5f);
                light.intensity = glowLightBaseIntensity;
                light.color = Color.white;
                light.shadowIntensity = 0f;
                light.falloffIntensity = 0.5f;
            }
            glowLight = light;
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1),
                                 new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
