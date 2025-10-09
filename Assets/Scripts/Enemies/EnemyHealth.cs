using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

namespace FadedDreams.Enemies
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("HP")]
        public float maxHp = 60f;
        public float spawnInvulnerableSeconds = 0.25f;   // ���������޵У������ʼ������
        private float _hp;
        private float _spawnTime;

        [Header("Death VFX / Drop")]
        public GameObject deathVfxPrefab;                 // ������Ч����ѡ��
        public GameObject dropPrefab;                     // ָ����� EnergyPickup ��Ԥ����

        [Header("Explosion Light (һ˲������󽥰�)")]
        public bool spawnExplosionLight = true;           // �Ƿ�������ʱ���ɱ�ը��
        public Color explosionLightColor = Color.red;     // �����ɫ
        [Min(0f)] public float explosionPeakIntensity = 8f; // ��ը��ֵ����
        [Min(0f)] public float explosionInnerRadius = 0.5f; // �ڰ뾶
        [Min(0f)] public float explosionOuterRadius = 4.0f; // ��뾶
        [Min(0.05f)] public float explosionFadeSeconds = 0.65f; // �ӷ�ֵ��0��ʱ��
        public AnimationCurve explosionIntensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        // ���ߣ�0~1ʱ�䣬1Ϊ��ֵ��0ΪϨ�𡣿���Inspector���Զ�����״���������ϳ������䣩

        [Header("Screen Shake (����)")]
        public bool shakeOnDeath = true;
        [Min(0f)] public float shakeDuration = 0.2f;     // ����ʱ�����룩
        [Min(0f)] public float shakeStrength = 0.5f;     // ���������ռ�ƫ�����ֵ��
        public float shakeFrequency = 24f;               // ÿ�붶������

        [Header("Explosion Audio")]
        public AudioClip explosionSFX;                   // 爆炸音效（钢琴音）
        [Range(0f, 1f)] public float explosionVolume = 0.8f;
        [Range(0f, 0.5f)] public float pitchVariation = 0.15f; // 音调随机变化范围

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

            // һ������Ч�����ӵȣ�
            if (deathVfxPrefab)
            {
                var vfx = Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            // ��ը��Ч��˲��������
            if (spawnExplosionLight) SpawnExplosionLight();

            // ����
            if (shakeOnDeath) CameraShake2D.Shake(shakeDuration, shakeStrength, shakeFrequency);

            // 播放爆炸音效（带随机音调）
            if (explosionSFX) PlayExplosionSound();

            // ����������Ӫ���෴ɫ�����䣨����ԭ�߼���
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
            l.intensity = explosionPeakIntensity; // �ȵ���ֵ
            l.pointLightInnerRadius = explosionInnerRadius;
            l.pointLightOuterRadius = explosionOuterRadius;
            l.color = explosionLightColor;
            l.shadowIntensity = 0f; // ������Ӱ�ɵ���
            l.volumeIntensity = 0f; // ��������⣬����URP���ú����

            // ����Э��
            go.AddComponent<ExplosionLightFader>().Begin(l, explosionFadeSeconds, explosionIntensityCurve, explosionPeakIntensity);
        }

        private void PlayExplosionSound()
        {
            // 创建临时AudioSource播放带随机音调的爆炸音效
            GameObject tempGO = new GameObject("TempExplosionSFX");
            tempGO.transform.position = transform.position;
            AudioSource tempSource = tempGO.AddComponent<AudioSource>();
            tempSource.clip = explosionSFX;
            tempSource.volume = explosionVolume;
            tempSource.spatialBlend = 0f; // 2D音效
            // 随机调整音调
            tempSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            tempSource.Play();
            // 播放完毕后销毁
            Destroy(tempGO, explosionSFX.length + 0.1f);
        }

        private static FadedDreams.Player.ColorMode Opposite(FadedDreams.Player.ColorMode m)
            => (m == FadedDreams.Player.ColorMode.Red)
                ? FadedDreams.Player.ColorMode.Green
                : FadedDreams.Player.ColorMode.Red;
    }

    /// <summary>
    /// �� Light2D �ӷ�ֵǿ����������ָ��ʱ���ڵ����� 0�����ڽ���ʱ���١�
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
    /// ����2D�������ݴ��ʼλ�� �� ��ʱ������Perlin�������� �� ��ԭ��
    /// ���˽ű��������ⳡ�������϶��ɾ�̬���ã������Զ�����һ�������壩��
    /// �������� Cinemachine Impulse���ɽ������滻Ϊ��Impulse��ʵ�֡�
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
