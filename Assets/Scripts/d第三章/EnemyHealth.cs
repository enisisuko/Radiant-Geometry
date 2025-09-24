using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

namespace FadedDreams.Enemies
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("HP")]
        public float maxHp = 60f;
        public float spawnInvulnerableSeconds = 0.25f;   // 出生短暂无敌，避免初始化误伤
        private float _hp;
        private float _spawnTime;

        [Header("Death VFX / Drop")]
        public GameObject deathVfxPrefab;                 // 死亡特效（可选）
        public GameObject dropPrefab;                     // 指向挂了 EnergyPickup 的预制体

        [Header("Explosion Light (一瞬间很亮后渐暗)")]
        public bool spawnExplosionLight = true;           // 是否在死亡时生成爆炸光
        public Color explosionLightColor = Color.red;     // 光的颜色
        [Min(0f)] public float explosionPeakIntensity = 8f; // 爆炸峰值亮度
        [Min(0f)] public float explosionInnerRadius = 0.5f; // 内半径
        [Min(0f)] public float explosionOuterRadius = 4.0f; // 外半径
        [Min(0.05f)] public float explosionFadeSeconds = 0.65f; // 从峰值到0的时长
        public AnimationCurve explosionIntensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        // 曲线：0~1时间，1为峰值，0为熄灭。可在Inspector里自定义形状（例如先上冲再下落）

        [Header("Screen Shake (震屏)")]
        public bool shakeOnDeath = true;
        [Min(0f)] public float shakeDuration = 0.2f;     // 震屏时长（秒）
        [Min(0f)] public float shakeStrength = 0.5f;     // 震幅（世界空间偏移最大值）
        public float shakeFrequency = 24f;               // 每秒抖动次数

        public bool IsDead { get; private set; }

        private FadedDreams.World.ModeVisibilityFilter _colorRef;

        private void Awake()
        {
            _hp = maxHp;
            _spawnTime = Time.time;
            _colorRef = GetComponent<FadedDreams.World.ModeVisibilityFilter>();
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            if (Time.time - _spawnTime < spawnInvulnerableSeconds) return;

            _hp -= amount;
            if (_hp <= 0f) Die();
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;

            // 一次性特效（粒子等）
            if (deathVfxPrefab)
            {
                var vfx = Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            // 爆炸光效（瞬亮→渐灭）
            if (spawnExplosionLight) SpawnExplosionLight();

            // 震屏
            if (shakeOnDeath) CameraShake2D.Shake(shakeDuration, shakeStrength, shakeFrequency);

            // 根据自身阵营的相反色来掉落（保持原逻辑）
            if (dropPrefab)
            {
                var go = Instantiate(dropPrefab, transform.position, Quaternion.identity);
                var pick = go.GetComponent<FadedDreams.World.EnergyPickup>();
                if (pick)
                {
                    var selfColor = _colorRef ? _colorRef.objectColor : FadedDreams.Player.ColorMode.Red;
                    pick.energyColor = Opposite(selfColor);
                }
            }

            Destroy(gameObject);
        }

        private void SpawnExplosionLight()
        {
            var go = new GameObject("ExplosionLight_Temp");
            go.transform.position = transform.position;

            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.intensity = explosionPeakIntensity; // 先到峰值
            l.pointLightInnerRadius = explosionInnerRadius;
            l.pointLightOuterRadius = explosionOuterRadius;
            l.color = explosionLightColor;
            l.shadowIntensity = 0f; // 如需阴影可调大
            l.volumeIntensity = 0f; // 若用体积光，可在URP启用后调整

            // 渐隐协程
            go.AddComponent<ExplosionLightFader>().Begin(l, explosionFadeSeconds, explosionIntensityCurve, explosionPeakIntensity);
        }

        private static FadedDreams.Player.ColorMode Opposite(FadedDreams.Player.ColorMode m)
            => (m == FadedDreams.Player.ColorMode.Red)
                ? FadedDreams.Player.ColorMode.Green
                : FadedDreams.Player.ColorMode.Red;
    }

    /// <summary>
    /// 将 Light2D 从峰值强度沿曲线在指定时长内淡出至 0，并在结束时销毁。
    /// </summary>
    internal class ExplosionLightFader : MonoBehaviour
    {
        private Light2D _l;
        private float _dur;
        private AnimationCurve _curve;
        private float _peak;

        public void Begin(Light2D l, float duration, AnimationCurve curve, float peak)
        {
            _l = l; _dur = Mathf.Max(0.01f, duration); _curve = curve ?? AnimationCurve.Linear(0, 1, 1, 0); _peak = peak;
            StartCoroutine(Fade());
        }

        private System.Collections.IEnumerator Fade()
        {
            float t = 0f;
            while (t < _dur && _l)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / _dur);
                float k = Mathf.Clamp01(_curve.Evaluate(norm));
                _l.intensity = _peak * k;
                yield return null;
            }
            if (_l) _l.intensity = 0f;
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 极简2D震屏：暂存初始位置 → 在时长内以Perlin噪声抖动 → 还原。
    /// 将此脚本放在任意场景物体上都可静态调用（它会自动生成一个运行体）。
    /// 如你已有 Cinemachine Impulse，可将这里替换为发Impulse的实现。
    /// </summary>
    public class CameraShake2D : MonoBehaviour
    {
        private static CameraShake2D _instance;
        private Transform _cam;
        private Vector3 _origin;
        private bool _shaking;

        private void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void EnsureCamera()
        {
            if (_cam == null && Camera.main != null)
            {
                _cam = Camera.main.transform;
            }
        }

        public static void Shake(float duration, float strength, float frequency)
        {
            if (_instance == null)
            {
                new GameObject("[CameraShake2D]").AddComponent<CameraShake2D>();
            }
            _instance.StartShake(duration, strength, frequency);
        }

        private void StartShake(float duration, float strength, float frequency)
        {
            EnsureCamera();
            if (_cam == null || duration <= 0f || strength <= 0f) return;
            if (_shaking) StopAllCoroutines();
            StartCoroutine(CoShake(duration, strength, frequency));
        }

        private System.Collections.IEnumerator CoShake(float duration, float strength, float frequency)
        {
            _shaking = true;
            _origin = _cam.localPosition;
            float t = 0f;
            float seedX = Random.value * 100f;
            float seedY = Random.value * 100f;
            frequency = Mathf.Max(0.01f, frequency);

            while (t < duration)
            {
                t += Time.deltaTime;
                float nX = Mathf.PerlinNoise(seedX, Time.time * frequency) * 2f - 1f;
                float nY = Mathf.PerlinNoise(seedY, Time.time * frequency) * 2f - 1f;
                _cam.localPosition = _origin + new Vector3(nX, nY, 0f) * strength;
                yield return null;
            }

            _cam.localPosition = _origin;
            _shaking = false;
        }
    }
}
