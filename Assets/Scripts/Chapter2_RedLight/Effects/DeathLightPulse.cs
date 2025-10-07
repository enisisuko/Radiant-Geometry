// DeathLightPulse.cs
using System.Collections;
using UnityEngine;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

public class DeathLightPulse : MonoBehaviour
{
#if UNITY_RENDERING_UNIVERSAL
    private Light2D l2d;
#endif
    private Component anyLightLike; // ���ã����� float intensity / pointLightOuterRadius �����

    public static void Spawn(Vector3 pos, float baseIntensity, float baseRadius, float riseSeconds = .25f, float fallSeconds = 3f)
    {
        var go = new GameObject("DeathLightPulse");
        go.transform.position = pos;
        var p = go.AddComponent<DeathLightPulse>();
        p.Init(baseIntensity, baseRadius, riseSeconds, fallSeconds);
    }

    public void Init(float baseIntensity, float baseRadius, float riseSeconds, float fallSeconds)
    {
#if UNITY_RENDERING_UNIVERSAL
        l2d = gameObject.AddComponent<Light2D>();
        l2d.lightType = Light2D.LightType.Point;
        l2d.intensity = Mathf.Max(0f, baseIntensity);
        l2d.pointLightOuterRadius = Mathf.Max(0f, baseRadius);
        l2d.shadowIntensity = 0f;
        l2d.color = Color.white;
#else
        // û�� URP Ҳ�ܹ����������ҵ�/����һ���� intensity/pointLightOuterRadius �����
        anyLightLike = this; // ��������һ��ռλ�������÷�������ʧ��Ҳ������
#endif
        StartCoroutine(CoPulse(baseIntensity, riseSeconds, fallSeconds));
    }

    private IEnumerator CoPulse(float baseIntensity, float riseSeconds, float fallSeconds)
    {
        float start = baseIntensity;
        float peak = baseIntensity * 2f;

        // ����
        float t = 0f;
        while (t < riseSeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / riseSeconds);
            SetIntensity(Mathf.Lerp(start, peak, u));
            yield return null;
        }

        // ˥��
        t = 0f;
        while (t < fallSeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fallSeconds);
            SetIntensity(Mathf.Lerp(peak, 0f, u));
            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetIntensity(float v)
    {
#if UNITY_RENDERING_UNIVERSAL
        if (l2d) l2d.intensity = Mathf.Max(0f, v);
#else
        if (anyLightLike)
        {
            var t = anyLightLike.GetType();
            var p = t.GetProperty("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(float)) p.SetValue(anyLightLike, v, null);
            var f = t.GetField("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) f.SetValue(anyLightLike, v);
        }
#endif
    }
}
