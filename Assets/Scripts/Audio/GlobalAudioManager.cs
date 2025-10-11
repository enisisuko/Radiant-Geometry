// GlobalAudioManager.cs
// 全局音量管理器 - 控制整个游戏的音量
// 使用单例模式，可在任何场景访问
// 音量设置会自动保存到PlayerPrefs

using UnityEngine;

namespace FadedDreams.Audio
{
    /// <summary>
    /// 全局音量管理器
    /// 提供主音量控制、保存/加载功能
    /// </summary>
    public class GlobalAudioManager : MonoBehaviour
    {
        // 单例实例
        private static GlobalAudioManager _instance;
        public static GlobalAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 在场景中查找现有实例
                    _instance = FindFirstObjectByType<GlobalAudioManager>();
                    
                    // 如果不存在，创建新实例
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GlobalAudioManager");
                        _instance = go.AddComponent<GlobalAudioManager>();
                        DontDestroyOnLoad(go); // 跨场景保持
                    }
                }
                return _instance;
            }
        }

        [Header("音量设置")]
        [Tooltip("主音量（0-1）")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;

        [Header("保存设置")]
        [Tooltip("PlayerPrefs键名")]
        [SerializeField] private string volumeKey = "MasterVolume";

        [Tooltip("是否在启动时自动加载音量")]
        [SerializeField] private bool autoLoadOnStart = true;

        [Header("调试")]
        [SerializeField] private bool verboseLogs = false;

        // 音量属性
        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                ApplyVolume();
                if (verboseLogs)
                    Debug.Log($"[GlobalAudioManager] 主音量设置为: {masterVolume:F2}");
            }
        }

        private void Awake()
        {
            // 单例检查：如果已存在其他实例，销毁当前对象
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (verboseLogs)
                Debug.Log("[GlobalAudioManager] 初始化成功");
        }

        private void Start()
        {
            // 自动加载音量设置
            if (autoLoadOnStart)
            {
                LoadVolume();
            }
            else
            {
                ApplyVolume();
            }
        }

        /// <summary>
        /// 应用音量到AudioListener
        /// </summary>
        private void ApplyVolume()
        {
            AudioListener.volume = masterVolume;
            
            if (verboseLogs)
                Debug.Log($"[GlobalAudioManager] 已应用音量: {masterVolume:F2}");
        }

        /// <summary>
        /// 保存音量设置到PlayerPrefs
        /// </summary>
        public void SaveVolume()
        {
            PlayerPrefs.SetFloat(volumeKey, masterVolume);
            PlayerPrefs.Save();

            if (verboseLogs)
                Debug.Log($"[GlobalAudioManager] 音量已保存: {masterVolume:F2}");
        }

        /// <summary>
        /// 从PlayerPrefs加载音量设置
        /// </summary>
        public void LoadVolume()
        {
            if (PlayerPrefs.HasKey(volumeKey))
            {
                masterVolume = PlayerPrefs.GetFloat(volumeKey);
                ApplyVolume();

                if (verboseLogs)
                    Debug.Log($"[GlobalAudioManager] 音量已加载: {masterVolume:F2}");
            }
            else
            {
                // 如果没有保存的设置，使用默认值并保存
                ApplyVolume();
                SaveVolume();

                if (verboseLogs)
                    Debug.Log("[GlobalAudioManager] 未找到保存的音量，使用默认值");
            }
        }

        /// <summary>
        /// 重置音量到默认值（1.0）
        /// </summary>
        public void ResetVolume()
        {
            MasterVolume = 1f;
            SaveVolume();

            if (verboseLogs)
                Debug.Log("[GlobalAudioManager] 音量已重置为默认值");
        }

        /// <summary>
        /// 设置音量并保存（常用于UI滑块）
        /// </summary>
        /// <param name="volume">音量值（0-1）</param>
        public void SetVolumeAndSave(float volume)
        {
            MasterVolume = volume;
            SaveVolume();
        }

        /// <summary>
        /// 静音切换
        /// </summary>
        public void ToggleMute()
        {
            if (masterVolume > 0f)
            {
                // 保存当前音量并静音
                PlayerPrefs.SetFloat(volumeKey + "_BeforeMute", masterVolume);
                MasterVolume = 0f;
            }
            else
            {
                // 恢复之前的音量
                float previousVolume = PlayerPrefs.GetFloat(volumeKey + "_BeforeMute", 1f);
                MasterVolume = previousVolume;
            }
            SaveVolume();
        }

        /// <summary>
        /// 获取当前是否静音
        /// </summary>
        public bool IsMuted()
        {
            return masterVolume <= 0.01f;
        }

        // 便捷静态方法
        /// <summary>
        /// 静态方法：设置主音量
        /// </summary>
        public static void SetMasterVolume(float volume)
        {
            Instance.SetVolumeAndSave(volume);
        }

        /// <summary>
        /// 静态方法：获取主音量
        /// </summary>
        public static float GetMasterVolume()
        {
            return Instance.MasterVolume;
        }

        /// <summary>
        /// 静态方法：静音切换
        /// </summary>
        public static void Mute()
        {
            Instance.ToggleMute();
        }

#if UNITY_EDITOR
        // 编辑器调试方法
        [ContextMenu("测试：设置音量为50%")]
        private void TestSetVolume50()
        {
            SetVolumeAndSave(0.5f);
        }

        [ContextMenu("测试：静音切换")]
        private void TestToggleMute()
        {
            ToggleMute();
        }

        [ContextMenu("测试：重置音量")]
        private void TestResetVolume()
        {
            ResetVolume();
        }
#endif
    }
}

