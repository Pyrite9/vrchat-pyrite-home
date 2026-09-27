// Tools ▸ Pyrite3 ▸ Z43a. Camp Pool Audit (씬 변경 없음)
//  의자 10 · 돗자리 6 의 제자리(홈)를 고르기 전 실측: 캠프 물건 bounds, 꽃 지도(x -22..2, z 42..62), 위에서 본 정사영 렌더(1 px = 2 cm)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteCampPoolAudit
{
    const float X0 = -22f, Z0 = 42f, SX = 24f, SZ = 20f;

    [MenuItem("Tools/Pyrite3/Z43a. Camp Pool Audit", false, 20)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z43a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(sb); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_pool.txt", sb.ToString());
    }

    static bool InRegion(Bounds b) => b.max.x > X0 && b.min.x < X0 + SX && b.max.z > Z0 && b.min.z < Z0 + SZ;

    static void Inner(StringBuilder sb)
    {
        PyriteSpawnAudit.CompileErrors(sb);
        // 1) 물건: 루트 (Camp·CampProps 는 한 단계 아래) 별 렌더러 합 bounds
        var skip = new HashSet<string> { "Terrain", "FlowerField", "PyriteCliffs_Visual", "PyriteTerraces", "Water", "LakeMirror", "SkyDiscs", "LightFX", "AmbientFX", "Crystals", "SpawnTable" };
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (skip.Contains(root.name)) continue;
            var list = (root.name == "Camp" || root.name == "CampProps") ? root.transform.Cast<Transform>().Select(x => x.gameObject).ToArray() : new[] { root };
            foreach (var g in list)
            {
                if (!g.activeInHierarchy) continue;
                var rs = g.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer)).ToArray();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (!InRegion(b) || b.size.x > 12f || b.size.z > 12f) continue;
                sb.AppendLine(string.Format("obj {0,-40} x {1,6:F2}..{2,6:F2}  z {3,6:F2}..{4,6:F2}  y {5,5:F2}..{6,5:F2}", P(g.transform), b.min.x, b.max.x, b.min.z, b.max.z, b.min.y, b.max.y));
            }
        }
        var terr = Terrain.activeTerrain;
        // 2) 꽃 지도 + 지면 높이 (1 m 칸)
        var ff = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "FlowerField");
        int NX = (int)SX, NZ = (int)SZ;
        var cnt = new int[NX, NZ];
        if (ff != null)
            foreach (var mf in ff.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var r = mf.GetComponent<Renderer>(); if (r != null && !InRegion(r.bounds)) continue;
                Vector3[] vs; try { vs = mf.sharedMesh.vertices; } catch { continue; }
                foreach (var lv in vs)
                {
                    var v = mf.transform.TransformPoint(lv);
                    int ix = Mathf.FloorToInt(v.x - X0), iz = Mathf.FloorToInt(v.z - Z0);
                    if (ix >= 0 && ix < NX && iz >= 0 && iz < NZ) cnt[ix, iz]++;
                }
            }
        sb.AppendLine("map 1 m 칸: 꽃 없음·평지(|h-1.81|<3cm) '.', 꽃 없음·기울기 '~', 꽃 적음 '-', 꽃 많음 '#'. 행 z 61→42, 열 x -22→1");
        for (int iz = NZ - 1; iz >= 0; iz--)
        {
            var row = new StringBuilder(string.Format("  z {0,3} ", (int)(Z0 + iz)));
            for (int ix = 0; ix < NX; ix++)
            {
                var c = new Vector3(X0 + ix + 0.5f, 0, Z0 + iz + 0.5f);
                float h = terr ? terr.SampleHeight(c) + terr.transform.position.y : 0f;
                int n = cnt[ix, iz];
                row.Append(n > 64 ? '#' : n > 0 ? '-' : Mathf.Abs(h - 1.81f) < 0.03f ? '.' : '~');
            }
            sb.AppendLine(row.ToString());
        }
        // 3) 위에서 본 렌더 (정사영, 1200×1000 = 24×20 m → 50 px/m)
        Directory.CreateDirectory("Assets/_preview/pool/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView; bool o0 = cam.orthographic; float s0 = cam.orthographicSize; float a0 = cam.aspect;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            cam.orthographic = true; cam.orthographicSize = SZ * 0.5f;
            cam.transform.SetPositionAndRotation(new Vector3(X0 + SX * 0.5f, 40f, Z0 + SZ * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            PyriteSpawnAudit.Shot(cam, "Assets/_preview/pool/top.png", 1200, 1000);
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.orthographic = o0; cam.orthographicSize = s0; cam.fieldOfView = f0; cam.aspect = a0; cam.ResetAspect(); cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        sb.AppendLine(string.Format("shot Assets/_preview/pool/top.png: x {0}..{1}, z {2}..{3}, 50 px/m, 위 = +z", X0, X0 + SX, Z0, Z0 + SZ));
    }

    static string P(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
