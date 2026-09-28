// Tools ▸ Pyrite3 ▸ Z46a. Flower Scatter Build / Z46b. Revert (→ 타일 층) / Z46c. Compare Renders
//  2026-09-28 관리자: 참고 월드만큼 빽빽하게 + 고르게 ("내 쪽은 듬성듬성")
//  원인(TFlower.fbx 실측): 타일 11.26 m 한 장 = 포기 600개. 포기 = 1.29 m × 0.65 m 판 2장 십자(판마다 띠 3개 = 삼각형 12개)
//   - 타일 안 1 m 칸당 포기 0~10개(가장자리 줄은 1~3개) → 타일 무늬대로 뭉침·빈칸
//   - 십자가 전부 0°/90° → 겹쳐 깔아도 같은 격자 줄무늬(스크린샷의 빗살)
//  → 타일을 버리고 **포기 단위로 다시 뿌린다**: 지터 격자(칸 1/√밀도, 칸 안 ±45% 흔들기) + 연속 회전 0~360° + 크기 0.9~1.1
//     포기 모양은 타일의 600개 중 무작위(UV·정점색 다양성 유지). 마스크(FlowerDensity.bin)·소품 발자국은 타일 층과 같다
//  층: FlowerField/FlowerScatter/Scatter_B{bx}_{bz} (40 m 블록), 메시 Assets/Flora/Generated/Scatter/ (유료 경로)
//  켜면 기존 타일 층(FlowerField 직계 Tile 렌더러 18 + FlowerDense)은 비활성, 꽃 목록(DayCycle·FlowerCull·Settings·옛 ToD)은 Scatter 로 교체
//  Z46b 는 Scatter 를 지우고 타일 층을 다시 켜고 목록을 되돌린다
//  ⚠ I 를 다시 돌리면 FlowerField 가 통째로 새로 만들어진다 → Z46a 다시
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

public static class PyriteFloraScatter
{
    const string LOG = "Logs/pyrite_scatter.txt";
    const string OUT_MESH = "Assets/Flora/Generated/Scatter/";
    const string LAYER = "FlowerScatter";
    const string PREFIX = "Scatter_";
    const float DENSITY = 20f;      // 포기/m² (타일 1장 ≈ 4.7, 타일+밀도 층 ≈ 9.4 — 관리자 "그래도 부족"). 삼각형 = 12 × 포기
    const float JIT = 0.45f;        // 칸 안 흔들기 (칸 크기 비율)
    const float SCALE_MIN = 0.9f, SCALE_MAX = 1.1f;
    const float BLOCK = 40f, COV_FULL = 1.35f;

    class Plant { public Vector3[] v; public Vector3[] n; public Vector2[] uv; public Color32[] c; public int[] t; }

    // 타일 메시 → 포기 600개 (삼각형 XZ 상자 중심이 같은 것끼리 = 십자 2장)
    static List<Plant> ExtractPlants(Mesh mesh, StringBuilder sb)
    {
        var mv = mesh.vertices; var mn = mesh.normals; var mu = mesh.uv; var mc = mesh.colors32; var mt = mesh.triangles;
        bool hasN = mn != null && mn.Length == mv.Length, hasU = mu != null && mu.Length == mv.Length, hasC = mc != null && mc.Length == mv.Length;
        var groups = new Dictionary<long, List<int>>();
        for (int t = 0; t < mt.Length; t += 3)
        {
            Vector3 a = mv[mt[t]], b = mv[mt[t + 1]], c = mv[mt[t + 2]];
            float cx = (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) + Mathf.Max(a.x, Mathf.Max(b.x, c.x))) * 0.5f;
            float cz = (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) + Mathf.Max(a.z, Mathf.Max(b.z, c.z))) * 0.5f;
            long key = ((long)Mathf.RoundToInt(cx * 50f) << 32) ^ (uint)Mathf.RoundToInt(cz * 50f);
            if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<int>();
            l.Add(t);
        }
        var plants = new List<Plant>();
        var sizes = new Dictionary<int, int>();
        foreach (var g in groups.Values)
        {
            // 중심(XZ 상자 중심)을 원점으로
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var t in g) for (int e = 0; e < 3; e++) { var p = mv[mt[t + e]]; x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z); }
            var ctr = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            var map = new Dictionary<int, int>();
            var pl = new Plant();
            var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var C = new List<Color32>(); var T = new List<int>();
            foreach (var t in g)
                for (int e = 0; e < 3; e++)
                {
                    int s = mt[t + e];
                    if (!map.TryGetValue(s, out int d))
                    {
                        d = V.Count; map[s] = d;
                        V.Add(mv[s] - ctr); N.Add(hasN ? mn[s] : Vector3.up); U.Add(hasU ? mu[s] : Vector2.zero); C.Add(hasC ? mc[s] : new Color32(255, 255, 255, 255));
                    }
                    T.Add(d);
                }
            pl.v = V.ToArray(); pl.n = N.ToArray(); pl.uv = U.ToArray(); pl.c = C.ToArray(); pl.t = T.ToArray();
            plants.Add(pl);
            int tc = g.Count; sizes[tc] = sizes.TryGetValue(tc, out int k) ? k + 1 : 1;
        }
        sb.AppendLine("plants from tile: " + plants.Count + " (tris per plant: " + string.Join(", ", sizes.OrderBy(x => x.Key).Select(x => x.Key + "×" + x.Value)) + ")");
        return plants;
    }

    [MenuItem("Tools/Pyrite3/Z46a. Flower Scatter Build", false, 160)]
    public static void Build()
    {
        var sb = new StringBuilder("[Z46a] " + System.DateTime.Now.ToString("HH:mm:ss") + " density " + DENSITY + "/m²\n");
        try { BuildInner(sb); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString());
        Debug.Log(sb.ToString());
    }

    static void BuildInner(StringBuilder sb)
    {
        var ff = PyriteFloraDense.Root("FlowerField");
        var terrain = PyriteFloraDense.FindTerrain();
        if (ff == null || terrain == null) { sb.AppendLine("!! FlowerField/Terrain 없음"); return; }
        int res; var cov = PyriteFloraDense.LoadCoverage(out res);
        if (cov == null) { sb.AppendLine("!! FlowerDensity.bin"); return; }
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Flora/P_TS_Nemophila.prefab");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Flora/M_TS_Nemophila_Day.mat");
        if (pf == null || mat == null) { sb.AppendLine("!! P_TS_Nemophila / M_TS_Nemophila_Day 없음"); return; }
        float baseY = terrain.transform.position.y;
        var plants = ExtractPlants(pf.GetComponent<MeshFilter>().sharedMesh, sb);
        if (plants.Count < 10) { sb.AppendLine("!! 포기 추출 실패"); return; }

        RemoveLayer(ff, sb);
        SetTileLayers(ff, true, sb);                       // 발자국 계산 전에 켜 둘 필요는 없지만 상태를 일정하게
        var obst = PyriteFloraDense.PropFootprints(ff, terrain, baseY, sb);
        if (!AssetDatabase.IsValidFolder("Assets/Flora/Generated/Scatter"))
            AssetDatabase.CreateFolder("Assets/Flora/Generated", "Scatter");

        var layer = new GameObject(LAYER);
        Undo.RegisterCreatedObjectUndo(layer, "flower scatter");
        layer.transform.SetParent(ff.transform, false);

        var vb = new Dictionary<string, List<Vector3>>(); var nb = new Dictionary<string, List<Vector3>>();
        var ub = new Dictionary<string, List<Vector2>>(); var cb = new Dictionary<string, List<Color32>>();
        var ib = new Dictionary<string, List<int>>();
        var rng = new System.Random(20260929);
        float cell = 1f / Mathf.Sqrt(DENSITY);
        int placed = 0, cutMask = 0, cutProps = 0; long tris = 0;
        for (float z = -100f; z < 100f; z += cell)
            for (float x = -100f; x < 100f; x += cell)
            {
                // 난수는 칸마다 같은 횟수
                float px = x + cell * (0.5f + ((float)rng.NextDouble() * 2f - 1f) * JIT);
                float pz = z + cell * (0.5f + ((float)rng.NextDouble() * 2f - 1f) * JIT);
                float yaw = (float)rng.NextDouble() * 360f;
                float sc = Mathf.Lerp(SCALE_MIN, SCALE_MAX, (float)rng.NextDouble());
                int pi = rng.Next(plants.Count);
                double keep = rng.NextDouble();
                float k = PyriteFloraDense.Sample(cov, res, px, pz) / COV_FULL;
                if (k < 0.02f || (k < 1f && keep > k)) { if (k >= 0.02f) cutMask++; continue; }
                bool blocked = false;
                for (int o = 0; o < obst.Count; o++) if (obst[o].Contains(new Vector2(px, pz))) { blocked = true; break; }
                if (blocked) { cutProps++; continue; }

                var pl = plants[pi];
                var rot = Quaternion.Euler(0f, yaw, 0f);
                int bx = Mathf.FloorToInt((px + 100f) / BLOCK), bz = Mathf.FloorToInt((pz + 100f) / BLOCK);
                string key = PREFIX + "B" + bx + "_" + bz;
                if (!vb.TryGetValue(key, out var V))
                {
                    vb[key] = V = new List<Vector3>(); nb[key] = new List<Vector3>(); ub[key] = new List<Vector2>();
                    cb[key] = new List<Color32>(); ib[key] = new List<int>();
                }
                int b0 = V.Count;
                for (int i = 0; i < pl.v.Length; i++)
                {
                    var lp = rot * new Vector3(pl.v[i].x * sc, pl.v[i].y * sc, pl.v[i].z * sc);
                    float wx = px + lp.x, wz = pz + lp.z;
                    float h = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + baseY;
                    V.Add(new Vector3(wx, h + lp.y, wz));
                    nb[key].Add(rot * pl.n[i]); ub[key].Add(pl.uv[i]); cb[key].Add(pl.c[i]);
                }
                var I = ib[key];
                for (int i = 0; i < pl.t.Length; i++) I.Add(b0 + pl.t[i]);
                placed++; tris += pl.t.Length / 3;
            }

        int made = 0;
        foreach (var kv in vb)
        {
            if (kv.Value.Count == 0) continue;
            var m = new Mesh { indexFormat = IndexFormat.UInt32, name = kv.Key };
            m.SetVertices(kv.Value); m.SetNormals(nb[kv.Key]); m.SetUVs(0, ub[kv.Key]); m.SetColors(cb[kv.Key]);
            m.SetTriangles(ib[kv.Key], 0); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, OUT_MESH + kv.Key + ".asset");
            {
                var so = new SerializedObject(m); var p = so.FindProperty("m_IsReadable");
                if (p != null) { p.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var go = new GameObject(kv.Key);
            go.transform.SetParent(layer.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            made++;
        }
        sb.AppendLine(string.Format("scatter: cell {0:0.000} m, plants {1:N0}, tris {2:N0}, meshes {3}, cut by mask {4:N0}, by props {5:N0}",
            cell, placed, tris, made, cutMask, cutProps));

        SetTileLayers(ff, false, sb);
        Wire(ff, sb);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        sb.AppendLine("RESULT: DONE");
    }

    // 기존 타일 층 = FlowerField 직계 MeshRenderer 들 + FlowerDense
    static List<GameObject> TileObjects(GameObject ff)
    {
        var l = new List<GameObject>();
        foreach (Transform t in ff.transform)
        {
            if (t.name == LAYER) continue;
            if (t.name == "FlowerDense" || t.GetComponent<MeshRenderer>() != null) l.Add(t.gameObject);
        }
        return l;
    }

    static void SetTileLayers(GameObject ff, bool on, StringBuilder sb)
    {
        int n = 0;
        foreach (var g in TileObjects(ff)) if (g.activeSelf != on) { Undo.RecordObject(g, "tile layers"); g.SetActive(on); n++; }
        sb.AppendLine("tile layers " + (on ? "on" : "off") + " (" + n + " changed)");
    }

    static void RemoveLayer(GameObject ff, StringBuilder sb)
    {
        var old = ff.transform.Find(LAYER);
        if (old != null) { Undo.DestroyObjectImmediate(old.gameObject); sb.AppendLine("removed old " + LAYER); }
        int del = 0;
        if (AssetDatabase.IsValidFolder("Assets/Flora/Generated/Scatter"))
            foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Flora/Generated/Scatter" }))
            { AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g)); del++; }
        if (del > 0) sb.AppendLine("deleted scatter meshes " + del);
    }

    // 꽃 목록 = 켜진 층의 렌더러 (Scatter 가 있으면 Scatter, 없으면 타일 층 전부)
    static void Wire(GameObject ff, StringBuilder sb)
    {
        var layer = ff.transform.Find(LAYER);
        Renderer[] list;
        if (layer != null) list = layer.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();
        else list = TileObjects(ff).SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Cast<Renderer>().ToArray();

        var cyc = Object.FindObjectOfType<PyriteDayCycle>(true);
        if (cyc != null) { Undo.RecordObject(cyc, "scatter wire"); cyc.flowerRenderers = list; UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc); }
        var fc = ff.GetComponent<PyriteFlowerCull>();
        if (fc != null) { Undo.RecordObject(fc, "scatter wire"); fc.renderers = list; UdonSharpEditorUtility.CopyProxyToUdon(fc); EditorUtility.SetDirty(fc); }
        var st = Object.FindObjectOfType<PyriteSettings>(true);
        if (st != null) { Undo.RecordObject(st, "scatter wire"); st.flowerRenderers = list; UdonSharpEditorUtility.CopyProxyToUdon(st); EditorUtility.SetDirty(st); }
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        if (tod != null) { Undo.RecordObject(tod, "scatter wire"); tod.flowerRenderers = list; UdonSharpEditorUtility.CopyProxyToUdon(tod); EditorUtility.SetDirty(tod); }
        sb.AppendLine("flower lists → " + list.Length + " renderers (" + (layer != null ? "scatter" : "tiles") + "), cull " + (fc != null) + ", settings " + (st != null) + ", tod " + (tod != null));
    }

    [MenuItem("Tools/Pyrite3/Z46b. Flower Scatter Revert (tiles back)", false, 161)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z46b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var ff = PyriteFloraDense.Root("FlowerField");
            if (ff == null) sb.AppendLine("!! FlowerField 없음");
            else { RemoveLayer(ff, sb); SetTileLayers(ff, true, sb); Wire(ff, sb); }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString());
        Debug.Log(sb.ToString());
    }

    // ───────── Z46c 비교 — 타일 층(기존) vs 뿌리기, 눈높이·내려다보기, 정오·21시 + Game 뷰 통계 ─────────
    const string SHOT = "Assets/_preview/scatter/";
    static readonly (string n, Vector3 eye, Vector3 look)[] CViews =
    {
        ("spawn",     new Vector3(-2f, 4.23f, 62f), new Vector3(-2f - 30f * 0.6157f, 2.4f, 62f - 30f * 0.7880f)),
        ("down",      new Vector3(-2f, 4.10f, 62f), new Vector3(-2f - 3f * 0.6157f, 2.65f, 62f - 3f * 0.7880f)),  // 관리자 스샷처럼 발밑 내려다보기 (스폰 지면 2.63)
        ("camp_lake", new Vector3(-10.0f, 3.65f, 49.0f), new Vector3(-10.0f, 3.0f, -40.0f)),
        ("west_wall", new Vector3(25f, 3.3f, 45f), new Vector3(-78f, 5f, 20f)),
    };
    static readonly float[] Hours = { 12f, 21f };
    static int mStep, mWait; static List<string> mRes; static GameObject mScatter; static List<GameObject> mTiles;
    static Vector3 mp0; static Quaternion mr0; static float mf0;

    [MenuItem("Tools/Pyrite3/Z46c. Flower Scatter Compare", false, 162)]
    public static void Compare()
    {
        var ff = PyriteFloraDense.Root("FlowerField");
        mRes = new List<string> { "[Z46c] " + System.DateTime.Now.ToString("HH:mm:ss") };
        var sc = ff != null ? ff.transform.Find(LAYER) : null;
        if (sc == null) { mRes.Add("!! " + LAYER + " 없음 — Z46a 먼저"); File.AppendAllText(LOG, string.Join("\n", mRes) + "\n"); return; }
        mScatter = sc.gameObject; mTiles = TileObjects(ff);
        var cam = Camera.main; mp0 = cam.transform.position; mr0 = cam.transform.rotation; mf0 = cam.fieldOfView;
        Directory.CreateDirectory(SHOT);
        var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) EditorWindow.GetWindow(gv, false, null, true);
        mStep = 0; mWait = 0;
        EditorApplication.update -= MTick; EditorApplication.update += MTick;
    }

    static void SetMode(bool scatter)
    {
        mScatter.SetActive(scatter);
        foreach (var g in mTiles) g.SetActive(!scatter);
    }

    static void MTick()
    {
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        int per = CViews.Length * 2;
        int hi = mStep / per, vi = (mStep % per) / 2, on = mStep % 2;
        if (mWait == 0)
        {
            if (hi >= Hours.Length)
            {
                EditorApplication.update -= MTick;
                SetMode(true);
                cam.transform.SetPositionAndRotation(mp0, mr0); cam.fieldOfView = mf0; cam.targetTexture = null;
                cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
                mRes.Add("RESULT: DONE"); File.AppendAllText(LOG, string.Join("\n", mRes) + "\n");
                AssetDatabase.Refresh(); return;
            }
            if (vi == 0 && on == 0) { cyc.ResetCache(); cyc.EvaluateAt(Hours[hi]); }
            SetMode(on == 1);
            var v = CViews[vi]; cam.transform.SetPositionAndRotation(v.eye, Quaternion.LookRotation(v.look - v.eye)); cam.fieldOfView = 60f;
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if (++mWait < 8) return;
        var vv = CViews[vi];
        mRes.Add(string.Format("  {0:00.00}h {1,-10} {2,-7} tris {3,10:N0} batches {4,5} setpass {5,4}",
            Hours[hi], vv.n, on == 1 ? "scatter" : "tiles", UnityStats.triangles, UnityStats.batches, UnityStats.setPassCalls));
        PyriteFloraDense.Shot(cam, vv.eye, vv.look, 60f, SHOT + vv.n + "_" + Hours[hi].ToString("00.00") + (on == 1 ? "_scatter" : "_tiles") + ".png");
        mWait = 0; mStep++;
    }
}
#endif
