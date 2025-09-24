// CameraShake2D.cs  ���� ����ʽ�������� Follow ���棬������λ�ã�
// �ؼ��Ķ���1) ʹ�á��ȳ�����һ֡ �� �ٵ��ӱ�֡����������ʽ
//          2) [DefaultExecutionOrder(1000)] ȷ���� CameraFollow2D ֮��ִ��
//          3) OnDisable ʱ��������ƫ������ת
using UnityEngine;

namespace FadedDreams.CameraFX
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // �� LateUpdate �ڸ���ű�֮�����
    public sealed class CameraShake2D : MonoBehaviour
    {
        [Header("General")]
        [Tooltip("�Ƿ�ʹ�� UnscaledTime������ Time.timeScale Ӱ�죩")]
        public bool useUnscaledTime = true;

        [Header("Amplitude")]
        [Tooltip("λ�ö������λ�ƣ����絥λ��")]
        public float maxPositionShake = 0.6f;
        [Tooltip("��ת�������Ƕȣ��ȣ�")]
        public float maxRotationShake = 8f;

        [Header("Noise / Feel")]
        [Tooltip("����Ƶ�ʣ�Խ��Խ����")]
        public float frequency = 22f;
        [Tooltip("����˥���ٶȣ�ÿ�룩")]
        public float traumaDecay = 1.4f;
        [Tooltip("����ָ����ƽ��~������Խ�߷�ֵ�̶�����")]
        [Range(1f, 3f)] public float traumaExponent = 2f;

        [Header("Continuous Drive")]
        [Tooltip("�������� �� ��Ŀ��ֵ��ƽ���ٶ�")]
        public float continuousLerpSpeed = 6f;
        [Tooltip("OnHoldShakeStrength(s) ������")]
        public float holdGain = 0.7f;

        [Header("Space")]
        [Tooltip("�Ա���������ӣ��Ƽ���������ǡ�����Ǽܡ���������ʱ��ѡ��")]
        public bool applyInLocalSpace = true;

        // �����������¼���/������ã�
        public static CameraShake2D Instance { get; private set; }

        // ״̬
        float trauma;               // ��ǰ���ˣ�0..1��
        float oneShotTimer;         // һ���Լ�ʱ��
        float continuousTarget;     // �ⲿ��������Ŀ�꣨0..1��
        float noiseSeedX, noiseSeedY, noiseSeedR;

        // ���� ��������¼����һ֡��Ӧ�õ��任�ϵ�ƫ������ת�������ȳ����ٵ���
        Vector3 _lastPosOffset = Vector3.zero;
        float _lastRotZ = 0f;

        void Awake()
        {
            if (Instance && Instance != this) { enabled = false; return; }
            Instance = this;

            // ������ӣ�����ͬ��λ
            noiseSeedX = Random.value * 1000f;
            noiseSeedY = Random.value * 2000f;
            noiseSeedR = Random.value * 3000f;
        }

        void OnEnable()
        {
            // ȷ������ʱ�޲���
            _lastPosOffset = Vector3.zero;
            _lastRotZ = 0f;
        }

        void OnDisable()
        {
            // �������һ�ε��ӣ���ֹ����
            if (applyInLocalSpace)
            {
                transform.localPosition -= _lastPosOffset;
                transform.localRotation = Quaternion.Euler(0f, 0f, -_lastRotZ) * transform.localRotation;
            }
            else
            {
                transform.position -= _lastPosOffset;
                transform.rotation = Quaternion.Euler(0f, 0f, -_lastRotZ) * transform.rotation;
            }
            _lastPosOffset = Vector3.zero;
            _lastRotZ = 0f;
        }

        void LateUpdate()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = useUnscaledTime ? Time.unscaledTime : Time.time;

            // ���� �ȳ�����һ֡��������֤�����������ű��ĸ��£�
            if (applyInLocalSpace)
            {
                transform.localPosition -= _lastPosOffset;
                transform.localRotation = Quaternion.Euler(0f, 0f, -_lastRotZ) * transform.localRotation;
            }
            else
            {
                transform.position -= _lastPosOffset;
                transform.rotation = Quaternion.Euler(0f, 0f, -_lastRotZ) * transform.rotation;
            }
            _lastPosOffset = Vector3.zero;
            _lastRotZ = 0f;

            // ���� ������˥��
            float current = Mathf.Clamp01(continuousTarget);
            trauma = Mathf.Max(trauma, current);

            if (oneShotTimer > 0f) oneShotTimer -= dt;
            else
            {
                trauma = Mathf.MoveTowards(trauma, current, traumaDecay * dt);
                if (Mathf.Approximately(current, 0f))
                    trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            }

            // ���� ����������ǿ��
            float intensity = Mathf.Pow(Mathf.Clamp01(trauma), traumaExponent);

            float nx = (Mathf.PerlinNoise(noiseSeedX, t * frequency) * 2f - 1f);
            float ny = (Mathf.PerlinNoise(noiseSeedY, t * frequency) * 2f - 1f);
            float nr = (Mathf.PerlinNoise(noiseSeedR, t * frequency) * 2f - 1f);

            Vector3 posOffset = new Vector3(nx, ny, 0f) * (maxPositionShake * intensity);
            float rotZ = nr * (maxRotationShake * intensity);

            // ���� ���ӵ�����ǰֵ���ϣ������������˶���
            if (applyInLocalSpace)
            {
                transform.localPosition += posOffset;
                transform.localRotation = Quaternion.Euler(0f, 0f, rotZ) * transform.localRotation;
            }
            else
            {
                transform.position += posOffset;
                transform.rotation = Quaternion.Euler(0f, 0f, rotZ) * transform.rotation;
            }

            // ��¼��֡������֡����
            _lastPosOffset = posOffset;
            _lastRotZ = rotZ;

            // ��ס�������Զ�����
            continuousTarget = Mathf.MoveTowards(continuousTarget, 0f, (continuousLerpSpeed * 0.25f) * dt);
        }

        /// <summary>һ����������strength �� [0..1]��duration ��</summary>
        public void Shake(float strength, float duration)
        {
            strength = Mathf.Clamp01(strength);
            trauma = Mathf.Max(trauma, strength);
            oneShotTimer = Mathf.Max(oneShotTimer, Mathf.Max(0.01f, duration));
        }

        /// <summary>���Ӵ���ֵ�����ӣ�</summary>
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
        }

        /// <summary>����������0..1�����Ỻ�����䣬�ʺϰ�ס�ڼ����</summary>
        public void SetContinuous(float normalized)
        {
            continuousTarget = Mathf.Clamp01(Mathf.Max(continuousTarget, normalized));
        }

        /// <summary>ֹͣ����������һ������ʱ�䣬Ĭ������ֹͣ��</summary>
        public void StopAllShakes(float fadeOut = 0f)
        {
            if (fadeOut <= 0f) { trauma = 0f; oneShotTimer = 0f; continuousTarget = 0f; }
            else { oneShotTimer = 0f; StartCoroutine(CoFadeOut(fadeOut)); }
        }

        System.Collections.IEnumerator CoFadeOut(float seconds)
        {
            float start = trauma;
            float t = 0f;
            while (t < seconds)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float u = 1f - Mathf.Clamp01(t / seconds);
                trauma = start * u;
                yield return null;
            }
            trauma = 0f; continuousTarget = 0f;
        }

        // ���� �¼��Žӣ�ֱ���� Inspector �󶨣� ����
        public void OnHoldShakeStrength(float s) => SetContinuous(Mathf.Clamp01(s * holdGain));
        public void OnSweepBlast() => Shake(0.9f, 0.25f);
    }
}
