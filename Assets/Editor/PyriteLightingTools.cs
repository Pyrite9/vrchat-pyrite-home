// 라이팅 기반 정비 — 라이트 프로브 / 베이크 설정 / 리플렉션 프로브 (에디터 전용)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteLightingTools
{
    // 월드 상수 — h2.py / 지형 생성과 동일
    static readonly Vector2 LAKE_C = new Vector2(0f, -14f);
    const float LAKE_R = 56f;
    static readonly Vector2 CAMP_C = new Vector2(-10f, 52f);
    const float BASIN_R = 77f;          // 이 밖은 절벽 사면
    const float WATER_Y = 0f;

    static Terrain FindTerrain()
    {
        var t = Terrain.activeTerrain;
        if (t != null) return t;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        { var x = r.GetComponentInChildren<Terrain>(); if (x != null) return x; }
        return null;
    }

    static GameObject EnsureRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) return r;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "create " + name);
        return go;
    }

    // ==================================================================
    [MenuItem("Tools/Pyrite/B. Place Light Probes &1")]
    public static void PlaceLightProbes()
    {
        var terrain = FindTerrain();
        if (terrain == null) { Debug.LogError("[Pyrite] Terrain 못 찾음"); return; }
        float baseY = terrain.transform.position.y;

        var pts = new List<Vector3>();
        var seen = new HashSet<long>();

        void Add(float x, float z, float[] offsets)
        {
            // 중복 제거 (0.5m 격자로 양자화)
            long key = ((long)Mathf.RoundToInt(x * 2f) << 20) ^ (long)Mathf.RoundToInt(z * 2f);
            if (!seen.Add(key)) return;
            float h = terrain.SampleHeight(new Vector3(x, 0f, z)) + baseY;
            foreach (var o in offsets) pts.Add(new Vector3(x, h + o, z));
        }

        float[] LAND  = { 0.4f, 1.7f, 4.0f, 9.0f };
        float[] CAMP  = { 0.4f, 1.2f, 2.0f, 3.2f, 6.0f };
        float[] SHORE = { 0.4f, 1.7f, 4.0f };

        // 1) 캠프 주변 조밀 (3m) — 모닥불·랜턴 빛이 아바타에 물들게
        for (float x = CAMP_C.x - 13f; x <= CAMP_C.x + 13f; x += 3f)
            for (float z = CAMP_C.y - 13f; z <= CAMP_C.y + 13f; z += 3f)
            {
                if (new Vector2(x - CAMP_C.x, z - CAMP_C.y).magnitude > 13f) continue;
                Add(x, z, CAMP);
            }

        // 2) 뭍 (7m) — 걸어다니는 영역 전부
        for (float x = -BASIN_R; x <= BASIN_R; x += 7f)
            for (float z = -BASIN_R; z <= BASIN_R; z += 7f)
            {
                float R = new Vector2(x, z).magnitude;
                if (R > BASIN_R) continue;
                float rl = new Vector2(x - LAKE_C.x, z - LAKE_C.y).magnitude;
                if (rl < LAKE_R - 2f) continue;                 // 호수는 아래에서 따로
                Add(x, z, LAND);
            }

        // 3) 물가 띠 (4m) — 밝기가 가장 급격히 변하는 곳
        for (float a = 0f; a < 360f; a += 2.2f)
            for (float rr = LAKE_R - 4f; rr <= LAKE_R + 8f; rr += 4f)
            {
                float rad = a * Mathf.Deg2Rad;
                float x = LAKE_C.x + rr * Mathf.Sin(rad);
                float z = LAKE_C.y + rr * Mathf.Cos(rad);
                if (new Vector2(x, z).magnitude > BASIN_R) continue;
                Add(x, z, SHORE);
            }

        // 4) 호수 — 수영/수중용. 수면 위아래로 얇게
        for (float x = -BASIN_R; x <= BASIN_R; x += 12f)
            for (float z = -BASIN_R; z <= BASIN_R; z += 12f)
            {
                float rl = new Vector2(x - LAKE_C.x, z - LAKE_C.y).magnitude;
                if (rl >= LAKE_R - 2f) continue;
                long key = ((long)Mathf.RoundToInt(x * 2f) << 20) ^ (long)Mathf.RoundToInt(z * 2f);
                if (!seen.Add(key)) continue;
                float bottom = terrain.SampleHeight(new Vector3(x, 0f, z)) + baseY;
                pts.Add(new Vector3(x, bottom + 0.6f, z));       // 바닥 위
                pts.Add(new Vector3(x, WATER_Y - 1.2f, z));      // 수면 아래
                pts.Add(new Vector3(x, WATER_Y + 1.8f, z));      // 수면 위
            }

        var root = EnsureRoot("LightProbes");
        root.transform.position = Vector3.zero;
        var grp = root.GetComponent<LightProbeGroup>();
        if (grp == null) grp = Undo.AddComponent<LightProbeGroup>(root);
        Undo.RecordObject(grp, "probes");
        grp.probePositions = pts.ToArray();

        EditorUtility.SetDirty(grp);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        float ymin = pts.Min(p => p.y), ymax = pts.Max(p => p.y);
        Debug.Log(string.Format("[Pyrite] 라이트 프로브 {0}개 배치 (y {1:0.0} ~ {2:0.0})", pts.Count, ymin, ymax));
    }

    // ==================================================================
    [MenuItem("Tools/Pyrite/C. Apply Bake Settings &2")]
    public static void ApplyBakeSettings()
    {
        if (!AssetDatabase.IsValidFolder("Assets/TerrainAssets"))
            AssetDatabase.CreateFolder("Assets", "TerrainAssets");

        const string path = "Assets/TerrainAssets/PyriteLighting.lightingsettings";
        var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if (ls == null) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, path); }

        ls.name = "PyriteLighting";
        ls.bakedGI = true;
        ls.realtimeGI = false;
        ls.mixedBakeMode = MixedLightingMode.Shadowmask;
        ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        ls.prioritizeView = true;

        ls.lightmapResolution = 18f;          // 40 → 18 (아틀라스를 키웠으니 낮춰도 실효 밀도가 균일해진다)
        ls.lightmapPadding = 4;
        ls.lightmapMaxSize = 2048;            // 1024 → 2048
        ls.compressLightmaps = true;

        ls.ao = true;                          // ★ 꺼져 있었음
        ls.aoMaxDistance = 1.2f;
        ls.aoExponentIndirect = 1.0f;
        ls.aoExponentDirect = 0.4f;

        ls.directSampleCount = 64;
        ls.indirectSampleCount = 512;
        ls.environmentSampleCount = 512;
        ls.maxBounces = 2;
        ls.denoiserTypeDirect = LightingSettings.DenoiserType.Optix;
        ls.denoiserTypeIndirect = LightingSettings.DenoiserType.Optix;
        ls.denoiserTypeAO = LightingSettings.DenoiserType.Optix;
        ls.filteringMode = LightingSettings.FilterMode.Auto;

        EditorUtility.SetDirty(ls);
        AssetDatabase.SaveAssets();
        Lightmapping.lightingSettings = ls;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pyrite] 베이크 설정 적용 — AO ON(dist 1.2) / 아틀라스 2048 / 해상도 18 / Shadowmask / GPU");
    }

    // ==================================================================
    [MenuItem("Tools/Pyrite/D. Add Camp Reflection Probe &3")]
    public static void AddCampProbe()
    {
        var go = EnsureRoot("RP_Camp");
        go.transform.position = new Vector3(-10f, 3.5f, 52f);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var p = go.GetComponent<ReflectionProbe>();
        if (p == null) p = Undo.AddComponent<ReflectionProbe>(go);
        Undo.RecordObject(p, "rp camp");
        p.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
        p.resolution = 128;
        p.boxProjection = true;
        p.size = new Vector3(28f, 16f, 28f);
        p.center = Vector3.zero;
        p.importance = 2;                      // 큰 프로브보다 우선
        p.nearClipPlane = 0.3f;
        p.farClipPlane = 220f;
        p.cullingMask = ~0;
        p.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;

        EditorUtility.SetDirty(p);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pyrite] RP_Camp 추가 (-10, 3.5, 52) box 28x16x28 importance 2");
    }

    // ==================================================================
    [MenuItem("Tools/Pyrite/E. Fix Overlapping Lightmap UVs &4")]
    public static void FixOverlappingUVs()
    {
        string[] roots = { "Assets/Noagami", "Assets/Meshes", "Assets/Flora" };
        var guids = AssetDatabase.FindAssets("t:Model", roots);
        int n = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null || !mi.generateSecondaryUV) continue;
            if (mi.secondaryUVPackMargin >= 24) continue;
            mi.secondaryUVPackMargin = 24;          // 12 → 24
            mi.secondaryUVHardAngle = 88;
            mi.secondaryUVAngleDistortion = 6;
            mi.secondaryUVAreaDistortion = 12;
            mi.SaveAndReimport();
            n++;
        }
        Debug.Log("[Pyrite] 라이트맵 UV 패킹 마진 상향: 모델 " + n + "개 (검사 " + guids.Length + "개)");
    }
}
#endif
