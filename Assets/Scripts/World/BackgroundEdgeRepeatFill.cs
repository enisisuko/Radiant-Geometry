using UnityEngine;

/// <summary>
/// ��һ��ͼ�ڡ�͸�ӻ�����������£�������ڸ���ȴ�����Ұ�ߴ��Զ�������Ļ��
/// ͨ�����ڲ��ʵ� Tiling���ظ���������Ե���ơ���Ҳ��ѡ����ƽ�̡�
/// �÷���
/// 1) ��ͼƬ��һ�� Quad��������ƽ�泯����������Ѳ�������ȥ��Texture ���� Wrap Mode ��Ϊ Repeat �� Mirror��
/// 2) �ѱ��ű��ҵ�������������ϣ��Ƽ���Ϊ����������壬Z��������Ҫ����Ȳ㡣
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class BackgroundEdgeRepeatFill : MonoBehaviour
{
    public Camera targetCamera;
    [Tooltip("�����ǰ����Ŀ����ȣ������������������ʱ���ã���Ϊ����ʾ�����forward����")]
    public float depthOverride = -1f; // ������ʱ���Զ��㣬������
    [Tooltip("�Ƿ��Ծ���ʽƽ�̣���Ҫ��ͼ WrapMode=Mirror �� MirrorOnce ֧�֣�")]
    public bool mirrorTiling = false;
    [Tooltip("ÿ��ͼ������ռ���������ʾ�Ŀ�ȣ����ڿ���ƽ���ܶȣ���Ϊ0��ʾ��һ��ͼ����պø�����Ļ��ȡ�")]
    public float desiredTileWorldWidth = 0f;
    [Tooltip("Ϊ�����Ե�ڱߣ�����΢�Ŵ�һЩ")]
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

        // �ڱ༭����Ҳ������ sharedMaterial������ʱʹ��ʵ��������Ӱ����������
        matInstance = Application.isPlaying ? rend.material : rend.sharedMaterial;
    }

    void LateUpdate()
    {
        if (!targetCamera || !rend || matInstance == null) return;

        // 1) ���㡰����ڸ�ƽ����ȴ�������Ұ��ߣ����絥λ��
        float depth = DepthAlongCameraForward();
        Vector2 viewSize = targetCamera.orthographic
            ? OrthoViewSize()
            : PerspectiveViewSizeAtDepth(depth);

        // ��һ���Ե����
        viewSize *= (1f + paddingPercent);

        // 2) �ó�������Quad��������ռ����ٸ��������Ұ���
        //   ���豳���泯������䱾��X������Y������
        Vector3 worldScale = transform.lossyScale;
        Vector2 currentSize = GetRendererSizeXY();
        // ���ģ����1x1��Quad��currentSize��lossyScale.xy�����з�1�ߴ�������bounds���ȣ�
        currentSize = new Vector2(rend.bounds.size.x, rend.bounds.size.y);

        // ��Ҫ�ķŴ���
        float sx = (currentSize.x > 1e-5f) ? viewSize.x / currentSize.x : 1f;
        float sy = (currentSize.y > 1e-5f) ? viewSize.y / currentSize.y : 1f;

        // �ԵȱȻ�ǵȱȶ��ɣ�����ǵȱȱ�֤�պó���
        Vector3 targetWorldScale = new Vector3(worldScale.x * sx, worldScale.y * sy, worldScale.z);
        // �� worldScale ת�� localScale�����⸸������Ӱ�죺�� TRS ������鷳���򵥷���ֱ�Ӱ������� localScale��
        Vector3 local = transform.localScale;
        if (worldScale.x != 0) local.x *= sx;
        if (worldScale.y != 0) local.y *= sy;
        transform.localScale = local;

        // 3) ���ò���ƽ�̣����ݡ���Ҫÿ����ͼ�������ȡ��������Ҫƽ�̶��ٴ�
        Vector2 tiling = Vector2.one;
        if (desiredTileWorldWidth > 0f)
        {
            float tilesX = Mathf.Max(1f, Mathf.Ceil(viewSize.x / desiredTileWorldWidth));
            // ͬ����߶��Ǳߵĵ�������߶�
            float worldTileHeight = (viewSize.x / tilesX) * (GetTextureAspectInv() /*�߶�/���*/);
            float tilesY = Mathf.Max(1f, Mathf.Ceil(viewSize.y / worldTileHeight));

            tiling = new Vector2(tilesX, tilesY);
        }
        else
        {
            // desiredTileWorldWidth==0��һ��ͼ����պø��������ӿڿ�� �� tilingX=1
            // ����ͼ�ݺ������Y������Ҫ�����ţ���ֹ���죩
            float tilesX = 1f;
            float tilesY = Mathf.Max(1f, viewSize.y / (viewSize.x * GetTextureAspectInv()));
            tiling = new Vector2(tilesX, Mathf.Ceil(tilesY));
        }

        // 4) ���� or ��ͨƽ�̣�����ʱƫ�ƿ��Ա���0����ͨƽ��Ҳ��0����
        Vector2 offset = Vector2.zero;

        // Ӧ�õ� _BaseMap �� _MainTex������ URP / ���ã�
        ApplyTilingAndOffset(tiling, offset);
    }

    float DepthAlongCameraForward()
    {
        // ������������ǰ��ͶӰ����
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
        // �����/�� �ı��������ڴӺ���������
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

        // ֻ�б仯ʱ���·��������ڱ༭����Ƶ������
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
