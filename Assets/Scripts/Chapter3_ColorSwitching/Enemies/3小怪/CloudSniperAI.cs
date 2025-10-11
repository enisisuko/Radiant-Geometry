using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.Enemies
{
    /// <summary>
    /// 云（改造版）：移动逻辑沿用 SmartMove，
    /// 攻击从“激光”改为：
    /// 1) 子弹齐射（5~7发，朝开火瞬间的玩家位置）
    /// 2) 震爆弹（蓄力2s：自身Light2D强度×2 → 发射慢速大弹 → 撞到物体爆炸）
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CloudSniperAI : MonoBehaviour
    {
        [Header("Common")]
        public LayerMask playerMask;
        public LayerMask obstacleMask;

        [Header("Bullet Volley")]
        public BulletProjectile bulletPrefab;
        public float bulletDamage = 12f;
        public float bulletSpeed = 12f;
        public Vector2 volleyCountRange = new Vector2(5, 7);
        public float volleyCooldown = 2.2f;

        [Header("Shockwave Grenade")]
        public ShockwaveGrenade grenadePrefab;
        public float grenadeDamage = 26f;
        public float grenadeSpeed = 6f;       // bulletSpeed 的一半
        public float grenadeCooldown = 4.0f;
        public float windupSeconds = 2.0f;    // 蓄力2秒
        public float glowIntensityMul = 2f;   // 蓄力时自身发光×2

        [Header("Movement (smart)")]
        public float idealRange = 8f;
        public float minRange = 4.5f;
        public float maxRange = 12f;
        public float moveSpeed = 2.2f;
        public float strafeSpeed = 1.8f;
        public float avoidTurn = 20f;
        public float obstacleProbe = 1.0f;

        [Header("音效配置")]
        [Tooltip("子弹发射音效")]
        public AudioClip bulletShootSound;
        [Tooltip("震爆弹蓄力音效")]
        public AudioClip grenadeChargeSound;
        [Tooltip("震爆弹发射音效")]
        public AudioClip grenadeShootSound;
        [Tooltip("音效音量")]
        [Range(0f, 1f)] public float soundVolume = 0.8f;

        // 音频组件
        private AudioSource _audioSource;

        private Transform _player;
        private Rigidbody2D _rb;
        private bool _busy;
        private Light2D _selfLight;
        private float _baseIntensity;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _selfLight = GetComponentInChildren<Light2D>();
            if (_selfLight) _baseIntensity = _selfLight.intensity;

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

        private void Update()
        {
            if (!_player) _player = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>()?.transform;
            if (!_player) { _rb.linearVelocity = Vector2.zero; return; }

            if (!_busy) StartCoroutine(CoAttackCycle());
            else SmartMove();
        }

        private IEnumerator CoAttackCycle()
        {
            _busy = true;

            // 轮换：先齐射 → 再震爆弹 → 冷却
            yield return StartCoroutine(CoBulletVolley());
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(CoGrenade());
            yield return new WaitForSeconds(0.35f);

            _busy = false;
        }

        private IEnumerator CoBulletVolley()
        {
            SmartMove(); // 起手时先调整下位置
            int count = Mathf.RoundToInt(Random.Range(volleyCountRange.x, volleyCountRange.y + 0.99f));

            Vector3 firePos = transform.position;
            Vector3 targetPos = _player.position; // 使用“开火瞬间”的玩家位置
            Vector2 dir = (targetPos - firePos).normalized;

            for (int i = 0; i < count; i++)
            {
                if (!bulletPrefab) break;
                
                // 播放子弹发射音效
                if (bulletShootSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(bulletShootSound, soundVolume * 0.6f); // 连发音效稍小
                }
                
                var b = Instantiate(bulletPrefab, firePos, Quaternion.identity);
                b.playerMask = playerMask;
                b.obstacleMask = obstacleMask;
                b.damage = bulletDamage;
                b.speed = bulletSpeed;

                // 可添加轻微散布（±3°）
                float spread = Random.Range(-3f, 3f);
                Vector2 d = Quaternion.Euler(0, 0, spread) * dir;
                b.Fire(d);

                yield return new WaitForSeconds(0.06f);
            }

            yield return new WaitForSeconds(volleyCooldown);
        }

        private IEnumerator CoGrenade()
        {
            // 播放蓄力音效
            if (grenadeChargeSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(grenadeChargeSound, soundVolume);
            }

            // 蓄力：2秒，自身Light2D强度×2
            float t = 0f;
            if (_selfLight) _selfLight.intensity = _baseIntensity * glowIntensityMul;

            while (t < windupSeconds)
            {
                t += Time.deltaTime;
                SmartMove(); // 蓄力同时做位移（保持机动）
                yield return null;
            }

            if (_selfLight) _selfLight.intensity = _baseIntensity;

            // 播放发射音效
            if (grenadeShootSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(grenadeShootSound, soundVolume);
            }

            // 发射
            if (grenadePrefab)
            {
                Vector3 S = transform.position;
                Vector2 D = (_player.position - S).normalized;
                var g = Instantiate(grenadePrefab, S, Quaternion.identity);
                g.playerMask = playerMask;
                g.obstacleMask = obstacleMask;
                g.damage = grenadeDamage;
                g.speed = grenadeSpeed;
                g.Fire(D);
            }

            yield return new WaitForSeconds(grenadeCooldown);
        }

        private void SmartMove()
        {
            Vector2 toPlayer = (_player.position - transform.position);
            float d = toPlayer.magnitude;
            Vector2 dir = toPlayer.normalized;

            Vector2 vel = Vector2.zero;

            if (d < minRange)
            {
                Vector2 away = -dir;
                Vector2 strafe = Vector2.Perpendicular(dir) * (Random.value < .5f ? 1f : -1f);
                vel = (away * 0.8f + strafe * 0.2f).normalized * moveSpeed;
            }
            else if (d > maxRange)
            {
                vel = dir * moveSpeed;
            }
            else
            {
                Vector2 tangent = Vector2.Perpendicular(dir) * (Mathf.Sin(Time.time * 1.7f) > 0 ? 1f : -1f);
                vel = tangent.normalized * strafeSpeed;
            }

            Vector2 fwd = vel.sqrMagnitude > 0.01f ? vel.normalized : dir;
            var hit = Physics2D.Raycast(transform.position, fwd, obstacleProbe, obstacleMask);
            if (hit.collider)
                vel = Quaternion.Euler(0, 0, (Random.value < .5f ? avoidTurn : -avoidTurn)) * vel;

            _rb.linearVelocity = vel;
        }
    }
}
