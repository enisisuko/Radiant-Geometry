using UnityEngine;

/// <summary>
/// ��ס���ʱ���� Player �����λ��֮�����һ���ߣ��ɿ������ء�
/// �÷����ѽű��ҵ����������ϣ�Ҳ��ֱ�ӹҵ� Player������ Player �Ͻ����Ｔ�ɡ�
/// </summary>
[DisallowMultipleComponent]
public class HoldTetherLine2D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("�������Զ�ʹ�� Camera.main")]
    [SerializeField] private Camera cam;
    [Tooltip("������㣨ͨ���� Player �� Transform���������գ������Բ��� tag==Player �Ķ���")]
    [SerializeField] private Transform player;

    [Tooltip("�����գ��ű����Զ�����һ�� LineRenderer")]
    [SerializeField] private LineRenderer line;

    [Header("Input")]
    [Tooltip("��ס�ĸ�������ʾ���ߣ�0 ���  / 1 �Ҽ� / 2 �м�")]
    [SerializeField, Range(0, 2)] private int mouseButton = 0;

    [Header("Style")]
    [SerializeField, Min(0f)] private float lineWidth = 0.08f;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private int capVertices = 4;
    [SerializeField] private int cornerVertices = 2;
    [SerializeField] private string sortingLayerName = ""; // ������
    [SerializeField] private int sortingOrder = 9999;

    [Header("Space")]
    [Tooltip("�����Ͷ�䵽����������� Z ƽ�桱����֤ 2D ��Ŀ���߲�ƫ�����ƽ��")]
    [SerializeField] private bool projectToPlayerZPlane = true;

    private bool _holding;
    private float _playerPlaneZ;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!player)
        {
            var byTag = GameObject.FindGameObjectWithTag("Player");
            if (byTag) player = byTag.transform;
        }

        if (player) _playerPlaneZ = player.position.z;

        if (!line)
        {
            // �Զ�������������� LineRenderer��������Ⱦ��ǰ�������
            var go = new GameObject("[TetherLine]");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
        }

        SetupLineRenderer();
        HideLine(); // ��ʼ����
    }

    void Update()
    {
        if (!player) return;

        // ���¿�ʼ��ʾ
        if (Input.GetMouseButtonDown(mouseButton))
        {
            _holding = true;
            ShowLine();
        }

        // ��סʱ�������˵�
        if (_holding)
        {
            Vector3 a = player.position;
            Vector3 b = GetMouseWorld();

            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        // �ɿ�����
        if (_holding && Input.GetMouseButtonUp(mouseButton))
        {
            _holding = false;
            HideLine();
        }
    }

    private void SetupLineRenderer()
    {
        line.useWorldSpace = true;
        line.receiveShadows = false;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.textureMode = LineTextureMode.Stretch;

        line.numCapVertices = Mathf.Max(0, capVertices);
        line.numCornerVertices = Mathf.Max(0, cornerVertices);
        line.startWidth = line.endWidth = lineWidth;

        if (!string.IsNullOrEmpty(sortingLayerName)) line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        // ����ʹ�� URP Unlit���Ҳ�������˵� Sprites/Default
        if (!line.material)
        {
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit)
            {
                var mat = new Material(urpUnlit);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", lineColor);
                line.material = mat;
            }
            else
            {
                Shader spritesDef = Shader.Find("Sprites/Default");
                var mat = new Material(spritesDef ? spritesDef : Shader.Find("Universal Render Pipeline/Simple Lit"));
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", lineColor);
                else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", lineColor);
                line.material = mat;
            }
        }
        else
        {
            // �������Ա����ʣ�����������ɫ
            if (line.material.HasProperty("_BaseColor")) line.material.SetColor("_BaseColor", lineColor);
            else if (line.material.HasProperty("_Color")) line.material.SetColor("_Color", lineColor);
        }

        // Ҳ���� LineRenderer ����Ľ���ɫ��˫����
        line.startColor = line.endColor = lineColor;
    }

    private Vector3 GetMouseWorld()
    {
        if (!cam) return player ? player.position : Vector3.zero;

        if (cam.orthographic)
        {
            var w = cam.ScreenToWorldPoint(Input.mousePosition);
            if (projectToPlayerZPlane && player) w.z = _playerPlaneZ;
            return w;
        }
        else
        {
            // ͸����������������Ͷ����������� z ƽ�桱
            if (!projectToPlayerZPlane || !player)
            {
                // ��Ͷƽ�棺�ÿ��������һ�����
                var w = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, Mathf.Abs(cam.transform.position.z)));
                return w;
            }

            Ray r = cam.ScreenPointToRay(Input.mousePosition);
            float denom = Mathf.Abs(r.direction.z) < 1e-6f ? 1e-6f : r.direction.z;
            float t = (_playerPlaneZ - r.origin.z) / denom;
            return r.origin + r.direction * t;
        }
    }

    private void ShowLine()
    {
        if (!line) return;
        line.enabled = true;
    }

    private void HideLine()
    {
        if (!line) return;
        line.enabled = false;
        line.positionCount = 0;
    }

#if UNITY_EDITOR
    // �ڱ༭���ﶯ̬��������ʱ��ͬ���������
    void OnValidate()
    {
        if (line)
        {
            line.startWidth = line.endWidth = Mathf.Max(0f, lineWidth);
            line.numCapVertices = Mathf.Max(0, capVertices);
            line.numCornerVertices = Mathf.Max(0, cornerVertices);
            line.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayerName)) line.sortingLayerName = sortingLayerName;

            if (line.material)
            {
                if (line.material.HasProperty("_BaseColor")) line.material.SetColor("_BaseColor", lineColor);
                else if (line.material.HasProperty("_Color")) line.material.SetColor("_Color", lineColor);
            }
            line.startColor = line.endColor = lineColor;
        }
    }
#endif
}
