// PyriteBedroomV3.cs — 텐트 침실 v3: 큰 에어매트 1 + 덮는 이불(몸 모양 불룩) + 조명 + TPU 창 + 창밖 파노라마
// Tools ▸ Pyrite3 ▸ Z49i. Bedroom v3 Build / Z49j. Bedroom v3 Revert / Z49k. Capture Night Pano / Z49l. Bedroom v3 Renders
//  2026-09-28 관리자: "인원수로 나누지 말 것 — 에어매트 가로 2.5개 크기 하나", "더 에어매트처럼", "이불 버튼 → 누운 채로 덮이고 몸 부분이 튀어나오게(담요 재질)"
//  ⚠ Z49b(셸 재빌드) 뒤에는 Z49i 다시. Z49f(옛 매트 4개)는 쓰지 않는다
//  매트: 둥근 상자 2.25×2.2×0.30 m, 윗면 플록(가로 골 16 cm 간격) + 머리 쪽 베개 턱(+5.5 cm) + 옆면 PVC + 밸브. 눕기 자리는 보이지 않는 Station 4개
//  이불: Pyrite/Blanket(정점에서 뼈 선분만큼 들어올림) + PyriteBlanket(U#, isOn 동기화) + 이불 주머니(버튼) + 발치에 접힌 이불(꺼졌을 때)
//  조명: 전부 실시간(베이크 없음 → 수면 모드에서 밝기 조절 가능). 걸이 랜턴 = 캠프 camp06 랜턴 복제(불꽃 파티클 포함) 1.4/6.5 m Soft 그림자, 촛불 랜턴 2 (0.55/2.8 m, 그림자 없음)
//  침실 렌더러는 레이어 24 'Bedroom' → Directional Light(해·달)에서 뺀다 (창밖은 밤 고정인데 정오 햇빛이 들어오지 않게)
//  창: 앞(+Z) 긴 벽 둥근 사각 1.8×1.2 m (중심 높이 1.05), 천 셰이더가 구멍을 clip, TPU 판(반투명) + 테두리 + 위에 말아 올린 덮개
//  창밖: 큰 구(r 400)에 Z49k 로 캠프에서 찍은 밤 파노라마(20.4시, 달이 호수 위 낮게)
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

public static class PyriteBedroomV3
{
    public const int LAYER = 24;
    const string DIR = "Assets/Bedroom";
    const string LOG = "Logs/pyrite_bedroom.txt";
    const string PREV = "Assets/_preview/bedroom/";
    public const float MW = 2.25f, ML = 2.2f, MH = 0.30f, MR = 0.07f;
    static float HEAD => PyriteBedroomBuild.MAT_HEAD;
    static float FOOT => PyriteBedroomBuild.MAT_HEAD + ML;
    static float MZC => PyriteBedroomBuild.MAT_HEAD + ML / 2f;
    // 창 v2 (2026-09-28 18:08 관리자 "넓이 5배"): 1.8×1.2 m r 0.22 (2.12 m²) → 4.6×2.3 m r 0.45 (10.4 m², ×4.9), 아래 끝 y 0.15 · 위 끝 2.45
    public static readonly Vector2 WIN_C = new Vector2(0f, 1.30f), WIN_H = new Vector2(2.3f, 1.15f);
    public const float WIN_R = 0.45f;
    static readonly Vector3 CAPTURE = new Vector3(-3.4f, 0f, 51.0f);
    const float CAPTURE_EYE = 0.9f, CAPTURE_HOUR = 20.4f;
    const string PANO = DIR + "/NightPano.png";
    static readonly float[] LIE_X = { -0.84f, -0.28f, 0.28f, 0.84f };
    static readonly string[] V3_CHILDREN = { "Beds", "Lights", "Window", "Backdrop", "Furniture" };
    static StringBuilder sb;

    // ───────────────────────── 메뉴 ─────────────────────────
    [MenuItem("Tools/Pyrite3/Z49i. Bedroom v3 Build", false, 4908)]
    public static void Build()
    {
        sb = new StringBuilder("[Z49i] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49j. Bedroom v3 Revert", false, 4909)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z49j] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom");
        if (room)
        {
            foreach (var n in V3_CHILDREN) { var t = room.transform.Find(n); if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(n + " 삭제"); } }
            var dome = room.transform.Find("Dome");
            if (dome) { var m = dome.GetComponent<Renderer>().sharedMaterial; if (m) { m.shader = Shader.Find("Standard"); sb.AppendLine("천 셰이더 → Standard"); } }
            foreach (var r in room.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = 0;
        }
        var sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (sun) { sun.cullingMask |= 1 << LAYER; EditorUtility.SetDirty(sun); sb.AppendLine("방향광 마스크 복구 " + sun.cullingMask); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (옛 매트는 Z49f, 임시 조명은 Z49b)");
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49k. Capture Night Pano", false, 4910)]
    public static void CaptureMenu()
    {
        sb = new StringBuilder("[Z49k] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { CapturePano(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49l. Bedroom v3 Renders", false, 4911)]
    public static void RenderMenu()
    {
        sb = new StringBuilder("[Z49l] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49t. Bedroom Window Rebuild", false, 4919)]
    public static void WindowRebuild()
    {
        sb = new StringBuilder("[Z49t] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var room = Root("TentBedroom");
            if (room == null) sb.AppendLine("!! TentBedroom 없음");
            else
            {
                var old = room.transform.Find("Window"); if (old) Object.DestroyImmediate(old.gameObject);
                var win = Child(room.transform, "Window");
                BuildWindow(win);
                SetupCanvas(room);
                foreach (var r in win.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = LAYER;
                foreach (var r in win.GetComponentsInChildren<Renderer>(true)) r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                float area = 4f * WIN_H.x * WIN_H.y - (4f - Mathf.PI) * WIN_R * WIN_R;
                sb.AppendLine("창 " + (2 * WIN_H.x) + "×" + (2 * WIN_H.y) + " m r " + WIN_R + " = " + area.ToString("F2") + " m² (이전 2.12), y " + (WIN_C.y - WIN_H.y).ToString("F2") + "~" + (WIN_C.y + WIN_H.y).ToString("F2"));
                foreach (var y in new[] { WIN_C.y - WIN_H.y, WIN_C.y, WIN_C.y + WIN_H.y })
                    sb.AppendLine("  벽 z at y " + y.ToString("F2") + ": x 0 → " + PyriteBedroomBuild.SurfZ(0, y).ToString("F2") + ", x ±" + (WIN_H.x - WIN_R).ToString("F2") + " → " + PyriteBedroomBuild.SurfZ(WIN_H.x - WIN_R, y).ToString("F2"));
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Renders();
                sb.AppendLine("RESULT: DONE");
            }
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static bool EnsureProgram(string name)
    {
        string asset = "Assets/Udon/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset) != null) return true;
        var paType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Udon/" + name + ".cs");
        if (paType == null || ms == null) { sb.AppendLine("!! 프로그램 에셋 생성 실패 " + name + " (cs " + (ms != null) + ")"); return false; }
        var pa = ScriptableObject.CreateInstance(paType);
        AssetDatabase.CreateAsset(pa, asset);
        var so = new SerializedObject(pa); so.FindProperty("sourceCsScript").objectReferenceValue = ms; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Ctrl+R 후 Z49i 다시");
        return false;
    }

    // ───────────────────────── 빌드 ─────────────────────────
    static bool Inner()
    {
        if (!EnsureProgram("PyriteBlanket")) return false;
        PyriteSpawnAudit.CompileErrors(sb);
        var room = Root("TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음 → Z49b 먼저"); return false; }
        var cot = Root("Cot");
        var cotSt = cot ? cot.GetComponent<VRC.SDK3.Components.VRCStation>() : null;
        if (cotSt == null) { sb.AppendLine("!! Cot VRCStation 없음"); return false; }
        foreach (var s in new[] { "Pyrite/TentCanvas", "Pyrite/Blanket", "Pyrite/Backdrop" })
            if (Shader.Find(s) == null) { sb.AppendLine("!! 셰이더 없음 " + s + " (컴파일 오류?)"); return false; }

        foreach (var n in V3_CHILDREN) { var t = room.transform.Find(n); if (t) Object.DestroyImmediate(t.gameObject); }
        var tl = room.transform.Find("TempLight"); if (tl) { Object.DestroyImmediate(tl.gameObject); sb.AppendLine("TempLight 삭제"); }
        Directory.CreateDirectory(DIR + "/Meshes");

        SetupLayer(room);
        SetupCanvas(room);
        var beds = Child(room.transform, "Beds");
        BuildMattress(beds);
        BuildStations(beds, cotSt);
        BuildBlanket(beds);
        var furn = Child(room.transform, "Furniture");
        BuildTable(furn);
        var lights = Child(room.transform, "Lights");
        BuildLights(lights);
        BuildWindow(Child(room.transform, "Window"));
        BuildBackdrop(Child(room.transform, "Backdrop"));

        foreach (var r in room.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = LAYER;
        foreach (var st in room.GetComponentsInChildren<VRC.SDK3.Components.VRCStation>(true)) st.gameObject.layer = 13;
        // 상호작용 오브젝트(Udon + 콜라이더)는 레이어 0 유지 — VRChat 상호작용 레이캐스트가 사용자 레이어 24 를 볼지 모름
        foreach (var ub in room.GetComponentsInChildren<VRC.Udon.UdonBehaviour>(true)) if (ub.gameObject.layer != 13) ub.gameObject.layer = 0;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        foreach (var n in V3_CHILDREN)
        {
            var t = room.transform.Find(n); if (!t) continue;
            int tris = t.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh && !f.name.Contains("Backdrop")).Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3 + (f.sharedMesh.subMeshCount > 1 ? (int)f.sharedMesh.GetIndexCount(1) / 3 : 0));
            sb.AppendLine(string.Format("  {0,-10} renderers {1,3} tris {2,6}", n, t.GetComponentsInChildren<Renderer>(true).Length, tris));
        }
        return true;
    }

    static Transform Child(Transform p, string n) { var g = new GameObject(n); g.transform.SetParent(p, false); Undo.RegisterCreatedObjectUndo(g, n); return g.transform; }

    static void SetupLayer(GameObject room)
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var l = tm.FindProperty("layers").GetArrayElementAtIndex(LAYER);
        if (string.IsNullOrEmpty(l.stringValue)) { l.stringValue = "Bedroom"; tm.ApplyModifiedProperties(); sb.AppendLine("레이어 24 = Bedroom"); }
        else if (l.stringValue != "Bedroom") sb.AppendLine("!! 레이어 24 이름이 이미 '" + l.stringValue + "'");
        var sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (sun) { int before = sun.cullingMask; sun.cullingMask &= ~(1 << LAYER); EditorUtility.SetDirty(sun); sb.AppendLine("방향광 cullingMask " + before + " → " + sun.cullingMask); }
    }

    static void SetupCanvas(GameObject room)
    {
        var dome = room.transform.Find("Dome");
        var m = dome.GetComponent<Renderer>().sharedMaterial;
        var c = m.HasProperty("_Color") ? m.GetColor("_Color") : new Color(0.62f, 0.47f, 0.31f);
        m.shader = Shader.Find("Pyrite/TentCanvas");
        m.SetColor("_Color", c); m.SetFloat("_Glossiness", 0.12f); m.SetFloat("_Weave", 0.08f);
        m.SetVector("_WinC", new Vector4(WIN_C.x, WIN_C.y, 0, 0));
        m.SetVector("_WinH", new Vector4(WIN_H.x, WIN_H.y, 0, 0));
        m.SetFloat("_WinR", WIN_R); m.SetFloat("_WinSide", 1f);
        EditorUtility.SetDirty(m);
        sb.AppendLine("천 셰이더 Pyrite/TentCanvas, 창 " + (2 * WIN_H.x) + "×" + (2 * WIN_H.y) + " m 중심 y " + WIN_C.y);
    }

    // ── 매트 ──
    static float TopH(float x, float z)
    {
        float zh = z + ML / 2f;
        float ridge = 0.055f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0f, 0.07f, zh)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.33f, zh)));
        float seam = -0.012f * Mathf.Exp(-Mathf.Pow((zh - 0.35f) / 0.014f, 2f));
        float ribs = 0f;
        if (zh > 0.36f)
        {
            float c = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * x / 0.16f);
            ribs = -0.010f * Mathf.Pow(c, 10f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.44f, zh));
        }
        float ex = Mathf.Clamp01(Mathf.InverseLerp(MW / 2f, MW / 2f - 0.10f, Mathf.Abs(x)));
        float ez = Mathf.Clamp01(Mathf.InverseLerp(ML / 2f, ML / 2f - 0.10f, z));
        float eh = Mathf.Clamp01(Mathf.InverseLerp(-ML / 2f, -ML / 2f + 0.02f, z));
        return (ridge + seam) * ex * eh + ribs * ex * ez;
    }

    static Mesh MattressMesh()
    {
        var h = new Vector3(MW / 2f, MH / 2f, ML / 2f);
        var inner = h - Vector3.one * MR;
        var v = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
        var top = new List<int>(); var side = new List<int>();
        // 면: (법선 축, 부호, u 축, v 축, res u, res v)
        var faces = new (int ax, float sg, int ua, int va, int ru, int rv)[]
        {
            (1, 1, 0, 2, 96, 48), (1, -1, 0, 2, 12, 6),
            (0, 1, 2, 1, 48, 8), (0, -1, 2, 1, 48, 8),
            (2, 1, 0, 1, 96, 8), (2, -1, 0, 1, 96, 8),
        };
        float e = 0.004f;
        foreach (var f in faces)
        {
            int b0 = v.Count;
            for (int j = 0; j <= f.rv; j++)
                for (int i = 0; i <= f.ru; i++)
                {
                    var c = Vector3.zero;
                    c[f.ax] = f.sg * h[f.ax];
                    c[f.ua] = Mathf.Lerp(-h[f.ua], h[f.ua], (float)i / f.ru);
                    c[f.va] = Mathf.Lerp(-h[f.va], h[f.va], (float)j / f.rv);
                    var inn = new Vector3(Mathf.Clamp(c.x, -inner.x, inner.x), Mathf.Clamp(c.y, -inner.y, inner.y), Mathf.Clamp(c.z, -inner.z, inner.z));
                    var d = c - inn; var n = d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.zero;
                    if (n == Vector3.zero) n[f.ax] = f.sg;
                    var p = inn + n * MR;
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, n.y));
                    if (w > 0)
                    {
                        float hh = TopH(p.x, p.z);
                        float gx = (TopH(p.x + e, p.z) - TopH(p.x - e, p.z)) / (2 * e);
                        float gz = (TopH(p.x, p.z + e) - TopH(p.x, p.z - e)) / (2 * e);
                        p.y += w * hh;
                        n = (n + w * new Vector3(-gx, 0, -gz)).normalized;
                    }
                    p.y += h.y;
                    v.Add(p); nrm.Add(n);
                    uv.Add(new Vector2(c[f.ua] + h[f.ua], c[f.va] + h[f.va]));
                }
            var list = (f.ax == 1 && f.sg > 0) ? top : side;
            int w1 = f.ru + 1;
            for (int j = 0; j < f.rv; j++)
                for (int i = 0; i < f.ru; i++)
                {
                    int a = b0 + j * w1 + i, b = a + 1, cc = a + w1, d = cc + 1;
                    AddTri(list, v, nrm, a, cc, b); AddTri(list, v, nrm, b, cc, d);
                }
        }
        var m = new Mesh { name = "AirMattressXL", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv);
        m.subMeshCount = 2; m.SetTriangles(top, 0); m.SetTriangles(side, 1);
        m.RecalculateBounds(); m.RecalculateTangents();
        Unwrapping.GenerateSecondaryUVSet(m);
        sb.AppendLine("  mesh AirMattressXL verts " + v.Count + " tris " + (top.Count + side.Count) / 3 + " (윗면 " + top.Count / 3 + ") size " + m.bounds.size.ToString("F3"));
        return m;
    }

    static void AddTri(List<int> list, List<Vector3> v, List<Vector3> n, int a, int b, int c)
    {
        var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(fn, n[a] + n[b] + n[c]) < 0) { list.Add(a); list.Add(c); list.Add(b); }
        else { list.Add(a); list.Add(b); list.Add(c); }
    }

    static void BuildMattress(Transform beds)
    {
        var mesh = SaveMesh(MattressMesh(), "AirMattressXL");
        var mFlock = Mat("M_MattressFlock", new Color(0.30f, 0.34f, 0.40f), 0.10f);
        var mPvc = Mat("M_MattressPVC", new Color(0.10f, 0.13f, 0.18f), 0.55f);
        var g = new GameObject("Mattress"); g.transform.SetParent(beds, false);
        g.transform.localPosition = new Vector3(0, 0, MZC);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterials = new[] { mFlock, mPvc };
        // 밸브 (발치 오른쪽 옆면)
        var valve = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(valve.GetComponent<Collider>());
        valve.name = "Valve"; valve.transform.SetParent(g.transform, false);
        valve.transform.localPosition = new Vector3(MW / 2f + 0.008f, MH * 0.55f, ML / 2f - 0.28f);
        valve.transform.localRotation = Quaternion.Euler(0, 0, 90); valve.transform.localScale = new Vector3(0.05f, 0.008f, 0.05f);
        valve.GetComponent<Renderer>().sharedMaterial = Mat("M_MattressValve", new Color(0.05f, 0.06f, 0.07f), 0.5f);
        sb.AppendLine("매트 " + MW + "×" + ML + "×" + MH + " m at z " + MZC.ToString("F2") + " (머리 " + HEAD + " / 발 " + FOOT.ToString("F2") + ")");
    }

    static void BuildStations(Transform beds, VRC.SDK3.Components.VRCStation cotSt)
    {
        float lane = MW / 4f;
        for (int i = 0; i < LIE_X.Length; i++)
        {
            float x = LIE_X[i];
            var lie = new GameObject("Lie_" + (i + 1)); lie.layer = 13; lie.transform.SetParent(beds, false);
            lie.transform.localPosition = new Vector3(x, 0, MZC);
            var bc = lie.AddComponent<BoxCollider>(); bc.center = new Vector3(0, MH * 0.5f + 0.02f, 0); bc.size = new Vector3(lane - 0.02f, MH + 0.04f, ML - 0.05f);
            var st = lie.AddComponent<VRC.SDK3.Components.VRCStation>();
            EditorUtility.CopySerialized(cotSt, st);
            st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize; st.seated = true; st.disableStationExit = false; st.canUseStationFromStation = true;
            var lp = new GameObject("LiePoint").transform; lp.SetParent(lie.transform, false);
            lp.localPosition = new Vector3(0, MH + 0.005f, PyriteLieViewTest.LIE_Z); lp.localRotation = Quaternion.identity;
            var ep = new GameObject("ExitPoint").transform; ep.SetParent(lie.transform, false);
            ep.localPosition = new Vector3(0, 0.02f, ML / 2f + 0.45f); ep.localRotation = Quaternion.identity;
            st.stationEnterPlayerLocation = lp; st.stationExitPlayerLocation = ep;
            EditorUtility.SetDirty(st);
            var cs = UdonSharpUndo.AddComponent<PyriteCarrySeat>(lie);
            cs.station = st; cs.pickup = null;
            UdonSharpEditorUtility.CopyProxyToUdon(cs);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs);
            if (ub != null) { ub.interactText = "Lie down"; ub.proximity = 2f; EditorUtility.SetDirty(ub); }
        }
        sb.AppendLine("눕기 Station " + LIE_X.Length + " (보이지 않음, 레인 폭 " + lane.ToString("F2") + " m, Cot 복사 " + (cotSt.animatorController ? cotSt.animatorController.name : "null") + ")");
    }

    // ── 이불 ──
    static Mesh BlanketMesh()
    {
        const float over = 0.30f;
        float x0 = -MW / 2f - over, x1 = MW / 2f + over, z0 = HEAD + 0.42f, z1 = FOOT + over;
        int nx = 72, nz = 54;
        float yTop = MH + 0.012f;
        float rr = MR;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var t = new List<int>();
        for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)i / nx), z = Mathf.Lerp(z0, z1, (float)j / nz);
                float ex = Mathf.Max(0f, Mathf.Abs(x) - (MW / 2f - 0.02f));
                float ez = Mathf.Max(0f, z - (FOOT - 0.02f));
                float L = Mathf.Sqrt(ex * ex + ez * ez);
                var dir = L > 1e-5f ? new Vector2(Mathf.Sign(x) * ex, ez) / L : Vector2.zero;
                var p0 = new Vector2(x - Mathf.Sign(x) * ex, z - ez);
                float hor = 0f, dn = 0f;
                if (L > 0f)
                {
                    if (L < rr * Mathf.PI / 2f) { float a = L / rr; hor = rr * Mathf.Sin(a); dn = rr * (1 - Mathf.Cos(a)); }
                    else { float rest = L - rr * Mathf.PI / 2f; hor = rr + rest * 0.12f; dn = rr + rest * 0.99f; }
                }
                float y = yTop - dn;
                if (y < 0.015f) { hor += 0.015f - y; y = 0.015f; }
                v.Add(new Vector3(p0.x + dir.x * hor, y, p0.y + dir.y * hor));
                uv.Add(new Vector2(x, z));
                col.Add(new Color(Mathf.Clamp01(1f - L / 0.05f), Mathf.Clamp01(L / 0.3f), 0, 1));
            }
        int w = nx + 1;
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
        // 윗면 법선이 위를 보게
        var fn = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
        if (fn.y < 0) for (int k = 0; k < t.Count; k += 3) { int x = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = x; }
        var m = new Mesh { name = "BlanketCover" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        m.bounds = new Bounds(m.bounds.center + Vector3.up * 0.5f, m.bounds.size + new Vector3(0, 1.2f, 0));   // 들어올림·떨어짐 여유 (컬링)
        sb.AppendLine("  mesh BlanketCover verts " + v.Count + " tris " + t.Count / 3);
        return m;
    }

    static Mesh SuperEllipsoid(string name, float a, float c, float b, float e1, float e2, int seg, int rings)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int j = 0; j <= rings; j++)
        {
            float vv = -Mathf.PI / 2f + Mathf.PI * j / rings;
            float cv = SP(Mathf.Cos(vv), e1), sv = SP(Mathf.Sin(vv), e1);
            for (int i = 0; i <= seg; i++)
            {
                float uu = 2f * Mathf.PI * i / seg;
                var p = new Vector3(a * cv * SP(Mathf.Cos(uu), e2), c * sv + c, b * cv * SP(Mathf.Sin(uu), e2));
                v.Add(p); uv.Add(new Vector2(p.x, p.z));
            }
        }
        int w = seg + 1;
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int q = j * w + i, r = q + 1, s = q + w, d = s + 1;
                t.AddRange(new[] { q, s, r, r, s, d });
            }
        var ctr = new Vector3(0, c, 0); double agree = 0;
        for (int k = 0; k < t.Count; k += 3) agree += Vector3.Dot(Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]), v[t[k]] - ctr);
        if (agree < 0) for (int k = 0; k < t.Count; k += 3) { int x = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = x; }
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.SetColors(Enumerable.Repeat(new Color(0, 0, 0, 1), v.Count).ToList());   // 이불 셰이더: 들어올림 없음
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }
    static float SP(float x, float e) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);

    static Mesh Lathe(string name, Vector2[] prof, int sides)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int j = 0; j < prof.Length; j++)
            for (int i = 0; i <= sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                v.Add(new Vector3(Mathf.Cos(a) * prof[j].x, prof[j].y, Mathf.Sin(a) * prof[j].x));
            }
        int w = sides + 1;
        for (int j = 0; j < prof.Length - 1; j++)
            for (int i = 0; i < sides; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                var fn = Vector3.Cross(v[c] - v[a], v[b] - v[a]);
                var outw = new Vector3(v[a].x, 0, v[a].z);
                if (Vector3.Dot(fn, outw) >= 0 || outw.sqrMagnitude < 1e-8f) t.AddRange(new[] { a, c, b, b, c, d });
                else t.AddRange(new[] { a, b, c, b, d, c });
            }
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static void BuildBlanket(Transform beds)
    {
        var sh = Shader.Find("Pyrite/Blanket");
        var mCover = MatShader("M_BlanketCover", sh);
        mCover.SetFloat("_Drop", 1f); mCover.SetFloat("_SegCount", 0f); EditorUtility.SetDirty(mCover);
        var mFold = MatShader("M_BlanketFold", sh);
        mFold.CopyPropertiesFromMaterial(mCover); mFold.SetFloat("_Drop", 1f); mFold.SetFloat("_SegCount", 0f); EditorUtility.SetDirty(mFold);

        var cover = new GameObject("BlanketCover"); cover.transform.SetParent(beds, false);
        cover.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BlanketMesh(), "BlanketCover");
        var cr = cover.AddComponent<MeshRenderer>(); cr.sharedMaterial = mCover; cr.enabled = false;

        var fold = new GameObject("BlanketFold"); fold.transform.SetParent(beds, false);
        fold.transform.localPosition = new Vector3(0.05f, MH - 0.012f, FOOT - 0.32f); fold.transform.localRotation = Quaternion.Euler(0, 2.5f, 0);
        fold.AddComponent<MeshFilter>().sharedMesh = SaveMesh(SuperEllipsoid("BlanketFoldXL", 0.62f, 0.045f, 0.24f, 0.30f, 0.16f, 40, 12), "BlanketFoldXL");
        fold.AddComponent<MeshRenderer>().sharedMaterial = mFold;

        // 이불 주머니 (버튼) — 발치 왼쪽 바닥
        var sack = new GameObject("StuffSack"); sack.transform.SetParent(beds, false);
        sack.transform.localPosition = new Vector3(-MW / 2f - 0.26f, 0f, FOOT + 0.05f);
        var prof = new[] { new Vector2(0.0f, 0.0f), new Vector2(0.11f, 0.005f), new Vector2(0.13f, 0.04f), new Vector2(0.135f, 0.18f), new Vector2(0.125f, 0.28f), new Vector2(0.07f, 0.33f), new Vector2(0.035f, 0.345f), new Vector2(0.045f, 0.36f), new Vector2(0.0f, 0.365f) };
        sack.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Lathe("StuffSack", prof, 24), "StuffSack");
        sack.AddComponent<MeshRenderer>().sharedMaterial = Mat("M_StuffSack", new Color(0.27f, 0.30f, 0.20f), 0.15f);
        var toggle = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(toggle.GetComponent<Collider>());
        toggle.name = "CordLock"; toggle.transform.SetParent(sack.transform, false);
        toggle.transform.localPosition = new Vector3(0.04f, 0.33f, 0.0f); toggle.transform.localScale = new Vector3(0.025f, 0.035f, 0.025f);
        toggle.GetComponent<Renderer>().sharedMaterial = Mat("M_MattressValve", new Color(0.05f, 0.06f, 0.07f), 0.5f);
        var sc = sack.AddComponent<CapsuleCollider>(); sc.center = new Vector3(0, 0.18f, 0); sc.radius = 0.14f; sc.height = 0.38f;

        var bl = UdonSharpUndo.AddComponent<PyriteBlanket>(sack);
        bl.blanket = cr; bl.mat = mCover; bl.folded = fold;
        bl.area = beds.Find("Mattress"); bl.half = new Vector3(MW / 2f, MH, ML / 2f); bl.isOn = false;
        UdonSharpEditorUtility.CopyProxyToUdon(bl); EditorUtility.SetDirty(bl);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(bl);
        if (ub != null) { ub.interactText = "Blanket"; ub.proximity = 3f; EditorUtility.SetDirty(ub); }
        sb.AppendLine("이불: 덮개(꺼짐) + 발치 접힌 이불 + 주머니 버튼 " + V(sack.transform.position) + " proximity 3 m");
    }

    // ── 가구 ──
    static void BuildTable(Transform furn)
    {
        var wood = Mat("M_TableWood", new Color(0.36f, 0.24f, 0.15f), 0.30f);
        var t = new GameObject("LowTable").transform; t.SetParent(furn, false);
        t.localPosition = new Vector3(0f, 0f, TableZ());
        Box(t, "Top", new Vector3(0, 0.34f, 0), new Vector3(1.0f, 0.025f, 0.40f), wood);
        foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                Box(t, "Leg", new Vector3(sx * 0.46f, 0.165f, sz * 0.17f), new Vector3(0.03f, 0.33f, 0.03f), wood);
        sb.AppendLine("낮은 테이블 z " + TableZ().ToString("F2") + " (창 아래), 윗면 y 0.35");
    }
    static float TableZ() => PyriteBedroomBuild.SurfZ(0.5f, 0.35f) - 0.26f;

    static GameObject Box(Transform p, string n, Vector3 lp, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = n; g.transform.SetParent(p, false); g.transform.localPosition = lp; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m; return g;
    }

    // ── 조명 ──
    static void BuildLights(Transform lights)
    {
        var room = lights.parent;
        var src = GameObject.Find("Camp/Pick_camp06_lantern_YLW/Body/camp06_lantern_YLW");
        float lz = 0.20f;
        if (src == null) sb.AppendLine("!! 캠프 랜턴 원본 없음");
        else
        {
            var lan = Object.Instantiate(src, lights); lan.name = "HangLantern";
            StripToVisual(lan);
            lan.transform.localRotation = Quaternion.identity; lan.transform.localScale = src.transform.lossyScale;
            lan.transform.localPosition = Vector3.zero;
            var mrs = lan.GetComponentsInChildren<MeshRenderer>(true);
            var b = mrs[0].bounds; foreach (var r in mrs) b.Encapsulate(r.bounds);
            var target = room.TransformPoint(new Vector3(0f, 2.55f, lz));
            lan.transform.position += new Vector3(target.x - b.center.x, target.y - b.max.y, target.z - b.center.z);
            foreach (var l in lan.GetComponentsInChildren<Light>(true))
            {
                l.intensity = 1.4f; l.range = 6.5f; l.shadows = LightShadows.Soft; l.shadowStrength = 0.85f;
                l.lightmapBakeType = LightmapBakeType.Realtime; l.renderMode = LightRenderMode.ForcePixel; l.cullingMask = ~0;
                EditorUtility.SetDirty(l);
            }
            float domeY = DomeY(0f, lz);
            var chain = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(chain.GetComponent<Collider>());
            chain.name = "Chain"; chain.transform.SetParent(lights, false);
            float len = domeY - 0.02f - 2.55f;
            chain.transform.localPosition = new Vector3(0f, 2.55f + len / 2f, lz); chain.transform.localScale = new Vector3(0.008f, len / 2f, 0.008f);
            chain.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_TentPole.mat");
            chain.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            sb.AppendLine("걸이 랜턴: 캠프 랜턴 복제, 위 끝 y 2.55, 사슬 " + len.ToString("F2") + " m → 천장 y " + domeY.ToString("F2") + ", 빛 1.4 / 6.5 m Soft");
        }
        // 촛불 랜턴 2 — 창 아래 테이블 위
        var glass = MatFade("M_LanternGlass", new Color(0.9f, 0.92f, 0.95f, 0.16f), 0.95f);
        var metal = Mat("M_LanternMetal", new Color(0.12f, 0.11f, 0.10f), 0.45f);
        var candleM = Mat("M_Candle", new Color(0.92f, 0.86f, 0.74f), 0.25f);
        candleM.EnableKeyword("_EMISSION"); candleM.SetColor("_EmissionColor", new Color(0.35f, 0.20f, 0.08f)); candleM.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(candleM);
        var fireSrc = src ? src.transform.Find("fire") : null;
        float tz = TableZ();
        foreach (var sx in new[] { -1f, 1f })
        {
            var c = new GameObject(sx < 0 ? "CandleLantern_L" : "CandleLantern_R").transform; c.SetParent(lights, false);
            c.localPosition = new Vector3(sx * 0.40f, 0.3525f, tz); c.localRotation = Quaternion.Euler(0, sx * 12f, 0);
            Box(c, "Base", new Vector3(0, 0.008f, 0), new Vector3(0.14f, 0.016f, 0.14f), metal);
            Box(c, "Cap", new Vector3(0, 0.245f, 0), new Vector3(0.15f, 0.02f, 0.15f), metal);
            Box(c, "Roof", new Vector3(0, 0.27f, 0), new Vector3(0.09f, 0.03f, 0.09f), metal);
            foreach (var px in new[] { -1f, 1f }) foreach (var pz in new[] { -1f, 1f })
                    Box(c, "Post", new Vector3(px * 0.064f, 0.125f, pz * 0.064f), new Vector3(0.012f, 0.225f, 0.012f), metal);
            var gl = Box(c, "Glass", new Vector3(0, 0.125f, 0), new Vector3(0.122f, 0.22f, 0.122f), glass); gl.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.name = "Ring"; ring.transform.SetParent(c, false); ring.transform.localPosition = new Vector3(0, 0.30f, 0); ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            ring.transform.localScale = new Vector3(0.04f, 0.004f, 0.04f); ring.GetComponent<Renderer>().sharedMaterial = metal;
            var cd = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(cd.GetComponent<Collider>());
            cd.name = "Candle"; cd.transform.SetParent(c, false); cd.transform.localPosition = new Vector3(0, 0.066f, 0); cd.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            cd.GetComponent<Renderer>().sharedMaterial = candleM;
            if (fireSrc)
            {
                var f = Object.Instantiate(fireSrc.gameObject, c); f.name = "Flame";
                foreach (var l in f.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject == f ? (Object)l : l.gameObject);
                f.transform.localPosition = new Vector3(0, 0.125f, 0); f.transform.localRotation = Quaternion.identity; f.transform.localScale = Vector3.one * 0.55f;
                foreach (var ps in f.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
            }
            var lg = new GameObject("Light"); lg.transform.SetParent(c, false); lg.transform.localPosition = new Vector3(0, 0.14f, 0);
            var L = lg.AddComponent<Light>(); L.type = LightType.Point; L.color = new Color(1f, 0.72f, 0.42f); L.intensity = 0.55f; L.range = 2.8f;
            L.shadows = LightShadows.None; L.lightmapBakeType = LightmapBakeType.Realtime;
            foreach (var t in c.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
        sb.AppendLine("촛불 랜턴 2: 테이블 위 x ±0.40, 빛 0.55 / 2.8 m, 불꽃 = 캠프 랜턴 파티클 ×0.55");
    }

    static void StripToVisual(GameObject g)
    {
        var keep = new System.Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(ParticleSystem), typeof(ParticleSystemRenderer), typeof(Light) };
        for (int pass = 0; pass < 3; pass++)
            foreach (var c in g.GetComponentsInChildren<Component>(true))
                if (c != null && !keep.Any(k => k.IsInstanceOfType(c))) Object.DestroyImmediate(c);
        foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    }

    public static float DomeY(float x, float z)
    {
        float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(x) / PyriteBedroomBuild.A, PyriteBedroomBuild.N) + Mathf.Pow(Mathf.Abs(z) / PyriteBedroomBuild.B, PyriteBedroomBuild.N), 1f / PyriteBedroomBuild.N);
        return PyriteBedroomBuild.H * Mathf.Sqrt(Mathf.Max(0f, 1f - r * r));
    }

    // ── 창 ──
    static List<Vector2> RoundRectLoop(float inset)
    {
        var pts = new List<Vector2>();
        float hx = WIN_H.x - inset, hy = WIN_H.y - inset, r = Mathf.Max(0.01f, WIN_R - inset);
        var corners = new[] { new Vector2(hx - r, hy - r), new Vector2(-(hx - r), hy - r), new Vector2(-(hx - r), -(hy - r)), new Vector2(hx - r, -(hy - r)) };
        for (int k = 0; k < 4; k++)
        {
            float a0 = k * Mathf.PI / 2f;
            for (int s = 0; s <= 10; s++) { float a = a0 + (Mathf.PI / 2f) * s / 10; pts.Add(WIN_C + corners[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            var nxt = corners[(k + 1) % 4]; float an = (k + 1) * Mathf.PI / 2f;
            var from = WIN_C + corners[k] + new Vector2(Mathf.Cos(a0 + Mathf.PI / 2f), Mathf.Sin(a0 + Mathf.PI / 2f)) * r;
            var to = WIN_C + nxt + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * r;
            for (int s = 1; s < 12; s++) pts.Add(Vector2.Lerp(from, to, s / 12f));
        }
        return pts;
    }
    static Vector3 OnWall(Vector2 p, float inward) => new Vector3(p.x, p.y, PyriteBedroomBuild.SurfZ(p.x, p.y) - inward);

    static void BuildWindow(Transform win)
    {
        // TPU 판: 격자, 둥근 사각 안쪽 칸만
        var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
        int nx = 40, ny = 28;
        float x0 = -WIN_H.x - 0.02f, x1 = WIN_H.x + 0.02f, y0 = WIN_C.y - WIN_H.y - 0.02f, y1 = WIN_C.y + WIN_H.y + 0.02f;
        var idx = new int[(nx + 1) * (ny + 1)];
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                var p = new Vector2(Mathf.Lerp(x0, x1, (float)i / nx), Mathf.Lerp(y0, y1, (float)j / ny));
                idx[j * (nx + 1) + i] = v.Count; v.Add(OnWall(p, 0.006f)); uv.Add(p);
            }
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = idx[j * (nx + 1) + i], b = idx[j * (nx + 1) + i + 1], c = idx[(j + 1) * (nx + 1) + i], d = idx[(j + 1) * (nx + 1) + i + 1];
                var ctr = (uv[a] + uv[d]) / 2f;
                if (SdRR(ctr - WIN_C, WIN_H, WIN_R) > 0.03f) continue;
                t.AddRange(new[] { a, b, c, b, d, c });
            }
        FixWinding(v, t, Vector3.back);
        var pane = MeshGO(win, "TPUPane", "TPUPane", v, t, uv, MatFade("M_TPU", new Color(0.86f, 0.90f, 0.95f, 0.10f), 0.93f));
        pane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        // 테두리: 경계를 따라 안 0.05 / 밖 0.04
        var loop = RoundRectLoop(0f);
        var tv = new List<Vector3>(); var tt = new List<int>(); var tuv = new List<Vector2>();
        for (int k = 0; k < loop.Count; k++)
        {
            var p = loop[k];
            var g = new Vector2(SdRR(p + new Vector2(0.001f, 0) - WIN_C, WIN_H, WIN_R) - SdRR(p - new Vector2(0.001f, 0) - WIN_C, WIN_H, WIN_R),
                                SdRR(p + new Vector2(0, 0.001f) - WIN_C, WIN_H, WIN_R) - SdRR(p - new Vector2(0, 0.001f) - WIN_C, WIN_H, WIN_R)).normalized;
            tv.Add(OnWall(p - g * 0.05f, 0.010f)); tv.Add(OnWall(p + g * 0.04f, 0.010f));
            tuv.Add(new Vector2(k / (float)loop.Count, 0)); tuv.Add(new Vector2(k / (float)loop.Count, 1));
        }
        for (int k = 0; k < loop.Count; k++)
        {
            int a = k * 2, b = a + 1, c = ((k + 1) % loop.Count) * 2, d = c + 1;
            tt.AddRange(new[] { a, c, b, b, c, d });
        }
        FixWinding(tv, tt, Vector3.back);
        MeshGO(win, "Trim", "WindowTrim", tv, tt, tuv, Mat("M_WindowTrim", new Color(0.11f, 0.10f, 0.09f), 0.2f));

        // 말아 올린 덮개 (창 위)
        var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(roll.GetComponent<Collider>());
        roll.name = "RolledFlap"; roll.transform.SetParent(win, false);
        float ry = WIN_C.y + WIN_H.y + 0.09f;
        roll.transform.localPosition = new Vector3(0, ry, PyriteBedroomBuild.SurfZ(0, ry) - 0.06f);
        roll.transform.localRotation = Quaternion.Euler(0, 0, 90); roll.transform.localScale = new Vector3(0.09f, WIN_H.x + 0.02f, 0.09f);
        roll.GetComponent<Renderer>().sharedMaterial = Mat("M_TentFlapRoll", new Color(0.50f, 0.37f, 0.24f), 0.1f);
        foreach (var sx in new[] { -0.6f * WIN_H.x, 0.6f * WIN_H.x })
        {
            var tie = Box(win, "Tie", new Vector3(sx, ry + 0.02f, PyriteBedroomBuild.SurfZ(sx, ry) - 0.055f), new Vector3(0.025f, 0.12f, 0.10f), Mat("M_WindowTrim", new Color(0.11f, 0.10f, 0.09f), 0.2f));
            tie.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        sb.AppendLine("창: TPU 판 " + t.Count / 3 + " tris, 테두리 " + tt.Count / 3 + " tris, 덮개 롤 y " + ry.ToString("F2"));
    }

    static float SdRR(Vector2 p, Vector2 h, float r)
    {
        var q = new Vector2(Mathf.Abs(p.x) - h.x + r, Mathf.Abs(p.y) - h.y + r);
        return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
    }

    static void FixWinding(List<Vector3> v, List<int> t, Vector3 want)
    {
        if (t.Count < 3) return;
        var fn = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
        if (Vector3.Dot(fn, want) < 0) for (int k = 0; k < t.Count; k += 3) { int x = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = x; }
    }

    static GameObject MeshGO(Transform p, string go, string meshName, List<Vector3> v, List<int> t, List<Vector2> uv, Material m)
    {
        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        var g = new GameObject(go); g.transform.SetParent(p, false);
        g.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, meshName);
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    // ── 창밖 ──
    static void BuildBackdrop(Transform bd)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(s.GetComponent<Collider>());
        s.name = "BackdropSphere"; s.transform.SetParent(bd, false); s.transform.localScale = Vector3.one * 800f;
        var m = MatShader("M_Backdrop", Shader.Find("Pyrite/Backdrop"));
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PANO);
        m.SetTexture("_Pano", tex); if (!m.HasProperty("_Yaw") || m.GetFloat("_Yaw") == 0) m.SetFloat("_Yaw", 180f);
        EditorUtility.SetDirty(m);
        var r = s.GetComponent<Renderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        sb.AppendLine("창밖 구 r 400, 파노라마 " + (tex ? tex.width + "x" + tex.height : "없음 → Z49k 먼저") + ", yaw " + m.GetFloat("_Yaw"));
    }

    // ───────────────────────── 파노라마 캡처 ─────────────────────────
    static readonly string[] HIDE = { "Camp", "CampProps", "CampPool", "QvPen", "SettingsProjector", "VideoProjector", "MediaPlayer", "TarpMirror",
        "CarryChair", "CarryChair_1", "CarryChair_2", "Cot", "TentDoor", "TentBedroom", "LakeMirrorSwitch", "Mirror", "SpinChair" };

    static void CapturePano()
    {
        var hidden = new List<GameObject>();
        foreach (var n in HIDE) { var g = Root(n); if (g && g.activeSelf) { g.SetActive(false); hidden.Add(g); } }
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var go = new GameObject("PanoCam");
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(CAPTURE_HOUR); }
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.allowHDR = false; cam.allowMSAA = false;
            if (main) cam.cullingMask = main.cullingMask;
            var pos = CAPTURE; pos.y = Ground(pos) + CAPTURE_EYE; go.transform.position = pos;
            var cube = new RenderTexture(2048, 2048, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { dimension = TextureDimension.Cube };
            cam.RenderToCubemap(cube, 63);
            var eq = new RenderTexture(4096, 2048, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cube.ConvertToEquirect(eq, Camera.MonoOrStereoscopicEye.Mono);
            var prev = RenderTexture.active; RenderTexture.active = eq;
            var tex = new Texture2D(4096, 2048, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 4096, 2048), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(PANO, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); cube.Release(); eq.Release();
            AssetDatabase.ImportAsset(PANO);
            var imp = (TextureImporter)AssetImporter.GetAtPath(PANO);
            imp.sRGBTexture = true; imp.mipmapEnabled = false; imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096; imp.textureCompression = TextureImporterCompression.Compressed; imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
            sb.AppendLine("파노라마 " + PANO + " from " + V(pos) + " at " + CAPTURE_HOUR + "h, 숨김 " + hidden.Count);
            // 기준 렌더: 같은 자리에서 호수(−Z) 쪽
            cam.fieldOfView = 60f; go.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Directory.CreateDirectory(PREV);
            ShotCam(cam, "v3_pano_ref_lake");
        }
        finally
        {
            Object.DestroyImmediate(go);
            foreach (var g in hidden) g.SetActive(true);
            foreach (var r in psr) r.enabled = true;
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_Backdrop.mat");
        if (m) { m.SetTexture("_Pano", AssetDatabase.LoadAssetAtPath<Texture2D>(PANO)); EditorUtility.SetDirty(m); sb.AppendLine("M_Backdrop 에 연결"); }
    }

    // ───────────────────────── 렌더 ─────────────────────────
    static void Renders()
    {
        var room = Root("TentBedroom"); if (room == null) return;
        Directory.CreateDirectory(PREV);
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled && !r.transform.IsChildOf(room.transform)).ToArray();
        foreach (var r in psr) r.enabled = false;
        var beds = room.transform.Find("Beds");
        var cover = beds ? beds.Find("BlanketCover") : null;
        var fold = beds ? beds.Find("BlanketFold") : null;
        var mCover = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_BlanketCover.mat");
        var o = room.transform;
        Vector3 W(float x, float y, float z) => o.TransformPoint(new Vector3(x, y, z));
        var winC = new Vector3(0, WIN_C.y, PyriteBedroomBuild.B);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            cam.fieldOfView = 75f;
            Shot(cam, W(-0.28f, 0.42f, HEAD + 0.35f), W(0, WIN_C.y, PyriteBedroomBuild.B), "v3_lie");
            Shot(cam, W(0.3f, 1.6f, -1.2f), W(0, 0.9f, PyriteBedroomBuild.B), "v3_stand");
            Shot(cam, W(0.1f, 1.25f, 1.2f), W(0, 1.0f, PyriteBedroomBuild.B), "v3_window");
            Shot(cam, W(1.9f, 1.75f, 2.1f), W(-0.3f, 0.3f, MZC), "v3_room");
            Shot(cam, W(-2.2f, 1.2f, 0.9f), W(0.3f, 0.2f, MZC), "v3_mattress");
            Shot(cam, W(0.6f, 1.9f, 1.1f), W(0f, 2.35f, 0.2f), "v3_lantern");
            // 이불 덮기 흉내: 누운 몸 3 (마네킹 선분)
            if (cover && mCover)
            {
                var segs = new Vector4[96]; int k = 0;
                foreach (var bx in new[] { -0.62f, 0.02f, 0.66f })
                {
                    Vector3 P(float x, float y, float z) => W(bx + x, y, z);
                    var head = P(0, 0.44f, HEAD + 0.30f); var chest = P(0, 0.42f, HEAD + 0.62f); var hips = P(0, 0.40f, HEAD + 1.05f);
                    var lk = P(-0.10f, 0.40f, HEAD + 1.50f); var lf = P(-0.12f, 0.38f, HEAD + 1.92f); var rk = P(0.10f, 0.40f, HEAD + 1.50f); var rf = P(0.13f, 0.38f, HEAD + 1.92f);
                    void S(Vector3 a, Vector3 b, float rad) { segs[k * 2] = new Vector4(a.x, a.y, a.z, rad); segs[k * 2 + 1] = new Vector4(b.x, b.y, b.z, 0); k++; }
                    S(head, chest, 0.11f); S(chest, hips, 0.14f); S(hips, lk, 0.085f); S(lk, lf, 0.065f); S(hips, rk, 0.085f); S(rk, rf, 0.065f);
                }
                mCover.SetVectorArray("_Seg", segs); mCover.SetFloat("_SegCount", k); mCover.SetFloat("_Drop", 1f);
                cover.GetComponent<Renderer>().enabled = true; if (fold) fold.gameObject.SetActive(false);
                Shot(cam, W(1.9f, 1.75f, 2.1f), W(-0.3f, 0.3f, MZC), "v3_blanket_on");
                Shot(cam, W(-2.4f, 0.9f, 0.6f), W(0.2f, 0.35f, MZC), "v3_blanket_side");
                mCover.SetFloat("_Drop", 0.45f);
                Shot(cam, W(1.9f, 1.75f, 2.1f), W(-0.3f, 0.3f, MZC), "v3_blanket_drop");
                mCover.SetFloat("_Drop", 1f); mCover.SetFloat("_SegCount", 0f);
                cover.GetComponent<Renderer>().enabled = false; if (fold) fold.gameObject.SetActive(true);
            }
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            Shot(cam, W(1.9f, 1.75f, 2.1f), W(-0.3f, 0.3f, MZC), "v3_room_noon");
            var dome = o.Find("Dome").gameObject; dome.SetActive(false);
            var bd = o.Find("Backdrop"); if (bd) bd.gameObject.SetActive(false);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            bool o0 = cam.orthographic; float s0 = cam.orthographicSize;
            cam.orthographic = true; cam.orthographicSize = 3.6f;
            Shot(cam, W(0, 30f, 0.001f), W(0, 0, 0), "v3_top");
            cam.orthographic = o0; cam.orthographicSize = s0;
            dome.SetActive(true); if (bd) bd.gameObject.SetActive(true);
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (mCover) { mCover.SetFloat("_Drop", 1f); mCover.SetFloat("_SegCount", 0f); }
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        var fwd = at - eye;
        cam.transform.SetPositionAndRotation(eye, Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.99f ? Quaternion.LookRotation(fwd, Vector3.forward) : Quaternion.LookRotation(fwd));
        ShotCam(cam, tag);
    }

    static void ShotCam(Camera cam, string tag)
    {
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double sum = 0; foreach (var p in px) sum += (p.r + p.g + p.b) / 3.0;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(82));
        sb.AppendLine("  shot " + tag + " mean " + (sum / px.Length).ToString("F1"));
        Object.DestroyImmediate(tex);
    }

    // ───────────────────────── 공통 ─────────────────────────
    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + "/Meshes/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material Mat(string name, Color c, float gloss)
    {
        string path = DIR + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Standard");
        m.SetColor("_Color", c); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m); return m;
    }

    static Material MatFade(string name, Color c, float gloss)
    {
        var m = Mat(name, c, gloss);
        m.SetFloat("_Mode", 2f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m); return m;
    }

    static Material MatShader(string name, Shader sh)
    {
        string path = DIR + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh; EditorUtility.SetDirty(m); return m;
    }

    static float Ground(Vector3 p) { var t = Terrain.activeTerrain; return t ? t.SampleHeight(p) + t.transform.position.y : 1.81f; }
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
    static void Flush() { Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString()); EditorGUIUtility.systemCopyBuffer = sb.ToString(); }
}
#endif
