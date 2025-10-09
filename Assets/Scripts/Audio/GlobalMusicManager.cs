using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace FadedDreams.Audio
{
    /// <summary>
    /// 全局背景音乐管理器
    /// 循环随机播放音乐列表中的音乐
    /// 使用DontDestroyOnLoad保持单例
    /// </summary>
    public class GlobalMusicManager : MonoBehaviour
    {
        [Header("=== 单例设置 ===")]
        private static GlobalMusicManager instance;
        
        [Header("=== 音乐列表 ===")]
        [Tooltip("所有背景音乐列表")]
        public List<AudioClip> musicTracks = new List<AudioClip>();
        
        [Header("=== 播放设置 ===")]
        [Tooltip("音乐音量")]
        [Range(0f, 1f)]
        public float musicVolume = 0.5f;
        
        [Tooltip("是否随机播放")]
        public bool shuffleMode = true;
        
        [Tooltip("歌曲之间的间隔时间")]
        [Range(0f, 5f)]
        public float trackTransitionDelay = 1f;
        
        [Tooltip("淡入淡出时间")]
        [Range(0f, 3f)]
        public float fadeTime = 1.5f;
        
        [Header("=== 音频源 ===")]
        private AudioSource audioSource;
        
        // 内部变量
        private List<int> playOrder = new List<int>();
        private int currentTrackIndex = 0;
        private bool isPlaying = false;
        private bool isFading = false;
        private Coroutine playCoroutine;
        
        void Awake()
        {
            // 单例模式
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 初始化音频源
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.volume = musicVolume;
            
            Debug.Log("🎵 全局音乐管理器已初始化");
        }
        
        void Start()
        {
            // 自动开始播放
            if (musicTracks.Count > 0)
            {
                StartPlaylist();
            }
            else
            {
                Debug.LogWarning("⚠️ 音乐列表为空！请在Inspector中添加音乐文件。");
            }
        }
        
        /// <summary>
        /// 开始播放播放列表
        /// </summary>
        public void StartPlaylist()
        {
            if (isPlaying) return;
            if (musicTracks.Count == 0)
            {
                Debug.LogWarning("⚠️ 无法播放：音乐列表为空");
                return;
            }
            
            isPlaying = true;
            GeneratePlayOrder();
            
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
            }
            playCoroutine = StartCoroutine(PlaylistCoroutine());
            
            Debug.Log("▶️ 开始播放音乐播放列表");
        }
        
        /// <summary>
        /// 停止播放
        /// </summary>
        public void StopPlaylist()
        {
            if (!isPlaying) return;
            
            isPlaying = false;
            
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }
            
            StartCoroutine(FadeOut());
            Debug.Log("⏹️ 停止播放音乐");
        }
        
        /// <summary>
        /// 暂停播放
        /// </summary>
        public void PausePlaylist()
        {
            if (audioSource.isPlaying)
            {
                audioSource.Pause();
                Debug.Log("⏸️ 暂停音乐");
            }
        }
        
        /// <summary>
        /// 恢复播放
        /// </summary>
        public void ResumePlaylist()
        {
            if (!audioSource.isPlaying && audioSource.clip != null)
            {
                audioSource.UnPause();
                Debug.Log("▶️ 恢复播放音乐");
            }
        }
        
        /// <summary>
        /// 跳到下一首
        /// </summary>
        public void NextTrack()
        {
            if (!isPlaying) return;
            
            // 重启播放协程
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
            }
            
            StartCoroutine(FadeOutAndNext());
        }
        
        /// <summary>
        /// 设置音量
        /// </summary>
        public void SetVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (audioSource != null)
            {
                audioSource.volume = musicVolume;
            }
        }
        
        /// <summary>
        /// 生成播放顺序
        /// </summary>
        private void GeneratePlayOrder()
        {
            playOrder.Clear();
            
            for (int i = 0; i < musicTracks.Count; i++)
            {
                playOrder.Add(i);
            }
            
            if (shuffleMode)
            {
                // Fisher-Yates 洗牌算法
                for (int i = playOrder.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int temp = playOrder[i];
                    playOrder[i] = playOrder[j];
                    playOrder[j] = temp;
                }
                Debug.Log("🔀 随机播放顺序已生成");
            }
            
            currentTrackIndex = 0;
        }
        
        /// <summary>
        /// 播放列表协程
        /// </summary>
        private IEnumerator PlaylistCoroutine()
        {
            while (isPlaying)
            {
                // 检查是否需要重新生成播放顺序
                if (currentTrackIndex >= playOrder.Count)
                {
                    GeneratePlayOrder();
                }
                
                // 获取当前要播放的音乐
                int trackIndex = playOrder[currentTrackIndex];
                AudioClip clip = musicTracks[trackIndex];
                
                if (clip != null)
                {
                    Debug.Log($"🎵 正在播放：{clip.name}");
                    
                    // 播放当前音乐（带淡入效果）
                    yield return StartCoroutine(PlayTrackWithFade(clip));
                    
                    // 等待歌曲播放完毕
                    while (audioSource.isPlaying && isPlaying)
                    {
                        yield return null;
                    }
                    
                    // 淡出
                    if (isPlaying)
                    {
                        yield return StartCoroutine(FadeOut());
                    }
                    
                    // 歌曲之间的间隔
                    if (trackTransitionDelay > 0 && isPlaying)
                    {
                        yield return new WaitForSeconds(trackTransitionDelay);
                    }
                }
                else
                {
                    Debug.LogWarning($"⚠️ 音乐索引 {trackIndex} 为空，跳过");
                }
                
                currentTrackIndex++;
            }
        }
        
        /// <summary>
        /// 播放音乐（带淡入效果）
        /// </summary>
        private IEnumerator PlayTrackWithFade(AudioClip clip)
        {
            audioSource.clip = clip;
            audioSource.volume = 0f;
            audioSource.Play();
            
            // 淡入
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                audioSource.volume = Mathf.Lerp(0f, musicVolume, elapsed / fadeTime);
                yield return null;
            }
            
            audioSource.volume = musicVolume;
        }
        
        /// <summary>
        /// 淡出当前音乐
        /// </summary>
        private IEnumerator FadeOut()
        {
            if (!audioSource.isPlaying) yield break;
            
            isFading = true;
            float startVolume = audioSource.volume;
            float elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }
            
            audioSource.volume = 0f;
            audioSource.Stop();
            isFading = false;
        }
        
        /// <summary>
        /// 淡出并切换到下一首
        /// </summary>
        private IEnumerator FadeOutAndNext()
        {
            yield return StartCoroutine(FadeOut());
            playCoroutine = StartCoroutine(PlaylistCoroutine());
        }
        
        /// <summary>
        /// 获取当前播放的音乐名称
        /// </summary>
        public string GetCurrentTrackName()
        {
            if (audioSource.clip != null)
            {
                return audioSource.clip.name;
            }
            return "无";
        }
        
        /// <summary>
        /// 获取是否正在播放
        /// </summary>
        public bool IsPlaying()
        {
            return isPlaying && audioSource.isPlaying;
        }
        
        // 调试方法
        [ContextMenu("播放下一首")]
        private void DebugNextTrack()
        {
            NextTrack();
        }
        
        [ContextMenu("显示当前播放")]
        private void DebugShowCurrent()
        {
            Debug.Log($"当前播放：{GetCurrentTrackName()}");
        }
        
        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}

