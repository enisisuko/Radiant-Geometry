// CameraShake2D.cs  —— 叠加式震屏（与 Follow 共存，不会抢位置）
// 关键改动：1) 使用“先撤销上一帧 → 再叠加本帧”的增量方式
//          2) [DefaultExecutionOrder(1000)] 确保在 CameraFollow2D 之后执行
//          3) OnDisable 时撤销残留偏移与旋转
using UnityEngine;

namespace FadedDreams.CameraFX
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // 让 LateUpdate 在跟随脚本之后调用
    public sealed class CameraShake2D : MonoBehaviour
    {
        [Header("General")]
        [Tooltip("是否使用 UnscaledTime（不受 Time.timeScale 影响）")]
        public bool useUnscaledTime = true;

        [Header("Amplitude")]
        [Tooltip("位置抖动最大位移（世界单位）")]
        public float maxPositionShake = 0.6f;
        [Tooltip("旋转抖动最大角度（度）")]
        public float maxRotationShake = 8f;

        [Header("Noise / Feel")]
        [Tooltip("噪声频率（越高越颤）")]
        public float frequency = 22f;
        [Tooltip("创伤衰减速度（每秒）")]
        public float traumaDecay = 1.4f;
        [Tooltip("创伤指数（平方~立方，越高峰值短而尖锐）")]
        [Range(1f, 3f)] public float traumaExponent = 2f;

        [Header("Continuous Drive")]
        [Tooltip("持续驱动 → 到目标值的平滑速度")]
        public float continuousLerpSpeed = 6f;
        [Tooltip("OnHoldShakeStrength(s) 的增益")]
        public float holdGain = 0.7f;

        [Header("Space")]
        [Tooltip("以本地坐标叠加（推荐：当相机是“跟随骨架”的子物体时勾选）")]
        public bool applyInLocalSpace = true;

        // 单例（方便事件绑定/代码调用）
        public static CameraShake2D Instance { get; private set; }

        // 状态
        float trauma;               // 当前创伤（0..1）
        float oneShotTimer;         // 一次性计时器
        float continuousTarget;     // 外部持续驱动目标（0..1）
        float noiseSeedX, noiseSeedY, noiseSeedR;

        // —— 新增：记录“上一帧”应用到变换上的偏移与旋转，用于先撤销再叠加
        Vector3 _lastPosOffset = Vector3.zero;
        float _lastRotZ = 0f;

        void Awake()
        {
            if (Instance && Instance != this) { enabled = false; return; }
            Instance = this;

            // 随机种子，避免同相位
            noiseSeedX = Random.value * 1000f;
            noiseSeedY = Random.value * 2000f;
            noiseSeedR = Random.value * 3000f;
        }

        void OnEnable()
        {
            // 确保启用时无残留
            _lastPosOffset = Vector3.zero;
            _lastRotZ = 0f;
        }

        void OnDisable()
        {
            // 撤销最后一次叠加，防止残留
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

            // —— 先撤销上一帧抖动（保证不覆盖其他脚本的更新）
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

            // —— 驱动与衰减
            float current = Mathf.Clamp01(continuousTarget);
            trauma = Mathf.Max(trauma, current);

            if (oneShotTimer > 0f) oneShotTimer -= dt;
            else
            {
                trauma = Mathf.MoveTowards(trauma, current, traumaDecay * dt);
                if (Mathf.Approximately(current, 0f))
                    trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            }

            // —— 采样噪声与强度
            float intensity = Mathf.Pow(Mathf.Clamp01(trauma), traumaExponent);

            float nx = (Mathf.PerlinNoise(noiseSeedX, t * frequency) * 2f - 1f);
            float ny = (Mathf.PerlinNoise(noiseSeedY, t * frequency) * 2f - 1f);
            float nr = (Mathf.PerlinNoise(noiseSeedR, t * frequency) * 2f - 1f);

            Vector3 posOffset = new Vector3(nx, ny, 0f) * (maxPositionShake * intensity);
            float rotZ = nr * (maxRotationShake * intensity);

            // —— 叠加到“当前值”上（不覆盖其它运动）
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

            // 记录本帧，供下帧撤销
            _lastPosOffset = posOffset;
            _lastRotZ = rotZ;

            // 按住驱动的自动回落
            continuousTarget = Mathf.MoveTowards(continuousTarget, 0f, (continuousLerpSpeed * 0.25f) * dt);
        }

        /// <summary>一次性震屏：strength ∈ [0..1]，duration 秒</summary>
        public void Shake(float strength, float duration)
        {
            strength = Mathf.Clamp01(strength);
            trauma = Mathf.Max(trauma, strength);
            oneShotTimer = Mathf.Max(oneShotTimer, Mathf.Max(0.01f, duration));
        }

        /// <summary>增加创伤值（叠加）</summary>
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
        }

        /// <summary>持续驱动（0..1）；会缓慢回落，适合按住期间调用</summary>
        public void SetContinuous(float normalized)
        {
            continuousTarget = Mathf.Clamp01(Mathf.Max(continuousTarget, normalized));
        }

        /// <summary>停止震屏（可设一个淡出时间，默认立刻停止）</summary>
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

        // —— 事件桥接（直接在 Inspector 绑定） ——
        public void OnHoldShakeStrength(float s) => SetContinuous(Mathf.Clamp01(s * holdGain));
        public void OnSweepBlast() => Shake(0.9f, 0.25f);
    }
}
