// DarkSpriteAI.cs — 红量→发光/染色；自爆时关闭这些可视化；可调常驻光半径
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using FadedDreams.Player;
using FadedDreams.Enemies;

namespace FadedDreams.Enemies
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(EnemySelfExplodeOnOverheat))] // 统一死亡=自爆
    public class DarkSpriteAI : MonoBehaviour, IDamageable
    {
        [Header("Refs")]
        public Transform player;
        public Rigidbody2D rb;

        [Header("Detect / Hover")]
        public float detectRadius = 10f;
        public float hoverHeight = 2.5f;
        public float orbitRadius = 2.2f;
        public float approachSpeed = 6f;
        public float orbitAngularSpeed = 120f; // deg/s

        [Header("Windup (Pre-attack)")]
        public float windupTime = 2f;
        public float windupMoveFactor = 0.2f;
        public float shakeAmpMax = 0.4f;
        public float shakeFreq = 18f;

        [Header("Dive Attack")]
        public float diveSpeed = 14f;
        public float recoverUpSpeed = 10f;
        public float postAttackCooldown = 1.0f;

        [Header("Combat")]
        public float maxHp = 100f;
        public float contactDamageCooldown = 0.8f;

        // === 红量→发光/染色（常驻，可视化反馈）===
        [Header("Red-Driven Glow (Aura)")]
        [Tooltip("常驻可视化的 Light2D（建议敌人子物体上一个Point Light2D）")]
        public Light2D auraLight;
        [Tooltip("0红量时的最小亮度")]
        public float auraMinIntensity = 0.1f;
        [Tooltip("满红量时的最大亮度")]
        public float auraMaxIntensity = 4f;
        [Tooltip("0红量→满红量 的颜色渐变")]
        public Gradient auraColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.4f,0.2f,0.2f), 0f),
                new GradientColorKey(new Color(1f,0.2f,0.1f), 1f),
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(1f,   1f),
            }
        };
        [Range(0.01f, 30f)]
        public float auraLerpSpeed = 10f;

        [Tooltip("0红量时的最小半径（可缩小可视化范围）")]
        public float auraMinRadius = 0.8f;
        [Tooltip("满红量时的最大半径")]
        public float auraMaxRadius = 2.0f;

        [Tooltip("自爆时是否立即关闭Aura/染色")]
        public bool disableAuraAfterExplode = true;

        [Header("Optional: Sprite Tint")]
        public SpriteRenderer bodySprite;
        public Color bodyColorMin = new Color(0.6f, 0.6f, 0.6f, 1f);
        public Color bodyColorMax = new Color(1f, 0.4f, 0.4f, 1f);

        [Header("音效配置")]
        [Tooltip("俯冲攻击音效")]
        public AudioClip attackSound;
        [Tooltip("受击音效")]
        public AudioClip hitSound;
        [Tooltip("死亡爆炸音效")]
        public AudioClip deathSound;
        [Tooltip("音效音量")]
        [Range(0f, 1f)] public float soundVolume = 0.8f;

        // 音频组件
        private AudioSource _audioSource;

        private float auraTargetIntensity, auraTargetRadius;
        private Color auraTargetColor, bodyTargetColor;

        private float hp, cd;
        private float theta;
        private float windupTimer, cooldown;
        private Vector2 diveDir;
        private Vector2 positionNoise;

        private enum State { Idle, Seek, Hover, Windup, Dive, Recover, Exploding, Dead }
        private State state = State.Idle;

        private RedLightController red;
        private EnemySelfExplodeOnOverheat explode;
        private UnityAction<float, float> _onRedChangedForExplode;
        private UnityAction<float, float> _onRedChangedForGlow;

        public bool IsDead => state == State.Dead;

        private void Awake()
        {
            hp = maxHp;
            if (!rb) rb = GetComponent<Rigidbody2D>();
            red = GetComponent<RedLightController>();
            explode = GetComponent<EnemySelfExplodeOnOverheat>();

            if (!auraLight) auraLight = GetComponentInChildren<Light2D>(includeInactive: true);
            if (auraLight) auraLight.enabled = true; // 常驻显示

            // 获取或添加音频组件
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D音效
            _audioSource.volume = soundVolume;
        }

        private void OnEnable()
        {
            if (red != null)
            {
                if (red.onChanged == null) red.onChanged = new UnityEvent<float, float>();

                // A) 满红=自爆
                _onRedChangedForExplode = (cur, max) =>
                {
                    if (cur >= max && state != State.Exploding && state != State.Dead)
                        SelfExplode();
                };
                red.onChanged.RemoveListener(_onRedChangedForExplode);
                red.onChanged.AddListener(_onRedChangedForExplode);

                // B) 红量变化→更新可视化目标
                _onRedChangedForGlow = (cur, max) =>
                {
                    float t = (max > 0f) ? Mathf.Clamp01(cur / max) : 0f;
                    auraTargetIntensity = Mathf.Lerp(auraMinIntensity, auraMaxIntensity, t);
                    auraTargetRadius = Mathf.Lerp(auraMinRadius, auraMaxRadius, t);
                    auraTargetColor = auraColor.Evaluate(t);
                    bodyTargetColor = Color.Lerp(bodyColorMin, bodyColorMax, t);
                };
                red.onChanged.RemoveListener(_onRedChangedForGlow);
                red.onChanged.AddListener(_onRedChangedForGlow);

                // 初始化一次
                _onRedChangedForGlow.Invoke(red.Current, red.Max);
            }
        }

        private void OnDisable()
        {
            if (red != null)
            {
                if (_onRedChangedForExplode != null) red.onChanged.RemoveListener(_onRedChangedForExplode);
                if (_onRedChangedForGlow != null) red.onChanged.RemoveListener(_onRedChangedForGlow);
            }
        }

        private void Update()
        {
            // 平滑插值常驻可视化（非爆炸期）
            if (state != State.Exploding)
            {
                if (auraLight)
                {
                    auraLight.intensity = Mathf.Lerp(auraLight.intensity, auraTargetIntensity, auraLerpSpeed * Time.deltaTime);
                    auraLight.color = Color.Lerp(auraLight.color, auraTargetColor, auraLerpSpeed * Time.deltaTime);
                    auraLight.pointLightOuterRadius =
                        Mathf.Lerp(auraLight.pointLightOuterRadius, auraTargetRadius, auraLerpSpeed * Time.deltaTime);
                }
                if (bodySprite)
                    bodySprite.color = Color.Lerp(bodySprite.color, bodyTargetColor, auraLerpSpeed * Time.deltaTime);
            }

            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }

            if (cd > 0) cd -= Time.deltaTime;
            if (cooldown > 0) cooldown -= Time.deltaTime;

            switch (state)
            {
                case State.Idle:
                case State.Seek: Tick_Seek(); break;
                case State.Hover: Tick_Hover(); break;
                case State.Windup: Tick_Windup(); break;
                case State.Dive: Tick_Dive(); break;
                case State.Recover: Tick_Recover(); break;
            }
        }

        private void Tick_Seek()
        {
            if (!player) return;
            float d = Vector2.Distance(player.position, transform.position);
            if (d <= detectRadius)
            {
                state = State.Hover;
                theta = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            }
        }

        private void Tick_Hover()
        {
            if (!player) { state = State.Seek; return; }
            Vector2 center = (Vector2)player.position + Vector2.up * hoverHeight;
            Vector2 targetOnRing = center + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * orbitRadius;
            MoveTowards(targetOnRing, approachSpeed);
            theta += orbitAngularSpeed * Mathf.Deg2Rad * Time.deltaTime;

            if (cooldown <= 0f && Vector2.Distance(transform.position, targetOnRing) < 0.6f)
            {
                state = State.Windup;
                windupTimer = windupTime;
            }
        }

        private void Tick_Windup()
        {
            if (!player) { state = State.Seek; return; }
            Vector2 center = (Vector2)player.position + Vector2.up * hoverHeight;
            Vector2 targetOnRing = center + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * orbitRadius;
            MoveTowards(targetOnRing, approachSpeed * windupMoveFactor);

            float t = 1f - Mathf.Clamp01((windupTimer / Mathf.Max(0.0001f, windupTime)));
            float amp = Mathf.Lerp(0f, shakeAmpMax, t);
            positionNoise.x = Mathf.Sin(Time.time * shakeFreq) * amp;
            positionNoise.y = Mathf.Cos(Time.time * shakeFreq * 0.9f) * amp;
            AddPositionOffset(positionNoise);

            windupTimer -= Time.deltaTime;
            if (windupTimer <= 0f)
            {
                diveDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
                state = State.Dive;
                
                // 播放攻击音效
                if (attackSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(attackSound, soundVolume);
                }
            }
        }

        private void Tick_Dive()
        {
            MoveAlong(diveDir, diveSpeed);
            windupTimer += Time.deltaTime;
            if (windupTimer >= 0.6f || (player && transform.position.y < player.position.y - 1f))
            {
                state = State.Recover;
                windupTimer = 0f;
                cooldown = postAttackCooldown;
            }
        }

        private void Tick_Recover()
        {
            MoveAlong(Vector2.up, recoverUpSpeed);
            if (player)
                state = (Vector2.Distance(transform.position, player.position) <= detectRadius) ? State.Hover : State.Seek;
            else
                state = State.Seek;
        }

        // --- movement helpers ---
        private void MoveTowards(Vector2 target, float speed)
        {
            Vector2 pos = transform.position;
            Vector2 next = Vector2.MoveTowards(pos, target, speed * Time.deltaTime);
            SetPosition(next);
        }
        private void MoveAlong(Vector2 dir, float speed)
        {
            Vector2 pos = transform.position;
            Vector2 next = pos + dir.normalized * (speed * Time.deltaTime);
            SetPosition(next);
        }
        private void AddPositionOffset(Vector2 delta)
        {
            SetPosition((Vector2)transform.position + delta * Time.deltaTime);
        }
        private void SetPosition(Vector2 p)
        {
            if (rb) rb.MovePosition(p);
            else transform.position = p;
        }

        // --- combat ---
        public void TakeDamage(float amount)
        {
            if (IsDead || state == State.Exploding) return;
            
            // 播放受击音效
            if (hitSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(hitSound, soundVolume * 0.6f); // 受击音效稍微小一点
            }
            
            hp -= amount;
            if (hp <= 0f) SelfExplode();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (cd > 0) return;
            if (collision.collider.CompareTag("Player"))
            {
                var r = collision.collider.GetComponentInParent<RedLightController>();
                if (r) r.OnHitByDarkSprite();
                cd = contactDamageCooldown;
            }
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (cd > 0) return;
            if (other.CompareTag("Player"))
            {
                var r = other.GetComponentInParent<RedLightController>();
                if (r) r.OnHitByDarkSprite();
                cd = contactDamageCooldown;
            }
        }
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (cd > 0) return;
            var r = collision.collider.GetComponentInParent<RedLightController>();
            if (r) { r.OnHitByDarkSprite(); cd = contactDamageCooldown; }
        }
        private void OnTriggerStay2D(Collider2D other)
        {
            if (cd > 0) return;
            var r = other.GetComponentInParent<RedLightController>();
            if (r) { r.OnHitByDarkSprite(); cd = contactDamageCooldown; }
        }

        public void SelfExplode()
        {
            if (state == State.Exploding || state == State.Dead) return;
            state = State.Exploding;

            // 播放死亡音效
            if (deathSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(deathSound, soundVolume);
            }

            // 自爆瞬间：取消可视化反馈（只保留爆炸光）
            if (disableAuraAfterExplode && auraLight) auraLight.enabled = false;
            if (bodySprite) bodySprite.color = bodyColorMin;

            var col = GetComponent<Collider2D>(); if (col) col.enabled = false;
            if (rb) { rb.linearVelocity = Vector2.zero; rb.isKinematic = true; }

            // 统一：交给爆炸器处理点燃/伤害/击退/屏震/延迟销毁
            explode.TriggerExplosion();
        }
    }
}
