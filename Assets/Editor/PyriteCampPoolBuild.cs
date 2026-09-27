// Tools ▸ Pyrite3 ▸ Z43b. Build Camp Pool  /  Z43c. Camp Pool Revert  /  Z43d. Camp Pool Renders
//  2026-09-27 관리자: 타프 뒤 버튼 테이블(Z42) 폐기 → 설정 프로젝터 2쪽에서 의자(최대 10)·돗자리(최대 6) 개수 + / −, QvPen 소환
//  루트 CampPool (새 오브젝트) = PyriteCampPool
//    chairs = CarryChair_1 · CarryChair_2 · CarryChair (기본, 켜짐) + CarryChair_X1~7 (CarryChair 복제, 꺼짐)
//    mats   = CampProps/PicnicMat (기본) + PicnicMat_X1~5 (꺼짐)
//    Homes  = 물건마다 제자리. 기본은 지금 자리, 추가분은 Z43a 꽃 지도의 꽃 없는 평지(x -19..-3, z 49..58)에
//             의자: 모닥불 (−10.5, 51.5) 둘레 r 2.1~2.3 m (텔레스코프·영상 스크린 방향 피함) + 타프 앞 1, 모두 불을 본다
//             돗자리: 타프 밑 서쪽 · 동쪽 둘 · 캠프 서쪽 평지 둘
//    QvPen  = Packages/net.ureishi.qvpen/QvPen.prefab 인스턴스, 캠프 동쪽 (−5.5, 51.8). 펜·지우개(QvPen_Pen / QvPen_Eraser 오브젝트)를 풀에 넘긴다
//  Z42 (SpawnTable) 은 지우고 그 스크립트·에셋도 지운다 (마지막에)
//  🔴 Z28b(의자)·Z31b(캠프 소품)를 다시 돌리면 참조가 끊긴다 → 그 뒤 Z43b 다시. Z43b 뒤엔 Z25a(설정 UI)도 다시 (풀 참조)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteCampPoolBuild
{
    static readonly Vector3 FIRE = new Vector3(-10.5f, 0f, 51.5f);
    static readonly Vector3[] CHAIR_HOMES =
    {
        new Vector3(-8.34f, 0, 52.29f), new Vector3(-8.28f, 0, 50.90f), new Vector3(-9.02f, 0, 49.74f), new Vector3(-10.30f, 0, 49.21f),
        new Vector3(-12.53f, 0, 50.96f), new Vector3(-12.58f, 0, 52.47f), new Vector3(-8.60f, 0, 55.00f),
    };
    static readonly Vector3[] MAT_HOMES =
    {
        new Vector3(-13.0f, 0, 57.6f), new Vector3(-5.6f, 0, 56.95f), new Vector3(-5.8f, 0, 58.3f), new Vector3(-16.5f, 0, 52.2f), new Vector3(-16.5f, 0, 53.8f),
    };
    static readonly Vector3 QVPEN_AT = new Vector3(-5.5f, 0f, 51.8f);
    const string QVPEN_PREFAB = "Packages/net.ureishi.qvpen/QvPen.prefab";
    const string LOG = "Logs/pyrite_pool.txt";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z43b. Build Camp Pool", false, 21)]
    public static void Build()
    {
        sb = new StringBuilder("[Z43b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); CleanupZ42(); sb.AppendLine("RESULT: DONE"); } }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z43c. Camp Pool Revert", false, 22)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z43c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        foreach (var n in new[] { "CampPool", "QvPen" }) { var g = RootByName(n); if (g != null) { Object.DestroyImmediate(g); sb.AppendLine(n + " 삭제"); } }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (설정 UI 는 Z25a 다시)");
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z43d. Camp Pool Renders", false, 23)]
    public static void RenderOnly()
    {
        sb = new StringBuilder("[Z43d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static GameObject RootByName(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static float Ground(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        return t ? t.SampleHeight(p) + t.transform.position.y : 1.81f;
    }

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
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Ctrl+R 후 Z43b 다시");
        return false;
    }

    static bool Inner()
    {
        if (!EnsureProgram("PyriteCampPool")) return false;
        PyriteSpawnAudit.CompileErrors(sb);

        // Z42 테이블 치우기 (추가분 풀 포함)
        var st = RootByName("SpawnTable"); if (st != null) { Object.DestroyImmediate(st); sb.AppendLine("SpawnTable(Z42) 삭제"); }
        foreach (var n in new[] { "CampPool", "QvPen" }) { var g = RootByName(n); if (g != null) { Object.DestroyImmediate(g); sb.AppendLine("이전 " + n + " 삭제"); } }

        var chairSrc = new[] { "CarryChair_1", "CarryChair_2", "CarryChair" }.Select(RootByName).ToArray();
        var campProps = RootByName("CampProps");
        var matSrc = campProps ? campProps.transform.Find("PicnicMat")?.gameObject : null;
        if (chairSrc.Any(c => c == null) || matSrc == null) { sb.AppendLine("!! 원본 없음"); return false; }

        var root = new GameObject("CampPool"); Undo.RegisterCreatedObjectUndo(root, "camp pool");
        var homes = new GameObject("Homes").transform; homes.SetParent(root.transform, false);
        var pool = new GameObject("Pool").transform; pool.SetParent(root.transform, false);

        var chairs = new List<GameObject>(chairSrc);
        var chairHomes = chairSrc.Select(c => Marker(homes, "Home_" + c.name, c.transform.position, c.transform.rotation)).ToList();
        for (int i = 0; i < CHAIR_HOMES.Length; i++)
        {
            var p = CHAIR_HOMES[i]; p.y = Ground(p);
            var f = FIRE - p; f.y = 0;
            var h = Marker(homes, "Home_CarryChair_X" + (i + 1), p, Quaternion.LookRotation(f.normalized, Vector3.up));   // 의자 앞(+Z)이 불을 본다
            chairHomes.Add(h);
            chairs.Add(Clone(chairSrc[2], pool, "CarryChair_X" + (i + 1), h));
        }
        var mats = new List<GameObject> { matSrc };
        var matHomes = new List<Transform> { Marker(homes, "Home_PicnicMat", matSrc.transform.position, matSrc.transform.rotation) };
        for (int i = 0; i < MAT_HOMES.Length; i++)
        {
            var p = MAT_HOMES[i]; p.y = Ground(p) + 0.002f;
            var h = Marker(homes, "Home_PicnicMat_X" + (i + 1), p, matSrc.transform.rotation);
            matHomes.Add(h);
            mats.Add(Clone(matSrc, pool, "PicnicMat_X" + (i + 1), h));
        }
        foreach (var h in chairHomes.Concat(matHomes))
        {
            var c = new[] { -1f, 1f }.SelectMany(a => new[] { -1f, 1f }.Select(b => Ground(h.position + new Vector3(a * 0.9f, 0, b * 0.6f)))).ToArray();
            sb.AppendLine(string.Format("  home {0,-26} {1} yaw {2:0} | 지면 차 {3:F3}", h.name, h.position.ToString("F2"), h.eulerAngles.y, c.Max() - c.Min()));
        }

        // QvPen
        var qvAsset = AssetDatabase.LoadAssetAtPath<GameObject>(QVPEN_PREFAB);
        GameObject qv = null; var pens = new List<GameObject>(); var erasers = new List<GameObject>();
        if (qvAsset == null) sb.AppendLine("!! QvPen 프리팹 없음 " + QVPEN_PREFAB);
        else
        {
            qv = (GameObject)PrefabUtility.InstantiatePrefab(qvAsset, SceneManager.GetActiveScene());
            var qp = QVPEN_AT; qp.y = Ground(qp);
            var f = FIRE - qp; f.y = 0;
            qv.transform.position = qp + (qv.transform.position);   // 프리팹 루트 로컬 y 0.8 유지
            qv.transform.rotation = Quaternion.LookRotation(-f.normalized, Vector3.up);
            foreach (var mb in qv.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string tn = mb.GetType().Name;
                if (tn == "QvPen_Pen" && !pens.Contains(mb.gameObject)) pens.Add(mb.gameObject);
                if (tn == "QvPen_Eraser" && !erasers.Contains(mb.gameObject)) erasers.Add(mb.gameObject);
            }
            var rs = qv.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var b = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : new Bounds();
            sb.AppendLine(string.Format("QvPen at {0} yaw {1:0} | bounds {2} .. {3} | pens {4} erasers {5}", qv.transform.position.ToString("F2"), qv.transform.eulerAngles.y,
                b.min.ToString("F2"), b.max.ToString("F2"), pens.Count, erasers.Count));
            foreach (var p in pens.Concat(erasers))
                sb.AppendLine(string.Format("   {0} sync {1} pickup {2} rb kinematic {3}", P(p.transform), p.GetComponent<VRCObjectSync>() != null, p.GetComponent<VRCPickup>() != null,
                    p.GetComponent<Rigidbody>() ? p.GetComponent<Rigidbody>().isKinematic.ToString() : "-"));
        }

        var mgr = UdonSharpUndo.AddComponent<PyriteCampPool>(root);
        mgr.chairs = chairs.ToArray(); mgr.chairHomes = chairHomes.ToArray();
        mgr.mats = mats.ToArray(); mgr.matHomes = matHomes.ToArray();
        mgr.pens = pens.ToArray(); mgr.erasers = erasers.ToArray();
        mgr.chairMask = 7; mgr.matMask = 1;
        UdonSharpEditorUtility.CopyProxyToUdon(mgr); EditorUtility.SetDirty(mgr);
        for (int i = 0; i < chairs.Count; i++) chairs[i].SetActive(i < 3);
        for (int i = 0; i < mats.Count; i++) mats[i].SetActive(i < 1);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine(string.Format("chairs {0} (켜짐 3) | mats {1} (켜짐 1) | syncs {2}", chairs.Count, mats.Count, root.GetComponentsInChildren<VRCObjectSync>(true).Length));
        return true;
    }

    static Transform Marker(Transform parent, string n, Vector3 p, Quaternion r)
    {
        var t = new GameObject(n).transform; t.SetParent(parent, false); t.SetPositionAndRotation(p, r); return t;
    }

    static GameObject Clone(GameObject src, Transform parent, string name, Transform at)
    {
        bool was = src.activeSelf; src.SetActive(true);
        var o = Object.Instantiate(src, parent);
        src.SetActive(was);
        o.name = name; o.transform.SetPositionAndRotation(at.position, at.rotation);
        var pk = o.GetComponent<VRCPickup>();
        foreach (var cs in o.GetComponentsInChildren<PyriteCarrySeat>(true))
        {
            cs.station = cs.GetComponent<VRCStation>(); cs.pickup = pk;
            UdonSharpEditorUtility.CopyProxyToUdon(cs); EditorUtility.SetDirty(cs);
        }
        foreach (var cc in o.GetComponentsInChildren<PyriteCarryChair>(true)) { UdonSharpEditorUtility.CopyProxyToUdon(cc); EditorUtility.SetDirty(cc); }
        foreach (var st in o.GetComponentsInChildren<VRCStation>(true))
        {
            bool okE = st.stationEnterPlayerLocation && st.stationEnterPlayerLocation.IsChildOf(o.transform);
            bool okX = st.stationExitPlayerLocation && st.stationExitPlayerLocation.IsChildOf(o.transform);
            if (!okE || !okX) sb.AppendLine("  !! " + name + "/" + st.name + " enter " + okE + " exit " + okX);
        }
        return o;
    }

    // Z42 스크립트·에셋 정리 (씬에서 SpawnTable 을 지운 뒤)
    static void CleanupZ42()
    {
        var paths = new[] { "Assets/Udon/PyriteSpawnTable.cs", "Assets/Udon/PyriteSpawnTable.asset", "Assets/Udon/PyriteSpawnButton.cs", "Assets/Udon/PyriteSpawnButton.asset",
            "Assets/Editor/PyriteSpawnTableBuild.cs", "Assets/Props/M_SpawnButtonGlow.mat", "Assets/Props/SpawnResetArrow.asset" };
        foreach (var p in paths) if (AssetDatabase.LoadAssetAtPath<Object>(p) != null) { AssetDatabase.DeleteAsset(p); sb.AppendLine("  삭제 " + p); }
        AssetDatabase.Refresh();
    }

    // ── 확인 렌더: 추가분을 전부 켜고 찍은 뒤 되돌린다
    static void Renders()
    {
        var root = RootByName("CampPool"); if (root == null) { sb.AppendLine("CampPool 없음"); return; }
        var mgr = root.GetComponent<PyriteCampPool>();
        Directory.CreateDirectory("Assets/_preview/pool/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView; bool o0 = cam.orthographic; float s0 = cam.orthographicSize;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var extras = mgr.chairs.Skip(3).Concat(mgr.mats.Skip(1)).Where(g => g != null).ToArray();
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            foreach (var full in new[] { false, true })
            {
                foreach (var e in extras) e.SetActive(full);
                string s = full ? "full" : "base";
                cam.orthographic = true; cam.orthographicSize = 6.5f;
                cam.transform.SetPositionAndRotation(new Vector3(-10.5f, 40f, 53.5f), Quaternion.Euler(90f, 0f, 0f));
                PyriteSpawnAudit.Shot(cam, "Assets/_preview/pool/top_" + s + ".png", 1200, 1000);
                cam.orthographic = false; cam.fieldOfView = 60f;
                var e1 = new Vector3(-10.5f, 4.2f, 45.5f); cam.transform.SetPositionAndRotation(e1, Quaternion.LookRotation(new Vector3(-10.5f, 1.9f, 53.0f) - e1));
                PyriteSpawnAudit.Shot(cam, "Assets/_preview/pool/lake_" + s + ".png", 1280, 720);
                var e2 = new Vector3(-10.5f, 4.4f, 60.5f); cam.transform.SetPositionAndRotation(e2, Quaternion.LookRotation(new Vector3(-10.5f, 1.9f, 52.0f) - e2));
                PyriteSpawnAudit.Shot(cam, "Assets/_preview/pool/tarp_" + s + ".png", 1280, 720);
            }
            var qv = RootByName("QvPen");
            if (qv != null)
            {
                cam.fieldOfView = 50f;
                var q = qv.transform.position;
                var e3 = q + (FIRE - q).normalized * 2.6f + Vector3.up * 0.9f; e3.y = q.y + 1.0f;
                cam.transform.SetPositionAndRotation(e3, Quaternion.LookRotation(q - e3));
                PyriteSpawnAudit.Shot(cam, "Assets/_preview/pool/qvpen.png", 1280, 720);
            }
        }
        finally
        {
            foreach (var e in extras) e.SetActive(false);
            foreach (var r in psr) r.enabled = true;
            cam.orthographic = o0; cam.orthographicSize = s0; cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        sb.AppendLine("shots Assets/_preview/pool/{top,lake,tarp}_{base,full}.png, qvpen.png");
    }

    static string P(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
    static void Flush() { Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString()); }
}
#endif
