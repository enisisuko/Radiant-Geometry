using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.Utilities
{
    public static class BeamReflector2D
    {
        public struct Result
        {
            public List<Vector3> points;     // 折线点，至少包含起点与最终点
            public Vector3 finalEnd;         // 最终落点
            public Collider2D finalCollider; // 最后命中的碰撞体（可能为 null）
            public bool reflected;           // 是否发生过反射
        }

        /// <summary> 从 origin 沿 dir 射线，命中 Mirror2D 时反射，最多弹 maxBounces 次。 </summary>
        public static Result Cast(Vector3 origin, Vector2 dir, int maxBounces, int layerMask, float stepEps = 0.01f, float maxDist = 1000f)
        {
            Result r = new Result { points = new List<Vector3>(maxBounces + 2) };
            r.points.Add(origin);
            Vector3 o = origin;
            Vector2 d = dir.normalized;

            for (int i = 0; i <= maxBounces; i++)
            {
                var hit = Physics2D.Raycast(o, d, maxDist, layerMask);
                if (!hit.collider)
                {
                    var far = o + (Vector3)(d * maxDist);
                    r.points.Add(far);
                    r.finalEnd = far;
                    r.finalCollider = null;
                    return r;
                }

                r.points.Add(hit.point);
                r.finalEnd = hit.point;
                r.finalCollider = hit.collider;

                var mirror = hit.collider.GetComponentInParent<FadedDreams.World.Mirror2D>();
                if (i < maxBounces && mirror != null && mirror.enabledReflection)
                {
                    Vector2 refl = Vector2.Reflect(d, hit.normal).normalized;
                    o = (Vector2)hit.point + refl * stepEps; // 轻微推进避免重复命中
                    d = refl;
                    r.reflected = true;
                    continue;
                }
                break;
            }
            return r;
        }
    }
}
