using UnityEngine;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单音频管理器
    /// 负责管理菜单的所有音频效果和背景音乐
    /// </summary>
    public class FluidMenuAudio : MonoBehaviour
    {
        [Header("音频源")]
        public AudioSource musicSource;
        public AudioSource sfxSource;
        
        [Header("背景音乐")]
        public AudioClip backgroundMusic;
        public float musicVolume = 0.6f;
        public bool loopMusic = true;
        public float musicFadeTime = 2f;
        
        [Header("音效")]
        public AudioClip hoverSound;
        public AudioClip clickSound;
        public AudioClip expandSound;
        public AudioClip squeezeSound;
        public AudioClip transitionSound;
        
        [Header("音效设置")]
        public float sfxVolume = 0.8f;
        public float hoverVolume = 0.5f;
        public float clickVolume = 0.7f;
        
        [Header("动态音效")]
        public bool enableDynamicAudio = true;
        public float pitchVariation = 0.2f;
        public float volumeVariation = 0.1f;
        
        // 音频状态
        private bool isMusicPlaying = false;
        private bool isFading = false;
        private Coroutine fadeCoroutine;
        
        // 音效池
        private AudioSource[] sfxPool;
        private int currentSfxIndex = 0;
        private const int SFX_POOL_SIZE = 5;
        
        void Awake()
        {
            InitializeAudioSources();
            CreateSfxPool();
        }
        
        void Start()
        {
            PlayBackgroundMusic();
        }
        
        void InitializeAudioSources()
        {
            // 创建音乐音频源
            if (musicSource == null)
            {
                GameObject musicGO = new GameObject("MusicSource");
                musicGO.transform.SetParent(transform);
                musicSource = musicGO.AddComponent<AudioSource>();
            }
            
            musicSource.loop = loopMusic;
            musicSource.volume = musicVolume;
            musicSource.playOnAwake = false;
            
            // 创建音效音频源
            if (sfxSource == null)
            {
                GameObject sfxGO = new GameObject("SfxSource");
                sfxGO.transform.SetParent(transform);
                sfxSource = sfxGO.AddComponent<AudioSource>();
            }
            
            sfxSource.volume = sfxVolume;
            sfxSource.playOnAwake = false;
        }
        
        void CreateSfxPool()
        {
            sfxPool = new AudioSource[SFX_POOL_SIZE];
            
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                GameObject sfxGO = new GameObject($"SfxPool_{i}");
                sfxGO.transform.SetParent(transform);
                sfxPool[i] = sfxGO.AddComponent<AudioSource>();
                sfxPool[i].volume = sfxVolume;
                sfxPool[i].playOnAwake = false;
            }
        }
        
        public void PlayBackgroundMusic()
        {
            if (backgroundMusic == null || isMusicPlaying) return;
            
            musicSource.clip = backgroundMusic;
            musicSource.Play();
            isMusicPlaying = true;
            
            Debug.Log("背景音乐开始播放");
        }
        
        public void StopBackgroundMusic()
        {
            if (!isMusicPlaying) return;
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(FadeOutMusicCoroutine());
        }
        
        public void FadeInMusic()
        {
            if (isFading) return;
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(FadeInMusicCoroutine());
        }
        
        public void FadeOutMusic()
        {
            if (isFading) return;
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(FadeOutMusicCoroutine());
        }
        
        IEnumerator FadeInMusicCoroutine()
        {
            isFading = true;
            
            if (!isMusicPlaying)
            {
                musicSource.clip = backgroundMusic;
                musicSource.Play();
                isMusicPlaying = true;
            }
            
            float startVolume = musicSource.volume;
            float targetVolume = musicVolume;
            float elapsedTime = 0f;
            
            while (elapsedTime < musicFadeTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / musicFadeTime;
                musicSource.volume = Mathf.Lerp(startVolume, targetVolume, progress);
                yield return null;
            }
            
            musicSource.volume = targetVolume;
            isFading = false;
            fadeCoroutine = null;
        }
        
        IEnumerator FadeOutMusicCoroutine()
        {
            isFading = true;
            
            float startVolume = musicSource.volume;
            float elapsedTime = 0f;
            
            while (elapsedTime < musicFadeTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / musicFadeTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, progress);
                yield return null;
            }
            
            musicSource.volume = 0f;
            musicSource.Stop();
            isMusicPlaying = false;
            isFading = false;
            fadeCoroutine = null;
        }
        
        public void PlayHoverSound()
        {
            PlaySfx(hoverSound, hoverVolume);
        }
        
        public void PlayClickSound()
        {
            PlaySfx(clickSound, clickVolume);
        }
        
        public void PlayExpandSound()
        {
            PlaySfx(expandSound, sfxVolume);
        }
        
        public void PlaySqueezeSound()
        {
            PlaySfx(squeezeSound, sfxVolume * 0.7f);
        }
        
        public void PlayTransitionSound()
        {
            PlaySfx(transitionSound, sfxVolume);
        }
        
        void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null) return;
            
            AudioSource source = GetAvailableSfxSource();
            if (source != null)
            {
                source.clip = clip;
                source.volume = volume;
                
                // 添加动态变化
                if (enableDynamicAudio)
                {
                    source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                    source.volume *= (1f + Random.Range(-volumeVariation, volumeVariation));
                }
                
                source.Play();
            }
        }
        
        AudioSource GetAvailableSfxSource()
        {
            // 查找可用的音频源
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                int index = (currentSfxIndex + i) % SFX_POOL_SIZE;
                if (!sfxPool[index].isPlaying)
                {
                    currentSfxIndex = (index + 1) % SFX_POOL_SIZE;
                    return sfxPool[index];
                }
            }
            
            // 如果都忙，使用第一个
            return sfxPool[0];
        }
        
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
        }
        
        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
            
            // 更新音效池
            if (sfxPool != null)
            {
                foreach (var source in sfxPool)
                {
                    if (source != null)
                    {
                        source.volume = sfxVolume;
                    }
                }
            }
        }
        
        public void SetHoverVolume(float volume)
        {
            hoverVolume = Mathf.Clamp01(volume);
        }
        
        public void SetClickVolume(float volume)
        {
            clickVolume = Mathf.Clamp01(volume);
        }
        
        public void MuteAll()
        {
            SetMusicVolume(0f);
            SetSfxVolume(0f);
        }
        
        public void UnmuteAll()
        {
            SetMusicVolume(0.6f);
            SetSfxVolume(0.8f);
        }
        
        public bool IsMusicPlaying()
        {
            return isMusicPlaying && musicSource.isPlaying;
        }
        
        public bool IsFading()
        {
            return isFading;
        }
        
        public float GetMusicVolume()
        {
            return musicVolume;
        }
        
        public float GetSfxVolume()
        {
            return sfxVolume;
        }
        
        // 调试接口
        [ContextMenu("Test Hover Sound")]
        public void TestHoverSound()
        {
            PlayHoverSound();
        }
        
        [ContextMenu("Test Click Sound")]
        public void TestClickSound()
        {
            PlayClickSound();
        }
        
        [ContextMenu("Test Expand Sound")]
        public void TestExpandSound()
        {
            PlayExpandSound();
        }
        
        [ContextMenu("Test Squeeze Sound")]
        public void TestSqueezeSound()
        {
            PlaySqueezeSound();
        }
        
        [ContextMenu("Test Transition Sound")]
        public void TestTransitionSound()
        {
            PlayTransitionSound();
        }
        
        void OnDestroy()
        {
            // 清理协程
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
        }
    }
}