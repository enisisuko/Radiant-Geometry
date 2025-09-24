using UnityEngine;

/// <summary>
/// 让一张图在【透视或正交】相机下，按相机在该深度处的视野尺寸自动铺满屏幕。
/// 通过调节材质的 Tiling（重复）来“边缘复制”，也可选镜像平铺。
/// 用法：
/// 1) 给图片建一个 Quad（或任意平面朝向相机），把材质贴上去；Texture 导入 Wrap Mode 设为 Repeat 或 Mirror。
/// 2) 把本脚本挂到这个背景物体上，推荐作为相机的子物体，Z拉到你想要的深度层。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class BackgroundEdgeRepeatFill : MonoBehaviour
{
    public Camera targetCamera;
    [Tooltip("在相机前方的目标深度（仅当不是相机子物体时才用）。为正表示沿相机forward方向。")]
    public float depthOverride = -1f; // 子物体时会自动算，无需填
    [Tooltip("是否以镜像方式平铺（需要贴图 WrapMode=Mirror 或 MirrorOnce 支持）")]
    public bool mirrorTiling = false;
    [Tooltip("每块图在世界空间里期望显示的宽度（用于控制平铺密度）。为0表示让一张图横向刚好覆盖屏幕宽度。")]
    public float desiredTileWorldWidth = 0f;
    [Tooltip("为避免边缘黑边，可稍微放大一些")]
    [Range(0f, 0.3f)] public float paddingPercent = 0.02f;

    Renderer rend;
    Material matInstance;
    int mainTexId = Shader.PropertyToID("_MainTex");
    int baseMapId = Shader.PropertyToID("_BaseMap");
    Vector2 lastAppliedScale, lastAppliedOffset;
    Vector3 lastAppliedScale3D;

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        if (!targetCamera) targetCamera = Camera.main;

        // 在编辑器下也尽量用 sharedMaterial；运行时使用实例，避免影响其他对象
        matInstance = Application.isPlaying ? rend.material : rend.sharedMaterial;
    }

    void LateUpdate()
    {
        if (!targetCamera || !rend || matInstance == null) return;

        // 1) 计算“相机在该平面深度处”的视野宽高（世界单位）
        float depth = DepthAlongCameraForward();
        Vector2 viewSize = targetCamera.orthographic
            ? OrthoViewSize()
            : PerspectiveViewSizeAtDepth(depth);

        // 加一点边缘余量
        viewSize *= (1f + paddingPercent);

        // 2) 让承载网格（Quad）在世界空间至少覆盖这个视野宽高
        //   假设背景面朝相机（其本地X→横向，Y→纵向）
        Vector3 worldScale = transform.lossyScale;
        Vector2 currentSize = GetRendererSizeXY();
        // 如果模型是1x1的Quad，currentSize≈lossyScale.xy；若有非1尺寸网格，用bounds更稳：
        currentSize = new Vector2(rend.bounds.size.x, rend.bounds.size.y);

        // 需要的放大倍数
        float sx = (currentSize.x > 1e-5f) ? viewSize.x / currentSize.x : 1f;
        float sy = (currentSize.y > 1e-5f) ? viewSize.y / currentSize.y : 1f;

        // 以等比或非等比都可，这里非等比保证刚好充满
        Vector3 targetWorldScale = new Vector3(worldScale.x * sx, worldScale.y * sy, worldScale.z);
        // 把 worldScale 转回 localScale（避免父级缩放影响：用 TRS 逆求很麻烦，简单法：直接按比例乘 localScale）
        Vector3 local = transform.localScale;
        if (worldScale.x != 0) local.x *= sx;
        if (worldScale.y != 0) local.y *= sy;
        transform.localScale = local;

        // 3) 设置材质平铺：根据“想要每张贴图的世界宽度”，算出需要平铺多少次
        Vector2 tiling = Vector2.one;
        if (desiredTileWorldWidth > 0f)
        {
            float tilesX = Mathf.Max(1f, Mathf.Ceil(viewSize.x / desiredTileWorldWidth));
            // 同比算高度那边的单张世界高度
            float worldTileHeight = (viewSize.x / tilesX) * (GetTextureAspectInv() /*高度/宽度*/);
            float tilesY = Mathf.Max(1f, Mathf.Ceil(viewSize.y / worldTileHeight));

            tiling = new Vector2(tilesX, tilesY);
        }
        else
        {
            // desiredTileWorldWidth==0：一张图横向刚好覆盖整个视口宽度 → tilingX=1
            // 按贴图纵横比推算Y方向需要多少张（防止拉伸）
            float tilesX = 1f;
            float tilesY = Mathf.Max(1f, viewSize.y / (viewSize.x * GetTextureAspectInv()));
            tiling = new Vector2(tilesX, Mathf.Ceil(tilesY));
        }

        // 4) 镜像 or 普通平铺：镜像时偏移可以保持0；普通平铺也设0即可
        Vector2 offset = Vector2.zero;

        // 应用到 _BaseMap 或 _MainTex（兼容 URP / 内置）
        ApplyTilingAndOffset(tiling, offset);
    }

    float DepthAlongCameraForward()
    {
        // 物体相对相机的前向投影距离
        if (transform.IsChildOf(targetCamera.transform) || depthOverride < 0f)
        {
            return Vector3.Dot(transform.position - targetCamera.transform.position, targetCamera.transform.forward);
        }
        else
        {
            return depthOverride;
        }
    }

    Vector2 OrthoViewSize()
    {
        float h = targetCamera.orthographicSize * 2f;
        float w = h * targetCamera.aspect;
        return new Vector2(w, h);
    }

    Vector2 PerspectiveViewSizeAtDepth(float depth)
    {
        depth = Mathf.Abs(depth);
        float h = 2f * depth * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * targetCamera.aspect;
        return new Vector2(w, h);
    }

    float GetTextureAspectInv()
    {
        // 纹理高/宽 的比例，便于从横向推纵向
        Texture tex = GetMainTexture();
        if (tex) return (float)tex.height / Mathf.Max(1, tex.width);
        return 1f;
    }

    Texture GetMainTexture()
    {
        if (matInstance == null) return null;
        if (matInstance.HasProperty(baseMapId)) return matInstance.GetTexture(baseMapId);
        if (matInstance.HasProperty(mainTexId)) return matInstance.GetTexture(mainTexId);
        return null;
    }

    void ApplyTilingAndOffset(Vector2 tiling, Vector2 offset)
    {
        if (matInstance == null) return;

        // 只有变化时才下发，避免在编辑器里频繁脏标记
        if (tiling != lastAppliedScale || transform.localScale != lastAppliedScale3D || offset != lastAppliedOffset)
        {
            if (matInstance.HasProperty(baseMapId))
            {
                matInstance.SetTextureScale(baseMapId, tiling);
                matInstance.SetTextureOffset(baseMapId, offset);
            }
            if (matInstance.HasProperty(mainTexId))
            {
                matInstance.SetTextureScale(mainTexId, tiling);
                matInstance.SetTextureOffset(mainTexId, offset);
            }
            lastAppliedScale = tiling;
            lastAppliedOffset = offset;
            lastAppliedScale3D = transform.localScale;
        }
    }

    Vector2 GetRendererSizeXY()
    {
        var b = rend.bounds.size;
        return new Vector2(b.x, b.y);
    }
}
