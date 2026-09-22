// TS 꽃 타일을 지형에 맞춰 구운 메시로 깐다 (에디터 전용)
//  - 타일은 11.26m 평면. 그대로 놓으면 기복에서 뜬다 → 정점 Y를 지형 높이로 보정
//  - 삼각형 단위로 밀도 마스크(FlowerDensity.bin)를 적용 → 가장자리가 자연스럽게 해짐
//  - 40m 블록 × 꽃 종류별로 메시를 쪼개서 프러스텀 컬링이 먹게 한다
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteFloraField
{
    const string OUT_MESH = "Assets/Flora/Generated/";
    const float  CELL     = 10.0f;     // 타일 격자 (메시 11.26m라 1.26m 겹침)
    const float  BLOCK    = 40.0f;     // 메시 분할 블록
    const float  COV_FULL = 1.35f;     // 이 커버리지에서 삼각형 100% 유지

    // ── 종 비율 ──────────────────────────────────────────────────────────
    // 세 종은 같은 TFlower 메시를 쓰고 텍스처(머티리얼)만 다르다.
    //   0 = Nemophila  큼직한 납작 꽃, 풀이 없어 파란 판때기처럼 보인다
    //   1 = Myosotis   잔잔한 물망초 + 풀잎. 지면을 촘촘히 덮는다
    //   2 = Dandelion  민들레 솜털(흰 점) — 액센트용
    // SpeciesNoise(0..1)를 이 두 경계로 잘라 쓴다. 경계만 바꾸면 비율이 바뀐다.
    //   네모필라 통일(현재): 0.00 / 0.00   → 네모필라 100%
    //   섞기(v2였던 것):    0.10 / 0.42   → 민들레 10%, 물망초 32%, 네모필라 58%
    //   물망초 위주:        0.12 / 1.00   → 민들레 12%, 물망초 88%
    //   민들레 액센트 추가:  0.10 / 0.10   → 민들레 10%, 네모필라 90%
    const float  SN_DANDELION = 0.00f;
    const float  SN_MYOSOTIS  = 0.00f;

    struct Proto { public string name; public Mesh mesh; public Material mat; public float weight; }


    /// 벤더 Day 재질을 Assets/Flora로 복제해서 우리 씬에 맞게 조정한다.
    /// 핵심은 ENABLE_LIGHTPROBE — 벤더 기본값이 꺼져 있어서 꽃이 씬 조명을 전혀 안 받는다.
    static Material TuneMaterial(Material src, string outName)
    {
        string path = "Assets/Flora/" + outName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(src);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = src.shader;
        m.CopyPropertiesFromMaterial(src);

        m.SetFloat("_EnaLgtPrb", 1f);
        m.EnableKeyword("ENABLE_LIGHTPROBE");     // ★ 씬 조명(라이트 프로브)을 받게
        m.SetFloat("_EnaSha", 1f);                // 구름 그림자 유지
        m.EnableKeyword("ENABLE_SHADOW");
        m.SetColor("_MainColor", new Color(0.66f, 0.66f, 0.66f, 1f));  // 0.8 → 살짝 낮춤
        m.enableInstancing = true;
        m.renderQueue = -1;
        EditorUtility.SetDirty(m);
        return m;
    }

    static float SpeciesNoise(float x, float z)
    {
        float n = 0.58f * Mathf.Sin(x * 0.045f + 1.3f) * Mathf.Cos(z * 0.039f - 0.7f)
                + 0.30f * Mathf.Sin(x * 0.083f - 2.1f) * Mathf.Sin(z * 0.071f + 0.9f)
                + 0.12f * Mathf.Sin(x * 0.161f + 0.4f) * Mathf.Cos(z * 0.149f + 2.2f);
        return Mathf.Clamp01((n + 1f) * 0.5f);
    }

    static float[,] LoadCoverage(out int res)
    {
        var p = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FlowerDensity.bin");
        res = 0;
        if (!File.Exists(p)) { Debug.LogError("[Pyrite] 없음: " + p); return null; }
        var by = File.ReadAllBytes(p);
        res = System.BitConverter.ToInt32(by, 0);
        if (by.Length != 4 + res * res * 2) { Debug.LogError("[Pyrite] bin 크기 불일치"); return null; }
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

    static float Sample(float[,] cov, int res, float x, float z)
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

    [MenuItem("Tools/Pyrite/I. Build Flower Field Meshes &#0")]
    public static void Build()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            { var t = r.GetComponentInChildren<Terrain>(); if (t != null) { terrain = t; break; } }
        if (terrain == null) { Debug.LogError("[Pyrite] Terrain 못 찾음"); return; }
        float baseY = terrain.transform.position.y;

        int res; var cov = LoadCoverage(out res);
        if (cov == null) return;

        var names = new[] { "P_TS_Nemophila", "P_TS_Myosotis", "P_TS_Dandelion" };
        var weights = new[] { 0.62f, 0.30f, 0.08f };
        var protos = new List<Proto>();
        for (int i = 0; i < names.Length; i++)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Flora/" + names[i] + ".prefab");
            if (pf == null) { Debug.LogError("[Pyrite] 프로토타입 없음: " + names[i]); return; }
            var mf = pf.GetComponent<MeshFilter>(); var mr = pf.GetComponent<MeshRenderer>();
            var tuned = TuneMaterial(mr.sharedMaterial, "M_TS_" + names[i].Substring(5) + "_Day");
            protos.Add(new Proto { name = names[i], mesh = mf.sharedMesh, mat = tuned, weight = weights[i] });
        }

        if (!AssetDatabase.IsValidFolder("Assets/Flora/Generated"))
            AssetDatabase.CreateFolder("Assets/Flora", "Generated");
        foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Flora/Generated" }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));

        // 씬 루트 정리
        GameObject root = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == "FlowerField") root = r;
        if (root != null) Undo.DestroyObjectImmediate(root);
        root = new GameObject("FlowerField");
        Undo.RegisterCreatedObjectUndo(root, "flower field");

        // 블록 × 종류별 버퍼
        var vbuf = new Dictionary<string, List<Vector3>>();
        var nbuf = new Dictionary<string, List<Vector3>>();
        var ubuf = new Dictionary<string, List<Vector2>>();
        var cbuf = new Dictionary<string, List<Color32>>();
        var ibuf = new Dictionary<string, List<int>>();
        var pIdx = new Dictionary<string, int>();

        var rng = new System.Random(20260922);
        int cells = 0; long tris = 0;

        for (float cz = -100f; cz < 100f; cz += CELL)
            for (float cx = -100f; cx < 100f; cx += CELL)
            {
                float ccx = cx + CELL * 0.5f, ccz = cz + CELL * 0.5f;
                if (Sample(cov, res, ccx, ccz) < 0.04f) continue;

                // 세 종류가 같은 TFlower 메시를 공유하므로, 삼각형 단위로 재질만 나눈다
                var pr = protos[0];
                int quarter = rng.Next(4);
                Quaternion rot = Quaternion.Euler(0f, 90f * quarter, 0f);

                var mv = pr.mesh.vertices; var mn = pr.mesh.normals;
                var mu = pr.mesh.uv; var mc = pr.mesh.colors32;
                var mt = pr.mesh.triangles;
                bool hasN = mn != null && mn.Length == mv.Length;
                bool hasU = mu != null && mu.Length == mv.Length;
                bool hasC = mc != null && mc.Length == mv.Length;

                int bx = Mathf.FloorToInt((ccx + 100f) / BLOCK);
                int bz = Mathf.FloorToInt((ccz + 100f) / BLOCK);
                var remap = new Dictionary<string, Dictionary<int, int>>();

                for (int t = 0; t < mt.Length; t += 3)
                {
                    Vector3 a = rot * mv[mt[t]], b = rot * mv[mt[t + 1]], c = rot * mv[mt[t + 2]];
                    float wx = ccx + (a.x + b.x + c.x) / 3f;
                    float wz = ccz + (a.z + b.z + c.z) / 3f;
                    float k = Sample(cov, res, wx, wz) / COV_FULL;
                    if (k < 0.02f) continue;
                    if (k < 1f && rng.NextDouble() > k) continue;

                    // 종류: 저주파 노이즈로 부드럽게 섞인다 (블록 경계가 안 보이게)
                    float sn = SpeciesNoise(wx, wz);
                    int pi = sn < SN_DANDELION ? 2 : (sn < SN_MYOSOTIS ? 1 : 0);
                    string key = protos[pi].name + "_B" + bx + "_" + bz;
                    if (!vbuf.ContainsKey(key))
                    {
                        vbuf[key] = new List<Vector3>(); nbuf[key] = new List<Vector3>();
                        ubuf[key] = new List<Vector2>(); cbuf[key] = new List<Color32>();
                        ibuf[key] = new List<int>(); pIdx[key] = pi;
                    }
                    if (!remap.ContainsKey(key)) remap[key] = new Dictionary<int, int>();
                    var rm = remap[key];

                    for (int e = 0; e < 3; e++)
                    {
                        int src = mt[t + e];
                        int dst;
                        if (!rm.TryGetValue(src, out dst))
                        {
                            Vector3 lp = rot * mv[src];
                            float px = ccx + lp.x, pz = ccz + lp.z;
                            float h = terrain.SampleHeight(new Vector3(px, 0f, pz)) + baseY;
                            dst = vbuf[key].Count;
                            vbuf[key].Add(new Vector3(px, h + lp.y, pz));
                            nbuf[key].Add(hasN ? rot * mn[src] : Vector3.up);
                            ubuf[key].Add(hasU ? mu[src] : Vector2.zero);
                            cbuf[key].Add(hasC ? mc[src] : new Color32(255, 255, 255, 255));
                            rm[src] = dst;
                        }
                        ibuf[key].Add(dst);
                    }
                    tris++;
                }
                cells++;
            }

        int made = 0;
        foreach (var kv in vbuf)
        {
            if (kv.Value.Count == 0) continue;
            var m = new Mesh();
            m.indexFormat = IndexFormat.UInt32;
            m.name = kv.Key;
            m.SetVertices(kv.Value);
            m.SetNormals(nbuf[kv.Key]);
            m.SetUVs(0, ubuf[kv.Key]);
            m.SetColors(cbuf[kv.Key]);
            m.SetTriangles(ibuf[kv.Key], 0);
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, OUT_MESH + kv.Key + ".asset");

            var go = new GameObject(kv.Key);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = protos[pIdx[kv.Key]].mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;   // 라이트맵 대신 프로브
            // BatchingStatic은 켜지 않는다 — 스태틱 배치에 들어간 메시는
            // ParticleSystem의 메시 방출(반딧불) 소스로 못 쓴다고 Unity가 경고한다.
            // 타일이 이미 40m 블록으로 합쳐져 있어서 배칭 이득도 거의 없다.
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            made++;
        }

        // 터레인 디테일(내가 만든 카드) 끈다
        var td = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "clear details");
        td.detailPrototypes = new DetailPrototype[0];

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(string.Format("[Pyrite] 꽃밭 메시 생성 — 타일 {0}칸 / 삼각형 {1} / 메시 {2}개. 터레인 디테일 제거됨",
            cells, tris, made));
    }
}
#endif
