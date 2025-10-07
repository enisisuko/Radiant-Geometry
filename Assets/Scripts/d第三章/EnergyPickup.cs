using UnityEngine;
using System.Collections;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal; // 可选：给 Light2D 做淡出
#endif

namespace FadedDreams.World
{
    /// <summary>
    /// 掉落能量拾取物：
    /// - 带磁吸与寿命
    /// - 被玩家吸收时不再“瞬间消失”，先播放吸收动画后销毁
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnergyPickup : MonoBehaviour
    {
        [Header("Energy")]
        public FadedDreams.Player.ColorMode energyColor = FadedDreams.Player.ColorMode.Red;
        public float amount = 20f;
        public float life = 12f;

        [Header("Magnet")]
        public float attractRadius = 6f;
        public float absorbRadius = 0.35f;   // 兜底吸收半径（不依赖触发器）
        public float flySpeed = 8f;

        [Header("Absorb Animation")]
        [Tooltip("吸收动画时长（秒）")]
        public float absorbDuration = 0.30f;
        [Tooltip("开始吸收时，先轻微放大到这个倍率（>1 有“吸”感）")]
        public float absorbScaleUp = 1.15f;
        [Tooltip("动画过程中每秒自旋的角速度（度/秒）")]
        public float absorbSpinSpeed = 540f;
        [Tooltip("动画结束时缩放到这个倍率（建议 0.0~0.2）")]
        public float endScale = 0.02f;
        [Tooltip("是否在动画期间缓动贴近玩家位置（更有“吸入”感）")]
        public bool glideToPlayer = true;
        [Tooltip("贴近玩家的位移强度（0~1，越大越快靠近玩家）")]
        [Range(0f, 1f)] public float glideStrength = 0.85f;
        [Tooltip("吸收发生时，是否立刻加能量（否则在动画结束才加）")]
        public bool grantEnergyAtStart = true;

        [Header("Optional FX")]
        [Tooltip("吸收瞬间播放的粒子预制体（可空）")]
        public GameObject absorbBurstVfx;
        [Tooltip("吸收瞬间播放的音效（可空）")]
        public AudioClip absorbSfx;
        [Range(0f, 1f)] public float absorbSfxVolume = 0.9f;

        // 缓存
        private Transform _player;
        private Collider2D _col;
        private SpriteRenderer _sr; // 可选淡出
#if UNITY_RENDERING_UNIVERSAL
        private Light2D _light;     // 可选淡出
#endif
        private bool _absorbing;
        private float _lifeTimer;

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
#if UNITY_RENDERING_UNIVERSAL
            _light = GetComponent<Light2D>();
#endif
            // 确保是触发器（与原行为一致）
            _col.isTrigger = true;
        }

        private void Start()
        {
            _player = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>()?.transform;
            _lifeTimer = life;
        }

        private void Update()
        {
            if (_absorbing) return; // 动画期间不再执行“磁吸移动/寿命销毁”

            // 寿命计时
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (!_player) return;

            float d = Vector2.Distance(transform.position, _player.position);
            if (d <= attractRadius)
            {
                // 朝玩家匀速飞去
                Vector3 dir = (_player.position - transform.position).normalized;
                transform.position += dir * flySpeed * Time.deltaTime;
            }

            // 额外兜底：贴脸也能吃到
            if (d <= absorbRadius)
                AbsorbToPlayer();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_absorbing) return;
            if (!other.CompareTag("Player")) return;
            AbsorbToPlayer();
        }

        private void AbsorbToPlayer()
        {
            if (_absorbing) return;
            _absorbing = true;

            // 禁用碰撞，避免二次触发
            if (_col) _col.enabled = false;

            // 恢复能量：可选择“立刻给”或“动画结束给”
            if (grantEnergyAtStart)
                GrantEnergy();

            // 播放一次粒子/音效
            if (absorbBurstVfx)
            {
                var v = Instantiate(absorbBurstVfx, transform.position, Quaternion.identity);
                // 让粒子自己销毁（若其内没有自毁，可在 2s 后强制）
                Destroy(v, 2f);
            }
            if (absorbSfx)
                AudioSource.PlayClipAtPoint(absorbSfx, transform.position, absorbSfxVolume);

            // 启动吸收动画协程
            StartCoroutine(CoAbsorbAnim());
        }

        private void GrantEnergy()
        {
            var pcm = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>();
            if (pcm) pcm.AddEnergy(energyColor, amount);
        }

        private IEnumerator CoAbsorbAnim()
        {
            float t = 0f;
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            Vector3 upScale = startScale * Mathf.Max(0.01f, absorbScaleUp);
            Vector3 endScaleVec = startScale * Mathf.Max(0.001f, endScale);

            // 先一个“轻微放大”的过渡（占总时长的 30%）
            float upTime = absorbDuration * 0.30f;
            float downTime = absorbDuration - upTime;

            // 放大段
            while (t < upTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / upTime);
                transform.localScale = Vector3.Lerp(startScale, upScale, EaseOutCubic(k));
                transform.Rotate(0f, 0f, absorbSpinSpeed * Time.deltaTime);

                if (glideToPlayer && _player)
                {
                    // 向玩家靠拢一点
                    Vector3 target = _player.position;
                    transform.position = Vector3.Lerp(transform.position, target, glideStrength * Time.deltaTime * 8f);
                }

                // 渐淡（前段稍微淡一点）
                FadeTo(1f - 0.25f * k);
                yield return null;
            }

            // 缩小消失段
            t = 0f;
            while (t < downTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / downTime);
                transform.localScale = Vector3.Lerp(upScale, endScaleVec, EaseInCubic(k));
                transform.Rotate(0f, 0f, absorbSpinSpeed * Time.deltaTime);

                if (glideToPlayer && _player)
                {
                    Vector3 target = _player.position;
                    transform.position = Vector3.Lerp(transform.position, target, glideStrength * Time.deltaTime * 10f);
                }

                // 完整淡出
                FadeTo(1f - k);
                yield return null;
            }

            // 若选择在动画结束才加能量，这里补发
            if (!grantEnergyAtStart)
                GrantEnergy();

            Destroy(gameObject);
        }

        // ===== 工具函数 =====
        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        private static float EaseInCubic(float x) => x * x * x;

        private void FadeTo(float alpha01)
        {
            if (_sr)
            {
                var c = _sr.color; c.a = Mathf.Clamp01(alpha01);
                _sr.color = c;
            }
#if UNITY_RENDERING_UNIVERSAL
            if (_light)
            {
                _light.intensity = Mathf.Max(0f, _light.intensity) * Mathf.Clamp01(alpha01);
            }
#endif
        }
    }
}
