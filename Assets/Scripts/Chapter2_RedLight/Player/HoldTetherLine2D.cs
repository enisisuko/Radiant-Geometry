using UnityEngine;

/// <summary>
/// 按住鼠标时，在 Player 与鼠标位置之间绘制一条线；松开后隐藏。
/// 用法：把脚本挂到任意物体上（也可直接挂到 Player），把 Player 拖进槽里即可。
/// </summary>
[DisallowMultipleComponent]
public class HoldTetherLine2D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("不填则自动使用 Camera.main")]
    [SerializeField] private Camera cam;
    [Tooltip("连线起点（通常填 Player 的 Transform）。若留空，将尝试查找 tag==Player 的对象")]
    [SerializeField] private Transform player;

    [Tooltip("可留空；脚本会自动创建一个 LineRenderer")]
    [SerializeField] private LineRenderer line;

    [Header("Input")]
    [Tooltip("按住哪个鼠标键显示连线：0 左键  / 1 右键 / 2 中键")]
    [SerializeField, Range(0, 2)] private int mouseButton = 0;

    [Header("Style")]
    [SerializeField, Min(0f)] private float lineWidth = 0.08f;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private int capVertices = 4;
    [SerializeField] private int cornerVertices = 2;
    [SerializeField] private string sortingLayerName = ""; // 可留空
    [SerializeField] private int sortingOrder = 9999;

    [Header("Space")]
    [Tooltip("将鼠标投射到“玩家所处的 Z 平面”，保证 2D 项目中线不偏离玩家平面")]
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
            // 自动创建子物体承载 LineRenderer，避免污染当前物体组件
            var go = new GameObject("[TetherLine]");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
        }

        SetupLineRenderer();
        HideLine(); // 初始隐藏
    }

    void Update()
    {
        if (!player) return;

        // 按下开始显示
        if (Input.GetMouseButtonDown(mouseButton))
        {
            _holding = true;
            ShowLine();
        }

        // 按住时更新两端点
        if (_holding)
        {
            Vector3 a = player.position;
            Vector3 b = GetMouseWorld();

            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        // 松开隐藏
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

        // 尝试使用 URP Unlit；找不到则回退到 Sprites/Default
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
            // 若你已自备材质，尽量设置颜色
            if (line.material.HasProperty("_BaseColor")) line.material.SetColor("_BaseColor", lineColor);
            else if (line.material.HasProperty("_Color")) line.material.SetColor("_Color", lineColor);
        }

        // 也设置 LineRenderer 自身的渐变色，双保险
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
            // 透视相机：把鼠标射线投到“玩家所在 z 平面”
            if (!projectToPlayerZPlane || !player)
            {
                // 不投平面：用靠近相机的一个深度
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
    // 在编辑器里动态调整参数时，同步部分外观
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
