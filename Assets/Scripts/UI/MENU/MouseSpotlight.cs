using UnityEngine;

[DisallowMultipleComponent]
public class MouseSpotlight : MonoBehaviour
{
    [Header("Light")]
    public float intensity = 3.0f;

    [Header("Base Shape/Color")]
    public Color baseColor = Color.white;
    public float baseSpotAngle = 35f;
    public float baseRange = 20f;

    [Header("Hover Shape/Color")]
    [Tooltip("���������ͣ����һ��ť��ʱ��������Ϊ����ɫ")]
    public Color hoverColor = new Color(0.45f, 1.0f, 0.45f, 1f);
    [Tooltip("��ͣʱ��΢������׶��")]
    public float hoverSpotAngle = 30f;
    [Tooltip("��ͣʱ��΢��С�ķ�Χ")]
    public float hoverRange = 18f;

    [Header("Follow")]
    public float aimLerp = 6f;             // ָ�����ӳ�
    public float defaultDistance = 10f;    // δ���е���ʱ��Ĭ�Ͼ���
    public LayerMask groundMask = -1;      // ���߼���

    [Header("Lerp Speeds")]
    public float colorLerp = 5f;
    public float shapeLerp = 6f;

    Light spot;
    Camera cam;
    Vector3 aimPoint;
    bool hoveringUI;     // �ɿ�������֪���Ƿ�����ͣ�ڰ�ť�ϡ�
    float hoverWeight;   // 0~1 ���뽥��

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam) cam = Camera.main;

        spot = GetComponentInChildren<Light>();
        if (!spot)
        {
            var go = new GameObject("MouseSpot");
            go.transform.SetParent(transform, false);
            spot = go.AddComponent<Light>();
        }
        spot.type = LightType.Spot;
        spot.spotAngle = baseSpotAngle;
        spot.range = baseRange;
        spot.intensity = intensity;
        spot.color = baseColor;
        spot.shadows = LightShadows.None; // �˵���ͨ����Ҫ��Ӱ
    }

    public void SetHoveringUI(bool v)
    {
        hoveringUI = v;
    }

    void Update()
    {
        // ��ͣȨ�ؽ��䣨������ɫ/��״ǿ�ȣ�
        hoverWeight = Mathf.MoveTowards(hoverWeight, hoveringUI ? 1f : 0f, Time.deltaTime * Mathf.Max(1f, Mathf.Min(colorLerp, shapeLerp)));

        // ׷������ڳ����ĵ㣨���ӳ٣�
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 want;
        if (Physics.Raycast(ray, out var hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
        {
            want = hit.point;
        }
        else
        {
            want = cam.transform.position + cam.transform.forward * defaultDistance;
        }
        aimPoint = Vector3.Lerp(aimPoint == default ? want : aimPoint, want, Time.deltaTime * aimLerp);

        // �������λ�ã����� aimPoint
        spot.transform.position = cam.transform.position;
        Vector3 dir = (aimPoint - spot.transform.position);
        if (dir.sqrMagnitude < 1e-4f) dir = cam.transform.forward;
        spot.transform.rotation = Quaternion.Slerp(spot.transform.rotation,
            Quaternion.LookRotation(dir.normalized, Vector3.up),
            Time.deltaTime * aimLerp * 0.8f);

        // ��ɫ/��״��ֵ
        Color targetC = Color.Lerp(baseColor, hoverColor, hoverWeight);
        float targetAngle = Mathf.Lerp(baseSpotAngle, hoverSpotAngle, hoverWeight);
        float targetRange = Mathf.Lerp(baseRange, hoverRange, hoverWeight);

        spot.color = Color.Lerp(spot.color, targetC, Time.deltaTime * colorLerp);
        spot.spotAngle = Mathf.Lerp(spot.spotAngle, targetAngle, Time.deltaTime * shapeLerp);
        spot.range = Mathf.Lerp(spot.range, targetRange, Time.deltaTime * shapeLerp);
    }
}
