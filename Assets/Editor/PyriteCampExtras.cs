using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

// 캠프 추가물
//  Q  : 간이침대 · 타프 랜턴 · 부두 끝 기둥 랜턴 · 테이블 이동 · 회전 의자 · 시간대 패널 재부모
//  N7 : 미디어 조작 UI 를 제자리에서 교체 (MediaPlayer 위치는 건드리지 않는다)
public static class PyriteCampExtras
{
    const string FBX    = "Assets/Noagami/camp/Fbx/";
    const string MAT    = "Assets/Materials/";

    // 테이블 새 자리 — 화로와 스크린 사이, 한 발 물러난 곳
    static readonly Vector2 TABLE_XZ = new Vector2(-12.90f, 55.30f);
    static readonly Vector2 FIRE     = new Vector2(-10.50f, 51.50f);
    const float CHAIR_FROM_TABLE = 1.00f;

    // 타프 중심 기준 오프셋 (월드)
    const float COT_DX = 1.00f,  COT_DZ =  0.40f;
    const float LAN_DX = -1.35f, LAN_DZ =  0.25f;

    // ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Pyrite/Q. Setup Camp Extras", false, 281)]
    public static void Setup()
    {
        var camp = FindRoot("Camp");
        if (camp == null) { Debug.LogError("[EXTRA] Camp 루트 없음"); return; }
        var terrain = Terrain.activeTerrain;

        int done = 0;
        done += MoveTable(camp, terrain) ? 1 : 0;
        done += DialOnTable(camp)    ? 1 : 0;
        done += SpinChair(terrain)   ? 1 : 0;
        done += Cot(terrain)         ? 1 : 0;
        done += TarpLantern(camp, terrain) ? 1 : 0;
        done += DockLantern()        ? 1 : 0;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[EXTRA] 완료 — 단계 " + done + "/6");
    }

    // ── 1. 다이얼을 테이블 상판에, 패널을 다이얼 자식으로 ──────
    // 다이얼이 테이블의 자식이 아니라 Camp 직속이라 테이블만 옮기면 따로 논다.
    // 패널도 씬 최상위여서 제자리에 남는다. 둘 다 테이블에 묶는다.
    static bool DialOnTable(GameObject camp)
    {
        var tbl   = FindUnder(camp.transform, "camp03_table");
        var dial  = FindAnywhere("TimeDial");
        var panel = FindAnywhere("TimePanel");
        if (tbl == null || dial == null) { Debug.LogWarning("[EXTRA] 테이블/다이얼 못 찾음"); return false; }

        Bounds b = new Bounds(); bool has = false;
        foreach (var r in tbl.GetComponentsInChildren<Renderer>(true))
        { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        if (!has) { Debug.LogWarning("[EXTRA] 테이블 렌더러 없음"); return false; }

        Undo.SetTransformParent(dial.transform, tbl, "dial to table");
        dial.transform.position = new Vector3(b.center.x, b.max.y + 0.015f, b.center.z);
        dial.transform.rotation = Quaternion.identity;
        dial.transform.localScale = Vector3.one;

        if (panel != null)
        {
            Undo.SetTransformParent(panel.transform, dial.transform, "panel to dial");
            panel.transform.localPosition = new Vector3(0f, 0.90f, 0f);
            panel.transform.localScale = Vector3.one;
        }
        Debug.Log("[EXTRA] 다이얼 → 테이블 상판 " + dial.transform.position.ToString("F2")
                  + " / 패널은 다이얼 자식" + (panel == null ? " (패널 없음)" : ""));
        return true;
    }

    // ── 2. 테이블 이동 ───────────────────────────────────────────
    static bool MoveTable(GameObject camp, Terrain t)
    {
        var tbl = FindUnder(camp.transform, "camp03_table");
        if (tbl == null) { Debug.LogWarning("[EXTRA] camp03_table 못 찾음"); return false; }
        float gy = Ground(t, TABLE_XZ, tbl.position.y);
        Undo.RecordObject(tbl, "move table");
        tbl.position = new Vector3(TABLE_XZ.x, gy, TABLE_XZ.y);
        Debug.Log("[EXTRA] 테이블 → " + tbl.position.ToString("F2"));
        return true;
    }

    // ── 3. 돌릴 수 있는 의자 ─────────────────────────────────────
    //  Camp 밑에 두면 6. Setup Chair Stations 가 다시 집어가서 화로 쪽으로 돌려버린다.
    //  그래서 별도 루트로 뺀다.
    static bool SpinChair(Terrain t)
    {
        var old = FindRoot("SpinChair");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(FBX + "camp04_chair_BRN.fbx");
        if (src == null) { Debug.LogError("[EXTRA] camp04_chair_BRN.fbx 없음"); return false; }

        // 테이블에서 화로 쪽으로 한 걸음 — 테이블에 앉는 자리
        Vector2 dir = (FIRE - TABLE_XZ).normalized;
        Vector2 cp  = TABLE_XZ + dir * CHAIR_FROM_TABLE;
        float gy = Ground(t, cp, 1.81f);

        var root = new GameObject("SpinChair");
        Undo.RegisterCreatedObjectUndo(root, "spin chair");
        root.transform.position = new Vector3(cp.x, gy, cp.y);
        // 기본 방향: 테이블을 본다
        root.transform.rotation = Quaternion.LookRotation(new Vector3(-dir.x, 0f, -dir.y), Vector3.up);

        var pivot = new GameObject("Pivot");
        pivot.transform.SetParent(root.transform, false);

        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(src, pivot.transform);
        mesh.name = "chair";
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;
        CopyMaterialsFrom(mesh, "camp04_chair");

        // 메시 바닥을 피벗 원점에 맞춘다
        Bounds b = new Bounds(); bool has = false;
        foreach (var r in mesh.GetComponentsInChildren<Renderer>(true))
        { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        if (has)
            mesh.transform.position += new Vector3(root.transform.position.x - b.center.x,
                                                   root.transform.position.y - b.min.y,
                                                   root.transform.position.z - b.center.z);

        // 좌석/퇴장 지점
        var seat = Child(pivot.transform, "SeatPoint", new Vector3(0f, -0.10f, -0.06f));
        var exit = Child(pivot.transform, "ExitPoint", new Vector3(0f, 0.05f, -0.95f));

        var box = Undo.AddComponent<BoxCollider>(pivot);
        box.center = new Vector3(0f, 0.45f, 0f);
        box.size   = new Vector3(0.58f, 0.20f, 0.58f);

        var st = Undo.AddComponent<VRCStation>(pivot);
        st.stationEnterPlayerLocation = seat;
        st.stationExitPlayerLocation  = exit;
        st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
        st.canUseStationFromStation = true;
        st.disableStationExit = false;
        st.seated = true;

        // 회전 손잡이 — 의자 오른쪽 팔걸이 끝. 피벗 자식이라 의자와 같이 돈다.
        var brass = SolidMat("M_Brass_Mirror", new Color(0.55f, 0.44f, 0.20f), 0.85f, 0.60f);
        var knob = new GameObject("SpinKnob");
        knob.transform.SetParent(pivot.transform, false);
        knob.transform.localPosition = new Vector3(0.34f, 0.62f, 0.02f);
        Prim(PrimitiveType.Cylinder, knob.transform, "Knob", new Vector3(0f, 0f, 0f),
             new Vector3(0.05f, 0.03f, 0.05f), brass);
        Prim(PrimitiveType.Cube, knob.transform, "Lever", new Vector3(0.045f, 0.02f, 0f),
             new Vector3(0.09f, 0.02f, 0.02f), brass);
        var kb = Undo.AddComponent<BoxCollider>(knob);
        kb.size = new Vector3(0.16f, 0.12f, 0.16f);

        var spin = knob.GetComponent<PyriteSpinChair>();
        if (spin == null) spin = UdonSharpUndo.AddComponent<PyriteSpinChair>(knob);
        spin.pivot = pivot.transform;
        spin.stepDegrees = 45f;
        UdonSharpEditorUtility.CopyProxyToUdon(spin);
        EditorUtility.SetDirty(spin);

        Debug.Log("[EXTRA] 회전 의자 @ " + root.transform.position.ToString("F2") + " — 손잡이 1회 = 45°");
        return true;
    }

    // ── 4. 간이침대 (에셋에 없어서 직접 만든다) ──────────────────
    static bool Cot(Terrain t)
    {
        var old = FindRoot("Cot");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var tarp = FindAnywhere("camp02_hexa_tarp_GRN") ?? FindContaining("hexa_tarp");
        if (tarp == null) { Debug.LogError("[EXTRA] 타프 못 찾음"); return false; }

        // 🔴 타프의 로컬 right 는 -Z 방향이라 그쪽으로 밀면 캐노피 밖으로 나간다(실측).
        //    캐노피는 월드 X 로 길다. 침대도 X 축으로 눕혀 캐노피 안쪽에 둔다.
        Vector3 tp = tarp.transform.position;
        Vector3 p = new Vector3(tp.x + COT_DX, tp.y, tp.z + COT_DZ);
        float gy = Ground(t, new Vector2(p.x, p.z), p.y);

        var root = new GameObject("Cot");
        Undo.RegisterCreatedObjectUndo(root, "cot");
        root.transform.position = new Vector3(p.x, gy, p.z);
        root.transform.rotation = Quaternion.identity;   // 긴 축 = 월드 X

        // 접이식 야전침대(헬리녹스형) — 얇은 천 + 가는 알루미늄 프레임 + X자 거미다리 + 볼 발
        var frame  = SolidMat("M_CotFrame",  new Color(0.085f, 0.085f, 0.095f), 0.55f, 0.45f);
        var foot   = SolidMat("M_CotFoot",   new Color(0.055f, 0.055f, 0.060f), 0.00f, 0.22f);
        var fabric = SolidMat("M_CotFabric", new Color(0.34f, 0.33f, 0.23f),    0.00f, 0.10f);

        const float L  = 1.90f;    // 길이
        const float W  = 0.62f;    // 폭
        const float DY = 0.190f;   // 천 윗면 높이 — 실제 야전침대는 이만큼 낮다
        float rz = W * 0.5f - 0.02f;               // 사이드 레일 z
        float ry = DY - 0.028f;                    // 레일 중심 높이

        // 천
        Prim(PrimitiveType.Cube, root.transform, "Fabric",
             new Vector3(0f, DY - 0.009f, 0f), new Vector3(L, 0.018f, W), fabric);

        // 사이드 레일 2 + 끝 레일 2
        Strut(root.transform, "Rail_L", new Vector3(-L * 0.5f, ry, -rz), new Vector3(L * 0.5f, ry, -rz), 0.013f, frame);
        Strut(root.transform, "Rail_R", new Vector3(-L * 0.5f, ry,  rz), new Vector3(L * 0.5f, ry,  rz), 0.013f, frame);
        Strut(root.transform, "End_A",  new Vector3(-L * 0.5f + 0.01f, ry, -rz), new Vector3(-L * 0.5f + 0.01f, ry, rz), 0.011f, frame);
        Strut(root.transform, "End_B",  new Vector3( L * 0.5f - 0.01f, ry, -rz), new Vector3( L * 0.5f - 0.01f, ry, rz), 0.011f, frame);

        // 거미다리 4벌 — 허브에서 네 방향으로 뻗어 볼 발로 끝나고, 위로 두 대가 레일을 받친다
        float[] legX = { -0.68f, -0.23f, 0.23f, 0.68f };
        for (int i = 0; i < legX.Length; i++)
        {
            float x = legX[i];
            var hub = new Vector3(x, 0.078f, 0f);
            Prim(PrimitiveType.Cylinder, root.transform, "Hub_" + i, hub, new Vector3(0.026f, 0.030f, 0.026f), frame);

            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var f = new Vector3(x + sx * 0.165f, 0.024f, sz * rz);
                    Strut(root.transform, "LegBar_" + i, hub, f, 0.0105f, frame);
                    Prim(PrimitiveType.Sphere, root.transform, "Foot_" + i, f, new Vector3(0.042f, 0.042f, 0.042f), foot);
                }

            foreach (var sz in new[] { -1f, 1f })
                Strut(root.transform, "Post_" + i, hub, new Vector3(x, ry, sz * rz), 0.0105f, frame);
        }

        // 천 모서리 고리 — 실루엣의 특징이라 작게 넣는다
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, root.transform, "Tab",
                     new Vector3(sx * (L * 0.5f + 0.025f), DY - 0.012f, sz * (W * 0.5f - 0.07f)),
                     new Vector3(0.06f, 0.012f, 0.05f), fabric);

        // 올라설 수 있게
        var col = Undo.AddComponent<BoxCollider>(root);
        col.center = new Vector3(0f, DY * 0.5f, 0f);
        col.size   = new Vector3(L + 0.06f, DY, W);

        // 눕기 스테이션 (휴머노이드 클립 + 컨트롤러 생성 포함)
        PyriteCotStation.Apply(root);

        Debug.Log("[EXTRA] 야전침대 @ " + root.transform.position.ToString("F2") + "  " + L + "×" + W + "m, 천 높이 " + DY.ToString("F3"));
        return true;
    }

    // ── 5. 타프 아래 랜턴 ────────────────────────────────────────
    static bool TarpLantern(GameObject camp, Terrain t)
    {
        var tarp = FindAnywhere("camp02_hexa_tarp_GRN") ?? FindContaining("hexa_tarp");
        if (tarp == null) return false;
        Vector3 tp = tarp.transform.position;
        Vector3 p = new Vector3(tp.x + LAN_DX, tp.y, tp.z + LAN_DZ);
        return PlaceLantern("Lantern_Tarp", camp.transform,
                            new Vector3(p.x, Ground(t, new Vector2(p.x, p.z), p.y), p.z));
    }

    // ── 6. 부두 끝 기둥 + 랜턴 ───────────────────────────────────
    // 부두 메시에는 이미 계선주가 들어 있다. 새로 세우지 말고 그 꼭대기를 찾아 올린다.
    // DockMesh 렌더러 bounds 의 max.y 를 쓰면 기둥 높이(1.25)가 잡히지만 x/z 는 모른다.
    // 그래서 정점 중 가장 높은 것들만 골라, 부두 끝(작은 z)에 있는 덩어리의 중심을 쓴다.
    static bool DockLantern()
    {
        var oldP = FindAnywhere("DockPost");
        if (oldP != null) Undo.DestroyObjectImmediate(oldP);   // 이전 버전이 세운 가짜 기둥 제거

        var mesh = FindAnywhere("DockMesh");
        var mf = mesh == null ? null : mesh.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) { Debug.LogError("[EXTRA] DockMesh 메시 없음"); return false; }

        var tr = mf.transform;
        var v = mf.sharedMesh.vertices;
        float maxY = float.MinValue;
        for (int i = 0; i < v.Length; i++) { float y = tr.TransformPoint(v[i]).y; if (y > maxY) maxY = y; }

        // 꼭대기 근처 정점만
        float minZ = float.MaxValue;
        for (int i = 0; i < v.Length; i++)
        {
            var w = tr.TransformPoint(v[i]);
            if (w.y > maxY - 0.12f && w.z < minZ) minZ = w.z;
        }
        double sx = 0, sz = 0; int n = 0;
        for (int i = 0; i < v.Length; i++)
        {
            var w = tr.TransformPoint(v[i]);
            if (w.y > maxY - 0.12f && w.z < minZ + 0.60f) { sx += w.x; sz += w.z; n++; }
        }
        if (n == 0) { Debug.LogError("[EXTRA] 계선주 꼭대기 정점을 못 찾음"); return false; }

        var top = new Vector3((float)(sx / n), maxY, (float)(sz / n));
        Debug.Log("[EXTRA] 계선주 꼭대기 " + top.ToString("F2") + " (정점 " + n + "개)");
        return PlaceLantern("Lantern_Dock", FindRoot("Dock").transform, top);
    }

    static bool PlaceLantern(string name, Transform parent, Vector3 pos)
    {
        var old = FindAnywhere(name);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(FBX + "camp06_lantern_YLW.fbx");
        if (src == null) { Debug.LogError("[EXTRA] camp06_lantern_YLW.fbx 없음"); return false; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        Undo.RegisterCreatedObjectUndo(go, "lantern");
        go.name = name;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        CopyMaterialsFrom(go, "camp06_lantern");

        // 바닥을 pos 에 맞춘다
        Bounds b = new Bounds(); bool has = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        if (has) go.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);

        // 작은 점광원 — 밤 프리셋에서 J 가 campLights 로 다시 잡아준다
        var lgo = new GameObject("Light");
        lgo.transform.SetParent(go.transform, false);
        lgo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.00f, 0.80f, 0.42f);
        l.intensity = 1.5f;
        l.range = 6.5f;
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Mixed;

        Debug.Log("[EXTRA] " + name + " @ " + go.transform.position.ToString("F2"));
        return true;
    }

    // ── N7. 조작 UI 제자리 교체 ──────────────────────────────────
    const string UI_MONO = "Packages/dev.architech.protv/Samples/Prefabs/Plugins/MediaControls V2 (Monochrome).prefab";

    [MenuItem("Tools/Pyrite/N7. Swap Control UI → Monochrome", false, 276)]
    public static void SwapUI()
    {
        var mp = FindRoot("MediaPlayer");
        if (mp == null) { Debug.LogError("[UI] MediaPlayer 없음"); return; }
        var room = mp.transform.Find("Room");
        if (room == null) { Debug.LogError("[UI] Room 없음"); return; }

        Transform oldUi = null;
        foreach (Transform c in room) if (c.name.StartsWith("MediaControls")) { oldUi = c; break; }
        Vector3 pos   = oldUi != null ? oldUi.localPosition : new Vector3(-2.5f, 1.05f, 0.35f);
        Quaternion rot= oldUi != null ? oldUi.localRotation : Quaternion.Euler(0f, 22f, 0f);
        Vector3 scl   = oldUi != null ? oldUi.localScale    : Vector3.one * 0.5f;
        if (oldUi != null) Undo.DestroyObjectImmediate(oldUi.gameObject);

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(UI_MONO);
        if (src == null) { Debug.LogError("[UI] 프리팹 없음: " + UI_MONO); return; }
        var mc = (GameObject)PrefabUtility.InstantiatePrefab(src, room);
        PrefabUtility.UnpackPrefabInstance(mc, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        mc.name = "MediaControls (Mono)";
        mc.transform.localPosition = pos;
        mc.transform.localRotation = rot;
        mc.transform.localScale    = scl;
        GameObjectUtility.SetStaticEditorFlags(mc, 0);

        // tv 배선
        var tvComp = mp.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "TVManager");
        int wired = 0;
        if (tvComp != null)
            foreach (var c in mc.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var f = c.GetType().GetField("tv");
                if (f == null || !f.FieldType.IsInstanceOfType(tvComp)) continue;
                f.SetValue(c, tvComp);
                var usb = c as UdonSharp.UdonSharpBehaviour;
                if (usb != null) UdonSharpEditorUtility.CopyProxyToUdon(usb);
                EditorUtility.SetDirty(c);
                wired++;
            }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[UI] Monochrome 로 교체 — 위치 유지 " + pos.ToString("F2") + " / tv 배선 " + wired + "개");
    }

    /// FBX 를 직접 인스턴스화하면 머티리얼이 안 붙어 흰색으로 나온다.
    /// 씬에 이미 있는 같은 종류 소품에서 sharedMaterials 를 그대로 옮겨온다.
    static void CopyMaterialsFrom(GameObject target, string donorPrefix)
    {
        var camp = FindRoot("Camp");
        if (camp == null) return;
        MeshRenderer donor = null;
        foreach (var t in camp.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(donorPrefix)) continue;
            if (target.transform.IsChildOf(t)) continue;
            donor = t.GetComponentInChildren<MeshRenderer>(true);
            if (donor != null) break;
        }
        if (donor == null) { Debug.LogWarning("[EXTRA] 머티리얼 원본 " + donorPrefix + " 못 찾음 — 흰색으로 남는다"); return; }
        foreach (var r in target.GetComponentsInChildren<MeshRenderer>(true))
            r.sharedMaterials = donor.sharedMaterials;
    }

    // ── 도우미 ───────────────────────────────────────────────────
    static float Ground(Terrain t, Vector2 xz, float fallback)
        => t != null ? t.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + t.transform.position.y : fallback;

    static GameObject FindRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) return r;
        return null;
    }

    static GameObject FindAnywhere(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
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

    static Transform FindUnder(Transform root, string prefix)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(prefix)) return t;
        return null;
    }

    static Transform Child(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }

    /// 두 점을 잇는 가는 봉. Cylinder 프리미티브는 Y축 높이 2가 기본이라 길이의 절반을 스케일로 준다.
    static GameObject Strut(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material mat)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 1e-5f) return null;
        var go = Prim(PrimitiveType.Cylinder, parent, name, (a + b) * 0.5f,
                      new Vector3(radius, len * 0.5f, radius), mat);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
        return go;
    }

    static Material SolidMat(string name, Color c, float metallic, float smooth)
    {
        var path = MAT + name + ".mat";
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
        var col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic);
        return go;
    }
}
