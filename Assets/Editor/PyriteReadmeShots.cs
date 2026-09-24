// Tools ▸ Pyrite2 ▸ Z41a. README Shots — GitHub README 용 스냅샷을 저장소 루트 docs/images/ 에 JPG 로 (Assets 밖 → Unity 가 임포트하지 않는다)
//  시간대 4장(같은 구도), 캠프·호수 장면, 황철석 근접(랜드마크·기울어진 결정·절벽 광맥). 에디터 렌더 — 인게임과 다를 수 있다
//  로그: Logs/pyrite_readme.txt
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteReadmeShots
{
    const string OUT = "docs/images/";
    static StringBuilder log;

    [MenuItem("Tools/Pyrite2/Z41a. README Shots", false, 150)]
    public static void Run()
    {
        Directory.CreateDirectory(OUT);
        log = new StringBuilder("[Z41a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var ps = FxSystems();
        try
        {
            bool all = !File.Exists("Logs/pyrite_readme_pyriteonly.txt");
            foreach (var old in new[] { "scene_dawn_lake" })   // README 에서 뺀 컷 (Z41a 가 만든 파일만)
                if (File.Exists(OUT + old + ".jpg")) { File.Delete(OUT + old + ".jpg"); log.AppendLine("  removed " + old); }
            if (all) {

            // 1) 시간대 — 캠프에서 호수 쪽, 같은 구도
            var todEye = G(-10.0f, 49.0f, 1.65f); var todAt = new Vector3(-10.0f, 3.0f, -40.0f);
            foreach (var (h, n) in new[] { (5.8f, "tod_1_dawn"), (12.0f, "tod_2_noon"), (18.5f, "tod_3_dusk"), (21.5f, "tod_4_night") })
                Shot(cam, cyc, ps, h, todEye, todAt, 60f, n, 1200, 675);

            // 2) 장면
            Shot(cam, cyc, ps, 18.5f, new Vector3(-13.5f, 3.9f, 57.5f), new Vector3(-8.0f, 2.6f, 30.0f), 55f, "scene_dusk_camp_lake", 1600, 900);
            Shot(cam, cyc, ps, 21.0f, G(-12.5f, 55.5f, 1.65f), new Vector3(-9.5f, 2.2f, 50.5f), 60f, "scene_night_camp", 1600, 900);
            }

            // 3) 황철석
            //  Landmark_1 (호수 건너, 중앙) — 물 위에서 가까이
            var lm1 = Center("Crystals/Landmark_1");
            var e1 = new Vector3(lm1.x + 4f, 1.7f, lm1.z + 20f);
            if (all) Shot(cam, cyc, ps, 18.4f, e1, lm1 + Vector3.up * 1.5f, 45f, "pyrite_landmark_dusk", 1600, 900);
            if (all) Shot(cam, cyc, ps, 21.5f, e1, lm1 + Vector3.up * 1.5f, 45f, "pyrite_landmark_night", 1600, 900);
            //  Landmark_2 (기울어진 결정) — 한낮
            var lm2 = Center("Crystals/Landmark_2");
            var d2 = (new Vector3(0f, 0f, -14f) - lm2); d2.y = 0f; d2.Normalize();
            var e2 = G(lm2.x + d2.x * 17f, lm2.z + d2.z * 17f, 1.6f);
            Shot(cam, cyc, ps, 12.0f, e2, lm2 + Vector3.up * 1.2f, 45f, "pyrite_landmark2_noon", 1600, 900);
            Shot(cam, cyc, ps, 18.4f, e2, lm2 + Vector3.up * 1.2f, 45f, "pyrite_landmark2_dusk", 1600, 900);
            //  절벽 광맥 — PyriteInCliff 정점을 방위로 묶어 가장 큰 광맥(북쪽 벽 우선: 노을 해가 남쪽)을 찾는다
            var vein = CliffVein(out float veinAz);
            if (vein != Vector3.zero)
            {
                var dir = new Vector3(Mathf.Sin(veinAz * Mathf.Deg2Rad), 0f, Mathf.Cos(veinAz * Mathf.Deg2Rad));
                var ev = G(-10f + dir.x * 71f, -14f + dir.z * 71f, 1.65f);
                Shot(cam, cyc, ps, 18.4f, ev, vein, 40f, "pyrite_cliff_vein_dusk", 1600, 900);
                Shot(cam, cyc, ps, 12.0f, ev, vein, 40f, "pyrite_cliff_vein_noon", 1600, 900);
            }
            log.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally
        {
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            foreach (var p in ps) if (p != null) p.Clear(true);
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
            Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_readme.txt", log.ToString());
        }
    }

    // 반딧불(LightFX)·캠프 불티처럼 계속 도는 파티클은 에디터 렌더에서 멈춰 있어 미리 돌린다
    static ParticleSystem[] FxSystems()
    {
        var list = new System.Collections.Generic.List<ParticleSystem>();
        foreach (var root in new[] { "LightFX", "AmbientFX/CampEmbers", "Camp" })
        {
            var go = GameObject.Find(root); if (go == null) continue;
            foreach (var p in go.GetComponentsInChildren<ParticleSystem>()) if (p.main.loop) list.Add(p);
        }
        return list.ToArray();
    }

    static Vector3 G(float x, float z, float up)
    {
        float y = 0f;
        var hits = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0) y = hits.Max(h => h.point.y);
        return new Vector3(x, Mathf.Max(y, 0f) + up, z);
    }

    static Vector3 Center(string path)
    {
        var go = GameObject.Find(path);
        if (go == null)   // "Crystals/Landmark_1" → 실제 이름은 Landmark_1_PyriteCluster_C 처럼 뒤가 붙는다
        {
            int k = path.LastIndexOf('/'); var parent = GameObject.Find(path.Substring(0, k)); string pre = path.Substring(k + 1);
            if (parent != null) foreach (Transform c in parent.transform) if (c.name.StartsWith(pre)) { go = c.gameObject; break; }
        }
        if (go == null) { log.AppendLine("  !! 없음 " + path); return Vector3.zero; }
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        log.AppendLine(string.Format("  {0} center {1} size {2} renderers {3}", path, b.center.ToString("F1"), b.size.ToString("F1"), rs.Length));
        return b.center;
    }

    static Vector3 CliffVein(out float az)
    {
        az = 0f;
        var go = GameObject.Find("Crystals/PyriteInCliff");
        var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) { log.AppendLine("  !! PyriteInCliff 메시 없음"); return Vector3.zero; }
        var tf = mf.transform; var vs = mf.sharedMesh.vertices;
        var bins = new System.Collections.Generic.List<Vector3>[72];
        for (int i = 0; i < 72; i++) bins[i] = new System.Collections.Generic.List<Vector3>();
        foreach (var v in vs)
        {
            var w = tf.TransformPoint(v);
            float a = Mathf.Atan2(w.x + 10f, w.z + 14f) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
            bins[(int)(a / 5f) % 72].Add(w);
        }
        int best = -1; float bestScore = -1f;
        for (int i = 0; i < 72; i++)
        {
            float a = i * 5f + 2.5f;
            float north = Mathf.Cos(a * Mathf.Deg2Rad);          // 북쪽(+z) 벽이 노을 해를 받는다
            if (north < 0.2f) continue;
            float score = bins[i].Count * (0.5f + north);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best < 0) return Vector3.zero;
        az = best * 5f + 2.5f;
        var c = Vector3.zero; foreach (var w in bins[best]) c += w; c /= bins[best].Count;
        log.AppendLine(string.Format("  cliff vein az {0:0} verts {1} center {2}", az, bins[best].Count, c.ToString("F1")));
        return c;
    }

    static void Shot(Camera cam, PyriteDayCycle cyc, ParticleSystem[] ps, float h, Vector3 eye, Vector3 at, float fov, string name, int W, int H)
    {
        cyc.ResetCache(); cyc.EvaluateAt(h);
        foreach (var p in ps) if (p != null) p.Simulate(8f, true, true, true);
        cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.DefaultHDR); rt.antiAliasing = 8;
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var ldr = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); Graphics.Blit(rt, ldr);
        RenderTexture.active = ldr; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
        var bytes = tx.EncodeToJPG(90); File.WriteAllBytes(OUT + name + ".jpg", bytes);
        Object.DestroyImmediate(tx); RenderTexture.ReleaseTemporary(ldr); rt.Release(); Object.DestroyImmediate(rt);
        log.AppendLine(string.Format("  {0} {1}x{2} hour {3} eye {4} fov {5} {6} KB", name, W, H, h, eye.ToString("F1"), fov, bytes.Length / 1024));
    }
    // Z41b. README 에서 안 쓰는 docs/images/*.jpg 지우기 (README.md 에 적힌 경로만 남긴다)
    [MenuItem("Tools/Pyrite2/Z41b. README Images Cleanup", false, 151)]
    public static void Cleanup()
    {
        var sb = new StringBuilder("[Z41b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        if (!File.Exists("README.md") || !Directory.Exists(OUT)) { sb.AppendLine("  README 또는 docs/images 없음"); }
        else
        {
            string md = File.ReadAllText("README.md");
            foreach (var f in Directory.GetFiles(OUT, "*.jpg"))
            {
                string rel = OUT + Path.GetFileName(f);
                if (md.Contains(rel)) sb.AppendLine("  keep " + rel + " " + (new FileInfo(f).Length / 1024) + " KB");
                else { File.Delete(f); sb.AppendLine("  removed " + rel); }
            }
            if (File.Exists("Logs/pyrite_readme_pyriteonly.txt")) { File.Delete("Logs/pyrite_readme_pyriteonly.txt"); sb.AppendLine("  Z41a 전체 모드로 복귀"); }
            sb.AppendLine("RESULT: DONE");
        }
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_readme.txt", sb.ToString());
    }
}
#endif
