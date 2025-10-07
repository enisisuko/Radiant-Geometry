using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.VFX
{
    /// <summary>
    /// �ϲ��� 2D ��Ӱ�����
    /// - BurstOnce(): ���˲���һ����Ӱ��һ���Ա�����
    /// - BeginEmit()/StopEmit(): ������β��Э������������ Update��
    /// - BeginTrail()/EndTrail(): ���ݾɽű������֣��ȼ��� BeginEmit()/StopEmit()
    ///
    /// �÷���
    ///   1) ֱ�ӹҵ����/���˸������ϼ��ɣ�Ĭ�ϻ��Զ��ռ��ӽڵ�� SpriteRenderer��
    ///   2) ��Ҫ������һ����Ӱ������ BurstOnce();
    ///   3) ��Ҫ��ʱ����β��BeginEmit(); ����ʱ StopEmit();
    ///   4) ���ɴ���ʹ�� BeginTrail/EndTrail�����øģ��Կ��á�
    /// </summary>
    [DisallowMultipleComponent]
    public class AfterimageTrail2D : MonoBehaviour
    {
        [Header("Snapshot�����ղ�����")]
        [Tooltip("��֡��Ӱ֮���ʱ����")]
        public float snapshotInterval = 0.02f;
        [Tooltip("BurstOnce ʱҪ���������")]
        public int snapshotCount = 6;
        [Tooltip("ÿ�Ų�Ӱ������")]
        public float snapshotLife = 0.25f;
        [Range(0, 1)] public float startAlpha = 0.8f;
        [Range(0, 1)] public float endAlpha = 0.0f;

        [Header("���")]
        [Tooltip("�Ƿ����Ӱͳһ��ɫ������̳�Դ��ɫ��")]
        public bool useTint = false;
        public Color tint = Color.white;
        [Tooltip("��Ӱ������˳�����Դ��ƫ��")]
        public int sortingOrderOffset = -1;
        [Tooltip("�Ƿ���Դ���ʣ�һ�㲻��Ҫ��")]
        public bool copyMaterial = false;

        [Header("Դ��Ⱦ�������ս��Զ��ռ��ӽڵ�ȫ�� SpriteRenderer��")]
        public List<SpriteRenderer> sourceSprites = new();
        [Tooltip("�����ֻ���õ�һ SpriteRenderer��Ҳ����ָֻ�����")]
        public SpriteRenderer spriteSource;

        // ����̬
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
            // ����û�ֻ���õ�һ SpriteRenderer���Ͱ����ŵ��б����ȥ�أ�
            if (spriteSource)
            {
                if (sourceSprites == null) sourceSprites = new List<SpriteRenderer>();
                if (!sourceSprites.Contains(spriteSource))
                    sourceSprites.Insert(0, spriteSource);
            }
        }

        // ===== �� API =====

        /// <summary> һ�������������Ų�Ӱ�������� Dash ˲�䣩 </summary>
        public void BurstOnce()
        {
            StopAllCoroutines();
            StartCoroutine(CoBurst());
        }

        /// <summary> ������β����ʼ�� </summary>
        public void BeginEmit()
        {
            _emitting = true;
            _emitTimer = 0f;
            if (_emitCo != null) StopCoroutine(_emitCo);
            // ������һ�ţ��ָи�����
            CreateSnapshot();
            _emitCo = StartCoroutine(CoEmitLoop());
        }

        /// <summary> ������β��ֹͣ�� </summary>
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

        // ===== �� API�������ã� =====

        /// <summary> �ȼ��� BeginEmit()�����������Ͻű����� </summary>
        public void BeginTrail() => BeginEmit();

        /// <summary> �ȼ��� StopEmit()�����������Ͻű����� </summary>
        public void EndTrail() => StopEmit();

        // ===== �ڲ�������/���� =====

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
