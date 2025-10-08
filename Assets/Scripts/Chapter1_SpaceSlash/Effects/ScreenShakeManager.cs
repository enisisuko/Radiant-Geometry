using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace FadedDreams.VFX
{
    /// <summary>
    /// 屏幕震动管理器 - 专业的屏幕震动效果
    /// 支持多种震动模式、叠加效果、平滑过渡
    /// </summary>
    public class ScreenShakeManager : MonoBehaviour
    {
        [Header("震动设置")]
        [Tooltip("震动强度倍数")]
        public float intensityMultiplier = 1f;
        [Tooltip("最大震动强度")]
        public float maxIntensity = 0.5f;
        [Tooltip("震动衰减速度")]
        public float damping = 1f;
        [Tooltip("是否启用震动")]
        public bool enableShake = true;

        [Header("震动模式")]
        [Tooltip("默认震动模式")]
        public ShakeMode defaultMode = ShakeMode.Smooth;
        [Tooltip("是否允许震动叠加")]
        public bool allowOverlap = true;
        [Tooltip("最大同时震动数量")]
        public int maxConcurrentShakes = 3;

        [Header("调试")]
        [Tooltip("是否显示调试信息")]
        public bool showDebugInfo = false;

        public enum ShakeMode
        {
            Smooth,     // 平滑震动
            Sharp,      // 尖锐震动
            Random,     // 随机震动
            Wave,       // 波浪震动
            Explosion   // 爆炸震动
        }

        [System.Serializable]
        public class ShakeData
        {
            public float intensity;
            public float duration;
            public ShakeMode mode;
            public Vector2 direction;
            public float frequency;
            public AnimationCurve intensityCurve;
            public bool isActive;
            public float elapsedTime;
            public Vector3 originalPosition;
            public Camera targetCamera;

            public ShakeData(float intensity, float duration, ShakeMode mode, Vector2 direction = default, float frequency = 1f)
            {
                this.intensity = intensity;
                this.duration = duration;
                this.mode = mode;
                this.direction = direction.normalized;
                this.frequency = frequency;
                this.intensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
                this.isActive = true;
                this.elapsedTime = 0f;
                this.originalPosition = Vector3.zero;
                this.targetCamera = null;
            }
        }

        private List<ShakeData> _activeShakes = new List<ShakeData>();
        private Camera _mainCamera;
        private Vector3 _basePosition;
        private bool _isShaking = false;

        void Awake()
        {
            _mainCamera = Camera.main;
            if (!_mainCamera) _mainCamera = FindObjectOfType<Camera>();
            
            if (_mainCamera)
            {
                _basePosition = _mainCamera.transform.position;
            }
        }

        void Update()
        {
            if (!enableShake || !_mainCamera) return;

            UpdateShakes();
            ApplyShake();
        }

        void UpdateShakes()
        {
            for (int i = _activeShakes.Count - 1; i >= 0; i--)
            {
                var shake = _activeShakes[i];
                if (!shake.isActive) continue;

                shake.elapsedTime += Time.deltaTime;
                
                if (shake.elapsedTime >= shake.duration)
                {
                    shake.isActive = false;
                    _activeShakes.RemoveAt(i);
                    continue;
                }

                // 应用衰减
                float progress = shake.elapsedTime / shake.duration;
                float curveValue = shake.intensityCurve.Evaluate(progress);
                shake.intensity *= Mathf.Exp(-damping * Time.deltaTime);
                
                if (shake.intensity <= 0.01f)
                {
                    shake.isActive = false;
                    _activeShakes.RemoveAt(i);
                }
            }

            _isShaking = _activeShakes.Count > 0;
        }

        void ApplyShake()
        {
            if (!_isShaking)
            {
                _mainCamera.transform.position = _basePosition;
                return;
            }

            Vector3 totalOffset = Vector3.zero;
            int activeCount = 0;

            foreach (var shake in _activeShakes)
            {
                if (!shake.isActive) continue;
                if (activeCount >= maxConcurrentShakes) break;

                Vector3 offset = CalculateShakeOffset(shake);
                totalOffset += offset;
                activeCount++;
            }

            // 限制最大震动强度
            if (totalOffset.magnitude > maxIntensity)
            {
                totalOffset = totalOffset.normalized * maxIntensity;
            }

            _mainCamera.transform.position = _basePosition + totalOffset * intensityMultiplier;
        }

        Vector3 CalculateShakeOffset(ShakeData shake)
        {
            float progress = shake.elapsedTime / shake.duration;
            float curveValue = shake.intensityCurve.Evaluate(progress);
            float currentIntensity = shake.intensity * curveValue;

            Vector3 offset = Vector3.zero;

            switch (shake.mode)
            {
                case ShakeMode.Smooth:
                    offset = SmoothShake(currentIntensity, shake.frequency);
                    break;
                case ShakeMode.Sharp:
                    offset = SharpShake(currentIntensity, shake.frequency);
                    break;
                case ShakeMode.Random:
                    offset = RandomShake(currentIntensity);
                    break;
                case ShakeMode.Wave:
                    offset = WaveShake(currentIntensity, shake.frequency, shake.direction);
                    break;
                case ShakeMode.Explosion:
                    offset = CalculateExplosionShake(currentIntensity, shake.frequency);
                    break;
            }

            return offset;
        }

        Vector3 SmoothShake(float intensity, float frequency)
        {
            float time = Time.time * frequency;
            return new Vector3(
                Mathf.Sin(time) * intensity,
                Mathf.Cos(time * 1.1f) * intensity,
                0
            );
        }

        Vector3 SharpShake(float intensity, float frequency)
        {
            float time = Time.time * frequency;
            return new Vector3(
                Mathf.Sin(time) * intensity * 0.5f + Random.Range(-intensity, intensity) * 0.5f,
                Mathf.Cos(time * 1.1f) * intensity * 0.5f + Random.Range(-intensity, intensity) * 0.5f,
                0
            );
        }

        Vector3 RandomShake(float intensity)
        {
            return new Vector3(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity),
                0
            );
        }

        Vector3 WaveShake(float intensity, float frequency, Vector2 direction)
        {
            float time = Time.time * frequency;
            float wave = Mathf.Sin(time) * intensity;
            return new Vector3(
                direction.x * wave,
                direction.y * wave,
                0
            );
        }

        Vector3 CalculateExplosionShake(float intensity, float frequency)
        {
            float time = Time.time * frequency;
            float explosion = Mathf.Exp(-time * 2f) * intensity;
            return new Vector3(
                Random.Range(-explosion, explosion),
                Random.Range(-explosion, explosion),
                0
            );
        }

        /// <summary>
        /// 添加震动效果
        /// </summary>
        public void AddShake(float intensity, float duration, ShakeMode mode = ShakeMode.Smooth, Vector2 direction = default, float frequency = 1f)
        {
            if (!enableShake) return;

            // 限制同时震动数量
            if (!allowOverlap && _activeShakes.Count > 0)
            {
                return;
            }

            if (_activeShakes.Count >= maxConcurrentShakes)
            {
                // 移除最弱的震动
                _activeShakes.Sort((a, b) => a.intensity.CompareTo(b.intensity));
                _activeShakes.RemoveAt(0);
            }

            var shake = new ShakeData(intensity, duration, mode, direction, frequency);
            shake.targetCamera = _mainCamera;
            shake.originalPosition = _mainCamera.transform.position;
            _activeShakes.Add(shake);

            if (showDebugInfo)
            {
                Debug.Log($"[ScreenShake] Added shake: intensity={intensity}, duration={duration}, mode={mode}");
            }
        }

        /// <summary>
        /// 快速震动（便捷方法）
        /// </summary>
        public void QuickShake(float intensity = 0.1f, float duration = 0.2f)
        {
            AddShake(intensity, duration, ShakeMode.Smooth);
        }

        /// <summary>
        /// 爆炸震动
        /// </summary>
        public void ExplosionShake(float intensity = 0.3f, float duration = 0.5f)
        {
            AddShake(intensity, duration, ShakeMode.Explosion);
        }

        /// <summary>
        /// 方向震动
        /// </summary>
        public void DirectionalShake(float intensity, float duration, Vector2 direction)
        {
            AddShake(intensity, duration, ShakeMode.Wave, direction);
        }

        /// <summary>
        /// 停止所有震动
        /// </summary>
        public void StopAllShakes()
        {
            _activeShakes.Clear();
            _isShaking = false;
            if (_mainCamera)
            {
                _mainCamera.transform.position = _basePosition;
            }
        }

        /// <summary>
        /// 设置震动强度倍数
        /// </summary>
        public void SetIntensityMultiplier(float multiplier)
        {
            intensityMultiplier = multiplier;
        }

        /// <summary>
        /// 设置震动开关
        /// </summary>
        public void SetShakeEnabled(bool enabled)
        {
            enableShake = enabled;
            if (!enabled)
            {
                StopAllShakes();
            }
        }

        /// <summary>
        /// 获取当前震动数量
        /// </summary>
        public int GetActiveShakeCount()
        {
            return _activeShakes.Count;
        }

        /// <summary>
        /// 获取当前震动强度
        /// </summary>
        public float GetCurrentIntensity()
        {
            float totalIntensity = 0f;
            foreach (var shake in _activeShakes)
            {
                if (shake.isActive)
                {
                    totalIntensity += shake.intensity;
                }
            }
            return totalIntensity;
        }
    }
}
