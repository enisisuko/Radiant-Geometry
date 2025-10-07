// Assets/Editor/GeometricMainMenuOneClickBuilder.cs
// 一键在当前 Scene 创建几何主菜单，并兼容旧版 GeometricMainMenu（无 firstScene/firstCheckpointId/TryStaticHasLastSave 也不报错）
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using System.Reflection;
using System;
using System.Linq;

public static class GeometricMainMenuOneClickBuilder
{
    const string RootName = "MainMenu";
    const string ScriptsFolder = "Assets/AutoMenu/Scripts";   // 运行时代码将放这里（仅当 WriteRuntimeScriptsIfMissing 为 true 时）
    const string MaterialsFolder = "Assets/AutoMenu/Materials";

    // 为避免重复脚本，默认关闭。若你没有那三份运行时代码且希望自动生成，将其改为 true
    const bool WriteRuntimeScriptsIfMissing = false;

    [MenuItem("Tools/Radiant Geometry/Build Geometric Main Menu")]
    public static void Build()
    {
        if (!EditorSceneManager.GetActiveScene().isLoaded)
        {
            EditorUtility.DisplayDialog("提示", "请先打开/新建一个场景。", "好的");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // 0) 仅在显式允许时补齐运行时代码，避免重复
        if (WriteRuntimeScriptsIfMissing)
            EnsureRuntimeScripts();

        // 1) 根节点
        var root = GameObject.Find(RootName);
        if (!root)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create MainMenu Root");
        }

        // 2) 相机Rig + MainCamera
        var camRig = GameObject.Find("CameraRig");
        if (!camRig)
        {
            camRig = new GameObject("CameraRig");
            Undo.RegisterCreatedObjectUndo(camRig, "Create CameraRig");
        }

        Camera cam = Camera.main;
        if (!cam)
        {
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            camGo.transform.SetParent(camRig.transform, false);
            camGo.transform.position = new Vector3(0, 1.5f, -8f);
            camGo.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(camGo, "Create MainCamera");
        }

        // 3) 后处理
        SetupPostProcessing();

        // 4) 材质
        Directory.CreateDirectory(MaterialsFolder);
        AssetDatabase.Refresh();

        var matCore = CreateEmissionMat("MAT_CoreCrystal", new Color(0.25f, 0.9f, 1.0f), 2.2f);
        var matNew = CreateEmissionMat("MAT_NewGame", new Color(1.0f, 0.6f, 0.2f), 2.0f);
        var matCont = CreateEmissionMat("MAT_Continue", new Color(0.4f, 1.0f, 0.7f), 1.8f);
        var matCoop = CreateEmissionMat("MAT_Coop", new Color(0.8f, 0.6f, 1.0f), 1.8f);
        var matDlc = CreateEmissionMat("MAT_DLC", new Color(1.0f, 0.35f, 0.6f), 1.9f);
        var matQuit = CreateEmissionMat("MAT_Quit", new Color(0.9f, 0.9f, 0.9f), 1.4f);

        // 5) 核心晶体（默认球体，可替换为十二面体 Mesh）
        var core = GameObject.Find("CoreCrystal");
        if (!core)
        {
            core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "CoreCrystal";
            core.transform.SetParent(root.transform, false);
            core.transform.position = Vector3.zero;
            core.transform.localScale = Vector3.one * 1.6f;
            var rend = core.GetComponent<Renderer>();
            rend.sharedMaterial = matCore;
            var col = core.GetComponent<Collider>();
            if (col) UnityEngine.Object.DestroyImmediate(col);
            Undo.RegisterCreatedObjectUndo(core, "Create CoreCrystal");
        }

        // 6) 五个选项
        var items = new[]
        {
            CreateOption(root.transform, "Option_NewGame",  matNew,  new Vector3(-3.0f,  1.0f, 0f)),
            CreateOption(root.transform, "Option_Continue", matCont, new Vector3(-1.2f, -0.3f, 0f)),
            CreateOption(root.transform, "Option_Coop",     matCoop, new Vector3( 1.2f, -0.3f, 0f)),
            CreateOption(root.transform, "Option_DLC",      matDlc,  new Vector3( 3.0f,  1.0f, 0f)),
            CreateOption(root.transform, "Option_Quit",     matQuit, new Vector3( 0.0f, -2.0f, 0f)),
        };

        // 7) UI + 白光过渡
        var canvasGo = GameObject.Find("UI");
        WhiteFlashTransition flash = null;
        if (!canvasGo)
        {
            canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

            var flashRoot = new GameObject("FullscreenFlash", typeof(CanvasGroup));
            flashRoot.transform.SetParent(canvasGo.transform, false);

            var imgGo = new GameObject("FlashImage", typeof(Image));
            imgGo.transform.SetParent(flashRoot.transform, false);
            var img = imgGo.GetComponent<Image>();
            img.color = Color.white;

            var rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            flash = flashRoot.AddComponent<WhiteFlashTransition>();
            flash.flashImage = img;
        }
        else
        {
            flash = UnityEngine.Object.FindFirstObjectByType<WhiteFlashTransition>();
            if (!flash)
            {
                var flashRoot = new GameObject("FullscreenFlash", typeof(CanvasGroup));
                flashRoot.transform.SetParent(canvasGo.transform, false);
                var imgGo = new GameObject("FlashImage", typeof(Image));
                imgGo.transform.SetParent(flashRoot.transform, false);
                var img = imgGo.AddComponent<Image>();
                img.color = Color.white;
                var rt = img.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                flash = flashRoot.AddComponent<WhiteFlashTransition>();
                flash.flashImage = img;
            }
        }

        // 8) EventSystem
        if (!UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 9) Main controller 绑定 + 兼容接线
        var main = root.GetComponent<GeometricMainMenu>();
        if (!main) main = root.AddComponent<GeometricMainMenu>();

        // 反射安全设置：mainCam / coreCrystal / items / flash
        TrySetObjectMember(main, "mainCam", cam);
        TrySetObjectMember(main, "coreCrystal", core.transform);
        var itemComps = items.Select(i => i.GetComponent<GeometricMenuItem>()).ToArray();
        TrySetArrayMember(main, "items", itemComps);
        TrySetObjectMember(main, "flash", flash);

        // 默认新游戏起点（兼容：旧版没有这些字段也不会报错）
        TrySetStringMember(main, "firstScene", "Chapter1");
        TrySetStringMember(main, "firstCheckpointId", "101");

        // 生成阶段先做一次“继续是否可点”的静态评估（没有该方法则用默认 true，运行时再判断）
        bool hasSaveGen = TryCallStaticBool(typeof(GeometricMainMenu), "TryStaticHasLastSave", true);
        if (itemComps.Length > 1 && itemComps[1]) itemComps[1].SetInteractable(hasSaveGen);

        // 收尾
        SceneView.lastActiveSceneView?.FrameSelected();
        Undo.CollapseUndoOperations(group);
        EditorUtility.DisplayDialog("完成", "几何主菜单已创建并接线（兼容旧版脚本）。\n如需修改默认起点，在 MainMenu 对象的 GeometricMainMenu 组件里设置。", "好的！");
    }

    // ---------- 工具函数（创建物体/材质/后处理） ----------
    static GameObject CreateOption(Transform parent, string name, Material mat, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * 0.8f;
        var rend = go.GetComponent<Renderer>();
        rend.sharedMaterial = mat;

        var item = go.AddComponent<GeometricMenuItem>();
        item.targetRenderer = rend;
        item.visualRoot = go.transform;
        item.baseEmission = 1.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = false;
        col.radius = 0.55f;

        return go;
    }

    static Material CreateEmissionMat(string fileName, Color hdrColor, float intensity)
    {
        string path = $"{MaterialsFolder}/{fileName}.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("HDRP/Lit");
        if (!shader) shader = Shader.Find("Standard");

        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            Directory.CreateDirectory(MaterialsFolder);
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }

        m.EnableKeyword("_EMISSION");
        var color = hdrColor * intensity;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.black);

        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static void SetupPostProcessing()
    {
        var volGo = GameObject.Find("Global Volume");
        if (!volGo)
        {
            volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.65f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 0.9f;
            bloom.scatter.overrideState = true;   bloom.scatter.value = 0.6f;

            var vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true; vig.intensity.value = 0.18f;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.8f;
#else
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = profile;
#endif
            Undo.RegisterCreatedObjectUndo(volGo, "Create Global Volume");
        }
    }

    // ---------- 兼容辅助（避免 CS1061） ----------
    static void TrySetStringMember(object target, string name, string value)
    {
        if (target == null) return;
        var t = target.GetType();

        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(string))
        {
            var cur = (string)f.GetValue(target);
            if (string.IsNullOrEmpty(cur)) f.SetValue(target, value);
            return;
        }

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(string) && p.CanWrite)
        {
            var cur = (string)p.GetValue(target, null);
            if (string.IsNullOrEmpty(cur)) p.SetValue(target, value, null);
        }
    }

    static void TrySetObjectMember(object target, string name, UnityEngine.Object obj)
    {
        if (target == null) return;
        var t = target.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType.IsAssignableFrom(obj?.GetType()))
        {
            if ((UnityEngine.Object)f.GetValue(target) == null) f.SetValue(target, obj);
            return;
        }
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType.IsAssignableFrom(obj?.GetType()) && p.CanWrite)
        {
            if ((UnityEngine.Object)p.GetValue(target, null) == null) p.SetValue(target, obj, null);
        }
    }

    static void TrySetArrayMember<T>(object target, string name, T[] arr) where T : UnityEngine.Object
    {
        if (target == null) return;
        var t = target.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType.IsArray)
        {
            var et = f.FieldType.GetElementType();
            if (et != null && et.IsAssignableFrom(typeof(T)))
            {
                f.SetValue(target, arr);
                return;
            }
        }
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType.IsArray && p.CanWrite)
        {
            var et = p.PropertyType.GetElementType();
            if (et != null && et.IsAssignableFrom(typeof(T)))
            {
                p.SetValue(target, arr, null);
            }
        }
    }

    static bool TryCallStaticBool(Type t, string methodName, bool fallback)
    {
        if (t == null) return fallback;
        var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (m != null && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0)
        {
            try { return (bool)m.Invoke(null, null); } catch { }
        }
        return fallback;
    }

    // ---------- 可选：自动写入运行时代码（默认关闭） ----------
    static void EnsureRuntimeScripts()
    {
        Directory.CreateDirectory(ScriptsFolder);
        TryWriteScript("GeometricMenuItem.cs", GeometricMenuItemCode);
        TryWriteScript("GeometricMainMenu.cs", GeometricMainMenuCode);
        TryWriteScript("WhiteFlashTransition.cs", WhiteFlashTransitionCode);
        AssetDatabase.Refresh();
    }

    static void TryWriteScript(string file, string code)
    {
        string path = Path.Combine(ScriptsFolder, file);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, code);
        }
    }

    // ====== 内置运行时代码（只有在 WriteRuntimeScriptsIfMissing=true 时才会落盘） ======
    const string GeometricMenuItemCode =
@"using UnityEngine;

[DisallowMultipleComponent]
public class GeometricMenuItem : MonoBehaviour
{
    [Header(""Visuals"")]
    public Renderer targetRenderer;
    public Transform visualRoot;
    public float baseEmission = 1.2f;
    public float hoverEmissionMul = 2.2f;
    public float confirmEmissionMul = 3.5f;

    [Header(""Breath (Idle)"")]
    public bool idleBreath = true;
    public float breathSpeed = 0.7f;
    public float breathScaleAmp = 0.035f;
    public float hoverScale = 1.06f;
    public float confirmScale = 1.15f;

    [Header(""Optional Availability"")]
    public bool interactable = true;

    MaterialPropertyBlock _mpb;
    static readonly int EmissionColorId = Shader.PropertyToID(""_EmissionColor"");
    Color _cachedEmissionColor = Color.white;
    float _hoverLerp;
    float _confirmPulse;
    Vector3 _initScale;

    void Reset()
    {
        targetRenderer = GetComponentInChildren<Renderer>();
        if (!visualRoot) visualRoot = transform;
    }

    void Awake()
    {
        if (!targetRenderer) targetRenderer = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _initScale = visualRoot ? visualRoot.localScale : Vector3.one;

        if (targetRenderer && targetRenderer.sharedMaterial && targetRenderer.sharedMaterial.HasProperty(EmissionColorId))
        {
            _cachedEmissionColor = targetRenderer.sharedMaterial.GetColor(EmissionColorId);
        }
    }

    public void SetHovered(bool hovered)
    {
        _hoverLerp = Mathf.MoveTowards(_hoverLerp, hovered ? 1f : 0f, Time.deltaTime * 6f);
    }

    public void TriggerConfirmPulse()
    {
        _confirmPulse = 1f;
    }

    public void SetInteractable(bool can)
    {
        interactable = can;
        if (!interactable) _hoverLerp = 0f;
    }

    void LateUpdate()
    {
        float emissionMul = Mathf.Lerp(1f, hoverEmissionMul, _hoverLerp);
        if (_confirmPulse > 0f)
        {
            float pulse = Mathf.SmoothStep(0f, 1f, _confirmPulse);
            emissionMul = Mathf.Lerp(emissionMul, confirmEmissionMul, pulse);
            _confirmPulse = Mathf.MoveTowards(_confirmPulse, 0f, Time.deltaTime * 2.5f);
        }

        Color finalEmission = _cachedEmissionColor * (baseEmission * emissionMul);
        if (!interactable) finalEmission *= 0.35f;

        if (targetRenderer)
        {
            _mpb.Clear();
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, finalEmission);
            targetRenderer.SetPropertyBlock(_mpb);
        }

        if (visualRoot)
        {
            float breath = idleBreath ? (1f + Mathf.Sin(Time.time * breathSpeed) * breathScaleAmp) : 1f;
            float scale = breath * Mathf.Lerp(1f, hoverScale, _hoverLerp);
            if (_confirmPulse > 0f) scale = Mathf.Lerp(scale, confirmScale, Mathf.SmoothStep(0, 1, _confirmPulse));
            visualRoot.localScale = _initScale * scale;
        }
    }
}
";

    const string GeometricMainMenuCode =
@"using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GeometricMainMenu : MonoBehaviour
{
    [Header(""References"")]
    public Camera mainCam;
    public Transform coreCrystal;
    public GeometricMenuItem[] items; // 顺序：New, Continue, Coop, DLC, Quit
    public WhiteFlashTransition flash;

    [Header(""Raycast"")]
    public LayerMask interactMask = ~0;
    public float rayMaxDistance = 100f;

    [Header(""Input"")]
    public string horizontalAxis = ""Horizontal"";
    public string verticalAxis   = ""Vertical"";
    public KeyCode confirmKey    = KeyCode.Return;
    public KeyCode confirmAltKey = KeyCode.Space;
    public KeyCode backKey       = KeyCode.Escape;

    [Header(""Actions"")]
    public UnityEvent onNewGame;
    public UnityEvent onContinue;
    public UnityEvent onCoop;
    public UnityEvent onDLC;
    public UnityEvent onQuit;

    [Header(""New Game Defaults"")]
    public string firstScene = ""Chapter1"";
    public string firstCheckpointId = ""101"";

    int _index = 0;
    float _moveCd = 0f;
    const float MoveRepeatDelay = 0.2f;

    void Reset(){ mainCam = Camera.main; }

    void Awake()
    {
        AutoWireIfEmpty();
        bool hasSave = ComputeHasLastSave();
        if (items != null && items.Length > 1 && items[1] != null)
            items[1].SetInteractable(hasSave);
    }

    void Start(){ if (!mainCam) mainCam = Camera.main; HighlightIndex(_index); }

    void Update()
    {
        HandleMouseHover();
        HandleDirectionalSelection();

        if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmAltKey) || Input.GetButtonDown(""Submit"")) ConfirmCurrent();
        if (Input.GetKeyDown(backKey)) { SetIndex(items.Length - 1); ConfirmCurrent(); }
    }

    void HandleMouseHover()
    {
        if (!mainCam) return;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, rayMaxDistance, interactMask))
        {
            var item = hit.collider.GetComponentInParent<GeometricMenuItem>();
            if (item)
            {
                int idx = Array.IndexOf(items, item);
                if (idx >= 0) SetIndex(idx);
                if (Input.GetMouseButtonDown(0)) ConfirmCurrent();
            }
        }
        for (int i = 0; i < items.Length; i++) if (items[i]) items[i].SetHovered(i == _index && items[i].interactable);
    }

    void HandleDirectionalSelection()
    {
        _moveCd -= Time.deltaTime;
        if (_moveCd > 0f) return;

        float h = Input.GetAxisRaw(horizontalAxis);
        float v = Input.GetAxisRaw(verticalAxis);

        int delta = 0;
        if (Mathf.Abs(h) > 0.5f) delta = (h > 0f ? 1 : -1);
        else if (Mathf.Abs(v) > 0.5f) delta = (v < 0f ? 1 : -1);

        if (delta != 0)
        {
            int tries = items.Length;
            int next = _index;
            while (tries-- > 0)
            {
                next = (next + delta + items.Length) % items.Length;
                if (items[next] && items[next].interactable) break;
            }
            SetIndex(next);
            _moveCd = MoveRepeatDelay;
        }

        for (int i = 0; i < items.Length; i++) if (items[i]) items[i].SetHovered(i == _index && items[i].interactable);
    }

    void SetIndex(int idx){ _index = Mathf.Clamp(idx, 0, items.Length - 1); }
    void HighlightIndex(int idx){ for (int i = 0; i < items.Length; i++) if (items[i]) items[i].SetHovered(i == idx && items[i].interactable); }

    void ConfirmCurrent()
    {
        if (_index < 0 || _index >= items.Length) return;
        var item = items[_index];
        if (!item || !item.interactable) return;
        item.TriggerConfirmPulse();
        switch (_index)
        {
            case 0: PlayConfirmAndInvoke(onNewGame);  break;
            case 1: PlayConfirmAndInvoke(onContinue); break;
            case 2: PlayConfirmAndInvoke(onCoop);     break;
            case 3: PlayConfirmAndInvoke(onDLC);      break;
            case 4: PlayConfirmAndInvoke(onQuit);     break;
        }
    }

    void PlayConfirmAndInvoke(UnityEvent e){ if (flash) flash.Blast(() => { e?.Invoke(); }); else e?.Invoke(); }

    public void StartNewGame(){ TryInvokeSaveSystemResetAll(); TryInvokeSceneLoaderLoadScene(firstScene, firstCheckpointId); }
    public void ContinueGame(){ TryInvokeSceneLoaderReloadAtLastCheckpoint(); }
    public void QuitGame(){ Application.Quit(); #if UNITY_EDITOR UnityEditor.EditorApplication.isPlaying = false; #endif }

    void AutoWireIfEmpty()
    {
        if (onNewGame == null) onNewGame = new UnityEvent();
        if (onContinue == null) onContinue = new UnityEvent();
        if (onQuit == null) onQuit = new UnityEvent();
        if (onNewGame.GetPersistentEventCount() == 0) onNewGame.AddListener(StartNewGame);
        if (onContinue.GetPersistentEventCount() == 0) onContinue.AddListener(ContinueGame);
        if (onQuit.GetPersistentEventCount() == 0) onQuit.AddListener(QuitGame);
    }

    public bool ComputeHasLastSave()
    {
        if (TryInvokeSaveSystemHasLastSave(out bool hasSave)) return hasSave;
        string path = Path.Combine(Application.persistentDataPath, ""faded_dreams_save.json"");
        if (File.Exists(path)) return true;
        if (PlayerPrefs.GetInt(""HasSave"", 0) == 1) return true;
        return false;
    }

    public static bool TryStaticHasLastSave(){ return true; }

    static Type FindTypeByName(string typeName)
    {
        var asms = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var a in asms){ var t = a.GetTypes().FirstOrDefault(x => x.Name == typeName); if (t != null) return t; }
        return null;
    }

    bool TryInvokeSaveSystemHasLastSave(out bool hasSave)
    {
        hasSave = false;
        var t = FindTypeByName(""SaveSystem"");
        if (t == null) return false;
        var instProp = t.GetProperty(""Instance"", BindingFlags.Public | BindingFlags.Static);
        var inst = instProp != null ? instProp.GetValue(null) : null;
        if (inst != null)
        {
            var m = t.GetMethod(""HasLastSave"", BindingFlags.Public | BindingFlags.Instance);
            if (m != null){ var r = m.Invoke(inst, null); if (r is bool b) { hasSave = b; return true; } }
            var dataField = t.GetField(""data"", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (dataField != null)
            {
                var data = dataField.GetValue(inst);
                if (data != null)
                {
                    var dt = data.GetType();
                    var f1 = dt.GetField(""lastScene"", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    var f2 = dt.GetField(""lastCheckpoint"", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    string s1 = f1 != null ? f1.GetValue(data) as string : null;
                    string s2 = f2 != null ? f2.GetValue(data) as string : null;
                    hasSave = !string.IsNullOrEmpty(s1) || !string.IsNullOrEmpty(s2);
                    return true;
                }
            }
        }
        return false;
    }

    void TryInvokeSaveSystemResetAll()
    {
        var t = FindTypeByName(""SaveSystem"");
        if (t == null) return;
        var instProp = t.GetProperty(""Instance"", BindingFlags.Public | BindingFlags.Static);
        var inst = instProp != null ? instProp.GetValue(null) : null;
        if (inst == null) return;
        var m = t.GetMethod(""ResetAll"", BindingFlags.Public | BindingFlags.Instance);
        if (m != null) m.Invoke(inst, null);
    }

    void TryInvokeSceneLoaderLoadScene(string scene, string checkpoint)
    {
        var t = FindTypeByName(""SceneLoader"");
        if (t == null) { SceneManager.LoadScene(scene); return; }
        var m = t.GetMethod(""LoadScene"", BindingFlags.Public | BindingFlags.Static);
        if (m != null)
        {
            var ps = m.GetParameters();
            if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(string))
            { m.Invoke(null, new object[]{ scene, checkpoint }); return; }
        }
        SceneManager.LoadScene(scene);
    }

    void TryInvokeSceneLoaderReloadAtLastCheckpoint()
    {
        var t = FindTypeByName(""SceneLoader"");
        if (t == null) return;
        var m = t.GetMethod(""ReloadAtLastCheckpoint"", BindingFlags.Public | BindingFlags.Static);
        if (m != null) m.Invoke(null, null);
    }
}
";

    const string WhiteFlashTransitionCode =
@"using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class WhiteFlashTransition : MonoBehaviour
{
    public Image flashImage;
    public float riseTime = 0.15f;
    public float holdTime = 0.05f;
    public float fallTime = 0.35f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0,0, 1,1);

    CanvasGroup _cg;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        _cg.alpha = 0f;
        if (!flashImage) flashImage = GetComponentInChildren<Image>(true);
    }

    public void Blast(Action onPeak = null)
    {
        StopAllCoroutines();
        StartCoroutine(Co_Blast(onPeak));
    }

    IEnumerator Co_Blast(Action onPeak)
    {
        float t = 0;
        while (t < riseTime)
        {
            t += Time.unscaledDeltaTime;
            float a = curve.Evaluate(Mathf.Clamp01(t / riseTime));
            _cg.alpha = a;
            yield return null;
        }
        _cg.alpha = 1f;

        if (holdTime > 0f) yield return new WaitForSecondsRealtime(holdTime);

        onPeak?.Invoke();

        t = 0;
        while (t < fallTime)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - curve.Evaluate(Mathf.Clamp01(t / fallTime));
            _cg.alpha = a;
            yield return null;
        }
        _cg.alpha = 0f;
    }
}
";
}
#endif
