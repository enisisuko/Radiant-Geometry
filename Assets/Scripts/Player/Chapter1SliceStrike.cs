using FadedDreams.Enemies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 第一章“空间斩”：按下左键减速+拖线；松开左键 → 沿屏幕无限直线扫出贯穿激光；命中 Enemy → 爆炸+震屏。
// 说明：
// 1) 正常使用正交相机（2D）。透视也能用，但 ScreenToWorldPoint 的 z 要设对（用玩家 z 平面）。
// 2) 激光横扫的“命中检测”使用 Physics2D.LinecastAll(worldA, worldB)。命中 tag=Enemy 则处理。
// 3) 慢速化用 Time.timeScale; 视觉/震屏用 Time.unscaledDeltaTime，不掉帧。
// 4) 仅在第一章启用（通过 GameManager.CurrentChapter==1 判定; 若你用别的章节系统，自行替换）。

public class Chapter1SpaceSlashController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform player;
    [SerializeField] private LineRenderer tetherLine;   // 玩家 -> 当前鼠标
    [SerializeField] private LineRenderer dragLine;     // 鼠标按下点 -> 当前鼠标
    [SerializeField] private GameObject startVfxPrefab; // 按下时起点特效
    [SerializeField] private GameObject beamLinePrefab; // 横扫用的临时 LineRenderer 预制体（白色发光材质）
    [SerializeField] private GameObject explosionPrefab;// 敌人被切中时的爆炸特效
    [SerializeField] private AudioSource sfx;           // 可选：音效播放器（起手/扫击/爆炸）

    [Header("SlowMo (unscaled 驱动)")]
    [SerializeField, Range(0.01f, 1f)] private float slowTarget = 0.1f;
    [SerializeField, Min(0f)] private float slowInSeconds = 0.25f;
    [SerializeField, Min(0f)] private float slowOutSeconds = 0.15f;

    [Header("Sweep Laser")]
    [SerializeField, Min(0f)] private float sweepSeconds = 0.12f; // 激光从近侧边缘扫到对面用时（真实时间）
    [SerializeField, Min(0f)] private float beamStaySeconds = 0.06f; // 扫过后保留一瞬
    [SerializeField, Min(0f)] private float lineWidth = 0.08f;
    [SerializeField] private LayerMask linecastMask = ~0; // 默认全部；有需要可排除地形层

    [Header("Shake While Holding")]
    [SerializeField] private AnimationCurve holdShakeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField, Min(0f)] private float holdShakeMax = 1.2f;  // 抖动强度峰值（配合你的 CameraShake2D 使用）
    [SerializeField, Min(0f)] private float holdShakeRampSeconds = 0.6f;
    [SerializeField] private UnityEvent<float> onShakeWhileHolding; // 把你的 CameraShake2D 强度接口绑到这里（参数=强度）
    [SerializeField] private UnityEvent onSweepBlast;               // 松开瞬间的强烈震屏/屏闪

    [Header("Only Chapter 1")]
    [SerializeField] private bool onlyChapterOne = true;
    [SerializeField] private int chapterIndex = 1; // 第一章编号

    // 内部
    private bool _holding;
    private Vector3 _pressScreen;
    private Vector3 _pressWorld;
    private Coroutine _slowCo;
    private float _baseFixedDelta;
    private float _playerPlaneZ;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        _baseFixedDelta = Time.fixedDeltaTime;
        _playerPlaneZ = player ? player.position.z : 0f;

        // 保护：如果没给 LineRenderer，就临时创建
        if (!tetherLine) tetherLine = CreateRuntimeLine("_TetherLine");
        if (!dragLine) dragLine = CreateRuntimeLine("_DragLine");

        SetupLine(tetherLine);
        SetupLine(dragLine);

        HideLine(tetherLine);
        HideLine(dragLine);
    }

    void Update()
    {
        if (onlyChapterOne)
        {
            // 你项目里 GameManager 有 CurrentChapter（默认 1），这里判一下
            // 如果你用别的系统，请改掉这一段。
            var gm = FindObjectOfType<MonoBehaviour>(); // 占位，防止空引用；真正的章节判定放下面 try/catch
            try
            {
                var gmType = System.Type.GetType("GameManager");
                if (gmType != null)
                {
                    var instProp = gmType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instProp?.GetValue(null);
                    var chapProp = gmType.GetProperty("CurrentChapter", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    int chap = (int)(chapProp?.GetValue(inst) ?? 1);
                    if (chap != chapterIndex) { SafelyClearState(); return; }
                }
            }
            catch { /* 安全失败则继续允许 */ }
        }

        // LMB
        if (Input.GetMouseButtonDown(0)) OnPress();
        if (_holding) OnHolding();
        if (Input.GetMouseButtonUp(0) && _holding) OnRelease();
    }

    // —— 按下：记录起点 + 慢速化 + 显示两条线 + 持续抖动开始
    private void OnPress()
    {
        _holding = true;
        _pressScreen = Input.mousePosition;
        _pressWorld = ScreenToWorldOnPlayerPlane(_pressScreen);

        if (startVfxPrefab) Instantiate(startVfxPrefab, _pressWorld, Quaternion.identity);

        ShowLine(tetherLine);
        ShowLine(dragLine);

        // 进慢速（unscaled）
        StartReplaceCo(ref _slowCo, CoScaleTime(1f, slowTarget, slowInSeconds));

        // 按住时震屏强度渐增（用事件把强度丢给你的 CameraShake2D）
        StartCoroutine(CoHoldShake());
    }

    // —— 按住：两条线跟随
    private void OnHolding()
    {
        Vector3 mouseWorld = ScreenToWorldOnPlayerPlane(Input.mousePosition);

        if (tetherLine)
        {
            tetherLine.positionCount = 2;
            tetherLine.SetPosition(0, player ? player.position : Vector3.zero);
            tetherLine.SetPosition(1, mouseWorld);
        }
        if (dragLine)
        {
            dragLine.positionCount = 2;
            dragLine.SetPosition(0, _pressWorld);
            dragLine.SetPosition(1, mouseWorld);
        }
    }

    // —— 松开：根据“按下→松开”的直线求与屏幕矩形的两交点，从靠近起点的边缘开始做贯穿扫击；命中敌人→爆炸/摧毁。
    private void OnRelease()
    {
        _holding = false;

        // ✅ 关键1：不要用旧的 _pressScreen；用“当前相机下”的屏幕坐标
        Vector2 a = cam ? (Vector2)cam.WorldToScreenPoint(_pressWorld) : (Vector2)_pressScreen;
        Vector2 b = (Vector2)Input.mousePosition;

        var ok = TryGetScreenRectIntersections(a, b, out Vector2 i0, out Vector2 i1);
        if (ok)
        {
            // 先转到“玩家所在Z平面”的世界坐标
            Vector3 i0W = ScreenToWorldOnPlayerPlane(i0);
            Vector3 i1W = ScreenToWorldOnPlayerPlane(i1);

            // ✅ 关键2：用世界距离来选近/远端，保证与起点世界位置一致
            Vector3 nearW = (Vector3.Distance(_pressWorld, i0W) < Vector3.Distance(_pressWorld, i1W)) ? i0W : i1W;
            Vector3 farW = (nearW == i0W) ? i1W : i0W;

            if (sfx) sfx.Play();
            onSweepBlast?.Invoke();
            StartCoroutine(CoSweepAndHit(nearW, farW));
        }

        // 退慢速 & 隐线保持原样
        StartReplaceCo(ref _slowCo, CoScaleTime(Time.timeScale, 1f, slowOutSeconds));
        HideLine(tetherLine);
        HideLine(dragLine);
    }


    // —— 扫描激光 + 命中检测
    private IEnumerator CoSweepAndHit(Vector3 nearW, Vector3 farW)
    {
        // 渲染一条临时横扫激光（从 near → far 线性推进）
        LineRenderer beam = null;
        if (beamLinePrefab)
        {
            var go = Instantiate(beamLinePrefab);
            beam = go.GetComponent<LineRenderer>() ?? go.AddComponent<LineRenderer>();
            SetupLine(beam);
        }
        else
        {
            beam = CreateRuntimeLine("_BeamSweep");
            SetupLine(beam);
        }
        beam.startWidth = beam.endWidth = lineWidth;
        beam.positionCount = 2;

        // 命中检测：整条线一次性判定（为了视觉一致，先扫特效，再按整条线判敌人）
        DoHitEnemiesAlongLine(nearW, farW);

        // 可选：你想要“刀光推进感”，就把终点从 near→far 插值；想要“瞬间全屏白线”，把下面插值时长设更短。
        float t = 0f;
        while (t < sweepSeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / sweepSeconds);

            Vector3 tip = Vector3.Lerp(nearW, farW, u);
            beam.SetPosition(0, nearW);
            beam.SetPosition(1, tip);

            yield return null;
        }

        beam.SetPosition(0, nearW);
        beam.SetPosition(1, farW);
        yield return new WaitForSecondsRealtime(beamStaySeconds);

        if (beam) Destroy(beam.gameObject);
    }

    private void DoHitEnemiesAlongLine(Vector3 a, Vector3 b)
    {
        var hits = Physics2D.LinecastAll(a, b, linecastMask);
        if (hits == null || hits.Length == 0) return;

        HashSet<GameObject> visited = new HashSet<GameObject>();
        foreach (var h in hits)
        {
            var go = h.collider ? h.collider.gameObject : null;
            if (!go || visited.Contains(go)) continue;
            visited.Add(go);

            if (go.CompareTag("Enemy"))
            {
                // 优先走 IDamageable
                var dmg = go.GetComponent(typeof(IDamageable)) as object;
                if (dmg != null)
                {
                    var m = dmg.GetType().GetMethod("TakeDamage");
                    m?.Invoke(dmg, new object[] { float.MaxValue }); // 直接处决
                }
                else
                {
                    // 没实现接口就直接摧毁
                    Destroy(go);
                }

                if (explosionPrefab)
                {
                    Instantiate(explosionPrefab, go.transform.position, Quaternion.identity);
                }
            }
        }
    }

    // —— 按住时镜头抖动强度从 0→holdShakeMax（按 unscaled 时间）
    private IEnumerator CoHoldShake()
    {
        float t = 0f;
        while (_holding)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(holdShakeRampSeconds <= 0f ? 1f : (t / holdShakeRampSeconds));
            float strength = holdShakeCurve.Evaluate(u) * holdShakeMax;
            onShakeWhileHolding?.Invoke(strength);
            yield return null;
        }
        // 松开后恢复到 0
        onShakeWhileHolding?.Invoke(0f);
    }

    // —— 慢速化/恢复（unscaled 线性插值），并保持 fixedDeltaTime 等比例
    private IEnumerator CoScaleTime(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            Time.timeScale = to;
            Time.fixedDeltaTime = _baseFixedDelta * Time.timeScale;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / seconds);
            float ts = Mathf.Lerp(from, to, u);
            Time.timeScale = ts;
            Time.fixedDeltaTime = _baseFixedDelta * Time.timeScale;
            yield return null;
        }
        Time.timeScale = to;
        Time.fixedDeltaTime = _baseFixedDelta * Time.timeScale;
    }

    private void SafelyClearState()
    {
        _holding = false;
        HideLine(tetherLine);
        HideLine(dragLine);
    }

    // —— 工具：求屏幕矩形的两交点（直线 a-b 与四条边）
    private bool TryGetScreenRectIntersections(Vector2 a, Vector2 b, out Vector2 i0, out Vector2 i1)
    {
        i0 = i1 = Vector2.zero;
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 1e-6f) return false;

        float w = cam ? cam.pixelWidth : Screen.width;
        float h = cam ? cam.pixelHeight : Screen.height;

        List<Vector2> pts = new List<Vector2>(4);
        // x = 0
        if (Mathf.Abs(dir.x) > 1e-6f)
        {
            float t = (0f - a.x) / dir.x;
            float y = a.y + t * dir.y;
            if (t != Mathf.Infinity && y >= 0f && y <= h) pts.Add(new Vector2(0f, y));
            // x = w
            t = (w - a.x) / dir.x;
            y = a.y + t * dir.y;
            if (t != Mathf.Infinity && y >= 0f && y <= h) pts.Add(new Vector2(w, y));
        }
        // y = 0
        if (Mathf.Abs(dir.y) > 1e-6f)
        {
            float t = (0f - a.y) / dir.y;
            float x = a.x + t * dir.x;
            if (t != Mathf.Infinity && x >= 0f && x <= w) pts.Add(new Vector2(x, 0f));
            // y = h
            t = (h - a.y) / dir.y;
            x = a.x + t * dir.x;
            if (t != Mathf.Infinity && x >= 0f && x <= w) pts.Add(new Vector2(x, h));
        }

        // 取唯一两点
        for (int i = 0; i < pts.Count; i++)
        {
            for (int j = i + 1; j < pts.Count; j++)
            {
                // 略去极端重合，允许微小误差
                if ((pts[i] - pts[j]).sqrMagnitude < 1e-4f) continue;
            }
        }
        // 去重
        List<Vector2> uniq = new List<Vector2>();
        foreach (var p in pts)
        {
            bool dup = false;
            foreach (var q in uniq) if ((p - q).sqrMagnitude < 1e-4f) { dup = true; break; }
            if (!dup) uniq.Add(p);
        }
        if (uniq.Count < 2) return false;

        // 取最远的两点（理论上就是矩形相对两边）
        float best = -1f; Vector2 A = uniq[0], B = uniq[0];
        for (int i = 0; i < uniq.Count; i++)
            for (int j = i + 1; j < uniq.Count; j++)
            {
                float d = (uniq[i] - uniq[j]).sqrMagnitude;
                if (d > best) { best = d; A = uniq[i]; B = uniq[j]; }
            }

        i0 = A; i1 = B;
        return true;
    }

    private Vector3 ScreenToWorldOnPlayerPlane(Vector3 screen)
    {
        if (!cam) return screen;
        if (cam.orthographic)
        {
            var w = cam.ScreenToWorldPoint(screen);
            w.z = _playerPlaneZ;
            return w;
        }
        else
        {
            // 透视：把鼠标屏幕点投到玩家 z 平面
            Ray r = cam.ScreenPointToRay(screen);
            float t = (_playerPlaneZ - r.origin.z) / (Mathf.Abs(r.direction.z) < 1e-6f ? 1e-6f : r.direction.z);
            return r.origin + r.direction * t;
        }
    }

    // —— LineRenderer helpers
    private LineRenderer CreateRuntimeLine(string name)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        return lr;
    }

    private void SetupLine(LineRenderer lr)
    {
        if (!lr) return;
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.material = lr.material ? lr.material : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.SetColor("_BaseColor", Color.white);
        lr.sortingOrder = 9999; // 保证在最上层
    }
    private void ShowLine(LineRenderer lr) { if (lr) lr.enabled = true; }
    private void HideLine(LineRenderer lr) { if (lr) lr.enabled = false; }

    private void StartReplaceCo(ref Coroutine slot, IEnumerator co)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(co);
    }
}
