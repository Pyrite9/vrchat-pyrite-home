// Tools ▸ Pyrite3 ▸ Z45a. Flower Dense Layer Build / Z45b. Revert / Z45c. Measure + Renders
//  꽃밭 밀도 올리기 (2026-09-28 관리자). 벤더 프리셋(Presets/Nemophila)도 10 m 격자에 타일 1장 = 지금 우리 밀도와 같다
//  → 밀도를 더 올리려면 타일을 겹쳐 까는 수밖에 없다. 기존 꽃밭(I)은 건드리지 않고, 반 칸(5 m) 어긋난 두 번째 층을 얹는다
//   - 층: FlowerField/FlowerDense/Dense_B{bx}_{bz} (40 m 블록), 메시 Assets/Flora/Generated/Dense/ (유료 경로 — 저장소 제외)
//   - 마스크: I 와 같은 FlowerDensity.bin, 삼각형 단위. 타일마다 90° 회전 + ±15° 비틀기 + ±1.5 m 흔들기 → 격자 무늬 안 보이게
//   - 머티리얼: 기존 M_TS_Nemophila_Day 그대로 (DayCycle 색 보정 그대로 먹는다)
//   - 연결: DayCycle.flowerRenderers · FlowerCull.renderers · Settings.flowerRenderers · (옛) TimeOfDay.flowerRenderers 에 추가
//     → '꽃 보이는 거리' 바가 두 층을 같이 다룬다
//  ⚠ I(꽃밭 재생성)는 FlowerField 를 통째로 지우고 Generated 메시도 지운다 → I 를 다시 돌렸다면 Z45a 도 다시
//  Z45d (2026-09-28): 꽃 머티리얼 — 플레이어 회피(젖혀짐) 끔 + 벤더 Night 발광(텍스처·색) 이식, 세기는 DayCycle 이 _EmiInt 로. 발광 세기 스윕 렌더
//  ⚠ E(반딧불)는 FlowerField 의 직계 자식만 방출원으로 쓴다 → FlowerDense(컨테이너)는 안 들어간다
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

public static class PyriteFloraDense
{
    const string LOG = "Logs/pyrite_dense.txt";
    const string OUT_MESH = "Assets/Flora/Generated/Dense/";
    const string LAYER = "FlowerDense";
    const string PREFIX = "Dense_";
    const float CELL = 10f, BLOCK = 40f, COV_FULL = 1.35f;
    const float OFFSET = 5f;        // 기존 층과 반 칸 어긋남
    const float JITTER = 1.5f;      // 타일 위치 흔들기 (m)
    const float TWIST = 15f;        // 타일 회전 흔들기 (°)

    // ── I(PyriteFloraField)와 같은 커버리지 읽기 ───────────────────────────
    internal static float[,] LoadCoverage(out int res)
    {
        var p = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FlowerDensity.bin");
        res = 0;
        if (!File.Exists(p)) return null;
        var by = File.ReadAllBytes(p);
        res = System.BitConverter.ToInt32(by, 0);
        if (by.Length != 4 + res * res * 2) return null;
        var cov = new float[res, res];
        float cellA = (200f / res) * (200f / res);
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                int o = 4 + (iz * res + ix) * 2;
                cov[iz, ix] = (by[o] * 0.137f + by[o + 1] * 0.504f) / cellA;
            }
        return cov;
    }

    internal static float Sample(float[,] cov, int res, float x, float z)
    {
        float fx = (x + 100f) / 200f * res - 0.5f;
        float fz = (z + 100f) / 200f * res - 0.5f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, res - 1);
        int x1 = Mathf.Min(x0 + 1, res - 1), z1 = Mathf.Min(z0 + 1, res - 1);
        float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
        return Mathf.Lerp(Mathf.Lerp(cov[z0, x0], cov[z0, x1], tx),
                          Mathf.Lerp(cov[z1, x0], cov[z1, x1], tx), tz);
    }

    internal static Terrain FindTerrain()
    {
        var t = Terrain.activeTerrain;
        if (t != null) return t;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        { t = r.GetComponentInChildren<Terrain>(); if (t != null) return t; }
        return null;
    }

    internal static GameObject Root(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == name) return r;
        return null;
    }

    // 지면에 놓인 작은·낮은 메시(텐트·테이블·돗자리·부두·결정 등)의 XZ 상자 (+0.25 m). 꽃 층들이 이 안을 비운다
    internal static List<Rect> PropFootprints(GameObject ff, Terrain terrain, float baseY, StringBuilder sb)
    {
            var obst = new List<Rect>(); var obstNames = new List<string>();
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!r.enabled || r.transform.IsChildOf(ff.transform)) continue;
                string n = r.name.ToLowerInvariant();
                if (n.Contains("water") || n.Contains("lake") || n.Contains("mirror") || n.Contains("sky") || n.Contains("cliff") || n.Contains("bedrock")) continue;
                var b = r.bounds;
                float area = b.size.x * b.size.z;
                if (area < 0.1f || area > 40f || b.size.y > 5f) continue;
                float g = terrain.SampleHeight(b.center) + baseY;
                if (b.min.y > g + 0.6f) continue;   // 떠 있는 것(랜턴·타프 천)은 제외
                obst.Add(Rect.MinMaxRect(b.min.x - 0.25f, b.min.z - 0.25f, b.max.x + 0.25f, b.max.z + 0.25f));
                obstNames.Add(r.name);
            }
            sb.AppendLine("prop footprints " + obst.Count + ": " + string.Join(", ", obstNames.Take(40)) + (obstNames.Count > 40 ? " …" : ""));
        return obst;
    }

    // ───────── Z45a 만들기 ─────────
    [MenuItem("Tools/Pyrite3/Z45a. Flower Dense Layer Build", false, 150)]
    public static void Build()
    {
        var sb = new StringBuilder("[Z45a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { BuildInner(sb); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString());
        Debug.Log(sb.ToString());
    }

    static void BuildInner(StringBuilder sb)
    {
        var ff = Root("FlowerField");
        var terrain = FindTerrain();
        if (ff == null || terrain == null) { sb.AppendLine("!! FlowerField/Terrain 없음"); return; }
        int res; var cov = LoadCoverage(out res);
        if (cov == null) { sb.AppendLine("!! FlowerDensity.bin 없음/크기 불일치"); return; }
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Flora/P_TS_Nemophila.prefab");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Flora/M_TS_Nemophila_Day.mat");
        if (pf == null || mat == null) { sb.AppendLine("!! P_TS_Nemophila / M_TS_Nemophila_Day 없음"); return; }
        var mesh = pf.GetComponent<MeshFilter>().sharedMesh;
        float baseY = terrain.transform.position.y;

        // 기존 층 규모 (비교용)
        long baseTris = 0; int baseR = 0;
        foreach (Transform t in ff.transform)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { baseTris += mf.sharedMesh.triangles.Length / 3; baseR++; }
        }
        sb.AppendLine(string.Format("base layer: {0} renderers, {1:N0} tris. tile mesh {2}: {3:N0} tris, bounds {4}",
            baseR, baseTris, mesh.name, mesh.triangles.Length / 3, mesh.bounds.size.ToString("F2")));

        // 이전 밀도 층 지우기 (재실행 안전)
        RemoveLayer(ff, sb);
        if (!AssetDatabase.IsValidFolder("Assets/Flora/Generated/Dense"))
            AssetDatabase.CreateFolder("Assets/Flora/Generated", "Dense");

        var layer = new GameObject(LAYER);
        Undo.RegisterCreatedObjectUndo(layer, "flower dense");
        layer.transform.SetParent(ff.transform, false);

        // 소품 발자국 — 지면에 놓인 작은·낮은 메시(텐트·테이블·돗자리·부두·결정 등)의 XZ 상자 안은 비운다
        //  기존 층은 타일 중심 판정으로 캠프 가장자리가 우연히 비어 있었는데, 이 층은 마스크를 그대로 따라 텐트를 덮었다(Z45c 1차)
        var obst = PropFootprints(ff, terrain, baseY, sb);
        int cutByProps = 0;

        var mv = mesh.vertices; var mn = mesh.normals; var mu = mesh.uv; var mc = mesh.colors32; var mt = mesh.triangles;
        bool hasN = mn != null && mn.Length == mv.Length, hasU = mu != null && mu.Length == mv.Length, hasC = mc != null && mc.Length == mv.Length;

        var vb = new Dictionary<string, List<Vector3>>(); var nb = new Dictionary<string, List<Vector3>>();
        var ub = new Dictionary<string, List<Vector2>>(); var cb = new Dictionary<string, List<Color32>>();
        var ib = new Dictionary<string, List<int>>();
        var rng = new System.Random(20260928);
        int cells = 0; long tris = 0;

        for (float cz = -100f + OFFSET; cz < 100f; cz += CELL)
            for (float cx = -100f + OFFSET; cx < 100f; cx += CELL)
            {
                // 난수는 셀마다 같은 횟수로 뽑는다 → 마스크가 바뀌어도 나머지 셀 배치가 안 흔들린다
                float jx = ((float)rng.NextDouble() * 2f - 1f) * JITTER;
                float jz = ((float)rng.NextDouble() * 2f - 1f) * JITTER;
                float yaw = 90f * rng.Next(4) + ((float)rng.NextDouble() * 2f - 1f) * TWIST;
                float ccx = cx + CELL * 0.5f + jx, ccz = cz + CELL * 0.5f + jz;
                if (Sample(cov, res, ccx, ccz) < 0.04f) continue;
                var rot = Quaternion.Euler(0f, yaw, 0f);
                int bx = Mathf.FloorToInt((ccx + 100f) / BLOCK), bz = Mathf.FloorToInt((ccz + 100f) / BLOCK);
                string key = PREFIX + "B" + bx + "_" + bz;
                if (!vb.ContainsKey(key))
                {
                    vb[key] = new List<Vector3>(); nb[key] = new List<Vector3>(); ub[key] = new List<Vector2>();
                    cb[key] = new List<Color32>(); ib[key] = new List<int>();
                }
                var rm = new Dictionary<int, int>();
                bool any = false;
                for (int t = 0; t < mt.Length; t += 3)
                {
                    Vector3 a = rot * mv[mt[t]], b = rot * mv[mt[t + 1]], c = rot * mv[mt[t + 2]];
                    float wx = ccx + (a.x + b.x + c.x) / 3f, wz = ccz + (a.z + b.z + c.z) / 3f;
                    if (wx < -100f || wx > 100f || wz < -100f || wz > 100f) continue;
                    float k = Sample(cov, res, wx, wz) / COV_FULL;
                    if (k < 0.02f) continue;
                    if (k < 1f && rng.NextDouble() > k) continue;
                    bool blocked = false;
                    for (int o = 0; o < obst.Count; o++) if (obst[o].Contains(new Vector2(wx, wz))) { blocked = true; break; }
                    if (blocked) { cutByProps++; continue; }
                    for (int e = 0; e < 3; e++)
                    {
                        int src = mt[t + e], dst;
                        if (!rm.TryGetValue(src, out dst))
                        {
                            Vector3 lp = rot * mv[src];
                            float px = ccx + lp.x, pz = ccz + lp.z;
                            float h = terrain.SampleHeight(new Vector3(px, 0f, pz)) + baseY;
                            dst = vb[key].Count;
                            vb[key].Add(new Vector3(px, h + lp.y, pz));
                            nb[key].Add(hasN ? rot * mn[src] : Vector3.up);
                            ub[key].Add(hasU ? mu[src] : Vector2.zero);
                            cb[key].Add(hasC ? mc[src] : new Color32(255, 255, 255, 255));
                            rm[src] = dst;
                        }
                        ib[key].Add(dst);
                    }
                    tris++; any = true;
                }
                if (any) cells++;
            }

        int made = 0;
        foreach (var kv in vb)
        {
            if (kv.Value.Count == 0) continue;
            var m = new Mesh { indexFormat = IndexFormat.UInt32, name = kv.Key };
            m.SetVertices(kv.Value); m.SetNormals(nb[kv.Key]); m.SetUVs(0, ub[kv.Key]); m.SetColors(cb[kv.Key]);
            m.SetTriangles(ib[kv.Key], 0); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, OUT_MESH + kv.Key + ".asset");
            {   // Read/Write 끔 — 런타임 메모리 절반 (검수 4번). UploadMeshData(true)는 저장 전에 CPU 사본을 지워 버려서 안 씀
                var so = new SerializedObject(m);
                var p = so.FindProperty("m_IsReadable");
                if (p != null) { p.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var go = new GameObject(kv.Key);
            go.transform.SetParent(layer.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            made++;
        }
        sb.AppendLine("tris cut by prop footprints " + cutByProps.ToString("N0"));
        sb.AppendLine(string.Format("dense layer: tiles {0}, tris {1:N0}, meshes {2}  → total flowers {3:N0} tris (×{4:0.00})",
            cells, tris, made, baseTris + tris, baseTris > 0 ? (baseTris + tris) / (float)baseTris : 0f));

        Wire(ff, sb);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        sb.AppendLine("RESULT: DONE");
    }

    static void RemoveLayer(GameObject ff, StringBuilder sb)
    {
        var old = ff.transform.Find(LAYER);
        if (old != null) { Undo.DestroyObjectImmediate(old.gameObject); sb.AppendLine("removed old " + LAYER); }
        int del = 0;
        if (AssetDatabase.IsValidFolder("Assets/Flora/Generated/Dense"))
            foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Flora/Generated/Dense" }))
            { AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g)); del++; }
        if (del > 0) sb.AppendLine("deleted dense meshes " + del);
    }

    // 꽃 렌더러 목록 = 기존(이름이 Dense_ 가 아닌 것) + 현재 밀도 층
    static Renderer[] Merge(Renderer[] cur, Renderer[] dense)
    {
        var keep = (cur ?? new Renderer[0]).Where(r => r != null && !r.name.StartsWith(PREFIX));
        return keep.Concat(dense).ToArray();
    }

    static void Wire(GameObject ff, StringBuilder sb)
    {
        var layer = ff.transform.Find(LAYER);
        var dense = layer != null ? layer.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray() : new Renderer[0];

        var cyc = Object.FindObjectOfType<PyriteDayCycle>(true);
        if (cyc != null)
        {
            Undo.RecordObject(cyc, "dense wire");
            cyc.flowerRenderers = Merge(cyc.flowerRenderers, dense);
            UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
            sb.AppendLine("DayCycle.flowerRenderers " + cyc.flowerRenderers.Length);
        }
        else sb.AppendLine("!! PyriteDayCycle 없음");

        var fc = ff.GetComponent<PyriteFlowerCull>();
        if (fc != null)
        {
            Undo.RecordObject(fc, "dense wire");
            fc.renderers = Merge(fc.renderers, dense);
            UdonSharpEditorUtility.CopyProxyToUdon(fc); EditorUtility.SetDirty(fc);
            sb.AppendLine("FlowerCull.renderers " + fc.renderers.Length);
        }
        else sb.AppendLine("!! FlowerCull 없음 (Z25a 가 만든다)");

        var st = Object.FindObjectOfType<PyriteSettings>(true);
        if (st != null)
        {
            Undo.RecordObject(st, "dense wire");
            st.flowerRenderers = Merge(st.flowerRenderers, dense);
            UdonSharpEditorUtility.CopyProxyToUdon(st); EditorUtility.SetDirty(st);
            sb.AppendLine("Settings.flowerRenderers " + st.flowerRenderers.Length);
        }

        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        if (tod != null)
        {
            Undo.RecordObject(tod, "dense wire");
            tod.flowerRenderers = Merge(tod.flowerRenderers, dense);
            UdonSharpEditorUtility.CopyProxyToUdon(tod); EditorUtility.SetDirty(tod);
            sb.AppendLine("(old) TimeOfDay.flowerRenderers " + tod.flowerRenderers.Length);
        }
    }

    // ───────── 꽃 머티리얼 공통 조정 — I(PyriteFloraField.TuneMaterial)도 이걸 부른다 ─────────
    const string NIGHT_MAT = "Assets/つきのすとあ/FlowersGrassland/Materials/T_Flower_05_Night.mat";
    public static string ApplyFlowerMaterial(Material m)
    {
        if (m == null) return "null";
        // 가까이 가면 젖혀지는 회피 — 카메라 기준 반경 _AvdRad 2 m, 세기 2 (관리자: 비주얼적으로 과함)
        m.SetFloat("_EnaAvd", 0f); m.DisableKeyword("ENABLE_AVOIDANCE");
        string glow = "-";
        if (m.name.Contains("Nemophila"))
        {
            var night = AssetDatabase.LoadAssetAtPath<Material>(NIGHT_MAT);
            if (night != null)
            {
                m.SetTexture("_EmiTex", night.GetTexture("_EmiTex"));
                m.SetColor("_EmiColor", night.GetColor("_EmiColor"));
                m.SetFloat("_EnaEmi", 1f); m.EnableKeyword("ENABLE_EMISSION");   // 셰이더 토글은 float + 키워드 둘 다
                m.SetFloat("_EmiInt", 0f);                                        // 런타임 세기는 DayCycle
                glow = "emi tex " + (night.GetTexture("_EmiTex") ? night.GetTexture("_EmiTex").name : "null") + ", color " + night.GetColor("_EmiColor") + ", vendor int " + night.GetFloat("_EmiInt");
            }
            else glow = "!! night mat 없음";
        }
        EditorUtility.SetDirty(m);
        return m.name + ": avoidance off, glow " + glow;
    }

    [MenuItem("Tools/Pyrite3/Z45d. Flower Material (no bend + night glow) + Sweep", false, 153)]
    public static void MaterialSetup()
    {
        var sb = new StringBuilder("[Z45d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>(true);
        try
        {
            foreach (var n in new[] { "M_TS_Nemophila_Day", "M_TS_Myosotis_Day", "M_TS_Dandelion_Day" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Flora/" + n + ".mat");
                sb.AppendLine("  " + (m != null ? ApplyFlowerMaterial(m) : n + " 없음"));
            }
            AssetDatabase.SaveAssets();
            if (cyc == null) { sb.AppendLine("!! DayCycle 없음"); return; }
            sb.AppendLine("  DayCycle.flowerMat " + (cyc.flowerMat ? cyc.flowerMat.name : "null") + ", flowerGlowInt " + cyc.flowerGlowInt + ", flowerGlow " + cyc.flowerGlow);
            Directory.CreateDirectory(SHOT);
            float keep = cyc.flowerGlowInt;
            foreach (var h in new[] { 19.5f, 20.2f, 21f })
                foreach (var g in new[] { 0f, 0.6f, 1.0f, 1.6f })
                {
                    cyc.flowerGlowInt = g; cyc.ResetCache(); cyc.EvaluateAt(h);
                    foreach (var v in Views.Where(x => x.n == "spawn" || x.n == "spawn_low" || x.n == "camp_lake"))
                        Shot(cam, v.eye, v.look, 60f, SHOT + "glow_" + v.n + "_" + h.ToString("00.00") + "_" + g.ToString("0.0") + ".png");
                    sb.AppendLine(string.Format("  {0:00.00}h glowInt {1:0.0} sunEl {2:0.0} → _EmiInt {3:0.000}", h, g, cyc.sunElNow, cyc.flowerMat.GetFloat("_EmiInt")));
                }
            cyc.flowerGlowInt = keep;
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (cyc != null) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
            Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString());
            AssetDatabase.Refresh();
        }
    }

    // ───────── Z45b 되돌리기 ─────────
    [MenuItem("Tools/Pyrite3/Z45b. Flower Dense Layer Revert", false, 151)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z45b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var ff = Root("FlowerField");
            if (ff == null) sb.AppendLine("!! FlowerField 없음");
            else { RemoveLayer(ff, sb); Wire(ff, sb); }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString());
        Debug.Log(sb.ToString());
    }

    // ───────── Z45c 측정 + 렌더 — 밀도 층 끔/켬, Game 뷰 통계 + 눈높이 렌더 ─────────
    //  시각: 21 시(밤, 관리자 스크린샷 비교) / 18.33 시(노을). 통계는 Game 뷰가 다시 그려질 때만 갱신 → update 틱
    const string SHOT = "Assets/_preview/dense/";
    internal static readonly (string n, Vector3 eye, Vector3 look)[] Views =
    {
        ("spawn",     new Vector3(-2f,   4.23f, 62f),   new Vector3(-2f - 30f * 0.6157f, 2.4f, 62f - 30f * 0.7880f)),  // 스폰 yaw 218, 30 m 앞
        ("spawn_low", new Vector3(-2f,   3.60f, 62f),   new Vector3(-2f - 6f * 0.6157f, 2.2f, 62f - 6f * 0.7880f)),    // 꽃 속 내려다보기
        ("camp",      new Vector3(-12.5f, 3.65f, 55.5f), new Vector3(-9.5f, 2.2f, 50.5f)),
        ("camp_lake", new Vector3(-10.0f, 3.65f, 49.0f), new Vector3(-10.0f, 3.0f, -40.0f)),
        ("west_wall", new Vector3(25f, 3.3f, 45f),      new Vector3(-78f, 5f, 20f)),
    };
    static readonly float[] Hours = { 21f, 18.33f };
    static int mStep, mWait; static List<string> mRes; static Renderer[] mDense; static Vector3 mp0; static Quaternion mr0; static float mf0;

    [MenuItem("Tools/Pyrite3/Z45c. Flower Dense Measure + Renders", false, 152)]
    public static void Measure()
    {
        var ff = Root("FlowerField");
        var layer = ff != null ? ff.transform.Find(LAYER) : null;
        mRes = new List<string> { "[Z45c] " + System.DateTime.Now.ToString("HH:mm:ss") };
        if (layer == null) { mRes.Add("!! " + LAYER + " 없음 — Z45a 먼저"); File.AppendAllText(LOG, string.Join("\n", mRes) + "\n"); return; }
        mDense = layer.GetComponentsInChildren<Renderer>(true);
        var cam = Camera.main; mp0 = cam.transform.position; mr0 = cam.transform.rotation; mf0 = cam.fieldOfView;
        Directory.CreateDirectory(SHOT);
        var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) EditorWindow.GetWindow(gv, false, null, true);
        mStep = 0; mWait = 0;
        EditorApplication.update -= MTick; EditorApplication.update += MTick;
    }

    static void MTick()
    {
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        int per = Views.Length * 2;
        int hi = mStep / per, vi = (mStep % per) / 2, on = mStep % 2;
        if (mWait == 0)
        {
            if (hi >= Hours.Length)
            {
                EditorApplication.update -= MTick;
                foreach (var r in mDense) if (r != null) r.enabled = true;
                cam.transform.SetPositionAndRotation(mp0, mr0); cam.fieldOfView = mf0; cam.targetTexture = null;
                cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
                mRes.Add("RESULT: DONE"); File.AppendAllText(LOG, string.Join("\n", mRes) + "\n");
                AssetDatabase.Refresh(); return;
            }
            if (vi == 0 && on == 0) { cyc.ResetCache(); cyc.EvaluateAt(Hours[hi]); }
            foreach (var r in mDense) if (r != null) r.enabled = on == 1;
            var v = Views[vi]; cam.transform.SetPositionAndRotation(v.eye, Quaternion.LookRotation(v.look - v.eye)); cam.fieldOfView = 60f;
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if (++mWait < 8) return;
        var vv = Views[vi];
        mRes.Add(string.Format("  {0:00.00}h {1,-10} dense {2,-3} tris {3,10:N0} batches {4,5} setpass {5,4}",
            Hours[hi], vv.n, on == 1 ? "on" : "off", UnityStats.triangles, UnityStats.batches, UnityStats.setPassCalls));
        Shot(cam, vv.eye, vv.look, 60f, SHOT + vv.n + "_" + Hours[hi].ToString("00.00") + (on == 1 ? "_on" : "_off") + ".png");
        mWait = 0; mStep++;
    }

    internal static void Shot(Camera cam, Vector3 eye, Vector3 at, float fov, string path)
    {
        cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        const int W = 1280, H = 720;
        var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
