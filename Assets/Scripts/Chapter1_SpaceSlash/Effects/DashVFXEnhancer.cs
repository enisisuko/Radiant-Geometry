using UnityEngine;
using System.Collections;
using FadedDreams.VFX;

namespace FadedDreams.Player
{
    /// <summary>
    /// 冲刺特效增强器 - 让冲刺更加华丽
    /// 功能：
    /// - 冲刺轨迹粒子效果
    /// - 冲击波效果
    /// - 屏幕震动
    /// - 环境交互
    /// - 音效增强
    /// </summary>
    public class DashVFXEnhancer : MonoBehaviour
    {
        [Header("粒子轨迹效果")]
        [Tooltip("是否启用粒子轨迹")]
        public bool enableParticleTrail = true;
        [Tooltip("粒子轨迹预制体")]
        public GameObject particleTrailPrefab;
        [Tooltip("粒子轨迹持续时间")]
        public float particleTrailDuration = 0.3f;
        [Tooltip("粒子轨迹密度")]
        public float particleTrailDensity = 10f;

        [Header("冲击波效果")]
        [Tooltip("是否启用冲击波")]
        public bool enableShockwave = true;
        [Tooltip("起跑冲击波预制体")]
        public GameObject startShockwavePrefab;
        [Tooltip("落地冲击波预制体")]
        public GameObject endShockwavePrefab;
        [Tooltip("冲击波大小")]
        public float shockwaveScale = 1f;
        [Tooltip("冲击波持续时间")]
        public float shockwaveDuration = 0.5f;

        [Header("屏幕震动")]
        [Tooltip("是否启用屏幕震动")]
        public bool enableScreenShake = true;
        [Tooltip("起跑震动强度")]
        public float startShakeIntensity = 0.1f;
        [Tooltip("落地震动强度")]
        public float endShakeIntensity = 0.15f;
        [Tooltip("震动持续时间")]
        public float shakeDuration = 0.2f;

        [Header("环境交互")]
        [Tooltip("是否启用地面痕迹")]
        public bool enableGroundTrail = true;
        [Tooltip("地面痕迹预制体")]
        public GameObject groundTrailPrefab;
        [Tooltip("地面痕迹持续时间")]
        public float groundTrailDuration = 2f;
        [Tooltip("地面痕迹间隔")]
        public float groundTrailInterval = 0.05f;

        [Header("音效增强")]
        [Tooltip("是否启用音效增强")]
        public bool enableAudioEnhancement = true;
        [Tooltip("起跑音效")]
        public AudioClip dashStartSound;
        [Tooltip("落地音效")]
        public AudioClip dashEndSound;
        [Tooltip("轨迹音效")]
        public AudioClip dashTrailSound;
        [Tooltip("音效音量")]
        [Range(0f, 1f)]
        public float audioVolume = 0.8f;

        [Header("后处理效果")]
        [Tooltip("是否启用后处理效果")]
        public bool enablePostProcessing = true;
        [Tooltip("冲刺时的模糊强度")]
        public float motionBlurIntensity = 0.5f;
        [Tooltip("冲刺时的色相偏移")]
        public float hueShift = 0.1f;
        [Tooltip("后处理持续时间")]
        public float postProcessDuration = 0.2f;

        [Header("能量反馈")]
        [Tooltip("是否启用能量反馈")]
        public bool enableEnergyFeedback = true;
        [Tooltip("能量反馈UI预制体")]
        public GameObject energyFeedbackUI;
        [Tooltip("能量消耗显示时间")]
        public float energyFeedbackDuration = 1f;

        // 私有变量
        private Camera _mainCamera;
        private AudioSource _audioSource;
        private Coroutine _particleTrailCoroutine;
        private Coroutine _groundTrailCoroutine;
        private Coroutine _screenShakeCoroutine;
        private Coroutine _postProcessCoroutine;
        private Vector3 _originalCameraPosition;
        private bool _isDashing = false;

        void Awake()
        {
            _mainCamera = Camera.main;
            _audioSource = GetComponent<AudioSource>();
            if (!_audioSource) _audioSource = gameObject.AddComponent<AudioSource>();
            
            if (_mainCamera) _originalCameraPosition = _mainCamera.transform.position;
        }

        /// <summary>
        /// 开始冲刺特效
        /// </summary>
        public void StartDashVFX(Vector2 dashDirection)
        {
            _isDashing = true;
            
            // 起跑冲击波
            if (enableShockwave && startShockwavePrefab)
            {
                SpawnShockwave(startShockwavePrefab, transform.position, dashDirection);
            }

            // 屏幕震动
            if (enableScreenShake)
            {
                StartScreenShake(startShakeIntensity);
            }

            // 音效
            if (enableAudioEnhancement && dashStartSound)
            {
                PlaySound(dashStartSound);
            }

            // 粒子轨迹
            if (enableParticleTrail)
            {
                StartParticleTrail(dashDirection);
            }

            // 地面痕迹
            if (enableGroundTrail)
            {
                StartGroundTrail(dashDirection);
            }

            // 后处理效果
            if (enablePostProcessing)
            {
                StartPostProcessEffect();
            }

            // 能量反馈
            if (enableEnergyFeedback && energyFeedbackUI)
            {
                ShowEnergyFeedback();
            }
        }

        /// <summary>
        /// 结束冲刺特效
        /// </summary>
        public void EndDashVFX(Vector2 dashDirection)
        {
            _isDashing = false;

            // 落地冲击波
            if (enableShockwave && endShockwavePrefab)
            {
                SpawnShockwave(endShockwavePrefab, transform.position, dashDirection);
            }

            // 屏幕震动
            if (enableScreenShake)
            {
                StartScreenShake(endShakeIntensity);
            }

            // 音效
            if (enableAudioEnhancement && dashEndSound)
            {
                PlaySound(dashEndSound);
            }

            // 停止粒子轨迹
            if (_particleTrailCoroutine != null)
            {
                StopCoroutine(_particleTrailCoroutine);
                _particleTrailCoroutine = null;
            }

            // 停止地面痕迹
            if (_groundTrailCoroutine != null)
            {
                StopCoroutine(_groundTrailCoroutine);
                _groundTrailCoroutine = null;
            }
        }

        void SpawnShockwave(GameObject prefab, Vector3 position, Vector2 direction)
        {
            var shockwave = Instantiate(prefab, position, Quaternion.identity);
            shockwave.transform.localScale = Vector3.one * shockwaveScale;
            
            // 让冲击波面向冲刺方向
            if (direction.x != 0)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                shockwave.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            
            Destroy(shockwave, shockwaveDuration);
        }

        void StartParticleTrail(Vector2 direction)
        {
            if (_particleTrailCoroutine != null) return;
            _particleTrailCoroutine = StartCoroutine(CoParticleTrail(direction));
        }

        IEnumerator CoParticleTrail(Vector2 direction)
        {
            float timer = 0f;
            float interval = 1f / particleTrailDensity;
            float nextSpawn = 0f;

            while (_isDashing && timer < particleTrailDuration)
            {
                timer += Time.deltaTime;
                
                if (timer >= nextSpawn && particleTrailPrefab)
                {
                    Vector3 spawnPos = transform.position + (Vector3)(direction * Random.Range(-0.5f, 0.5f));
                    var particle = Instantiate(particleTrailPrefab, spawnPos, Quaternion.identity);
                    Destroy(particle, particleTrailDuration);
                    
                    nextSpawn = timer + interval;
                }
                
                yield return null;
            }
        }

        void StartGroundTrail(Vector2 direction)
        {
            if (_groundTrailCoroutine != null) return;
            _groundTrailCoroutine = StartCoroutine(CoGroundTrail(direction));
        }

        IEnumerator CoGroundTrail(Vector2 direction)
        {
            float timer = 0f;
            float nextSpawn = 0f;

            while (_isDashing && timer < particleTrailDuration)
            {
                timer += Time.deltaTime;
                
                if (timer >= nextSpawn && groundTrailPrefab)
                {
                    Vector3 spawnPos = transform.position;
                    spawnPos.y -= 0.5f; // 稍微向下一点，确保在地面上
                    
                    var trail = Instantiate(groundTrailPrefab, spawnPos, Quaternion.identity);
                    Destroy(trail, groundTrailDuration);
                    
                    nextSpawn = timer + groundTrailInterval;
                }
                
                yield return null;
            }
        }

        void StartScreenShake(float intensity)
        {
            if (_screenShakeCoroutine != null) return;
            _screenShakeCoroutine = StartCoroutine(CoScreenShake(intensity));
        }

        IEnumerator CoScreenShake(float intensity)
        {
            if (!_mainCamera) yield break;

            float timer = 0f;
            Vector3 originalPos = _mainCamera.transform.position;

            while (timer < shakeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / shakeDuration;
                float currentIntensity = intensity * (1f - progress); // 逐渐减弱

                Vector3 randomOffset = Random.insideUnitSphere * currentIntensity;
                randomOffset.z = 0; // 只在XY平面震动
                _mainCamera.transform.position = originalPos + randomOffset;

                yield return null;
            }

            _mainCamera.transform.position = originalPos;
            _screenShakeCoroutine = null;
        }

        void StartPostProcessEffect()
        {
            if (_postProcessCoroutine != null) return;
            _postProcessCoroutine = StartCoroutine(CoPostProcessEffect());
        }

        IEnumerator CoPostProcessEffect()
        {
            // 这里可以添加后处理效果
            // 比如修改Volume Profile或者使用自定义后处理
            yield return new WaitForSeconds(postProcessDuration);
            _postProcessCoroutine = null;
        }

        void ShowEnergyFeedback()
        {
            if (energyFeedbackUI)
            {
                var feedback = Instantiate(energyFeedbackUI, transform.position, Quaternion.identity);
                feedback.transform.SetParent(transform);
                Destroy(feedback, energyFeedbackDuration);
            }
        }

        void PlaySound(AudioClip clip)
        {
            if (_audioSource && clip)
            {
                _audioSource.PlayOneShot(clip, audioVolume);
            }
        }

        /// <summary>
        /// 设置冲刺状态（供外部调用）
        /// </summary>
        public void SetDashing(bool isDashing)
        {
            _isDashing = isDashing;
        }

        /// <summary>
        /// 清理所有特效
        /// </summary>
        public void ClearAllEffects()
        {
            _isDashing = false;
            
            if (_particleTrailCoroutine != null)
            {
                StopCoroutine(_particleTrailCoroutine);
                _particleTrailCoroutine = null;
            }
            
            if (_groundTrailCoroutine != null)
            {
                StopCoroutine(_groundTrailCoroutine);
                _groundTrailCoroutine = null;
            }
            
            if (_screenShakeCoroutine != null)
            {
                StopCoroutine(_screenShakeCoroutine);
                _screenShakeCoroutine = null;
            }
            
            if (_postProcessCoroutine != null)
            {
                StopCoroutine(_postProcessCoroutine);
                _postProcessCoroutine = null;
            }
        }

        void OnDestroy()
        {
            ClearAllEffects();
        }
    }
}
