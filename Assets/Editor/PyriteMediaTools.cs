using System;
using System.Linq;
using System.Reflection;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ProTV 배치 — 타프 밑 스크린
//  · Simple (ProTV) 프리팹을 씬 최상위에 깔고, Room 밑의 화면/스피커/조작UI 위치만 잡는다
//  · 조작 UI 는 Retro 로 갈아 끼운다. TV 루트의 자손으로 두어야 플러그인이 TV 를 찾는다
//  · 다시 돌리면 MediaPlayer 루트를 통째로 지우고 새로 만든다
public static class PyriteMediaTools
{
    const string TV_PREFAB     = "Packages/dev.architech.protv/Simple (ProTV).prefab";
    // 조작 UI 테마 — 한 줄만 바꾸면 갈아끼워진다
    const string UI_PREFAB =
        "Packages/dev.architech.protv.extras/Samples/Prefabs/Retro/MediaControls Standard (Retro).prefab";
    //  Monochrome: "Packages/dev.architech.protv/Samples/Prefabs/Plugins/MediaControls V2 (Monochrome).prefab"
    //  Neon:       "Packages/dev.architech.protv.extras/Samples/Prefabs/Neon/MediaControls V2 (Neon).prefab"
    const string TARP          = "camp02_hexa_tarp";

    static readonly Vector2 SEAT = new Vector2(-10.5f, 51.5f);   // 모닥불 = 관객이 앉는 곳

    const float SCREEN_W   = 3.20f;
    const float SCREEN_H   = 1.80f;
    const float SCREEN_BOT = 0.80f;    // 지면에서 화면 아랫변까지
    const float UI_SCALE   = 0.50f;    // 조작 캔버스 축소 배율
    const float MID_T      = 0.50f;    // 거울(0) ↔ 타프(1) 사이에서 화면이 설 지점

    [MenuItem("Tools/Pyrite/N3. Setup Media Player", false, 272)]
    public static void Setup()
    {
        var old = FindRoot("MediaPlayer");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(TV_PREFAB);
        if (src == null) { Debug.LogError("[TV] 프리팹 없음: " + TV_PREFAB); return; }

        // ── 화면 자리 — 거울과 타프 사이 ──
        // 두 소품의 중간에 놓고, 모닥불(관객) 쪽을 보게 돌린다.
        var mirror = FindRoot("Mirror");
        if (mirror == null) { Debug.LogError("[TV] Mirror 루트가 없다. 먼저 L. Setup Mirror 를 돌릴 것"); return; }
        var tarp = FindByName(TARP) ?? FindContaining("tarp");
        if (tarp == null)
        {
            var camp = FindRoot("Camp");
            string kids = camp == null ? "(Camp 루트 없음)"
                : string.Join(", ", camp.GetComponentsInChildren<Transform>(true).Select(t => t.name).Take(40));
            Debug.LogError("[TV] 타프를 못 찾음. Camp 아래 이름: " + kids);
            return;
        }

        Vector2 mXZ = new Vector2(mirror.transform.position.x, mirror.transform.position.z);
        Vector2 tXZ = new Vector2(tarp.transform.position.x,   tarp.transform.position.z);
        Vector2 sp  = Vector2.Lerp(mXZ, tXZ, MID_T);

        Vector2 dir2 = (SEAT - sp);
        if (dir2.sqrMagnitude < 1e-4f) dir2 = Vector2.down;
        dir2.Normalize();

        var terrain = Terrain.activeTerrain;
        float gy = terrain != null
            ? terrain.SampleHeight(new Vector3(sp.x, 0f, sp.y)) + terrain.transform.position.y
            : mirror.transform.position.y;

        Vector3 facing = new Vector3(dir2.x, 0f, dir2.y);          // 화면이 바라볼 방향(관객 쪽)

        // ── 프리팹 인스턴스 ──
        var root = (GameObject)PrefabUtility.InstantiatePrefab(src);
        root.name = "MediaPlayer";
        root.transform.position = new Vector3(sp.x, gy, sp.y);
        root.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var room = root.transform.Find("Room");
        if (room == null) { Debug.LogError("[TV] Room 없음"); return; }
        room.localPosition = Vector3.zero;
        room.localRotation = Quaternion.identity;

        // ── 화면 ──
        var screen = room.Find("Main Screen");
        if (screen != null)
        {
            var mf = screen.GetComponent<MeshFilter>();
            Vector3 mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.one;
            float sx = mb.x > 1e-4f ? SCREEN_W / mb.x : SCREEN_W;
            float sy = mb.y > 1e-4f ? SCREEN_H / mb.y : SCREEN_H;
            screen.localPosition = new Vector3(0f, SCREEN_BOT + SCREEN_H * 0.5f, 0f);
            screen.localRotation = Quaternion.identity;
            screen.localScale = new Vector3(sx, sy, 1f);

            var mr = screen.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
            GameObjectUtility.SetStaticEditorFlags(screen.gameObject, 0);

            AddFrame(room, screen, gy);
        }

        // ── 스피커 ──
        var spk = room.Find("Speakers");
        int spkN = 0;
        if (spk != null)
        {
            spk.localPosition = new Vector3(0f, SCREEN_BOT + SCREEN_H * 0.5f, -0.06f);
            foreach (var a in spk.GetComponentsInChildren<AudioSource>(true))
            {
                a.spatialBlend = 1.0f;
                a.dopplerLevel = 0f;
                a.rolloffMode  = AudioRolloffMode.Linear;
                a.minDistance  = 2.5f;
                a.maxDistance  = 26.0f;
                AddSpatial(a.gameObject);
                spkN++;
            }
        }

        // ── 조작 UI: 기본(Monochrome) 제거하고 Retro 로 ──
        string ui = "없음";
        foreach (var t in room.GetComponentsInChildren<Transform>(true).ToArray())
            if (t != null && t != room && t.name.StartsWith("MediaControls"))
                UnityEngine.Object.DestroyImmediate(t.gameObject);

        var retro = AssetDatabase.LoadAssetAtPath<GameObject>(UI_PREFAB);
        if (retro == null) Debug.LogWarning("[TV] Retro 조작 UI 프리팹 없음 — 기본 UI 없이 진행");
        else
        {
            var mc = (GameObject)PrefabUtility.InstantiatePrefab(retro, room);
            PrefabUtility.UnpackPrefabInstance(mc, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            mc.name = "MediaControls (Retro)";
            // 화면 오른쪽 아래, 관객 쪽을 보게
            // 프리팹 캔버스가 1.4m 급으로 크다. 0.5배로 줄여 캠프 소품 크기에 맞춘다.
            // 로컬 +X 는 관객이 볼 때 왼쪽(거울 쪽)이다. 거울을 가리지 않게 -X(타프 쪽)에 둔다.
            mc.transform.localPosition = new Vector3(-(SCREEN_W * 0.5f + 0.90f), 1.05f, 0.35f);
            mc.transform.localRotation = Quaternion.Euler(0f, 22f, 0f);
            mc.transform.localScale    = mc.transform.localScale * UI_SCALE;
            GameObjectUtility.SetStaticEditorFlags(mc, 0);
            BindTV(mc, root);
            AddUiPost(room, mc.transform.localPosition, gy);
            ui = mc.name;
        }

        EditorSceneManager_MarkDirty();
        Debug.Log(string.Format(
            "[TV] 완료\n  거울({0:F1},{1:F1}) ↔ 타프({2:F1},{3:F1}) 사이 t={4:F2}\n  화면 ({5:F2}, {6:F2}, {7:F2})  {8}×{9}m  바라보는 방향 ({10:F2},{11:F2})\n  스피커 {12}개 / 조작 UI {13}",
            mXZ.x, mXZ.y, tXZ.x, tXZ.y, MID_T,
            sp.x, gy + SCREEN_BOT + SCREEN_H * 0.5f, sp.y, SCREEN_W, SCREEN_H, facing.x, facing.z,
            spkN, ui));
    }

    // ── 화면 틀 + 기둥 (스크린이 허공에 뜨지 않게) ─────────────
    static void AddFrame(Transform room, Transform screen, float gy)
    {
        var mat = SolidMat("M_ScreenRig", new Color(0.21f, 0.15f, 0.10f), 0.0f, 0.25f);
        var rig = new GameObject("ScreenRig");
        rig.transform.SetParent(room, false);
        float hw = SCREEN_W * 0.5f + 0.08f;
        float top = SCREEN_BOT + SCREEN_H + 0.10f;
        Prim(PrimitiveType.Cylinder, rig.transform, "Pole_L", new Vector3(-hw, top * 0.5f, -0.05f), new Vector3(0.07f, top * 0.5f, 0.07f), mat);
        Prim(PrimitiveType.Cylinder, rig.transform, "Pole_R", new Vector3( hw, top * 0.5f, -0.05f), new Vector3(0.07f, top * 0.5f, 0.07f), mat);
        Prim(PrimitiveType.Cube,     rig.transform, "Bar_Top", new Vector3(0f, top, -0.05f), new Vector3(hw * 2f + 0.07f, 0.07f, 0.07f), mat);
        // 스크린 천 — 화면 뒤(-Z)에 둔다.
        // 🔴 +Z 로 두면 관객 쪽이라 영상이 통째로 가려진다(실제로 그랬다).
        Prim(PrimitiveType.Cube, rig.transform, "Backing",
             new Vector3(0f, SCREEN_BOT + SCREEN_H * 0.5f, -0.045f),
             new Vector3(SCREEN_W + 0.06f, SCREEN_H + 0.06f, 0.02f),
             SolidMat("M_ScreenCloth", new Color(0.80f, 0.78f, 0.72f), 0f, 0.05f));
    }

    static void AddUiPost(Transform room, Vector3 uiLocal, float gy)
    {
        var mat = SolidMat("M_ScreenRig", new Color(0.21f, 0.15f, 0.10f), 0.0f, 0.25f);
        var post = new GameObject("ControlPost");
        post.transform.SetParent(room, false);
        post.transform.localPosition = new Vector3(uiLocal.x, 0f, uiLocal.z);
        Prim(PrimitiveType.Cylinder, post.transform, "Post", new Vector3(0f, 0.55f, 0f), new Vector3(0.06f, 0.55f, 0.06f), mat);
        Prim(PrimitiveType.Cylinder, post.transform, "Foot", new Vector3(0f, 0.02f, 0f), new Vector3(0.26f, 0.02f, 0.26f), mat);
    }

    // ── ProTV 플러그인에 TV 물리기 (타입 참조 없이 리플렉션으로) ──
    static void BindTV(GameObject uiGo, GameObject tvRoot)
    {
        var tvComp = tvRoot.GetComponents<Component>()
            .FirstOrDefault(c => c != null && c.GetType().Name == "TVManager");
        if (tvComp == null) { Debug.LogWarning("[TV] TVManager 컴포넌트를 못 찾음 — tv 배선 생략"); return; }

        foreach (var c in uiGo.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            var f = c.GetType().GetField("tv", BindingFlags.Public | BindingFlags.Instance);
            if (f == null || !f.FieldType.IsInstanceOfType(tvComp)) continue;
            f.SetValue(c, tvComp);
            var usb = c as UdonSharp.UdonSharpBehaviour;
            if (usb != null) UdonSharpEditorUtility.CopyProxyToUdon(usb);
            EditorUtility.SetDirty(c);
            Debug.Log("[TV] tv 배선: " + c.GetType().Name + " @ " + c.gameObject.name);
        }
    }

    // ── 도우미 ────────────────────────────────────────────────
    static GameObject FindRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) return r;
        return null;
    }

    static GameObject FindByName(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name == name) return r;
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        return null;
    }

    static GameObject FindContaining(string frag)
    {
        frag = frag.ToLowerInvariant();
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains(frag)) return t.gameObject;
        return null;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static Material SolidMat(string name, Color c, float metallic, float smooth)
    {
        var path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go;
    }

    static void AddSpatial(GameObject go)
    {
        Type t = FindType("VRC.SDK3.Components.VRCSpatialAudioSource")
              ?? FindType("VRC.SDKBase.VRC_SpatialAudioSource");
        if (t == null) return;
        var c = go.GetComponent(t) ?? go.AddComponent(t);
        SetMember(c, "EnableSpatialization", false);
        SetMember(c, "Gain", 0f);
    }

    static Type FindType(string full)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        { var t = asm.GetType(full); if (t != null) return t; }
        return null;
    }

    static void SetMember(object o, string name, object v)
    {
        var t = o.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null && f.FieldType.IsInstanceOfType(v)) { f.SetValue(o, v); return; }
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(v)) p.SetValue(o, v, null);
    }

    static void EditorSceneManager_MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
