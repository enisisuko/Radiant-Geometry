// VolumeControlUI.cs
// 音量控制UI组件 - 主菜单音量滑块
// 配合GlobalAudioManager使用，提供直观的音量调节界面

using UnityEngine;
using UnityEngine.UI;
using FadedDreams.Audio;

namespace FadedDreams.UI
{
    /// <summary>
    /// 音量控制UI
    /// 提供滑块、百分比显示、静音按钮等功能
    /// </summary>
    public class VolumeControlUI : MonoBehaviour
    {
        [Header("UI组件")]
        [Tooltip("音量滑块")]
        public Slider volumeSlider;

        [Tooltip("音量百分比文本")]
        public Text volumePercentText;

        [Tooltip("静音按钮")]
        public Button muteButton;

        [Tooltip("静音按钮图标（可选）")]
        public Image muteButtonImage;

        [Header("图标设置")]
        [Tooltip("正常音量图标")]
        public Sprite normalVolumeIcon;

        [Tooltip("静音图标")]
        public Sprite mutedVolumeIcon;

        [Header("音效反馈")]
        [Tooltip("滑块调整时的音效")]
        public AudioClip volumeChangeSound;

        [Tooltip("静音切换音效")]
        public AudioClip muteToggleSound;

        [Tooltip("音效音量")]
        [Range(0f, 1f)]
        public float soundVolume = 0.5f;

        [Header("显示设置")]
        [Tooltip("显示百分比符号")]
        public bool showPercentSign = true;

        [Tooltip("百分比格式（例如：0、0.0）")]
        public string percentFormat = "0";

        [Header("调试")]
        [SerializeField] private bool verboseLogs = false;

        private AudioSource _audioSource;
        private bool _isInitialized = false;
        private bool _isMuted = false;

        private void Awake()
        {
            // 获取或添加AudioSource用于UI音效
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D音效
        }

        private void Start()
        {
            InitializeUI();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 设置滑块范围
            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;

                // 从GlobalAudioManager加载当前音量
                float currentVolume = GlobalAudioManager.Instance.MasterVolume;
                volumeSlider.value = currentVolume;

                // 添加滑块值变化监听
                volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);

                if (verboseLogs)
                    Debug.Log($"[VolumeControlUI] 滑块初始化完成，当前音量: {currentVolume:F2}");
            }
            else
            {
                Debug.LogWarning("[VolumeControlUI] 未找到音量滑块！");
            }

            // 设置静音按钮
            if (muteButton != null)
            {
                muteButton.onClick.AddListener(OnMuteButtonClicked);
            }

            // 更新显示
            UpdateVolumeDisplay(GlobalAudioManager.Instance.MasterVolume);
            UpdateMuteButtonVisual();

            _isInitialized = true;

            if (verboseLogs)
                Debug.Log("[VolumeControlUI] UI初始化完成");
        }

        /// <summary>
        /// 滑块值变化回调
        /// </summary>
        /// <param name="value">新的音量值</param>
        private void OnVolumeSliderChanged(float value)
        {
            if (!_isInitialized) return;

            // 设置音量并保存
            GlobalAudioManager.Instance.SetVolumeAndSave(value);

            // 更新显示
            UpdateVolumeDisplay(value);
            UpdateMuteButtonVisual();

            // 播放音效反馈
            PlayVolumeChangeSound();

            if (verboseLogs)
                Debug.Log($"[VolumeControlUI] 音量已调整: {value:F2}");
        }

        /// <summary>
        /// 静音按钮点击回调
        /// </summary>
        private void OnMuteButtonClicked()
        {
            // 切换静音状态
            GlobalAudioManager.Instance.ToggleMute();

            // 更新滑块值
            if (volumeSlider != null)
            {
                volumeSlider.value = GlobalAudioManager.Instance.MasterVolume;
            }

            // 更新显示
            UpdateVolumeDisplay(GlobalAudioManager.Instance.MasterVolume);
            UpdateMuteButtonVisual();

            // 播放音效
            if (muteToggleSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(muteToggleSound, soundVolume);
            }

            if (verboseLogs)
                Debug.Log($"[VolumeControlUI] 静音切换: {GlobalAudioManager.Instance.IsMuted()}");
        }

        /// <summary>
        /// 更新音量百分比显示
        /// </summary>
        /// <param name="volume">音量值（0-1）</param>
        private void UpdateVolumeDisplay(float volume)
        {
            if (volumePercentText != null)
            {
                int percent = Mathf.RoundToInt(volume * 100f);
                string percentStr = percent.ToString(percentFormat);
                volumePercentText.text = showPercentSign ? $"{percentStr}%" : percentStr;
            }
        }

        /// <summary>
        /// 更新静音按钮图标
        /// </summary>
        private void UpdateMuteButtonVisual()
        {
            _isMuted = GlobalAudioManager.Instance.IsMuted();

            if (muteButtonImage != null)
            {
                if (_isMuted && mutedVolumeIcon != null)
                {
                    muteButtonImage.sprite = mutedVolumeIcon;
                }
                else if (!_isMuted && normalVolumeIcon != null)
                {
                    muteButtonImage.sprite = normalVolumeIcon;
                }
            }
        }

        /// <summary>
        /// 播放音量调整音效
        /// </summary>
        private void PlayVolumeChangeSound()
        {
            if (volumeChangeSound != null && _audioSource != null && !_isMuted)
            {
                // 只在滑块停止时播放（避免连续播放）
                if (!_audioSource.isPlaying)
                {
                    _audioSource.PlayOneShot(volumeChangeSound, soundVolume);
                }
            }
        }

        /// <summary>
        /// 刷新UI显示（用于外部调用）
        /// </summary>
        public void RefreshUI()
        {
            if (!_isInitialized) return;

            float currentVolume = GlobalAudioManager.Instance.MasterVolume;

            if (volumeSlider != null)
            {
                volumeSlider.value = currentVolume;
            }

            UpdateVolumeDisplay(currentVolume);
            UpdateMuteButtonVisual();

            if (verboseLogs)
                Debug.Log("[VolumeControlUI] UI已刷新");
        }

        /// <summary>
        /// 设置音量（用于外部调用）
        /// </summary>
        /// <param name="volume">音量值（0-1）</param>
        public void SetVolume(float volume)
        {
            if (volumeSlider != null)
            {
                volumeSlider.value = Mathf.Clamp01(volume);
            }
        }

        private void OnDestroy()
        {
            // 移除监听器
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeSliderChanged);
            }

            if (muteButton != null)
            {
                muteButton.onClick.RemoveListener(OnMuteButtonClicked);
            }
        }

#if UNITY_EDITOR
        // 编辑器调试方法
        [ContextMenu("测试：设置音量为50%")]
        private void TestSetVolume50()
        {
            SetVolume(0.5f);
        }

        [ContextMenu("测试：刷新UI")]
        private void TestRefreshUI()
        {
            RefreshUI();
        }
#endif
    }
}

