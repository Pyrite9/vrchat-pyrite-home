// Tools ▸ Pyrite3 ▸ Z42b. Build Spawn Table  /  Z42c. Spawn Table Revert  /  Z42d. Spawn Table Renders
//  타프 뒤(평지 끝 z≈61 안쪽) 낮은 캠프 테이블 + 버튼 3개(의자 꺼내기 · 돗자리 꺼내기 · 정리)
//  Z42a 실측: 평지(1.81)는 z 61.0 까지, 그 뒤는 z 62 에서 +9 cm, 62.5 에서 +29 cm 로 올라간다 → 테이블 중심 z 60.35
//  루트 SpawnTable (새 오브젝트 — 기존 오브젝트에 Udon 을 더 붙이지 않는다)
//    Table      : camp03_table 메시만 복제(자식 TimeDial 제외), 스케일 (1.6, 1, 1.5), 정적 플래그 없음(재베이크 불필요, 라이트 프로브), BoxCollider
//    Btn_*      : 받침(짙은 나무) + 윗판(황동) + 미니어처(의자 / 돗자리 / 되돌리기 화살표). PyriteSpawnButton → PyriteSpawnTable
//    Homes/*    : 원래 의자 3개·돗자리 1개의 처음 자리
//    Spots/*    : 생성 자리 (의자 4, 돗자리 3)
//    Pool/*     : 추가분 — CarryChair 복제 6개, PicnicMat 복제 3개 (꺼진 채로 시작)
//  되돌리기 Z42c = SpawnTable 삭제 (원래 의자·돗자리는 안 건드린다)
//  🔴 Z28b(의자)나 Z31b(캠프 소품: PicnicMat)를 다시 돌리면 참조가 끊긴다 → 그 뒤 Z42b 다시
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteSpawnTableBuild
{
    const int PICKUP_LAYER = 13;
    const int EXTRA_CHAIRS = 6, EXTRA_MATS = 3;
    static readonly Vector3 TABLE = new Vector3(-9.3f, 0f, 60.55f);
    static readonly float[] BTN_X = { -9.85f, -9.55f, -9.25f };
    const float BTN_Z = 60.46f;
    static readonly string[] BTN_NAME = { "Btn_Chair", "Btn_Mat", "Btn_Reset" };
    static readonly string[] BTN_EVENT = { "SpawnChair", "SpawnMat", "ResetAll" };
    static readonly string[] BTN_TEXT = { "Chair +", "Mat +", "Tidy up" };
    // 사용자는 테이블 앞(-Z, 타프 뒤 끝 z 59.5 = 지면 +1.23 m 바로 밖)에 서서 +Z 를 본다
    // 생성 자리: 꽃은 z 59 부터 (Z42d 꽃 지도) → 전부 꽃 없는 타프 밑. 의자는 모닥불을 보게, 기둥 (-11.71, 57.6)·기존 의자·야전침대·텐트를 피해서
    static readonly Vector3[] CHAIR_SPOTS = { new Vector3(-10.75f, 0, 58.10f), new Vector3(-10.90f, 0, 57.30f), new Vector3(-12.50f, 0, 57.20f), new Vector3(-12.60f, 0, 58.30f) };
    static readonly Vector3[] MAT_SPOTS = { new Vector3(-5.60f, 0, 57.20f), new Vector3(-14.00f, 0, 57.90f), new Vector3(-5.60f, 0, 58.60f) };
    static readonly Vector3 FIRE = new Vector3(-10.5f, 0f, 51.5f);
    const string LOG = "Logs/pyrite_spawn.txt";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z42b. Build Spawn Table", false, 10)]
    public static void Build()
    {
        sb = new StringBuilder("[Z42b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z42c. Spawn Table Revert", false, 11)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z42c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = Root(false);
        if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("SpawnTable 삭제"); } else sb.AppendLine("SpawnTable 없음");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z42d. Spawn Table Renders", false, 12)]
    public static void RenderOnly()
    {
        sb = new StringBuilder("[Z42d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { TarpHeights(); FlowerMap(); Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    // 꽃 분포: FlowerField 메시 정점 수를 0.5 m 칸으로 (x -15..-3, z 53..63). 돗자리·의자 자리가 꽃 위인지
    static void FlowerMap()
    {
        var ff = RootByName("FlowerField"); if (ff == null) { sb.AppendLine("FlowerField 없음"); return; }
        const float X0 = -15f, Z0 = 53f, C = 0.5f; const int NX = 24, NZ = 20;
        var cnt = new int[NX, NZ];
        foreach (var mf in ff.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.GetComponent<Renderer>() ? mf.GetComponent<Renderer>().bounds : new Bounds();
            if (b.max.x < X0 || b.min.x > X0 + NX * C || b.max.z < Z0 || b.min.z > Z0 + NZ * C) continue;
            Vector3[] vs; try { vs = mf.sharedMesh.vertices; } catch { continue; }
            var tr = mf.transform;
            foreach (var lv in vs)
            {
                var v = tr.TransformPoint(lv);
                int ix = Mathf.FloorToInt((v.x - X0) / C), iz = Mathf.FloorToInt((v.z - Z0) / C);
                if (ix >= 0 && ix < NX && iz >= 0 && iz < NZ) cnt[ix, iz]++;
            }
        }
        sb.AppendLine("flowers: 0.5 m 칸 정점 수 (. = 0, 숫자 = log2), 행 z 62.5→53, 열 x -15→-3");
        for (int iz = NZ - 1; iz >= 0; iz--)
        {
            var row = new StringBuilder(string.Format("  z {0,5:0.0} ", Z0 + iz * C));
            for (int ix = 0; ix < NX; ix++) row.Append(cnt[ix, iz] == 0 ? " ." : " " + Mathf.Min(9, Mathf.FloorToInt(Mathf.Log(cnt[ix, iz], 2f))));
            sb.AppendLine(row.ToString());
        }
    }

    // 타프 천 높이: z 줄마다(0.25 m) x -10.5..-8.0 안 정점의 최저 y (사용자가 테이블 앞 타프 밑에 설 수 있는지)
    static void TarpHeights()
    {
        var tarp = GameObject.Find("Camp/camp02_hexa_tarp_GRN"); if (tarp == null) { sb.AppendLine("tarp 없음"); return; }
        var mf = tarp.GetComponentInChildren<MeshFilter>(); var m = mf.sharedMesh; var tr = mf.transform;
        var vs = m.vertices.Select(v => tr.TransformPoint(v)).Where(v => v.x > -10.5f && v.x < -8.0f && v.y > 2.2f).ToArray();
        for (float z = 57.5f; z <= 61.01f; z += 0.25f)
        {
            var row = vs.Where(v => Mathf.Abs(v.z - z) < 0.125f).ToArray();
            sb.AppendLine(row.Length == 0 ? string.Format("  tarp z {0:0.00}: 없음", z) : string.Format("  tarp z {0:0.00}: y {1:F2}..{2:F2} (지면 위 {3:F2}) n {4}", z, row.Min(v => v.y), row.Max(v => v.y), row.Min(v => v.y) - 1.81f, row.Length));
        }
    }

    static GameObject Root(bool create)
    {
        var r = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "SpawnTable");
        if (r == null && create) { r = new GameObject("SpawnTable"); Undo.RegisterCreatedObjectUndo(r, "spawn table"); }
        return r;
    }

    static GameObject RootByName(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static float Ground(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        return t ? t.SampleHeight(p) + t.transform.position.y : 1.81f;
    }

    // 새 U# 스크립트는 프로그램 에셋이 있어야 컴파일된다. 없으면 만들고 멈춘다(Ctrl+R 후 다시)
    static bool EnsureProgram(string name)
    {
        string asset = "Assets/Udon/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset) != null) return true;
        var paType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Udon/" + name + ".cs");
        if (paType == null || ms == null) { sb.AppendLine("!! 프로그램 에셋 생성 실패 " + name); return false; }
        var pa = ScriptableObject.CreateInstance(paType);
        AssetDatabase.CreateAsset(pa, asset);
        var so = new SerializedObject(pa); so.FindProperty("sourceCsScript").objectReferenceValue = ms; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Ctrl+R 후 Z42b 다시");
        return false;
    }

    // 밤에 테이블 주변에 불빛이 없다(21:00 렌더에서 버튼이 안 보임) → 윗판만 약하게 스스로 빛나는 황동
    static Material GlowMat(Material brass)
    {
        const string p = "Assets/Props/M_SpawnButtonGlow.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(brass != null ? brass : new Material(Shader.Find("Standard"))); Directory.CreateDirectory("Assets/Props"); AssetDatabase.CreateAsset(m, p); }
        if (brass != null) m.CopyPropertiesFromMaterial(brass);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.90f, 0.62f, 0.30f) * EMIT);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        sb.AppendLine("  button glow " + p + " emission x" + EMIT);
        return m;
    }
    const float EMIT = 0.35f;

    static Material FindMat(string n)
    {
        foreach (var g in AssetDatabase.FindAssets(n + " t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (m != null && m.name == n) return m;
        }
        sb.AppendLine("  !! 머티리얼 없음 " + n); return null;
    }

    static bool Inner()
    {
        bool okA = EnsureProgram("PyriteSpawnTable"), okB = EnsureProgram("PyriteSpawnButton");
        if (!okA || !okB) { AssetDatabase.Refresh(); return false; }
        PyriteSpawnAudit.CompileErrors(sb);

        var chairSrc = new[] { "CarryChair", "CarryChair_1", "CarryChair_2" }.Select(RootByName).ToArray();
        var campProps = RootByName("CampProps");
        var matSrc = campProps ? campProps.transform.Find("PicnicMat")?.gameObject : null;
        var tableSrc = GameObject.Find("Camp/camp03_table");
        if (chairSrc.Any(c => c == null) || matSrc == null || tableSrc == null)
        {
            sb.AppendLine("!! 원본 없음: chairs " + string.Join(",", chairSrc.Select(c => c ? c.name : "null")) + " mat " + (matSrc ? "ok" : "null") + " table " + (tableSrc ? "ok" : "null"));
            return false;
        }

        var old = Root(false);
        if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("이전 SpawnTable 삭제"); }
        var root = Root(true);

        // ── 테이블
        var tpos = new Vector3(TABLE.x, Ground(TABLE), TABLE.z);
        var table = new GameObject("Table");
        table.transform.SetParent(root.transform, false);
        table.transform.SetPositionAndRotation(tpos, Quaternion.Euler(0f, 180f, 0f));
        table.transform.localScale = tableSrc.transform.localScale;
        var tmf = table.AddComponent<MeshFilter>(); tmf.sharedMesh = tableSrc.GetComponent<MeshFilter>().sharedMesh;
        var tmr = table.AddComponent<MeshRenderer>(); var smr = tableSrc.GetComponent<MeshRenderer>();
        tmr.sharedMaterials = smr.sharedMaterials; tmr.shadowCastingMode = ShadowCastingMode.On; tmr.receiveShadows = true;
        tmr.lightProbeUsage = LightProbeUsage.BlendProbes; tmr.reflectionProbeUsage = smr.reflectionProbeUsage;
        GameObjectUtility.SetStaticEditorFlags(table, 0);
        var tb = tmf.sharedMesh.bounds;
        var tcol = table.AddComponent<BoxCollider>(); tcol.center = tb.center; tcol.size = tb.size;
        float top = tmr.bounds.max.y;
        sb.AppendLine(string.Format("table {0} scale {1} | bounds {2} .. {3} | top y {4:F3} (지면 +{5:F3})", tpos.ToString("F2"), table.transform.localScale.ToString("F2"),
            tmr.bounds.min.ToString("F2"), tmr.bounds.max.ToString("F2"), top, top - tpos.y));
        // 상판 가장자리 확인 (버튼이 상판 안에 있는지)
        sb.AppendLine(string.Format("  buttons x {0}..{1} z {2} vs top x {3:F2}..{4:F2} z {5:F2}..{6:F2}", BTN_X[0], BTN_X[2], BTN_Z, tmr.bounds.min.x, tmr.bounds.max.x, tmr.bounds.min.z, tmr.bounds.max.z));

        // ── 자리 표시
        var homes = new GameObject("Homes").transform; homes.SetParent(root.transform, false);
        var spots = new GameObject("Spots").transform; spots.SetParent(root.transform, false);
        var chairHomes = chairSrc.Select(c => Marker(homes, "Home_" + c.name, c.transform.position, c.transform.rotation)).ToArray();
        var matHomes = new[] { Marker(homes, "Home_PicnicMat", matSrc.transform.position, matSrc.transform.rotation) };
        var chairSpots = CHAIR_SPOTS.Select((p, i) => { var f = FIRE - p; f.y = 0; return Marker(spots, "ChairSpot_" + i, new Vector3(p.x, Ground(p), p.z), Quaternion.LookRotation(f.normalized, Vector3.up)); }).ToArray();
        var matSpots = MAT_SPOTS.Select((p, i) => Marker(spots, "MatSpot_" + i, new Vector3(p.x, Ground(p) + 0.002f, p.z), matSrc.transform.rotation)).ToArray();
        foreach (var t in chairSpots.Concat(matSpots)) sb.AppendLine("  spot " + t.name + " " + t.position.ToString("F2"));

        // ── 풀 (추가분)
        var pool = new GameObject("Pool").transform; pool.SetParent(root.transform, false);
        var chairs = new List<GameObject>(chairSrc);
        for (int i = 0; i < EXTRA_CHAIRS; i++) chairs.Add(CloneChair(chairSrc[0], pool, "CarryChair_X" + (i + 1), chairSpots[i % chairSpots.Length]));
        var mats = new List<GameObject> { matSrc };
        for (int i = 0; i < EXTRA_MATS; i++) mats.Add(CloneMat(matSrc, pool, "PicnicMat_X" + (i + 1), matSpots[i % matSpots.Length]));

        // ── 관리자
        var mgr = UdonSharpUndo.AddComponent<PyriteSpawnTable>(root);
        mgr.chairs = chairs.ToArray(); mgr.chairHomes = chairHomes; mgr.chairSpots = chairSpots;
        mgr.mats = mats.ToArray(); mgr.matHomes = matHomes; mgr.matSpots = matSpots;
        mgr.chairMask = 0; mgr.matMask = 0;
        UdonSharpEditorUtility.CopyProxyToUdon(mgr); EditorUtility.SetDirty(mgr);

        // ── 버튼
        var mWood = FindMat("M_PropWoodDark"); var mBrass = GlowMat(FindMat("M_PropBrass"));
        var chairMesh = chairSrc[0].transform.Find("Chair");
        var matMat = matSrc.GetComponent<MeshRenderer>() ? matSrc.GetComponent<MeshRenderer>().sharedMaterial : null;
        for (int b = 0; b < 3; b++)
        {
            var bp = new Vector3(BTN_X[b], top, BTN_Z);
            var go = new GameObject(BTN_NAME[b]); go.layer = PICKUP_LAYER;
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(bp, Quaternion.Euler(0f, 180f, 0f));   // 로컬 +Z = 사용자 쪽(-Z 월드)
            Cyl(go.transform, "Base", new Vector3(0, 0.009f, 0), 0.20f, 0.018f, mWood);
            var cap = new GameObject("Cap").transform; cap.SetParent(go.transform, false); cap.localPosition = new Vector3(0, 0.018f, 0);
            Cyl(cap, "Plate", new Vector3(0, 0.005f, 0), 0.17f, 0.010f, mBrass);
            if (b == 0 && chairMesh != null)
            {
                var mini = Object.Instantiate(chairMesh.gameObject, cap);
                mini.name = "Mini"; mini.layer = PICKUP_LAYER;
                mini.transform.localPosition = new Vector3(0, 0.010f, 0); mini.transform.localRotation = Quaternion.identity;   // 의자 앞(+Z) = 사용자 쪽
                mini.transform.localScale = chairMesh.localScale * 0.17f;
                foreach (var c in mini.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                Shadowless(mini);
            }
            else if (b == 1)
            {
                var mini = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(mini.GetComponent<Collider>());
                mini.name = "Mini"; mini.layer = PICKUP_LAYER; mini.transform.SetParent(cap, false);
                mini.transform.localPosition = new Vector3(0, 0.014f, 0); mini.transform.localScale = new Vector3(0.12f, 0.008f, 0.085f);
                mini.GetComponent<MeshRenderer>().sharedMaterial = matMat; Shadowless(mini);
                // 둘둘 만 돗자리 (꺼낸다는 느낌)
                var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(roll.GetComponent<Collider>());
                roll.name = "Roll"; roll.layer = PICKUP_LAYER; roll.transform.SetParent(cap, false);
                roll.transform.localPosition = new Vector3(0, 0.028f, -0.03f); roll.transform.localRotation = Quaternion.Euler(0, 0, 90f); roll.transform.localScale = new Vector3(0.022f, 0.060f, 0.022f);
                roll.GetComponent<MeshRenderer>().sharedMaterial = matMat; Shadowless(roll);
            }
            else if (b == 2)
            {
                var arrow = new GameObject("Mini"); arrow.layer = PICKUP_LAYER; arrow.transform.SetParent(cap, false);
                arrow.transform.localPosition = new Vector3(0, 0.018f, 0);
                arrow.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
                var ar = arrow.AddComponent<MeshRenderer>(); ar.sharedMaterial = mWood; Shadowless(arrow);
            }
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.03f, 0); bc.size = new Vector3(0.22f, 0.07f, 0.22f);
            var btn = UdonSharpUndo.AddComponent<PyriteSpawnButton>(go);
            btn.table = mgr; btn.eventName = BTN_EVENT[b]; btn.cap = cap;
            UdonSharpEditorUtility.CopyProxyToUdon(btn);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(btn);
            if (ub != null) { ub.interactText = BTN_TEXT[b]; ub.proximity = 2f; EditorUtility.SetDirty(ub); }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = PICKUP_LAYER; GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); }
            sb.AppendLine("  " + BTN_NAME[b] + " " + bp.ToString("F3") + " → " + BTN_EVENT[b]);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine(string.Format("chairs {0} (기본 3 + 추가 {1}) | mats {2} (기본 1 + 추가 {3}) | syncs {4} | udon {5} | tris {6}",
            chairs.Count, EXTRA_CHAIRS, mats.Count, EXTRA_MATS, root.GetComponentsInChildren<VRCObjectSync>(true).Length,
            root.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true).Length,
            root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).Sum(f => f.sharedMesh.triangles.Length / 3)));
        return true;
    }

    static Transform Marker(Transform parent, string n, Vector3 p, Quaternion r)
    {
        var t = new GameObject(n).transform; t.SetParent(parent, false); t.SetPositionAndRotation(p, r); return t;
    }

    static GameObject CloneChair(GameObject src, Transform parent, string name, Transform at)
    {
        var o = Object.Instantiate(src, parent);
        o.name = name; o.transform.SetPositionAndRotation(at.position, at.rotation);
        var pk = o.GetComponent<VRCPickup>();
        foreach (var cs in o.GetComponentsInChildren<PyriteCarrySeat>(true))
        {
            cs.station = cs.GetComponent<VRCStation>(); cs.pickup = pk;
            UdonSharpEditorUtility.CopyProxyToUdon(cs); EditorUtility.SetDirty(cs);
        }
        foreach (var cc in o.GetComponentsInChildren<PyriteCarryChair>(true)) { UdonSharpEditorUtility.CopyProxyToUdon(cc); EditorUtility.SetDirty(cc); }
        CheckStation(o);
        o.SetActive(false);
        return o;
    }

    static GameObject CloneMat(GameObject src, Transform parent, string name, Transform at)
    {
        var o = Object.Instantiate(src, parent);
        o.name = name; o.transform.SetPositionAndRotation(at.position, at.rotation);
        var pk = o.GetComponent<VRCPickup>();
        foreach (var cs in o.GetComponentsInChildren<PyriteCarrySeat>(true))
        {
            cs.station = cs.GetComponent<VRCStation>(); cs.pickup = pk;
            UdonSharpEditorUtility.CopyProxyToUdon(cs); EditorUtility.SetDirty(cs);
        }
        foreach (var cc in o.GetComponentsInChildren<PyriteCarryChair>(true)) { UdonSharpEditorUtility.CopyProxyToUdon(cc); EditorUtility.SetDirty(cc); }
        CheckStation(o);
        o.SetActive(false);
        return o;
    }

    // 복제본의 스테이션 들어가기/나가기 자리가 복제본 안을 가리키는지 확인
    static void CheckStation(GameObject o)
    {
        foreach (var st in o.GetComponentsInChildren<VRCStation>(true))
        {
            bool inE = st.stationEnterPlayerLocation && st.stationEnterPlayerLocation.IsChildOf(o.transform);
            bool inX = st.stationExitPlayerLocation && st.stationExitPlayerLocation.IsChildOf(o.transform);
            var cs = st.GetComponent<PyriteCarrySeat>();
            bool pkOk = cs != null && cs.pickup != null && cs.pickup.transform == o.transform;
            if (!inE || !inX || !pkOk) sb.AppendLine("  !! " + o.name + "/" + st.name + " enter " + inE + " exit " + inX + " pickup " + pkOk);
        }
    }

    static void Cyl(Transform parent, string n, Vector3 lp, float dia, float h, Material m)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(c.GetComponent<Collider>());
        c.name = n; c.transform.SetParent(parent, false); c.transform.localPosition = lp; c.transform.localScale = new Vector3(dia, h * 0.5f, dia);
        c.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    static void Shadowless(GameObject g) { foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off; }

    // 되돌리기 화살표: 수평 원호(40°→320°, 반지름 4.2 cm, 굵기 7 mm) + 끝 화살촉
    static Mesh ArrowMesh()
    {
        string path = "Assets/Props/SpawnResetArrow.asset";
        var mesh = new Mesh { name = "SpawnResetArrow" };
        var v = new List<Vector3>(); var tri = new List<int>();
        const float R = 0.042f, r = 0.0065f; const int SEG = 28, SIDE = 8;
        float a0 = 40f * Mathf.Deg2Rad, a1 = 320f * Mathf.Deg2Rad;
        for (int i = 0; i <= SEG; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)SEG);
            var c = new Vector3(Mathf.Cos(a) * R, 0, Mathf.Sin(a) * R);
            var n0 = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            for (int s = 0; s < SIDE; s++)
            {
                float b = s / (float)SIDE * Mathf.PI * 2f;
                v.Add(c + n0 * Mathf.Cos(b) * r + Vector3.up * Mathf.Sin(b) * r);
            }
        }
        for (int i = 0; i < SEG; i++)
            for (int s = 0; s < SIDE; s++)
            {
                int a = i * SIDE + s, b = i * SIDE + (s + 1) % SIDE, c = a + SIDE, d = b + SIDE;
                tri.AddRange(new[] { a, c, b, b, c, d });
            }
        // 화살촉: 끝(320°)에서 원호 진행 방향(+각도 방향)으로 뾰족한 삼각뿔
        var end = new Vector3(Mathf.Cos(a1) * R, 0, Mathf.Sin(a1) * R);
        var tan = new Vector3(-Mathf.Sin(a1), 0, Mathf.Cos(a1));
        var rad = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
        int baseI = v.Count;
        const float HW = 0.016f, HL = 0.024f, HH = 0.008f;
        v.Add(end + rad * HW); v.Add(end - rad * HW); v.Add(end + Vector3.up * HH); v.Add(end - Vector3.up * HH); v.Add(end + tan * HL);
        int p0 = baseI, p1 = baseI + 1, up = baseI + 2, dn = baseI + 3, tip = baseI + 4;
        tri.AddRange(new[] { p0, up, tip, up, p1, tip, p1, dn, tip, dn, p0, tip, p0, dn, up, up, dn, p1 });
        mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        // 원호 안팎이 뒤집히지 않았는지는 렌더로 확인 (Lathe 함정)
        var oldM = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (oldM != null) AssetDatabase.DeleteAsset(path);
        Directory.CreateDirectory("Assets/Props");
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ── 확인 렌더: 추가분을 생성 자리에 잠깐 켜고 찍은 뒤 다시 끈다
    static void Renders()
    {
        var root = Root(false); if (root == null) { sb.AppendLine("SpawnTable 없음"); return; }
        var mgr = root.GetComponent<PyriteSpawnTable>();
        Directory.CreateDirectory("Assets/_preview/spawn/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView; bool o0 = cam.orthographic; float s0 = cam.orthographicSize;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var extras = new List<GameObject>();
        if (mgr != null)
        {
            for (int i = 3; i < mgr.chairs.Length && i - 3 < mgr.chairSpots.Length; i++) { extras.Add(mgr.chairs[i]); mgr.chairs[i].transform.SetPositionAndRotation(mgr.chairSpots[i - 3].position, mgr.chairSpots[i - 3].rotation); }
            for (int i = 1; i < mgr.mats.Length && i - 1 < mgr.matSpots.Length; i++) { extras.Add(mgr.mats[i]); mgr.mats[i].transform.SetPositionAndRotation(mgr.matSpots[i - 1].position, mgr.matSpots[i - 1].rotation); }
        }
        try
        {
            foreach (var (tag, hour) in new[] { ("12", 12f), ("21", 21f) })
            {
                if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(hour); }
                foreach (var full in new[] { false, true })
                {
                    foreach (var e in extras) e.SetActive(full);
                    string suf = (full ? "full_" : "") + tag;
                    cam.orthographic = false; cam.fieldOfView = 55f;
                    var e1 = new Vector3(-9.3f, 3.30f, 59.75f); cam.transform.SetPositionAndRotation(e1, Quaternion.LookRotation(new Vector3(-9.45f, 2.19f, 60.55f) - e1));
                    PyriteSpawnAudit.Shot(cam, "Assets/_preview/spawn/near_" + suf + ".png", 1280, 720);
                    cam.fieldOfView = 60f;
                    var e2 = new Vector3(-9.0f, 3.6f, 53.8f); cam.transform.SetPositionAndRotation(e2, Quaternion.LookRotation(new Vector3(-9.3f, 2.0f, 60.2f) - e2));
                    PyriteSpawnAudit.Shot(cam, "Assets/_preview/spawn/camp_" + suf + ".png", 1280, 720);
                    var e4 = new Vector3(-8.0f, 3.2f, 54.6f); cam.transform.SetPositionAndRotation(e4, Quaternion.LookRotation(new Vector3(-9.8f, 1.95f, 58.0f) - e4)); cam.fieldOfView = 75f;
                    PyriteSpawnAudit.Shot(cam, "Assets/_preview/spawn/under_" + suf + ".png", 1280, 720);
                    if (tag == "12")
                    {
                        cam.orthographic = true; cam.orthographicSize = 4.6f;
                        cam.transform.SetPositionAndRotation(new Vector3(-9.3f, 30f, 58.2f), Quaternion.Euler(90f, 0f, 0f));
                        PyriteSpawnAudit.Shot(cam, "Assets/_preview/spawn/top_" + suf + ".png", 1200, 1200);
                        cam.orthographic = false;
                    }
                }
                // 버튼 가까이
                foreach (var e in extras) e.SetActive(false);
                cam.fieldOfView = 40f;
                var e3 = new Vector3(-9.55f, 2.75f, 59.93f); cam.transform.SetPositionAndRotation(e3, Quaternion.LookRotation(new Vector3(-9.55f, 2.22f, 60.46f) - e3));
                PyriteSpawnAudit.Shot(cam, "Assets/_preview/spawn/buttons_" + tag + ".png", 1280, 720);
            }
        }
        finally
        {
            foreach (var e in extras) e.SetActive(false);
            foreach (var r in psr) r.enabled = true;
            cam.orthographic = o0; cam.orthographicSize = s0; cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        sb.AppendLine("shots Assets/_preview/spawn/{near,camp,top}_[full_]{12,21}.png, buttons_{12,21}.png");
    }

    static void Flush() { Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString()); }
}
#endif
