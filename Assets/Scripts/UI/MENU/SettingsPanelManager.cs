// SettingsPanelManager.cs
// 设置面板管理器 - 控制设置面板的显示、隐藏和动画
// 整合音量控制和其他游戏设置

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using FadedDreams.Audio;

namespace FadedDreams.UI
{
    /// <summary>
    /// 设置面板管理器
    /// 提供设置面板的完整功能，包括音量控制、显示设置等
    /// </summary>
    public class SettingsPanelManager : MonoBehaviour
    {
        [Header("面板引用")]
        [Tooltip("设置面板根对象")]
        public GameObject panelRoot;

        [Tooltip("面板的CanvasGroup（用于淡入淡出）")]
        public CanvasGroup canvasGroup;

        [Header("按钮")]
        [Tooltip("关闭按钮")]
        public Button closeButton;

        [Tooltip("应用按钮（可选）")]
        public Button applyButton;

        [Tooltip("重置按钮（可选）")]
        public Button resetButton;

        [Header("音量控制")]
        [Tooltip("音量控制UI组件")]
        public VolumeControlUI volumeControl;

        [Header("动画设置")]
        [Tooltip("淡入持续时间")]
        public float fadeInDuration = 0.3f;

        [Tooltip("淡出持续时间")]
        public float fadeOutDuration = 0.25f;

        [Tooltip("缩放动画")]
        public bool useScaleAnimation = true;

        [Tooltip("起始缩放")]
        public float startScale = 0.9f;

        [Tooltip("动画曲线")]
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("音效")]
        [Tooltip("打开面板音效")]
        public AudioClip openSound;

        [Tooltip("关闭面板音效")]
        public AudioClip closeSound;

        [Tooltip("应用设置音效")]
        public AudioClip applySound;

        [Tooltip("音效音量")]
        [Range(0f, 1f)]
        public float soundVolume = 0.7f;

        [Header("调试")]
        [SerializeField] private bool verboseLogs = false;

        private AudioSource _audioSource;
        private bool _isOpen = false;
        private Coroutine _animationCoroutine;

        private void Awake()
        {
            // 获取或创建必要组件
            if (canvasGroup == null && panelRoot != null)
            {
                canvasGroup = panelRoot.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = panelRoot.AddComponent<CanvasGroup>();
                }
            }

            // 音频组件
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D音效
            _audioSource.volume = soundVolume;

            // 按钮事件绑定
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(ApplySettings);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(ResetSettings);
            }
        }

        private void Start()
        {
            // 初始化时确保面板隐藏
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (verboseLogs)
                Debug.Log("[SettingsPanelManager] 初始化完成");
        }

        /// <summary>
        /// 打开设置面板
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;

            // 停止之前的动画
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }

            // 启用面板
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // 播放打开音效
            if (openSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(openSound, soundVolume);
            }

            // 刷新音量UI
            if (volumeControl != null)
            {
                volumeControl.RefreshUI();
            }

            // 开始淡入动画
            _animationCoroutine = StartCoroutine(FadeIn());

            if (verboseLogs)
                Debug.Log("[SettingsPanelManager] 面板已打开");
        }

        /// <summary>
        /// 关闭设置面板
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;

            // 停止之前的动画
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }

            // 播放关闭音效
            if (closeSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(closeSound, soundVolume);
            }

            // 开始淡出动画
            _animationCoroutine = StartCoroutine(FadeOut());

            if (verboseLogs)
                Debug.Log("[SettingsPanelManager] 面板已关闭");
        }

        /// <summary>
        /// 应用设置
        /// </summary>
        public void ApplySettings()
        {
            // 音量设置会自动保存，这里只是反馈
            if (applySound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(applySound, soundVolume);
            }

            if (verboseLogs)
                Debug.Log("[SettingsPanelManager] 设置已应用");
        }

        /// <summary>
        /// 重置设置
        /// </summary>
        public void ResetSettings()
        {
            // 重置音量
            if (GlobalAudioManager.Instance != null)
            {
                GlobalAudioManager.Instance.ResetVolume();
            }

            // 刷新UI
            if (volumeControl != null)
            {
                volumeControl.RefreshUI();
            }

            if (verboseLogs)
                Debug.Log("[SettingsPanelManager] 设置已重置");
        }

        /// <summary>
        /// 切换面板显示状态
        /// </summary>
        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// 淡入动画
        /// </summary>
        private IEnumerator FadeIn()
        {
            float elapsed = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (useScaleAnimation && panelRoot != null)
            {
                panelRoot.transform.localScale = Vector3.one * startScale;
            }

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                float curved = animationCurve.Evaluate(t);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = curved;
                }

                if (useScaleAnimation && panelRoot != null)
                {
                    float scale = Mathf.Lerp(startScale, 1f, curved);
                    panelRoot.transform.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            // 确保最终状态
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (useScaleAnimation && panelRoot != null)
            {
                panelRoot.transform.localScale = Vector3.one;
            }

            _animationCoroutine = null;
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private IEnumerator FadeOut()
        {
            float elapsed = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                float curved = animationCurve.Evaluate(1f - t);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = curved;
                }

                if (useScaleAnimation && panelRoot != null)
                {
                    float scale = Mathf.Lerp(startScale, 1f, curved);
                    panelRoot.transform.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            // 确保最终状态
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            // 禁用面板
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            _animationCoroutine = null;
        }

        /// <summary>
        /// 获取面板是否打开
        /// </summary>
        public bool IsOpen => _isOpen;

        private void OnDestroy()
        {
            // 移除按钮监听
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(ApplySettings);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(ResetSettings);
            }
        }

#if UNITY_EDITOR
        // 编辑器调试
        [ContextMenu("测试：打开面板")]
        private void TestOpen()
        {
            Open();
        }

        [ContextMenu("测试：关闭面板")]
        private void TestClose()
        {
            Close();
        }

        [ContextMenu("测试：切换面板")]
        private void TestToggle()
        {
            Toggle();
        }
#endif
    }
}

