using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.VFX
{
    /// <summary>
    /// 残影 2D 拖尾系统 - 增强版
    /// - BurstOnce(): 瞬间生成一堆残影，一次性的（如 Dash 瞬间）
    /// - BeginEmit()/StopEmit(): 持续拖尾系统，建议在 Update 中调用
    /// - BeginTrail()/EndTrail(): 兼容旧脚本命名，等同于 BeginEmit()/StopEmit()
    ///
    /// 新增功能：
    ///   - 动态颜色渐变：残影会从原色渐变到指定颜色
    ///   - 动态缩放效果：残影会逐渐缩小
    ///   - 动态透明度：更平滑的淡出效果
    ///   - 动态旋转：残影可以添加旋转效果
    ///   - 粒子系统集成：可选的粒子效果增强
    ///   - 速度响应：根据移动速度调整残影密度
    ///
    /// 用法：
    ///   1) 直接挂到角色/敌人身上即可，默认会自动收集子节点的 SpriteRenderer
    ///   2) 需要瞬间残影效果时调用 BurstOnce();
    ///   3) 需要持续拖尾时调用 BeginEmit(); 结束时 StopEmit();
    ///   4) 兼容旧脚本可以使用 BeginTrail/EndTrail，功能相同。
    /// </summary>
    [DisallowMultipleComponent]
    public class AfterimageTrail2D : MonoBehaviour
    {
        [Header("Snapshot 基础设置")]
        [Tooltip("每帧残影之间的时间间隔")]
        public float snapshotInterval = 0.02f;
        [Tooltip("BurstOnce 时要生成的残影数量")]
        public int snapshotCount = 6;
        [Tooltip("每张残影的存活时间")]
        public float snapshotLife = 0.25f;
        [Range(0, 1)] public float startAlpha = 0.8f;
        [Range(0, 1)] public float endAlpha = 0.0f;

        [Header("颜色效果")]
        [Tooltip("是否让残影统一颜色，否则继承源颜色")]
        public bool useTint = false;
        public Color tint = Color.white;
        [Tooltip("是否启用颜色渐变效果")]
        public bool enableColorGradient = true;
        [Tooltip("残影结束时的颜色")]
        public Color endColor = new Color(1f, 0.5f, 0f, 0f); // 橙红色渐变
        [Tooltip("颜色渐变曲线")]
        public AnimationCurve colorGradientCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("缩放效果")]
        [Tooltip("是否启用动态缩放")]
        public bool enableScaleAnimation = true;
        [Tooltip("残影开始时的缩放倍数")]
        public float startScaleMultiplier = 1.0f;
        [Tooltip("残影结束时的缩放倍数")]
        public float endScaleMultiplier = 0.3f;
        [Tooltip("缩放动画曲线")]
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 0.3f);

        [Header("旋转效果")]
        [Tooltip("是否启用旋转效果")]
        public bool enableRotation = false;
        [Tooltip("旋转速度（度/秒）")]
        public float rotationSpeed = 180f;
        [Tooltip("是否随机旋转方向")]
        public bool randomizeRotation = true;

        [Header("透明度效果")]
        [Tooltip("透明度动画曲线")]
        public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [Tooltip("是否启用闪烁效果")]
        public bool enableFlicker = false;
        [Tooltip("闪烁频率")]
        public float flickerFrequency = 10f;

        [Header("排序和材质")]
        [Tooltip("残影的排序顺序偏移（相对于源）")]
        public int sortingOrderOffset = -1;
        [Tooltip("是否复制源材质，一般不需要")]
        public bool copyMaterial = false;
        [Tooltip("是否启用材质着色器效果")]
        public bool enableShaderEffects = false;
        [Tooltip("着色器属性名称")]
        public string shaderPropertyName = "_MainTex";

        [Header("速度响应")]
        [Tooltip("是否根据移动速度调整残影密度")]
        public bool enableSpeedResponse = true;
        [Tooltip("速度阈值，超过此速度时增加残影密度")]
        public float speedThreshold = 5f;
        [Tooltip("高速时的残影间隔倍数")]
        public float highSpeedIntervalMultiplier = 0.5f;

        [Header("粒子效果增强")]
        [Tooltip("是否启用粒子效果")]
        public bool enableParticleEffect = false;
        [Tooltip("粒子系统预制体")]
        public GameObject particleEffectPrefab;
        [Tooltip("粒子效果持续时间")]
        public float particleEffectDuration = 0.1f;

        [Header("源渲染器（会自动收集子节点全部的 SpriteRenderer）")]
        public List<SpriteRenderer> sourceSprites = new();
        [Tooltip("如果只需要用到一 SpriteRenderer，也可以只指定这个")]
        public SpriteRenderer spriteSource;

        // 运行时状态
        bool _emitting;
        float _emitTimer;
        Coroutine _emitCo;
        Vector3 _lastPosition;
        float _currentSpeed;
        List<GameObject> _activeSnapshots = new List<GameObject>();

        void Reset()
        {
            if (!spriteSource) spriteSource = GetComponentInChildren<SpriteRenderer>();
        }

        void Awake()
        {
            AutoCollectIfNeeded();
            _lastPosition = transform.position;
        }

        void Update()
        {
            if (enableSpeedResponse)
            {
                CalculateSpeed();
            }
        }

        void CalculateSpeed()
        {
            Vector3 currentPosition = transform.position;
            _currentSpeed = Vector3.Distance(currentPosition, _lastPosition) / Time.deltaTime;
            _lastPosition = currentPosition;
        }

        void AutoCollectIfNeeded()
        {
            if ((sourceSprites == null || sourceSprites.Count == 0))
            {
                sourceSprites = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>(true));
            }
            // 如果只用到一 SpriteRenderer，就把它放到列表里（去重）
            if (spriteSource)
            {
                if (sourceSprites == null) sourceSprites = new List<SpriteRenderer>();
                if (!sourceSprites.Contains(spriteSource))
                    sourceSprites.Insert(0, spriteSource);
            }
        }

        // ===== 公共 API =====

        /// <summary> 一瞬间生成一堆残影，适合 Dash 瞬间 </summary>
        public void BurstOnce()
        {
            StopAllCoroutines();
            StartCoroutine(CoBurst());
        }

        /// <summary> 开始持续拖尾 </summary>
        public void BeginEmit()
        {
            _emitting = true;
            _emitTimer = 0f;
            if (_emitCo != null) StopCoroutine(_emitCo);
            // 立即生成一张，给用户即时反馈
            CreateSnapshot();
            _emitCo = StartCoroutine(CoEmitLoop());
        }

        /// <summary> 停止持续拖尾 </summary>
        public void StopEmit()
        {
            _emitting = false;
            if (_emitCo != null) { StopCoroutine(_emitCo); _emitCo = null; }
        }

        /// <summary> 清理所有活跃的残影 </summary>
        public void ClearAllSnapshots()
        {
            foreach (var snapshot in _activeSnapshots)
            {
                if (snapshot) Destroy(snapshot);
            }
            _activeSnapshots.Clear();
        }

        IEnumerator CoEmitLoop()
        {
            while (_emitting)
            {
                _emitTimer += Time.deltaTime;
                
                float interval = snapshotInterval;
                if (enableSpeedResponse && _currentSpeed > speedThreshold)
                {
                    interval *= highSpeedIntervalMultiplier;
                }
                
                if (_emitTimer >= interval)
                {
                    _emitTimer = 0f;
                    CreateSnapshot();
                }
                yield return null;
            }
        }

        IEnumerator CoBurst()
        {
            int count = Mathf.Max(1, snapshotCount);
            for (int i = 0; i < count; i++)
            {
                CreateSnapshot();
                yield return new WaitForSeconds(Mathf.Max(0.001f, snapshotInterval));
            }
        }

        // ===== 兼容 API（向后兼容） =====

        /// <summary> 等同于 BeginEmit()，为了兼容旧脚本 </summary>
        public void BeginTrail() => BeginEmit();

        /// <summary> 等同于 StopEmit()，为了兼容旧脚本 </summary>
        public void EndTrail() => StopEmit();

        // ===== 内部方法/协程 =====

        void CreateSnapshot()
        {
            AutoCollectIfNeeded();
            if (sourceSprites == null || sourceSprites.Count == 0) return;

            var root = new GameObject("AfterimageSnapshot");
            root.transform.position = transform.position;
            root.transform.rotation = transform.rotation;
            root.transform.localScale = transform.lossyScale;

            // 添加粒子效果
            if (enableParticleEffect && particleEffectPrefab)
            {
                var particle = Instantiate(particleEffectPrefab, root.transform);
                Destroy(particle, particleEffectDuration);
            }

            foreach (var src in sourceSprites)
            {
                if (!src || !src.sprite) continue;

                var go = new GameObject(src.name + "_snap");
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                go.transform.localScale = src.transform.lossyScale;

                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = src.sprite;
                s.flipX = src.flipX;
                s.flipY = src.flipY;
                s.sortingLayerID = src.sortingLayerID;
                s.sortingOrder = src.sortingOrder + sortingOrderOffset;
                if (copyMaterial) s.sharedMaterial = src.sharedMaterial;

                // 设置初始颜色
                var baseColor = useTint ? tint : src.color;
                s.color = new Color(baseColor.r, baseColor.g, baseColor.b, startAlpha);

                // 设置初始缩放
                if (enableScaleAnimation)
                {
                    go.transform.localScale *= startScaleMultiplier;
                }
            }

            _activeSnapshots.Add(root);
            StartCoroutine(AnimateAndKill(root));
        }

        IEnumerator AnimateAndKill(GameObject root)
        {
            float t = 0f;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>();
            var initialColors = new Color[renderers.Length];
            var initialScales = new Vector3[renderers.Length];
            
            // 记录初始状态
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i])
                {
                    initialColors[i] = renderers[i].color;
                    initialScales[i] = renderers[i].transform.localScale;
                }
            }

            while (t < snapshotLife)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / snapshotLife);
                
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (!r) continue;

                    // 透明度动画
                    float alpha = alphaCurve.Evaluate(u);
                    if (enableFlicker)
                    {
                        alpha *= (Mathf.Sin(t * flickerFrequency * Mathf.PI * 2) + 1f) * 0.5f;
                    }
                    alpha = Mathf.Clamp01(alpha);

                    // 颜色渐变
                    Color currentColor;
                    if (enableColorGradient)
                    {
                        float colorLerp = colorGradientCurve.Evaluate(u);
                        currentColor = Color.Lerp(initialColors[i], endColor, colorLerp);
                    }
                    else
                    {
                        currentColor = initialColors[i];
                    }

                    r.color = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);

                    // 缩放动画
                    if (enableScaleAnimation)
                    {
                        float scaleMultiplier = scaleCurve.Evaluate(u);
                        r.transform.localScale = initialScales[i] * scaleMultiplier;
                    }

                    // 旋转动画
                    if (enableRotation)
                    {
                        float rotationAmount = rotationSpeed * t;
                        if (randomizeRotation && i == 0) // 只对第一个应用随机旋转
                        {
                            rotationAmount *= Random.Range(-1f, 1f);
                        }
                        r.transform.Rotate(0, 0, rotationAmount * Time.deltaTime);
                    }
                }
                yield return null;
            }
            
            _activeSnapshots.Remove(root);
            Destroy(root);
        }

        void OnDestroy()
        {
            ClearAllSnapshots();
        }
    }
}