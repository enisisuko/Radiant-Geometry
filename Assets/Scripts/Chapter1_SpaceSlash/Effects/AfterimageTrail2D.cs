using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.VFX
{
    /// <summary>
    /// 合并版 2D 残影组件：
    /// - BurstOnce(): 冲刺瞬间打一串残影（一次性爆发）
    /// - BeginEmit()/StopEmit(): 持续拖尾（协程驱动，无需 Update）
    /// - BeginTrail()/EndTrail(): 兼容旧脚本的名字，等价于 BeginEmit()/StopEmit()
    ///
    /// 用法：
    ///   1) 直接挂到玩家/敌人根物体上即可，默认会自动收集子节点的 SpriteRenderer；
    ///   2) 需要立即打一串残影：调用 BurstOnce();
    ///   3) 需要长时间拖尾：BeginEmit(); 结束时 StopEmit();
    ///   4) 若旧代码使用 BeginTrail/EndTrail，不用改，仍可用。
    /// </summary>
    [DisallowMultipleComponent]
    public class AfterimageTrail2D : MonoBehaviour
    {
        [Header("Snapshot（快照参数）")]
        [Tooltip("两帧残影之间的时间间隔")]
        public float snapshotInterval = 0.02f;
        [Tooltip("BurstOnce 时要打出的张数")]
        public int snapshotCount = 6;
        [Tooltip("每张残影的寿命")]
        public float snapshotLife = 0.25f;
        [Range(0, 1)] public float startAlpha = 0.8f;
        [Range(0, 1)] public float endAlpha = 0.0f;

        [Header("外观")]
        [Tooltip("是否给残影统一上色（否则继承源颜色）")]
        public bool useTint = false;
        public Color tint = Color.white;
        [Tooltip("残影的排序顺序相对源的偏移")]
        public int sortingOrderOffset = -1;
        [Tooltip("是否复制源材质（一般不需要）")]
        public bool copyMaterial = false;

        [Header("源渲染器（留空将自动收集子节点全部 SpriteRenderer）")]
        public List<SpriteRenderer> sourceSprites = new();
        [Tooltip("如果你只想用单一 SpriteRenderer，也可以只指定这个")]
        public SpriteRenderer spriteSource;

        // 运行态
        bool _emitting;
        float _emitTimer;
        Coroutine _emitCo;

        void Reset()
        {
            if (!spriteSource) spriteSource = GetComponentInChildren<SpriteRenderer>();
        }

        void Awake()
        {
            AutoCollectIfNeeded();
        }

        void AutoCollectIfNeeded()
        {
            if ((sourceSprites == null || sourceSprites.Count == 0))
            {
                sourceSprites = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>(true));
            }
            // 如果用户只想用单一 SpriteRenderer，就把它放到列表里（且去重）
            if (spriteSource)
            {
                if (sourceSprites == null) sourceSprites = new List<SpriteRenderer>();
                if (!sourceSprites.Contains(spriteSource))
                    sourceSprites.Insert(0, spriteSource);
            }
        }

        // ===== 新 API =====

        /// <summary> 一次性连发若干张残影（常用于 Dash 瞬间） </summary>
        public void BurstOnce()
        {
            StopAllCoroutines();
            StartCoroutine(CoBurst());
        }

        /// <summary> 持续拖尾（开始） </summary>
        public void BeginEmit()
        {
            _emitting = true;
            _emitTimer = 0f;
            if (_emitCo != null) StopCoroutine(_emitCo);
            // 立即打一张，手感更利落
            CreateSnapshot();
            _emitCo = StartCoroutine(CoEmitLoop());
        }

        /// <summary> 持续拖尾（停止） </summary>
        public void StopEmit()
        {
            _emitting = false;
            if (_emitCo != null) { StopCoroutine(_emitCo); _emitCo = null; }
        }

        IEnumerator CoEmitLoop()
        {
            while (_emitting)
            {
                _emitTimer += Time.deltaTime;
                if (_emitTimer >= snapshotInterval)
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

        // ===== 旧 API（兼容用） =====

        /// <summary> 等价于 BeginEmit()，用来兼容老脚本调用 </summary>
        public void BeginTrail() => BeginEmit();

        /// <summary> 等价于 StopEmit()，用来兼容老脚本调用 </summary>
        public void EndTrail() => StopEmit();

        // ===== 内部：生成/淡出 =====

        void CreateSnapshot()
        {
            AutoCollectIfNeeded();
            if (sourceSprites == null || sourceSprites.Count == 0) return;

            var root = new GameObject("AfterimageSnapshot");
            root.transform.position = transform.position;
            root.transform.rotation = transform.rotation;
            root.transform.localScale = transform.lossyScale;

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

                var baseColor = useTint ? tint : src.color;
                s.color = new Color(baseColor.r, baseColor.g, baseColor.b, startAlpha);
            }

            StartCoroutine(FadeAndKill(root));
        }

        IEnumerator FadeAndKill(GameObject root)
        {
            float t = 0f;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>();
            while (t < snapshotLife)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / snapshotLife);
                float a = Mathf.Lerp(startAlpha, endAlpha, u);
                foreach (var r in renderers)
                {
                    if (r) r.color = new Color(r.color.r, r.color.g, r.color.b, a);
                }
                yield return null;
            }
            Destroy(root);
        }
    }
}
