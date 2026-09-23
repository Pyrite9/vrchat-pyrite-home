// Tools ▸ Pyrite ▸ Y7. Inner Terraces (low columns)
//  외벽 안쪽, 꽃이 없는 띠(절벽 발치 ~R74~79)에 낮은 주상절리를 계단식으로 채운다.
//  기존 벽은 가장 낮은 기둥도 6.5m 이상이라 땅에서 바로 벽이 솟아 "꽉 막힌" 느낌 → 앞줄 0.3~1.2m 에서 벽 쪽으로 올라가게.
//   · 격자는 기존 절벽과 같은 육각 격자(외접 1.1m, 행 1.65m) — 기존 벽(R≥79) 바로 앞 칸부터 안쪽으로 이어 붙인다.
//     (OBJ 임포트의 X 반전을 거쳐도 이 격자는 자기 자신으로 겹친다 → 월드 좌표로 바로 계산해도 정합)
//   · 꽃(FlowerDensity.bin)·물·씬 오브젝트(랜드마크 등) 자리는 비운다. 벽에서 이어진 칸만 남긴다(BFS).
//   · 높이 = 앞(0.4~1.4m) → 뒤(그 방향 벽 높이의 ~62%, 3~16m), 0.55m 단으로 끊어 계단 느낌 + 가끔 솟은 기둥
//   · 충돌: 닫힌 육각기둥. 1.3m 넘는 기둥은 콜라이더를 60m 까지 올려 벽 위로 기어오르지 못하게(앞줄 낮은 돌만 밟힘)
//   · 황철석: 가려지는 절벽 결정은 위로 올리거나 앞 기둥 위에 얹고, 기둥 자리에 묻힌 지상 결정은 앞쪽으로 뺀다.
//            새 계단에 작은 광맥 몇 개를 더한다.
//  재실행 안전(PyriteTerraces 루트 재생성, 결정 메시는 원본 OBJ 에서 다시 계산). 라이트맵은 다시 구워야 한다.
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

public static class PyriteTerraces
{
    const float HR = 1.10f;                               // 외접 반경
    static readonly float HW = Mathf.Sqrt(3f) * HR;       // 가로 간격
    const float ROWS = 1.5f * HR;                         // 행 간격
    const float R_WALL = 79f, R_MIN = 64f;
    const float FLOWER_CLEAR = 0.6f;                      // 칸 중심에서 이 거리 안에 꽃이 있으면 제외
    const int SEG = 8;
    const float TERR_LM = 0.12f;                          // 절벽 대비 라이트맵 배율 — 절벽은 1024 한 장에 눌려 있어 같은 배율이면 계단이 4장을 먹었다
    const string OUTDIR = "Assets/Meshes/Terraces/";
    const string ROOT = "PyriteTerraces";

    class Cell { public int i, j; public float x, z, R, th, g, gmax, gmin, h, top, t; public bool keep; }

    static Terrain T;
    static float G(float x, float z) => T.SampleHeight(new Vector3(x, 0f, z)) + T.transform.position.y;
    static float Ang(float x, float z) { float a = Mathf.Atan2(x, z) * Mathf.Rad2Deg; return a < 0 ? a + 360f : a; }
    static float AngDiff(float a, float b) { float d = Mathf.Abs(a - b) % 360f; return d > 180f ? 360f - d : d; }

    [MenuItem("Tools/Pyrite/Y7. Inner Terraces (low columns)", false, 297)]
    public static void Run()
    {
        var log = new StringBuilder("[Y7] ");
        T = Terrain.activeTerrain;
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var cliffs = roots.FirstOrDefault(g => g.name == "PyriteCliffs_Visual");
        var crystals = roots.FirstOrDefault(g => g.name == "Crystals");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (T == null || cliffs == null || crystals == null || tod == null) { Debug.LogError("[Y7] Terrain/PyriteCliffs_Visual/Crystals/ToD 없음"); return; }
        var rnd = new System.Random(20260923);
        float Rf(double a, double b) => (float)(a + (b - a) * rnd.NextDouble());

        // ── 꽃 마스크
        byte[] fb = File.ReadAllBytes("FlowerDensity.bin");
        int DR = (int)System.BitConverter.ToUInt32(fb, 0);
        float cellW = 200f / DR;
        bool FlowerNear(float x, float z, float r)
        {
            int n = Mathf.CeilToInt(r / cellW) + 1;
            int cx = Mathf.FloorToInt((x + 100f) / cellW), cz = Mathf.FloorToInt((z + 100f) / cellW);
            for (int dz = -n; dz <= n; dz++)
                for (int dx = -n; dx <= n; dx++)
                {
                    int ix = cx + dx, iz = cz + dz;
                    if (ix < 0 || iz < 0 || ix >= DR || iz >= DR) continue;
                    float px = -100f + (ix + 0.5f) * cellW, pz = -100f + (iz + 0.5f) * cellW;
                    if ((px - x) * (px - x) + (pz - z) * (pz - z) > r * r) continue;
                    int o = 4 + (iz * DR + ix) * 2;
                    if (fb[o] + fb[o + 1] > 0) return true;
                }
            return false;
        }

        // ── 기존 벽의 방향별 높이 (가장 안쪽 줄의 꼭대기)
        var wallTop = new float[360];
        foreach (var mf in cliffs.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v0 in mf.sharedMesh.vertices)
            {
                var v = m.MultiplyPoint3x4(v0);
                float R = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (R > 80.8f || v.y < 0f) continue;
                int b = Mathf.Clamp((int)Ang(v.x, v.z), 0, 359);
                if (v.y > wallTop[b]) wallTop[b] = v.y;
            }
        }
        for (int pass = 0; pass < 4; pass++)
            for (int b = 0; b < 360; b++)
                if (wallTop[b] <= 0f) wallTop[b] = Mathf.Max(wallTop[(b + 359) % 360], wallTop[(b + 1) % 360]);
        log.Append("wall ").Append(wallTop.Min().ToString("F1")).Append("~").Append(wallTop.Max().ToString("F1")).Append("m | ");

        // ── 비워 둘 씬 오브젝트 (랜드마크·소품 등)
        string[] skipRoots = { "PyriteCliffs_Visual", "FlowerField", "Water", "SkyDiscs", "LightFX", ROOT, "Terrain" };
        var excl = new List<Bounds>(); var exclNames = new List<string>();
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (skipRoots.Contains(r.transform.root.name)) continue;
            bool crystalMesh = false;
            for (var p = r.transform; p != null; p = p.parent) if (p.name.StartsWith("PyriteIn") || p.name.StartsWith("PyriteAccent")) crystalMesh = true;
            if (crystalMesh) continue;
            var b = r.bounds;
            if (b.extents.x > 30f || b.extents.z > 30f) continue;
            float R = Mathf.Sqrt(b.center.x * b.center.x + b.center.z * b.center.z);
            if (R < 58f || R > 82f) continue;
            b.Expand(new Vector3(2.4f, 0f, 2.4f));
            excl.Add(b); exclNames.Add(r.name);
        }
        log.Append("exclude[").Append(string.Join(",", exclNames.Distinct())).Append("] | ");
        bool Excluded(float x, float z) { foreach (var b in excl) if (x > b.min.x && x < b.max.x && z > b.min.z && z < b.max.z) return true; return false; }

        // ── 후보 칸
        var cells = new Dictionary<long, Cell>();
        long Key(int i, int j) => ((long)i << 32) ^ (uint)j;
        int im = Mathf.CeilToInt(R_WALL / HW) + 2, jm = Mathf.CeilToInt(R_WALL / ROWS) + 2;
        for (int j = -jm; j <= jm; j++)
            for (int i = -im; i <= im; i++)
            {
                float x = HW * (i + 0.5f * (j & 1)), z = ROWS * j;
                float R = Mathf.Sqrt(x * x + z * z);
                if (R >= R_WALL || R < R_MIN) continue;
                float g = G(x, z);
                float rl = Mathf.Sqrt(x * x + (z + 14f) * (z + 14f));
                if (g < 0.25f || rl < 57.5f) continue;
                if (FlowerNear(x, z, FLOWER_CLEAR) || Excluded(x, z)) continue;
                var c = new Cell { i = i, j = j, x = x, z = z, R = R, th = Ang(x, z), g = g };
                cells[Key(i, j)] = c;
            }
        // 벽에서 이어진 칸만 (BFS)
        var q = new Queue<Cell>();
        foreach (var c in cells.Values) if (c.R >= R_WALL - 1.9f) { c.keep = true; q.Enqueue(c); }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            int o = c.j & 1;
            var nb = new[] { (c.i - 1, c.j), (c.i + 1, c.j), (c.i - 1 + o, c.j - 1), (c.i + o, c.j - 1), (c.i - 1 + o, c.j + 1), (c.i + o, c.j + 1) };
            foreach (var (ni, nj) in nb)
            {
                Cell n; if (!cells.TryGetValue(Key(ni, nj), out n) || n.keep) continue;
                n.keep = true; q.Enqueue(n);
            }
        }
        var kept = cells.Values.Where(c => c.keep).ToList();

        // 방향별 앞줄 반경
        var front = new float[360];
        for (int b = 0; b < 360; b++) front[b] = R_WALL;
        foreach (var c in kept)
            for (int d = -3; d <= 3; d++) { int b = ((int)c.th + d + 360) % 360; if (c.R < front[b]) front[b] = c.R; }

        // 앞줄 가장자리를 들쭉날쭉하게 — 앞 25% 칸을 일부 뺀다
        foreach (var c in kept)
        {
            float fr = front[(int)c.th % 360];
            c.t = (R_WALL - fr) < 1.0f ? 1f : Mathf.Clamp01((c.R - fr) / (R_WALL - fr));
        }
        kept = kept.Where(c => !(c.t < 0.22f && rnd.NextDouble() < 0.33)).ToList();

        // ── 높이
        float Low(float th) { float a = th * Mathf.Deg2Rad; return 0.55f * Mathf.Sin(3f * a + 1.3f) + 0.30f * Mathf.Sin(7f * a + 0.2f) + 0.15f * Mathf.Sin(13f * a + 2.9f); }
        foreach (var c in kept)
        {
            float g0 = float.MaxValue, g1 = float.MinValue;
            for (int k = 0; k < 6; k++)
            {
                float a = (90f + 60f * k) * Mathf.Deg2Rad;
                float gy = G(c.x + HR * Mathf.Cos(a), c.z + HR * Mathf.Sin(a));
                g0 = Mathf.Min(g0, gy); g1 = Mathf.Max(g1, gy);
            }
            c.gmin = g0; c.gmax = g1;
            float hw = wallTop[(int)c.th % 360] - c.gmax;
            float hback = Mathf.Clamp(0.62f * hw * (0.8f + 0.4f * Low(c.th)), 3.0f, 16f);
            float hfront = Rf(0.4, 1.4);
            float h = Mathf.Lerp(hfront, hback, c.t) + Rf(-0.4, 0.4);
            h = Mathf.Round(h / 0.55f) * 0.55f + Rf(-0.08, 0.08);
            if (c.t > 0.3f && rnd.NextDouble() < 0.04) h += Rf(1.5, 3.5);
            h = Mathf.Clamp(h, 0.3f, Mathf.Max(0.5f, hw - 1.5f));
            c.h = h; c.top = c.gmax + h;
        }
        log.Append("cells ").Append(kept.Count).Append(" (front R ").Append(kept.Min(c => c.R).ToString("F1")).Append(") | ");

        // ── 루트 재생성
        var old = roots.FirstOrDefault(g => g.name == ROOT);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(ROOT); Undo.RegisterCreatedObjectUndo(root, "terraces");
        Directory.CreateDirectory(OUTDIR);

        var cliffR = cliffs.GetComponentsInChildren<MeshRenderer>(true).First();
        var cliffMat = cliffR.sharedMaterial;
        var flags = GameObjectUtility.GetStaticEditorFlags(cliffR.gameObject);
        float lmScale = new SerializedObject(cliffR).FindProperty("m_ScaleInLightmap").floatValue;

        // 이웃 조회 — 옆면이 이웃 기둥에 가려지는 만큼은 만들지 않는다 (라이트맵 면적·삼각형 절약)
        var byKey = kept.ToDictionary(c => Key(c.i, c.j));
        float NeighborTop(Cell c, int side)
        {
            float a = (120f + 60f * side) * Mathf.Deg2Rad;
            float nx = c.x + HW * Mathf.Cos(a), nz = c.z + HW * Mathf.Sin(a);
            if (Mathf.Sqrt(nx * nx + nz * nz) >= R_WALL) return float.MaxValue;     // 기존 벽
            int nj = Mathf.RoundToInt(nz / ROWS);
            int ni = Mathf.RoundToInt(nx / HW - 0.5f * (nj & 1));
            Cell n; return byKey.TryGetValue(Key(ni, nj), out n) ? n.top : float.MinValue;
        }

        // ── 시각 메시 (8구간)
        var newRenderers = new List<Renderer>();
        int triTotal = 0;
        for (int s = 0; s < SEG; s++)
        {
            var part = kept.Where(c => (int)(c.th / (360f / SEG)) == s).ToList();
            if (part.Count == 0) continue;
            var mb = new MB();
            foreach (var c in part) Column(mb, c, rnd, false, 0f, k => NeighborTop(c, k));
            var mesh = mb.Build("PyriteTerrace_Seg" + (s + 1).ToString("00"));
            Unwrapping.GenerateSecondaryUVSet(mesh);
            SaveMesh(mesh, OUTDIR + mesh.name + ".asset");
            triTotal += mesh.triangles.Length / 3;
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OUTDIR + mesh.name + ".asset");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cliffMat;
            mr.shadowCastingMode = cliffR.shadowCastingMode; mr.receiveGI = ReceiveGI.Lightmaps;
            var so = new SerializedObject(mr); so.FindProperty("m_ScaleInLightmap").floatValue = lmScale * TERR_LM; so.ApplyModifiedPropertiesWithoutUndo();
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            newRenderers.Add(mr);
        }
        log.Append("tris ").Append(triTotal).Append(" | ");

        // ── 충돌
        {
            var mb = new MB();
            foreach (var c in kept) Column(mb, c, rnd, true, c.h > 1.3f ? 60f : 0f, null);
            var mesh = mb.Build("PyriteTerrace_Collider");
            SaveMesh(mesh, OUTDIR + mesh.name + ".asset");
            var go = new GameObject("PyriteTerrace_Collider");
            go.transform.SetParent(root.transform, false);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OUTDIR + mesh.name + ".asset");
            var refCol = Object.FindObjectsOfType<MeshCollider>().FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.name.Contains("Collider") && x.transform.root.name != ROOT);
            if (refCol != null) go.layer = refCol.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            log.Append("collider layer ").Append(LayerMask.LayerToName(go.layer)).Append(" | ");
        }

        // ── 황철석
        int movedCliff = 0, perched = 0, movedGround = 0;
        // 앞쪽 기둥 조회
        List<Cell> Near(Vector3 p, float lateral, float r0, float r1)
        {
            float R = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            var dir = new Vector2(p.x / R, p.z / R);
            return kept.Where(c =>
            {
                float along = c.x * dir.x + c.z * dir.y;
                float lat = Mathf.Abs(-c.x * dir.y + c.z * dir.x);
                return lat < lateral && R - along > r0 && R - along < r1;
            }).ToList();
        }
        foreach (var mf in crystals.GetComponentsInChildren<MeshFilter>(true))
        {
            string grp = null;
            for (var p = mf.transform; p != null; p = p.parent)
                if (p.name.StartsWith("PyriteIn") || p.name.StartsWith("PyriteAccent")) { grp = p.name; break; }
            if (grp == null) continue;
            bool cliffGroup = grp == "PyriteInCliff";
            EditCubes(mf, grp, (c, e) =>
            {
                if (cliffGroup)
                {
                    var fr = Near(c, 2.2f, -0.5f, 4.5f);
                    if (fr.Count == 0) return null;
                    float maxTop = fr.Max(x => x.top);
                    if (maxTop < c.y - e * 0.35f) return null;
                    float y = maxTop + e * 0.55f + 0.8f;
                    float wt = wallTop[(int)Ang(c.x, c.z) % 360];
                    if (y + e * 0.9f < wt) { movedCliff++; return new Vector3(0f, y - c.y, 0f); }
                    var hi = fr.OrderByDescending(x => x.top).First();
                    perched++;
                    return new Vector3(hi.x, hi.top + e * 0.15f, hi.z) - c;
                }
                else
                {
                    if (!kept.Any(k => (k.x - c.x) * (k.x - c.x) + (k.z - c.z) * (k.z - c.z) < (HR + e * 0.6f) * (HR + e * 0.6f))) return null;
                    float R = Mathf.Sqrt(c.x * c.x + c.z * c.z);
                    float fr = front[(int)Ang(c.x, c.z) % 360];
                    float nr = Mathf.Min(R, fr - HR - e * 0.6f - 0.3f);
                    var n = new Vector3(c.x / R * nr, 0f, c.z / R * nr);
                    n.y = c.y - G(c.x, c.z) + G(n.x, n.z);
                    movedGround++;
                    return n - c;
                }
            });
        }
        log.Append("crystals: cliff raised ").Append(movedCliff).Append(" perched ").Append(perched).Append(" ground moved ").Append(movedGround).Append(" | ");

        // 새 광맥 — 계단 위·앞면
        {
            var mb = new MB();
            var pockets = new List<Cell>();
            foreach (var c in kept.OrderBy(_ => rnd.NextDouble()))
            {
                if (c.t < 0.3f || c.t > 0.9f || c.h < 1.5f) continue;
                if (pockets.Any(p => (p.x - c.x) * (p.x - c.x) + (p.z - c.z) * (p.z - c.z) < 22f * 22f)) continue;
                pockets.Add(c);
                if (pockets.Count >= 9) break;
            }
            int cubes = 0;
            foreach (var p in pockets)
            {
                float R = p.R; var n = new Vector3(-p.x / R, 0f, -p.z / R);          // 호수 쪽
                var yawBase = Quaternion.Euler(0f, Rf(0, 360), 0f);
                // 주 결정 — 기둥 위에 반쯤 묻힘
                float e0 = Rf(0.8, 1.5);
                var r0 = yawBase * Quaternion.Euler(Rf(-25, 25), 0f, Rf(-25, 25));
                mb.Cube(new Vector3(p.x, p.top + e0 * 0.12f, p.z), e0, r0); cubes++;
                int nsmall = rnd.Next(1, 4);
                for (int k = 0; k < nsmall; k++)
                {
                    float e = Rf(0.35, 0.8);
                    var tg = new Vector3(n.z, 0f, -n.x) * Rf(-0.45, 0.45);
                    float y = Mathf.Lerp(p.gmax + 0.4f, p.top - e * 0.6f, (float)rnd.NextDouble());
                    var pos = new Vector3(p.x, y, p.z) + n * (HR * 0.87f - e * (0.5f - Rf(0.25, 0.4))) + tg;
                    mb.Cube(pos, e, yawBase * Quaternion.Euler(Rf(-12, 12), Rf(-12, 12), Rf(-12, 12))); cubes++;
                }
            }
            var mesh = mb.Build("PyriteOnTerrace");
            Unwrapping.GenerateSecondaryUVSet(mesh);
            SaveMesh(mesh, OUTDIR + mesh.name + ".asset");
            var go = new GameObject("PyriteOnTerrace");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OUTDIR + mesh.name + ".asset");
            var mr = go.AddComponent<MeshRenderer>();
            var cm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Pyrite_Cliff.mat");
            mr.sharedMaterial = cm;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            log.Append("new pockets ").Append(pockets.Count).Append(" cubes ").Append(cubes).Append(" | ");
        }

        // ── 라이트 프로브 — 새 기둥 안에 든 것 제거
        int removed = 0;
        foreach (var grp in Object.FindObjectsOfType<LightProbeGroup>())
        {
            var tr = grp.transform;
            var pos = grp.probePositions;
            var keep = pos.Where(lp =>
            {
                var w = tr.TransformPoint(lp);
                return !kept.Any(c => (c.x - w.x) * (c.x - w.x) + (c.z - w.z) * (c.z - w.z) < 1.15f * 1.15f && w.y < c.top + 0.15f);
            }).ToArray();
            if (keep.Length != pos.Length) { Undo.RecordObject(grp, "probes"); removed += pos.Length - keep.Length; grp.probePositions = keep; EditorUtility.SetDirty(grp); }
        }
        log.Append("probes removed ").Append(removed).Append(" | ");

        // ── 시간대: 밤 머티리얼 교체 대상에 편입
        var cr = (tod.cliffRenderers ?? new Renderer[0]).Where(r => r != null && r.transform.root.name != ROOT).ToList();
        cr.AddRange(newRenderers);
        tod.cliffRenderers = cr.ToArray();
        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(tod, false);
        tod.index = 0; tod.Apply();
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(log.ToString());
        Directory.CreateDirectory("Assets/_preview/");
        File.WriteAllText("Assets/_preview/terraces.txt", log.ToString());
        PyriteViews.CaptureSet("camp_lake camp_left lm1 lm2 north_wall west_wall", new[] { 0, 1 });
    }

    // ── 결정 메시(OBJ 병합본)를 큐브 단위로 나눠 옮긴다. 원본은 프리팹(OBJ) 쪽에서 매번 다시 읽는다.
    static void EditCubes(MeshFilter mf, string grp, System.Func<Vector3, float, Vector3?> mover)
    {
        var srcMf = PrefabUtility.GetCorrespondingObjectFromSource(mf);
        var src = srcMf != null ? srcMf.sharedMesh : mf.sharedMesh;
        if (src == null) return;
        if (!src.isReadable)
        {
            var mi = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(src)) as ModelImporter;
            if (mi != null) { mi.isReadable = true; mi.SaveAndReimport(); srcMf = PrefabUtility.GetCorrespondingObjectFromSource(mf); src = srcMf != null ? srcMf.sharedMesh : src; }
        }
        var m = Object.Instantiate(src); m.name = src.name + "_" + grp + "_Terr";
        var v = m.vertices; var tr = mf.transform;
        // 위치가 같은 정점끼리 묶어 큐브(연결 성분)를 찾는다 — 면마다 정점이 따로라 인덱스로는 안 이어진다
        int n = v.Length; var par = new int[n]; for (int k = 0; k < n; k++) par[k] = k;
        int Find(int a) { while (par[a] != a) { par[a] = par[par[a]]; a = par[a]; } return a; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) par[a] = b; }
        var byPos = new Dictionary<Vector3Int, int>();
        for (int k = 0; k < n; k++)
        {
            var key = Vector3Int.RoundToInt(v[k] * 1000f);
            int o; if (byPos.TryGetValue(key, out o)) Union(k, o); else byPos[key] = k;
        }
        var tris = m.triangles;
        for (int k = 0; k < tris.Length; k += 3) { Union(tris[k], tris[k + 1]); Union(tris[k], tris[k + 2]); }
        var comps = new Dictionary<int, List<int>>();
        for (int k = 0; k < n; k++) { int r = Find(k); if (!comps.TryGetValue(r, out var l)) comps[r] = l = new List<int>(); l.Add(k); }
        foreach (var l in comps.Values)
        {
            var c = Vector3.zero; foreach (var k in l) c += tr.TransformPoint(v[k]); c /= l.Count;
            float rmax = 0f; foreach (var k in l) rmax = Mathf.Max(rmax, (tr.TransformPoint(v[k]) - c).magnitude);
            float e = rmax * 2f / Mathf.Sqrt(3f);
            var d = mover(c, e);
            if (d == null) continue;
            foreach (var k in l) v[k] = tr.InverseTransformPoint(tr.TransformPoint(v[k]) + d.Value);
        }
        m.vertices = v; m.RecalculateBounds();
        string path = OUTDIR + m.name + ".asset";
        Directory.CreateDirectory(OUTDIR);
        SaveMesh(m, path);
        Undo.RecordObject(mf, "crystal move");
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    static void SaveMesh(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(m, old); EditorUtility.SetDirty(old); }
        else AssetDatabase.CreateAsset(m, path);
    }

    // ── 육각기둥
    static void Column(MB mb, Cell c, System.Random rnd, bool closed, float colliderTop, System.Func<int, float> neighborTop)
    {
        float top = colliderTop > 0f ? colliderTop : c.top;
        float bot = c.gmin - 1.5f;
        float tx = (float)(rnd.NextDouble() * 0.1 - 0.05), tz = (float)(rnd.NextDouble() * 0.1 - 0.05);
        var B = new Vector3[6]; var U = new Vector3[6];
        for (int k = 0; k < 6; k++)
        {
            float a = (90f + 60f * k) * Mathf.Deg2Rad;
            float ox = HR * Mathf.Cos(a), oz = HR * Mathf.Sin(a);
            B[k] = new Vector3(c.x + ox, bot, c.z + oz);
            float jit = closed ? 0f : (float)(rnd.NextDouble() * 0.16 - 0.08);
            U[k] = new Vector3(c.x + ox, top + (closed ? 0f : tx * ox + tz * oz + jit), c.z + oz);
        }
        var ctr = new Vector3(c.x, (top + bot) * 0.5f, c.z);
        for (int k = 0; k < 6; k++)
        {
            int k1 = (k + 1) % 6;
            float v0 = bot / 35f, v1 = U[k].y / 35f, v2 = U[k1].y / 35f;
            var ow = (B[k] + B[k1]) * 0.5f - ctr; ow.y = 0f;
            if (neighborTop != null)
            {
                float nt = neighborTop(k);
                float faceTop = Mathf.Min(U[k].y, U[k1].y);
                if (nt >= faceTop - 0.02f) continue;                       // 완전히 가려짐
                float nb0 = Mathf.Max(bot, nt - 0.05f);                     // 이웃 위로 드러난 부분만
                var b0 = new Vector3(B[k].x, nb0, B[k].z); var b1 = new Vector3(B[k1].x, nb0, B[k1].z);
                float w0 = nb0 / 35f;
                mb.Quad(b0, b1, U[k1], U[k], new Vector2(0, w0), new Vector2(1, w0), new Vector2(1, v2), new Vector2(0, v1), ow);
                continue;
            }
            mb.Quad(B[k], B[k1], U[k1], U[k], new Vector2(0, v0), new Vector2(1, v0), new Vector2(1, v2), new Vector2(0, v1), ow);
        }
        var tuv = U.Select(p => new Vector2(p.x * 0.25f, p.z * 0.25f)).ToArray();
        mb.Fan(U, tuv, ctr);
        if (closed) mb.Fan(B, tuv, ctr);
    }

    // ── 메시 빌더 (면마다 정점 분리 = 각진 음영, 권취는 바깥 방향으로 자동 정렬)
    class MB
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector2> UV = new List<Vector2>(); public List<int> Tr = new List<int>();
        void Tri(int a, int b, int c, Vector3 outward)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(n, outward) >= 0f) { Tr.Add(a); Tr.Add(b); Tr.Add(c); } else { Tr.Add(a); Tr.Add(c); Tr.Add(b); }
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward)
        {
            int i = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d); UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
            Tri(i, i + 1, i + 2, outward); Tri(i, i + 2, i + 3, outward);
        }
        public void Fan(Vector3[] p, Vector2[] uv, Vector3 ctr)
        {
            int i = V.Count; V.AddRange(p); UV.AddRange(uv);
            var o = p[0] - ctr; o.x = 0f; o.z = 0f;               // 위 뚜껑이면 +y, 아래면 -y
            for (int k = 1; k < p.Length - 1; k++) Tri(i, i + k, i + k + 1, o);
        }
        public void Cube(Vector3 c, float e, Quaternion r)
        {
            float h = e * 0.5f;
            for (int ax = 0; ax < 3; ax++)
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    var nrm = Vector3.zero; nrm[ax] = sg;
                    int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                    var pts = new Vector3[4];
                    int k = 0;
                    foreach (var (s1, s2) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    {
                        var p = Vector3.zero; p[ax] = sg * h; p[a1] = s1 * h; p[a2] = s2 * h;
                        pts[k++] = c + r * p;
                    }
                    Quad(pts[0], pts[1], pts[2], pts[3], new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), r * nrm);
                }
        }
        public Mesh Build(string name)
        {
            var m = new Mesh { name = name, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(Tr, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }
    }
}
#endif
