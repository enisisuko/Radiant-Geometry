// BossC1_VisualSystem.cs
// 视觉效果系统 - 负责淡入淡出效果、颜色渐变、VFX生成和光源控制
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.Boss
{
    /// <summary>
    /// BossC1视觉效果系统 - 负责淡入淡出效果、颜色渐变、VFX生成和光源控制
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC1_VisualSystem : MonoBehaviour
    {
        [Header("== Visual Components ==")]
        public SpriteRenderer spriteRenderer;
        public Light2D selfLight;

        [Header("== Fade Settings ==")]
        public float spawnLeadVfxSeconds = 1.0f;
        public float spawnFadeSeconds = 0.6f;
        public float vanishFadeSeconds = 0.5f;
        public float appearFadeSeconds = 0.5f;
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== Color Settings ==")]
        public Color phase1Color = Color.white;
        public Color phase2Color = new Color(1f, .55f, .1f, 1f);
        public Color phase3Color = Color.red;
        public float colorTransitionDuration = 1f;
        public AnimationCurve colorTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("== VFX ==")]
        public GameObject vfxPhaseOut;
        public GameObject vfxPhaseIn;
        public GameObject vfxTransform12;
        public GameObject vfxTransform23;
        public GameObject vfxSpawn;
        public GameObject vfxDeath;

        [Header("== Light Settings ==")]
        public bool enableLightControl = true;
        public float lightIntensity = 1f;
        public Color lightColor = Color.white;
        public float lightFadeSpeed = 2f;

        [Header("== HP Bar ==")]
        public bool createHPBar = true;
        public Vector2 hpBarSize = new Vector2(3.8f, 0.15f);
        public Vector3 hpBarOffset = new Vector3(0, 2.4f, 0);
        public Color hpBarColor = Color.red;
        public Color hpBarBackgroundColor = Color.gray;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 组件引用
        private BossC1_Core core;
        private BossC1_PhaseSystem phaseSystem;

        // 视觉效果状态
        private bool _isVisible = true;
        private bool _isTransitioning = false;
        private Coroutine _fadeCoroutine;
        private Coroutine _colorTransitionCR;

        // HP条
        private LineRenderer _hpBarRenderer;
        private LineRenderer _hpBarBackgroundRenderer;

        // 原始设置
        private Color _originalColor;
        private float _originalLightIntensity;
        private Color _originalLightColor;

        // 事件
        public event System.Action OnFadeStarted;
        public event System.Action OnFadeCompleted;
        public event System.Action OnColorTransitionStarted;
        public event System.Action OnColorTransitionCompleted;
        public event System.Action OnVfxSpawned;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<BossC1_Core>();
            phaseSystem = GetComponent<BossC1_PhaseSystem>();

            // 获取组件引用
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (selfLight == null)
                selfLight = GetComponent<Light2D>();
        }

        private void Start()
        {
            // 保存原始设置
            if (spriteRenderer != null)
            {
                _originalColor = spriteRenderer.color;
            }

            if (selfLight != null)
            {
                _originalLightIntensity = selfLight.intensity;
                _originalLightColor = selfLight.color;
            }

            // 创建HP条
            if (createHPBar)
            {
                CreateHPBar();
            }

            // 订阅事件
            if (core != null)
            {
                core.OnAggroStarted += OnAggroStarted;
                core.OnDeath += OnDeath;
            }

            if (phaseSystem != null)
            {
                phaseSystem.OnPhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (core != null)
            {
                core.OnAggroStarted -= OnAggroStarted;
                core.OnDeath -= OnDeath;
            }

            if (phaseSystem != null)
            {
                phaseSystem.OnPhaseChanged -= OnPhaseChanged;
            }

            // 停止协程
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            if (_colorTransitionCR != null)
            {
                StopCoroutine(_colorTransitionCR);
            }
        }

        #endregion

        #region Fade Effects

        /// <summary>
        /// 淡入淡出效果
        /// </summary>
        public IEnumerator FadeVisible(bool show, float seconds)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeCoroutine(show, seconds));
            yield return _fadeCoroutine;
        }

        /// <summary>
        /// 淡入淡出协程
        /// </summary>
        private IEnumerator FadeCoroutine(bool show, float seconds)
        {
            _isTransitioning = true;
            OnFadeStarted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC1_VisualSystem] Starting fade: {(show ? "in" : "out")}");

            float elapsed = 0f;
            Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            Color endColor = startColor;
            endColor.a = show ? 1f : 0f;

            while (elapsed < seconds)
            {
                float t = elapsed / seconds;
                float curveValue = fadeCurve.Evaluate(t);
                Color currentColor = Color.Lerp(startColor, endColor, curveValue);

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = currentColor;
                }

                // 同时调整光源
                if (enableLightControl && selfLight != null)
                {
                    selfLight.intensity = show ? lightIntensity : 0f;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 确保最终值正确
            if (spriteRenderer != null)
            {
                spriteRenderer.color = endColor;
            }

            if (enableLightControl && selfLight != null)
            {
                selfLight.intensity = show ? lightIntensity : 0f;
            }

            _isVisible = show;
            _isTransitioning = false;
            OnFadeCompleted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC1_VisualSystem] Fade completed: {(show ? "in" : "out")}");
        }

        /// <summary>
        /// 初始出现效果
        /// </summary>
        public IEnumerator InitialAppear()
        {
            if (verboseLogs)
                Debug.Log("[BossC1_VisualSystem] Starting initial appear");

            // 播放出现特效
            if (vfxSpawn != null)
            {
                SpawnVfx(vfxSpawn, transform.position);
            }

            // 等待特效时间
            yield return new WaitForSeconds(spawnLeadVfxSeconds);

            // 淡入
            yield return StartCoroutine(FadeVisible(true, spawnFadeSeconds));
        }

        #endregion

        #region Color Transitions

        /// <summary>
        /// 设置阶段颜色
        /// </summary>
        public void SetPhaseColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// 获取当前阶段颜色
        /// </summary>
        public Color GetCurrentPhaseColor()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            switch (currentPhase)
            {
                case 1: return phase1Color;
                case 2: return phase2Color;
                case 3: return phase3Color;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 颜色过渡
        /// </summary>
        public IEnumerator LerpColor(Color target, float seconds)
        {
            if (_colorTransitionCR != null)
            {
                StopCoroutine(_colorTransitionCR);
            }

            _colorTransitionCR = StartCoroutine(ColorTransitionCoroutine(target, seconds));
            yield return _colorTransitionCR;
        }

        /// <summary>
        /// 颜色过渡协程
        /// </summary>
        private IEnumerator ColorTransitionCoroutine(Color target, float seconds)
        {
            _isTransitioning = true;
            OnColorTransitionStarted?.Invoke();

            if (verboseLogs)
                Debug.Log($"[BossC1_VisualSystem] Starting color transition to {target}");

            float elapsed = 0f;
            Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

            while (elapsed < seconds)
            {
                float t = elapsed / seconds;
                float curveValue = colorTransitionCurve.Evaluate(t);
                Color currentColor = Color.Lerp(startColor, target, curveValue);

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = currentColor;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 确保最终颜色正确
            if (spriteRenderer != null)
            {
                spriteRenderer.color = target;
            }

            _isTransitioning = false;
            OnColorTransitionCompleted?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC1_VisualSystem] Color transition completed");
        }

        #endregion

        #region VFX Management

        /// <summary>
        /// 生成特效
        /// </summary>
        public void SpawnVfx(GameObject prefab, Vector3 position)
        {
            if (prefab != null)
            {
                GameObject vfx = Instantiate(prefab, position, Quaternion.identity);
                Destroy(vfx, 5f); // 5秒后销毁

                OnVfxSpawned?.Invoke();

                if (verboseLogs)
                    Debug.Log($"[BossC1_VisualSystem] Spawned VFX: {prefab.name}");
            }
        }

        /// <summary>
        /// 播放阶段转换特效
        /// </summary>
        public void PlayPhaseTransitionVfx(int newPhase)
        {
            GameObject vfxPrefab = null;

            switch (newPhase)
            {
                case 2:
                    vfxPrefab = vfxTransform12;
                    break;
                case 3:
                    vfxPrefab = vfxTransform23;
                    break;
            }

            if (vfxPrefab != null)
            {
                SpawnVfx(vfxPrefab, transform.position);
            }
        }

        /// <summary>
        /// 播放遁入虚无特效
        /// </summary>
        public void PlayPhaseOutVfx()
        {
            if (vfxPhaseOut != null)
            {
                SpawnVfx(vfxPhaseOut, transform.position);
            }
        }

        /// <summary>
        /// 播放回归特效
        /// </summary>
        public void PlayPhaseInVfx()
        {
            if (vfxPhaseIn != null)
            {
                SpawnVfx(vfxPhaseIn, transform.position);
            }
        }

        /// <summary>
        /// 播放死亡特效
        /// </summary>
        public void PlayDeathVfx()
        {
            if (vfxDeath != null)
            {
                SpawnVfx(vfxDeath, transform.position);
            }
        }

        #endregion

        #region HP Bar

        /// <summary>
        /// 创建HP条
        /// </summary>
        private void CreateHPBar()
        {
            // 创建HP条背景
            _hpBarBackgroundRenderer = CreateLineRenderer("HPBarBackground");
            SetupHPBarRenderer(_hpBarBackgroundRenderer, hpBarBackgroundColor);

            // 创建HP条
            _hpBarRenderer = CreateLineRenderer("HPBar");
            SetupHPBarRenderer(_hpBarRenderer, hpBarColor);

            if (verboseLogs)
                Debug.Log("[BossC1_VisualSystem] HP bar created");
        }

        /// <summary>
        /// 创建LineRenderer
        /// </summary>
        private LineRenderer CreateLineRenderer(string name)
        {
            GameObject lineObj = new GameObject(name);
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;

            return lineObj.AddComponent<LineRenderer>();
        }

        /// <summary>
        /// 设置HP条渲染器
        /// </summary>
        private void SetupHPBarRenderer(LineRenderer renderer, Color color)
        {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.startWidth = 0.1f;
            renderer.endWidth = 0.1f;
            renderer.positionCount = 4;
            renderer.useWorldSpace = true;
            renderer.sortingOrder = 10;
        }

        /// <summary>
        /// 更新HP条
        /// </summary>
        public void UpdateHPBar(float healthPercentage)
        {
            if (_hpBarRenderer == null || _hpBarBackgroundRenderer == null) return;

            Vector3 bossPosition = transform.position;
            Vector3 hpBarPosition = bossPosition + hpBarOffset;

            // 更新HP条位置
            UpdateBarPosition(_hpBarRenderer, hpBarPosition, hpBarSize.x, hpBarSize.y);
            UpdateBarPosition(_hpBarBackgroundRenderer, hpBarPosition, hpBarSize.x, hpBarSize.y);

            // 更新HP条填充
            UpdateBarFill(_hpBarRenderer, healthPercentage);
        }

        /// <summary>
        /// 更新条位置
        /// </summary>
        private void UpdateBarPosition(LineRenderer renderer, Vector3 position, float width, float height)
        {
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            Vector3[] positions = new Vector3[4]
            {
                position + new Vector3(-halfWidth, -halfHeight, 0),
                position + new Vector3(halfWidth, -halfHeight, 0),
                position + new Vector3(halfWidth, halfHeight, 0),
                position + new Vector3(-halfWidth, halfHeight, 0)
            };

            renderer.SetPositions(positions);
        }

        /// <summary>
        /// 更新条填充
        /// </summary>
        private void UpdateBarFill(LineRenderer renderer, float percentage)
        {
            if (renderer == null) return;

            Vector3[] positions = new Vector3[4];
            renderer.GetPositions(positions);

            // 计算填充宽度
            float totalWidth = positions[1].x - positions[0].x;
            float fillWidth = totalWidth * percentage;

            // 更新填充位置
            positions[1].x = positions[0].x + fillWidth;
            positions[2].x = positions[0].x + fillWidth;

            renderer.SetPositions(positions);
        }

        #endregion

        #region Light Control

        /// <summary>
        /// 设置光源强度
        /// </summary>
        public void SetLightIntensity(float intensity)
        {
            if (selfLight != null)
            {
                selfLight.intensity = intensity;
            }
        }

        /// <summary>
        /// 设置光源颜色
        /// </summary>
        public void SetLightColor(Color color)
        {
            if (selfLight != null)
            {
                selfLight.color = color;
            }
        }

        /// <summary>
        /// 淡出光源
        /// </summary>
        public IEnumerator FadeOutLight(float duration)
        {
            if (selfLight == null) yield break;

            float startIntensity = selfLight.intensity;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                selfLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            selfLight.intensity = 0f;
        }

        /// <summary>
        /// 淡入光源
        /// </summary>
        public IEnumerator FadeInLight(float duration)
        {
            if (selfLight == null) yield break;

            float startIntensity = selfLight.intensity;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                selfLight.intensity = Mathf.Lerp(startIntensity, lightIntensity, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            selfLight.intensity = lightIntensity;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 激怒开始处理
        /// </summary>
        private void OnAggroStarted()
        {
            // 可以在这里添加激怒时的视觉效果
        }

        /// <summary>
        /// 死亡处理
        /// </summary>
        private void OnDeath()
        {
            // 播放死亡特效
            PlayDeathVfx();

            // 淡出光源
            if (enableLightControl)
            {
                StartCoroutine(FadeOutLight(1f));
            }
        }

        /// <summary>
        /// 阶段变化处理
        /// </summary>
        private void OnPhaseChanged(int newPhase)
        {
            // 播放阶段转换特效
            PlayPhaseTransitionVfx(newPhase);

            // 颜色过渡
            Color targetColor = GetCurrentPhaseColor();
            StartCoroutine(LerpColor(targetColor, colorTransitionDuration));
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取是否可见
        /// </summary>
        public bool IsVisible() => _isVisible;

        /// <summary>
        /// 获取是否正在过渡
        /// </summary>
        public bool IsTransitioning() => _isTransitioning;

        /// <summary>
        /// 设置可见性
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = visible ? 1f : 0f;
                spriteRenderer.color = color;
            }

            _isVisible = visible;
        }

        /// <summary>
        /// 重置视觉系统
        /// </summary>
        public void ResetVisualSystem()
        {
            // 停止协程
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (_colorTransitionCR != null)
            {
                StopCoroutine(_colorTransitionCR);
                _colorTransitionCR = null;
            }

            // 恢复原始设置
            if (spriteRenderer != null)
            {
                spriteRenderer.color = _originalColor;
            }

            if (selfLight != null)
            {
                selfLight.intensity = _originalLightIntensity;
                selfLight.color = _originalLightColor;
            }

            _isVisible = true;
            _isTransitioning = false;

            if (verboseLogs)
                Debug.Log("[BossC1_VisualSystem] Visual system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Visible: {_isVisible}, Transitioning: {_isTransitioning}, HP Bar: {(_hpBarRenderer != null)}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制HP条位置
            if (createHPBar)
            {
                Gizmos.color = hpBarColor;
                Vector3 hpBarPos = transform.position + hpBarOffset;
                Gizmos.DrawWireCube(hpBarPos, new Vector3(hpBarSize.x, hpBarSize.y, 0));
            }
        }

        #endregion
    }
}
