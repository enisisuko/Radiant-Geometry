using System.Collections.Generic;
using UnityEngine;

namespace FadedDreams.Utilities
{
    public static class BeamReflector2D
    {
        public struct Result
        {
            public List<Vector3> points;     // ���ߵ㣬���ٰ�����������յ�
            public Vector3 finalEnd;         // �������
            public Collider2D finalCollider; // ������е���ײ�壨����Ϊ null��
            public bool reflected;           // �Ƿ���������
        }

        /// <summary> �� origin �� dir ���ߣ����� Mirror2D ʱ���䣬��൯ maxBounces �Ρ� </summary>
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
                    o = (Vector2)hit.point + refl * stepEps; // ��΢�ƽ������ظ�����
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
