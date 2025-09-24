using System.Collections.Generic;
using UnityEngine;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal; // Light2D
#endif
using FadedDreams.World;                // LightSource2D
using FadedDreams.Player;               // PlayerLightController

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    public class LightIrradianceSensor : MonoBehaviour
    {
        [Header("Sampling Area")]
        [Tooltip("�����뾶�����絥λ�����������Ӵ˰뾶�ڵĹ�Դ/Light2D �ۼƹ���")]
        public float radius = 2.5f;
        [Tooltip("���� Physics2D.OverlapCircle �� LayerMask������=ȫ����")]
        public LayerMask sampleMask = ~0;

        [Header("Normalization")]
        [Tooltip("��Ϊ�����������ԭʼǿ�ȡ�Խ����Խ����������")]
        public float fullIntensity = 5f;
        [Tooltip("ÿ����Ȼ˥������ֹ��������0=��˥��")]
        public float decayPerSecond = 0f;

        [Header("Hysteresis")]
        [Tooltip("�ﵽ�˱�����0..1���ж�Ϊ������")]
        [Range(0f, 1f)] public float saturateThreshold01 = 1f;
        [Tooltip("���ƴ˱�����0..1���ж�Ϊ�����񣨳��ͷ�����")]
        [Range(0f, 1f)] public float desaturateThreshold01 = 0.9f;

        [Header("Debug")]
        public bool drawGizmo = true;
        public Color gizmoColor = new Color(1, 1, 0, 0.2f);

        // �����ԭʼǿ����0..1 ��һ
        public float IrradianceRaw { get; private set; }
        public float Irradiance01 => fullIntensity <= 0f ? 0f : Mathf.Clamp01(IrradianceRaw / fullIntensity);
        public bool IsSaturated { get; private set; }

        // ���棬���� GC
        readonly Collider2D[] _hits = new Collider2D[32];

        void Update()
        {
            float raw = SampleIrradiance();
            if (decayPerSecond > 0f)
            {
                // �ö������ȶ���ֻ�ڱ�ǿʱ����̧��������ʱ����˥��
                if (raw >= IrradianceRaw) IrradianceRaw = raw;
                else IrradianceRaw = Mathf.Max(0f, IrradianceRaw - decayPerSecond * Time.deltaTime);
            }
            else
            {
                IrradianceRaw = raw;
            }

            // �����ж�
            float k = Irradiance01;
            if (IsSaturated)
            {
                if (k < desaturateThreshold01) IsSaturated = false;
            }
            else
            {
                if (k >= saturateThreshold01) IsSaturated = true;
            }
        }

        float SampleIrradiance()
        {
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _hits, sampleMask);
            float sum = 0f;

            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                if (!col) continue;

                // 1) ������̬��Դ���Դ�ǿ��/�������ã�
                var src = col.GetComponent<LightSource2D>();
                if (src != null)
                {
                    // ͨ�������� intensity��LightSource2D �ڲ�Ҳ�÷��䶵�ף�
                    var comp = src.light2DAny;
#if UNITY_RENDERING_UNIVERSAL
                    if (src.light2D) sum += Mathf.Max(0f, src.light2D.intensity);
                    else if (comp) sum += TryGetIntensityViaReflection(comp);
#else
                    if (comp) sum += TryGetIntensityViaReflection(comp);
#endif
                    continue;
                }

                // 2) URP Light2D ֱ�Ӳ���
#if UNITY_RENDERING_UNIVERSAL
                var l2d = col.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                if (l2d) { sum += Mathf.Max(0f, l2d.intensity); continue; }
#endif

                // 3) ��ұ��巢�⣨ȡ��� Light2D �� intensity��
                //    ����ֻ������ҡ���ײ�壺Tag=Player
                if (col.CompareTag("Player"))
                {
                    sum += EstimatePlayerLight(col.transform);
                }
            }

            return sum;
        }

        float EstimatePlayerLight(Transform player)
        {
            // ���� ReadingStateController ��������Ѱ������Ӳ㼶����� Light2D ��Ϊ��׼����
            // ����1��û�м������룬��������� Light2D �Ի��������ӳ��ǿ�ȣ���
            float best = 0f;
#if UNITY_RENDERING_UNIVERSAL
            var lights = player.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
            float bestDist = float.MaxValue;
            foreach (var l in lights)
            {
                float d = (l.transform.position - player.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = Mathf.Max(best, l.intensity); }
            }
#else
            // δ���� URP ����ʱ���������������Ϸ����� intensity �ֶ�/������Ϊ "Light2D"
            var comps = player.GetComponentsInChildren<Component>(true);
            float bestDist = float.MaxValue;
            foreach (var c in comps)
            {
                if (!c) continue;
                if (c.GetType().Name != "Light2D") continue;
                float d = (c.transform.position - player.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Mathf.Max(best, TryGetIntensityViaReflection(c));
                }
            }
#endif
            return best;
        }

        float TryGetIntensityViaReflection(Component comp)
        {
            if (!comp) return 0f;
            var t = comp.GetType();
            var p = t.GetProperty("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float))
            {
                object v = p.GetValue(comp, null);
                return v is float f ? Mathf.Max(0f, f) : 0f;
            }
            var f0 = t.GetField("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f0 != null && f0.FieldType == typeof(float))
            {
                object v = f0.GetValue(comp);
                return v is float f ? Mathf.Max(0f, f) : 0f;
            }
            return 0f;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
