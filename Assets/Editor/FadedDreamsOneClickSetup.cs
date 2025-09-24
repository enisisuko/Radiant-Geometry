// FadedDreamsOneClickSetup.cs
// 一键生成“褪色的梦”所需目录、占位资源、预制体与基础场景（Unity 2021.3+）
// 菜单：Tools ▸ FadedDreams ▸ One-Click Setup
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

public static class FadedDreamsOneClickSetup
{
    private const string Root = "Assets/FadedDreams";
    private static string Scenes => $"{Root}/Scenes";
    private static string Prefabs => $"{Root}/Prefabs";
    private static string Art => $"{Root}/Art";
    private static string UIPath => $"{Root}/UI";

    [MenuItem("Tools/FadedDreams/One-Click Setup", priority = 1)]
    public static void Run()
    {
        AssetDatabase.Refresh();
        EnsureTags(); // 先确保 Player/Enemy tag 存在
        AssetDatabase.StartAssetEditing();
        try
        {
            CreateFolders();
            var sprites = CreatePlaceholderSprites();

            // Prefabs（含自动挂脚本）
            var projectile = CreateProjectilePrefab(sprites.white);
            var hud = CreateHUDPrefab();
            var lightSrc = CreateLightSourcePrefab(sprites.white);
            var enemy = CreateEnemyPrefab(sprites.black);
            var checkpoint = CreateCheckpointPrefab(sprites.green);
            var player = CreatePlayerPrefab(sprites.white, projectile);

            // Scenes（主菜单 + 4 章节）
            CreateMainMenuScene();
            CreateChapterScene("Chapter1", player, hud, lightSrc, enemy, checkpoint, false);
            CreateChapterScene("Chapter2", player, hud, lightSrc, enemy, checkpoint, false);
            CreateChapterScene("Chapter3", player, hud, lightSrc, enemy, checkpoint, false);
            CreateChapterScene("Chapter4", player, hud, lightSrc, enemy, checkpoint, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("FadedDreams", "初始化完成！Scenes 里已生成 MainMenu 和 Chapter1~4。\n打开 Chapter1 试跑吧。", "OK");
        }
        finally { AssetDatabase.StopAssetEditing(); }
    }

    [MenuItem("Tools/FadedDreams/Validate Prefabs", priority = 2)]
    private static void ValidatePrefabs()
    {
        string[] prefabPaths = {
            $"{Prefabs}/Player.prefab",
            $"{Prefabs}/Enemy_DarkSprite.prefab",
            $"{Prefabs}/LightSource.prefab",
            $"{Prefabs}/Checkpoint.prefab",
            $"{Prefabs}/HUD.prefab",
            $"{Prefabs}/Projectile.prefab",
        };
        string[][] required = {
            new []{"FadedDreams.Player.PlayerController2D","FadedDreams.Player.PlayerLightController","FadedDreams.Player.PlayerHealthLight"},
            new []{"FadedDreams.Enemies.DarkSpriteAI"},
            new []{"FadedDreams.World.LightSource2D"},
            new []{"FadedDreams.Core.Checkpoint"},
            new []{"FadedDreams.UI.HUDController"},
            new []{"FadedDreams.Enemies.Projectile"},
        };
        for (int i = 0; i < prefabPaths.Length; i++)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            if (go == null) { Debug.LogWarning($"[Validate] Missing: {prefabPaths[i]}"); continue; }
            foreach (var tn in required[i])
            {
                var t = FindTypeByAnyName(tn);
                if (t == null) { Debug.LogWarning($"[Validate] Type not found: {tn}"); continue; }
                bool ok = go.GetComponent(t) != null;
                Debug.Log($"[Validate] {go.name} {(ok ? "✓ has" : "✗ missing")} {tn}");
            }
        }
        Debug.Log("[Validate] Done.");
    }

    // -------------------- Folders --------------------
    private static void CreateFolders()
    {
        void Ensure(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path).Replace("\\", "/");
                var name = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
        Ensure("Assets/FadedDreams");
        Ensure(Scenes);
        Ensure(Prefabs);
        Ensure(Art);
        Ensure(UIPath);
    }

    // -------------------- Sprites --------------------
    private struct SpriteSet { public Sprite black, white, red, green, blue; }

    private static SpriteSet CreatePlaceholderSprites()
    {
        Sprite Make(string name, Color c)
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var cols = new Color[32 * 32];
            for (int i = 0; i < cols.Length; i++) cols[i] = c;
            tex.SetPixels(cols);
            tex.Apply();

            var bytes = tex.EncodeToPNG();
            var path = $"{Art}/{name}.png";
            File.WriteAllBytes(path, bytes);

            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.Refresh();
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                AssetDatabase.Refresh();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sprite;
        }

        return new SpriteSet
        {
            black = Make("black", Color.black),
            white = Make("white", Color.white),
            red = Make("red", Color.red),
            green = Make("green", Color.green),
            blue = Make("blue", Color.blue),
        };
    }

    // -------------------- Prefabs --------------------
    private static GameObject CreateProjectilePrefab(Sprite sprite)
    {
        var go = new GameObject("Projectile");
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite;
        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true;
        var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        AttachOrWarn(go, "FadedDreams.Enemies.Projectile", "Projectile");

        return SaveAsPrefabAndDestroy(go, "Projectile");
    }

    private static GameObject CreateLightSourcePrefab(Sprite sprite)
    {
        var go = new GameObject("LightSource");
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = new Color(1, 1, 1, 0.6f);
        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true;

        AttachOrWarn(go, "FadedDreams.World.LightSource2D", "LightSource2D");

        return SaveAsPrefabAndDestroy(go, "LightSource");
    }

    private static GameObject CreateEnemyPrefab(Sprite sprite)
    {
        var go = new GameObject("DarkSprite");
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = new Color(0, 0, 0, 1);
        var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0.2f;
        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = false;
        try { go.tag = "Enemy"; } catch { }

        AttachOrWarn(go, "FadedDreams.Enemies.DarkSpriteAI", "DarkSpriteAI");

        return SaveAsPrefabAndDestroy(go, "Enemy_DarkSprite");
    }

    private static GameObject CreateCheckpointPrefab(Sprite sprite)
    {
        var go = new GameObject("Checkpoint");
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = new Color(0.5f, 1, 0.5f, 0.9f);
        var col = go.AddComponent<BoxCollider2D>(); col.isTrigger = true;

        AttachOrWarn(go, "FadedDreams.Core.Checkpoint", "Checkpoint");

        return SaveAsPrefabAndDestroy(go, "Checkpoint");
    }

    private static GameObject CreateHUDPrefab()
    {
        var canvasGO = new GameObject("HUD_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Energy bar
        var sliderGO = new GameObject("EnergyBar");
        sliderGO.transform.SetParent(canvasGO.transform, false);
        var sliderRect = sliderGO.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0.92f);
        sliderRect.anchorMax = new Vector2(0.45f, 0.98f);
        sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<Slider>();
        var bg = new GameObject("Background");
        bg.transform.SetParent(sliderGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0.05f, 0.2f); faRect.anchorMax = new Vector2(0.95f, 0.8f); faRect.offsetMin = faRect.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>(); fillImg.color = new Color(1, 1, 1, 0.85f);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0); fillRect.anchorMax = new Vector2(1, 1); fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        slider.fillRect = fillRect;
        slider.targetGraphic = fillImg;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.5f;

        // Mode label
        var labelGO = new GameObject("ModeLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "None";
        label.alignment = TextAnchor.MiddleLeft;
        var lrect = labelGO.GetComponent<RectTransform>();
        lrect.anchorMin = new Vector2(0.47f, 0.92f);
        lrect.anchorMax = new Vector2(0.8f, 0.98f);
        lrect.offsetMin = lrect.offsetMax = Vector2.zero;

        // HUDController（若存在）自动连线
        AttachOrWarn(canvasGO, "FadedDreams.UI.HUDController", "HUDController");
        var hudType = FindTypeByAnyName("FadedDreams.UI.HUDController");
        var hud = (hudType != null) ? canvasGO.GetComponent(hudType) : null;
        if (hud != null)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("energyBar").objectReferenceValue = slider;
            so.FindProperty("modeLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return SaveAsPrefabAndDestroy(canvasGO, "HUD");
    }

    private static GameObject CreatePlayerPrefab(Sprite sprite, GameObject projectilePrefab)
    {
        var go = new GameObject("Player");
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = 10;
        var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 2.5f; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var col = go.AddComponent<CapsuleCollider2D>();

        try { go.tag = "Player"; } catch { }

        // ground check
        var ground = new GameObject("GroundCheck");
        ground.transform.SetParent(go.transform, false);
        ground.transform.localPosition = new Vector3(0, -0.6f, 0);

        // fire point
        var fire = new GameObject("FirePoint");
        fire.transform.SetParent(go.transform, false);
        fire.transform.localPosition = new Vector3(0.6f, 0.0f, 0);

        // 挂脚本
        AttachOrWarn(go, "FadedDreams.Player.PlayerController2D", "PlayerController2D");
        AttachOrWarn(go, "FadedDreams.Player.PlayerLightController", "PlayerLightController");
        AttachOrWarn(go, "FadedDreams.Player.PlayerHealthLight", "PlayerHealthLight");

        // 自动填字段
        var plcType = FindTypeByAnyName("FadedDreams.Player.PlayerLightController");
        var plc = (plcType != null) ? go.GetComponent(plcType) : null;
        if (plc != null)
        {
            var so = new SerializedObject(plc);
            so.FindProperty("firePoint").objectReferenceValue = fire.transform;
            so.FindProperty("laserProjectilePrefab").objectReferenceValue = projectilePrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var pcType = FindTypeByAnyName("FadedDreams.Player.PlayerController2D");
        var pc = (pcType != null) ? go.GetComponent(pcType) : null;
        if (pc != null)
        {
            var so = new SerializedObject(pc);
            so.FindProperty("groundCheck").objectReferenceValue = ground.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return SaveAsPrefabAndDestroy(go, "Player");
    }

    // -------------------- Scenes --------------------
    private static GameObject SafeInstantiate(GameObject prefab, string hintName)
    {
        if (prefab == null)
        {
            Debug.LogError($"Prefab '{hintName}' is null. Check creation earlier.");
            return null;
        }
        var obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (obj == null)
        {
            Debug.LogError($"Failed to instantiate prefab '{hintName}'.");
        }
        return obj;
    }

    private static void CreateMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera");
        cam.AddComponent<Camera>();
        cam.tag = "MainCamera";

        var canvasGO = new GameObject("MainMenu_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Title
        var title = CreateUIText(canvasGO.transform, "褪色的梦 | Faded Dreams", new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.9f), 36, TextAnchor.MiddleCenter);

        // Buttons
        var b1 = CreateUIButton(canvasGO.transform, "开始游戏", new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.7f));
        var b2 = CreateUIButton(canvasGO.transform, "继续", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.6f));
        var b3 = CreateUIButton(canvasGO.transform, "退出", new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f));
        var b4 = CreateUIButton(canvasGO.transform, "支持我", new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.4f));

        // 绑定主菜单脚本（如存在）
        AttachOrWarn(canvasGO, "FadedDreams.UI.MainMenu", "MainMenu");
        var menuType = FindTypeByAnyName("FadedDreams.UI.MainMenu");
        var menu = (menuType != null) ? canvasGO.GetComponent(menuType) : null;
        if (menu != null)
        {
            WireButton(b1, menu, "NewGame");
            WireButton(b2, menu, "ContinueGame");
            WireButton(b3, menu, "Quit");
            WireButton(b4, menu, "SupportMe");
        }
        else
        {
            // 兜底行为
            b1.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("Chapter1"));
            b2.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("Chapter1"));
            b3.onClick.AddListener(() => Application.Quit());
            b4.onClick.AddListener(() => Application.OpenURL("https://example.com/support"));
        }

        var path = $"{Scenes}/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void CreateChapterScene(string sceneName, GameObject playerPrefab, GameObject hudPrefab, GameObject lightPrefab, GameObject enemyPrefab, GameObject checkpointPrefab, bool spawnWithWhite)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 简易地面（2D 碰撞）
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -2, 0);
        ground.transform.localScale = new Vector3(20, 1, 1);
        Object.DestroyImmediate(ground.GetComponent<MeshRenderer>());
        var grb = ground.GetComponent<BoxCollider>();
        Object.DestroyImmediate(grb); // 移除 3D 碰撞
        ground.AddComponent<BoxCollider2D>();
        ground.layer = 0;

        // Player
        var player = SafeInstantiate(playerPrefab, "Player");
        if (player != null) player.transform.position = new Vector3(-7, -1, 0);

        // HUD
        var hud = SafeInstantiate(hudPrefab, "HUD");

        // HUD->Player 自动连线
        var hudCtrlType = FindTypeByAnyName("FadedDreams.UI.HUDController");
        var hudCtrl = (hudCtrlType != null && hud != null) ? hud.GetComponent(hudCtrlType) : null;
        if (hudCtrl != null && player != null)
        {
            var plcType = FindTypeByAnyName("FadedDreams.Player.PlayerLightController");
            var plcComp = (plcType != null) ? player.GetComponent(plcType) : null;
            var so = new SerializedObject(hudCtrl);
            so.FindProperty("player").objectReferenceValue = plcComp as UnityEngine.Object;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Light sources
        for (int i = 0; i < 3; i++)
        {
            var l = SafeInstantiate(lightPrefab, "LightSource");
            if (l != null) l.transform.position = new Vector3(-4 + i * 4, -0.5f + (i == 1 ? 1f : 0f), 0);
        }

        // Enemies
        for (int i = 0; i < 2; i++)
        {
            var e = SafeInstantiate(enemyPrefab, "Enemy");
            if (e != null) e.transform.position = new Vector3(2 + i * 3, -1, 0);
        }

        // Checkpoints
        for (int i = 0; i < 3; i++)
        {
            var cp = SafeInstantiate(checkpointPrefab, "Checkpoint");
            if (cp != null)
            {
                cp.name = $"CP_{i + 1}";
                cp.transform.position = new Vector3(-6 + i * 6, -1, 0);
                // Id 即名称，Checkpoint 脚本内部用 name 暴露（只读）
            }
        }

        // 章节标识（仅用于场景名识别）
        var marker = new GameObject("ChapterMarker_" + sceneName);

        // Chapter4：出生即拥有 RGB（白光由运行时逻辑判定）
        var plcType2 = FindTypeByAnyName("FadedDreams.Player.PlayerLightController");
        var plc = (plcType2 != null && player != null) ? player.GetComponent(plcType2) : null;
        if (plc != null && spawnWithWhite)
        {
            var so = new SerializedObject(plc);
            so.FindProperty("hasRed").boolValue = true;
            so.FindProperty("hasGreen").boolValue = true;
            so.FindProperty("hasBlue").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var path = $"{Scenes}/{sceneName}.unity";
        EditorSceneManager.SaveScene(scene, path);
    }

    // -------------------- Tag Utilities --------------------
    private static void EnsureTags()
    {
        AddTagIfMissing("Player");
        AddTagIfMissing("Enemy");
    }
    private static void AddTagIfMissing(string tag)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) { Debug.LogError("TagManager.asset not found"); return; }

        var so = new SerializedObject(assets[0]);
        var tagsProp = so.FindProperty("tags");
        bool exists = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            var t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue == tag) { exists = true; break; }
        }
        if (!exists)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }

    // -------------------- UI Helpers --------------------
    private static Text CreateUIText(Transform parent, string text, Vector2 aMin, Vector2 aMax, int size = 24, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return t;
    }

    private static Button CreateUIButton(Transform parent, string label, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0.15f);
        var btn = go.AddComponent<Button>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;

        var txt = CreateUIText(go.transform, label, new Vector2(0, 0), new Vector2(1, 1), 24, TextAnchor.MiddleCenter);
        return btn;
    }

    private static void WireButton(Button b, Object target, string method)
    {
        var so = new SerializedObject(b);
        var onClick = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        int idx = onClick.arraySize;
        onClick.arraySize++;
        var call = onClick.GetArrayElementAtIndex(idx);
        call.FindPropertyRelative("m_Target").objectReferenceValue = target;
        call.FindPropertyRelative("m_MethodName").stringValue = method;
        call.FindPropertyRelative("m_Mode").intValue = 1; // PersistentListenerMode.Void
        call.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue = typeof(Object).AssemblyQualifiedName;
        call.FindPropertyRelative("m_CallState").intValue = 2; // RuntimeOnly
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // -------------------- Type search & attach (robust) --------------------
    private static System.Type FindTypeByAnyName(string fullOrShortName)
    {
        if (string.IsNullOrEmpty(fullOrShortName)) return null;

        // 1) 直接 Type.GetType
        var t = System.Type.GetType(fullOrShortName);
        if (t != null) return t;

        // 2) 所有已加载程序集按 FullName 查
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try { t = asm.GetType(fullOrShortName); } catch { }
            if (t != null) return t;
        }

        // 3) 退而求其次：按短名扫描
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type hit = null;
            try
            {
                foreach (var tp in asm.GetTypes())
                {
                    if (tp.Name == fullOrShortName || tp.FullName == fullOrShortName) { hit = tp; break; }
                }
            }
            catch { }
            if (hit != null) return hit;
        }
        return null;
    }

    private static void AttachOrWarn(GameObject go, params string[] candidateTypeNames)
    {
        foreach (var name in candidateTypeNames)
        {
            var t = FindTypeByAnyName(name);
            if (t == null) continue;
            if (go.GetComponent(t) == null)
            {
                go.AddComponent(t);
                Debug.Log($"[FadedDreams] Attached {t.FullName} -> {go.name}");
            }
            return; // 成功或已存在即返回
        }
        Debug.LogWarning($"[FadedDreams] Script not found for {go.name}: {string.Join(", ", candidateTypeNames)}。请确认 Scripts 已导入并编译完成。");
    }

    private static GameObject SaveAsPrefabAndDestroy(GameObject go, string prefabName)
    {
        var path = $"{Prefabs}/{prefabName}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
#endif
