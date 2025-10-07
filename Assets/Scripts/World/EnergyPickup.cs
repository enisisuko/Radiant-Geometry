using UnityEngine;
using System.Collections;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal; // ��ѡ���� Light2D ������
#endif

namespace FadedDreams.World
{
    /// <summary>
    /// ��������ʰȡ�
    /// - ������������
    /// - ���������ʱ���١�˲����ʧ�����Ȳ������ն���������
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
        public float absorbRadius = 0.35f;   // �������հ뾶����������������
        public float flySpeed = 8f;

        [Header("Absorb Animation")]
        [Tooltip("���ն���ʱ�����룩")]
        public float absorbDuration = 0.30f;
        [Tooltip("��ʼ����ʱ������΢�Ŵ�������ʣ�>1 �С������У�")]
        public float absorbScaleUp = 1.15f;
        [Tooltip("����������ÿ�������Ľ��ٶȣ���/�룩")]
        public float absorbSpinSpeed = 540f;
        [Tooltip("��������ʱ���ŵ�������ʣ����� 0.0~0.2��")]
        public float endScale = 0.02f;
        [Tooltip("�Ƿ��ڶ����ڼ仺���������λ�ã����С����롱�У�")]
        public bool glideToPlayer = true;
        [Tooltip("������ҵ�λ��ǿ�ȣ�0~1��Խ��Խ�쿿����ң�")]
        [Range(0f, 1f)] public float glideStrength = 0.85f;
        [Tooltip("���շ���ʱ���Ƿ����̼������������ڶ��������żӣ�")]
        public bool grantEnergyAtStart = true;

        [Header("Optional FX")]
        [Tooltip("����˲�䲥�ŵ�����Ԥ���壨�ɿգ�")]
        public GameObject absorbBurstVfx;
        [Tooltip("����˲�䲥�ŵ���Ч���ɿգ�")]
        public AudioClip absorbSfx;
        [Range(0f, 1f)] public float absorbSfxVolume = 0.9f;

        // ����
        private Transform _player;
        private Collider2D _col;
        private SpriteRenderer _sr; // ��ѡ����
#if UNITY_RENDERING_UNIVERSAL
        private Light2D _light;     // ��ѡ����
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
            // ȷ���Ǵ���������ԭ��Ϊһ�£�
            _col.isTrigger = true;
        }

        private void Start()
        {
            _player = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>()?.transform;
            _lifeTimer = life;
        }

        private void Update()
        {
            if (_absorbing) return; // �����ڼ䲻��ִ�С������ƶ�/�������١�

            // ������ʱ
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
                // ��������ٷ�ȥ
                Vector3 dir = (_player.position - transform.position).normalized;
                transform.position += dir * flySpeed * Time.deltaTime;
            }

            // ���ⶵ�ף�����Ҳ�ܳԵ�
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

            // ������ײ��������δ���
            if (_col) _col.enabled = false;

            // �ָ���������ѡ�����̸����򡰶�����������
            if (grantEnergyAtStart)
                GrantEnergy();

            // ����һ������/��Ч
            if (absorbBurstVfx)
            {
                var v = Instantiate(absorbBurstVfx, transform.position, Quaternion.identity);
                // �������Լ����٣�������û���Ի٣����� 2s ��ǿ�ƣ�
                Destroy(v, 2f);
            }
            if (absorbSfx)
                AudioSource.PlayClipAtPoint(absorbSfx, transform.position, absorbSfxVolume);

            // �������ն���Э��
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

            // ��һ������΢�Ŵ󡱵Ĺ��ɣ�ռ��ʱ���� 30%��
            float upTime = absorbDuration * 0.30f;
            float downTime = absorbDuration - upTime;

            // �Ŵ��
            while (t < upTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / upTime);
                transform.localScale = Vector3.Lerp(startScale, upScale, EaseOutCubic(k));
                transform.Rotate(0f, 0f, absorbSpinSpeed * Time.deltaTime);

                if (glideToPlayer && _player)
                {
                    // ����ҿ�£һ��
                    Vector3 target = _player.position;
                    transform.position = Vector3.Lerp(transform.position, target, glideStrength * Time.deltaTime * 8f);
                }

                // ������ǰ����΢��һ�㣩
                FadeTo(1f - 0.25f * k);
                yield return null;
            }

            // ��С��ʧ��
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

                // ��������
                FadeTo(1f - k);
                yield return null;
            }

            // ��ѡ���ڶ��������ż����������ﲹ��
            if (!grantEnergyAtStart)
                GrantEnergy();

            Destroy(gameObject);
        }

        // ===== ���ߺ��� =====
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
