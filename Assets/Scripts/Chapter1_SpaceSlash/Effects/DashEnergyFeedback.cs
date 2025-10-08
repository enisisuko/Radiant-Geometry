using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace FadedDreams.Player
{
    /// <summary>
    /// 冲刺能量反馈UI - 显示冲刺能量状态和冷却
    /// </summary>
    public class DashEnergyFeedback : MonoBehaviour
    {
        [Header("UI组件")]
        [Tooltip("能量条Image组件")]
        public Image energyBar;
        [Tooltip("能量条背景")]
        public Image energyBarBackground;
        [Tooltip("冷却指示器")]
        public Image cooldownIndicator;
        [Tooltip("能量数值文本")]
        public TextMeshProUGUI energyText;
        [Tooltip("状态文本")]
        public TextMeshProUGUI statusText;

        [Header("视觉效果")]
        [Tooltip("能量条颜色渐变")]
        public Gradient energyGradient;
        [Tooltip("冷却时颜色")]
        public Color cooldownColor = Color.gray;
        [Tooltip("可用时颜色")]
        public Color availableColor = Color.cyan;
        [Tooltip("闪烁效果")]
        public bool enableFlicker = true;
        [Tooltip("闪烁频率")]
        public float flickerFrequency = 2f;

        [Header("动画设置")]
        [Tooltip("填充动画速度")]
        public float fillSpeed = 2f;
        [Tooltip("缩放动画强度")]
        public float scaleIntensity = 0.1f;
        [Tooltip("动画持续时间")]
        public float animationDuration = 0.3f;

        [Header("音效")]
        [Tooltip("能量恢复音效")]
        public AudioClip energyRestoreSound;
        [Tooltip("能量耗尽音效")]
        public AudioClip energyDepletedSound;
        [Tooltip("音效音量")]
        [Range(0f, 1f)]
        public float audioVolume = 0.7f;

        // 私有变量
        private float _currentEnergy = 1f;
        private float _targetEnergy = 1f;
        private bool _isCooldown = false;
        private bool _isAnimating = false;
        private Coroutine _flickerCoroutine;
        private Coroutine _animationCoroutine;
        private AudioSource _audioSource;
        private Vector3 _originalScale;

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (!_audioSource) _audioSource = gameObject.AddComponent<AudioSource>();
            
            _originalScale = transform.localScale;
        }

        void Start()
        {
            UpdateUI();
        }

        void Update()
        {
            UpdateEnergyBar();
        }

        void UpdateEnergyBar()
        {
            if (_isAnimating) return;

            // 平滑过渡到目标能量
            _currentEnergy = Mathf.MoveTowards(_currentEnergy, _targetEnergy, fillSpeed * Time.deltaTime);
            
            // 更新UI
            UpdateUI();
        }

        void UpdateUI()
        {
            if (!energyBar) return;

            // 更新能量条填充
            energyBar.fillAmount = _currentEnergy;
            
            // 更新颜色
            if (_isCooldown)
            {
                energyBar.color = cooldownColor;
            }
            else
            {
                energyBar.color = energyGradient.Evaluate(_currentEnergy);
            }

            // 更新文本
            if (energyText)
            {
                energyText.text = Mathf.RoundToInt(_currentEnergy * 100) + "%";
            }

            if (statusText)
            {
                if (_isCooldown)
                {
                    statusText.text = "冷却中...";
                    statusText.color = cooldownColor;
                }
                else if (_currentEnergy >= 1f)
                {
                    statusText.text = "准备就绪";
                    statusText.color = availableColor;
                }
                else
                {
                    statusText.text = "恢复中...";
                    statusText.color = Color.yellow;
                }
            }

            // 更新冷却指示器
            if (cooldownIndicator)
            {
                cooldownIndicator.fillAmount = _isCooldown ? 1f : 0f;
            }
        }

        /// <summary>
        /// 设置能量值（0-1）
        /// </summary>
        public void SetEnergy(float energy)
        {
            _targetEnergy = Mathf.Clamp01(energy);
        }

        /// <summary>
        /// 设置冷却状态
        /// </summary>
        public void SetCooldown(bool isCooldown)
        {
            _isCooldown = isCooldown;
            
            if (isCooldown)
            {
                StartFlicker();
                PlaySound(energyDepletedSound);
            }
            else
            {
                StopFlicker();
                PlaySound(energyRestoreSound);
            }
        }

        /// <summary>
        /// 消耗能量
        /// </summary>
        public void ConsumeEnergy(float amount)
        {
            _targetEnergy = Mathf.Max(0f, _targetEnergy - amount);
            StartAnimation();
        }

        /// <summary>
        /// 恢复能量
        /// </summary>
        public void RestoreEnergy(float amount)
        {
            _targetEnergy = Mathf.Min(1f, _targetEnergy + amount);
            StartAnimation();
        }

        /// <summary>
        /// 完全恢复能量
        /// </summary>
        public void FullRestore()
        {
            _targetEnergy = 1f;
            _isCooldown = false;
            StartAnimation();
            PlaySound(energyRestoreSound);
        }

        /// <summary>
        /// 开始动画
        /// </summary>
        void StartAnimation()
        {
            if (_animationCoroutine != null) return;
            _animationCoroutine = StartCoroutine(CoAnimation());
        }

        IEnumerator CoAnimation()
        {
            _isAnimating = true;
            float timer = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = _originalScale * (1f + scaleIntensity);

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / animationDuration;
                float easedProgress = Mathf.Sin(progress * Mathf.PI * 0.5f); // 缓动

                transform.localScale = Vector3.Lerp(startScale, targetScale, easedProgress);
                yield return null;
            }

            // 回到原始大小
            timer = 0f;
            while (timer < animationDuration * 0.5f)
            {
                timer += Time.deltaTime;
                float progress = timer / (animationDuration * 0.5f);
                transform.localScale = Vector3.Lerp(targetScale, _originalScale, progress);
                yield return null;
            }

            transform.localScale = _originalScale;
            _isAnimating = false;
            _animationCoroutine = null;
        }

        /// <summary>
        /// 开始闪烁
        /// </summary>
        void StartFlicker()
        {
            if (!enableFlicker || _flickerCoroutine != null) return;
            _flickerCoroutine = StartCoroutine(CoFlicker());
        }

        /// <summary>
        /// 停止闪烁
        /// </summary>
        void StopFlicker()
        {
            if (_flickerCoroutine != null)
            {
                StopCoroutine(_flickerCoroutine);
                _flickerCoroutine = null;
            }
            
            if (energyBar)
            {
                energyBar.color = energyGradient.Evaluate(_currentEnergy);
            }
        }

        IEnumerator CoFlicker()
        {
            while (_isCooldown)
            {
                float alpha = (Mathf.Sin(Time.time * flickerFrequency * Mathf.PI * 2) + 1f) * 0.5f;
                Color flickerColor = Color.Lerp(cooldownColor, Color.white, alpha);
                
                if (energyBar)
                {
                    energyBar.color = flickerColor;
                }
                
                yield return null;
            }
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        void PlaySound(AudioClip clip)
        {
            if (_audioSource && clip)
            {
                _audioSource.PlayOneShot(clip, audioVolume);
            }
        }

        /// <summary>
        /// 设置可见性
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// 重置UI
        /// </summary>
        public void ResetUI()
        {
            _currentEnergy = 1f;
            _targetEnergy = 1f;
            _isCooldown = false;
            _isAnimating = false;
            
            StopFlicker();
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
            
            transform.localScale = _originalScale;
            UpdateUI();
        }

        /// <summary>
        /// 获取当前能量值
        /// </summary>
        public float GetCurrentEnergy()
        {
            return _currentEnergy;
        }

        /// <summary>
        /// 获取目标能量值
        /// </summary>
        public float GetTargetEnergy()
        {
            return _targetEnergy;
        }

        /// <summary>
        /// 是否在冷却中
        /// </summary>
        public bool IsCooldown()
        {
            return _isCooldown;
        }

        void OnDestroy()
        {
            StopFlicker();
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
        }
    }
}
