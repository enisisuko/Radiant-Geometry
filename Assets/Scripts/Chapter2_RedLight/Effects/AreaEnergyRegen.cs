using System.Collections.Generic;
using UnityEngine;
using FadedDreams.Player; // 引用 ColorMode / PlayerColorModeController

namespace FadedDreams.World
{
    /// <summary>
    /// 将本物体作为“能量回复区”，一定半径内的 Player 按每秒速率缓慢恢复红/绿值。
    /// - 可选距离衰减：越靠近中心恢复越快；
    /// - 支持多 Player；
    /// - 无需 Collider，纯 OverlapCircleAll 扫描（Unity6 兼容封装）。
    /// </summary>
    public class AreaEnergyRegen : MonoBehaviour
    {
        [Header("Area")]
        [Min(0.1f)] public float radius = 3.5f;
        public LayerMask playerMask = ~0;   // 仅用于 OverlapCircle 过滤

        [Header("Regen Rates (per second)")]
        [Min(0f)] public float redPerSecond = 8f;
        [Min(0f)] public float greenPerSecond = 8f;

        [Header("Falloff")]
        [Tooltip("是否按距离做线性衰减：中心=100%，半径边缘=Min Multiplier")]
        public bool useDistanceFalloff = true;
        [Tooltip("距离到达边缘时的倍率（0~1），1=不衰减；例如 0.2 表示边缘处仅 20% 速率")]
        [Range(0f, 1f)] public float edgeMultiplier = 0.2f;

        [Header("QoL")]
        [Tooltip("每秒扫描次数。调低可省性能，但进入区域的响应会更迟缓")]
        [Range(1, 20)] public int scansPerSecond = 6;

        private readonly Dictionary<Collider2D, PlayerColorModeController> _cache = new();
        private float _scanTimer;

        private static readonly Collider2D[] _hitsBuffer = new Collider2D[32]; // 小型临时缓冲

        private void Update()
        {
            _scanTimer += Time.deltaTime;
            float scanInterval = 1f / Mathf.Max(1, scansPerSecond);
            if (_scanTimer < scanInterval) return;
            _scanTimer = 0f;

            // ✅ 修正为调用我们自己的兼容封装
            int count = Physics2DExt.OverlapCircleNonAllocCompat(transform.position, radius, _hitsBuffer, playerMask);
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                var col = _hitsBuffer[i];
                if (!col) continue;

                if (!_cache.TryGetValue(col, out var pcm) || !pcm)
                {
                    pcm = col.GetComponentInParent<PlayerColorModeController>();
                    _cache[col] = pcm; // 就算是 null 也缓存，减少重复 GetComponent
                }
                if (!pcm) continue;

                // 计算衰减倍率
                float mul = 1f;
                if (useDistanceFalloff)
                {
                    float d = Vector2.Distance(transform.position, pcm.transform.position);
                    float t = Mathf.Clamp01(d / Mathf.Max(0.001f, radius));
                    mul = Mathf.Lerp(1f, edgeMultiplier, t);
                }

                // 本“扫描间隔”内的回复量（保证稳定速率）
                float dt = scanInterval;
                float redAdd = redPerSecond * dt * mul;
                float greenAdd = greenPerSecond * dt * mul;

                if (redAdd > 0f) pcm.AddEnergy(ColorMode.Red, redAdd);
                if (greenAdd > 0f) pcm.AddEnergy(ColorMode.Green, greenAdd);
            }
        }

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            // ✅ Gizmos 只有 DrawSphere / DrawWireSphere，没有 DrawSolidSphere
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawSphere(transform.position, Mathf.Min(radius, 0.2f));
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.8f);
            DrawCircle(transform.position, radius, 48);
        }

        private void DrawCircle(Vector3 c, float r, int seg)
        {
            if (r <= 0f) return;
            Vector3 prev = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
        #endregion
    }

    /// <summary>
    /// Unity 6 对部分 NonAlloc 重载的弃用兼容：在 Unity6 下退化为 All + 拷贝。
    /// </summary>
    internal static class Physics2DExt
    {
        public static int OverlapCircleNonAllocCompat(Vector2 pos, float radius, Collider2D[] results, LayerMask mask)
        {
#if UNITY_6_0_OR_NEWER
            var cols = Physics2D.OverlapCircleAll(pos, radius, mask);
            int n = Mathf.Min(results.Length, cols.Length);
            for (int i = 0; i < n; i++) results[i] = cols[i];
            return n;
#else
            return Physics2D.OverlapCircleNonAlloc(pos, radius, results, mask);
#endif
        }
    }
}
