// Tools ▸ Pyrite2 ▸ Z39a. Thumbnail Candidates — VRChat 월드 썸네일 후보(1200×900, 4:3) 몇 장을 Assets/_preview/thumbnail/ 에
//  관리자가 SDK 창 'Select Image' 로 고른다 (업로드는 관리자)
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PyriteThumbnail
{
    [MenuItem("Tools/Pyrite2/Z39a. Thumbnail Candidates", false, 130)]
    public static void Run()
    {
        const string OUT = "Assets/_preview/thumbnail/"; Directory.CreateDirectory(OUT);
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var shots = new (string n, float h, Vector3 eye, Vector3 at, float fov)[]
        {
            ("A_camp_lake_1850", 18.5f, new Vector3(-13.5f, 3.9f, 57.5f), new Vector3(-8.0f, 2.6f, 30.0f), 55f),
            ("B_camp_lake_1900", 19.0f, new Vector3(-13.5f, 3.9f, 57.5f), new Vector3(-8.0f, 2.6f, 30.0f), 55f),
            ("C_dock_camp_1850", 18.5f, new Vector3(-7.8f, 1.9f, 30.2f), new Vector3(-10.5f, 3.2f, 52.0f), 55f),
            ("D_camp_night_2130", 21.5f, new Vector3(-13.5f, 3.9f, 57.5f), new Vector3(-9.5f, 2.6f, 45.0f), 55f),
        };
        try
        {
            foreach (var s in shots)
            {
                cyc.ResetCache(); cyc.EvaluateAt(s.h);
                cam.fieldOfView = s.fov; cam.transform.SetPositionAndRotation(s.eye, Quaternion.LookRotation(s.at - s.eye));
                const int W = 1200, H = 900;
                var rt = new RenderTexture(W, H, 24); rt.antiAliasing = 4; cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(OUT + s.n + ".png", tx.EncodeToPNG());
                cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
        finally
        {
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        AssetDatabase.Refresh();
        File.AppendAllText("Logs/pyrite_sfx.txt", "[Z39a] " + System.DateTime.Now.ToString("HH:mm:ss") + " thumbnails " + shots.Length + "\nRESULT: DONE\n");
    }
    // Z39b. 밤 썸네일 — 관리자 인게임 스샷 구도(부두 뿌리에서 호수·크리스탈 쪽, 위로 올려다봄) + 깔끔한 유성 고정
    //  유성은 실제 TrailRenderer(M_FxMeteor, 6 m, 흰→푸른 끝) 에 궤적 점을 직접 넣어 찍는다 (Z34e 와 같은 방법)
    [MenuItem("Tools/Pyrite2/Z39b. Night Thumbnail", false, 131)]
    public static void Night()
    {
        const string OUT = "Assets/_preview/thumbnail/"; Directory.CreateDirectory(OUT);
        var log = new System.Text.StringBuilder("[Z39b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var met = Object.FindObjectOfType<PyriteMeteors>(true);
        var tr = met.trail; var head = met.head;
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        System.Func<float, float, Vector3> D = (el, az) => { float e = el * Mathf.Deg2Rad, a = az * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)); };
        // 부두: 가장 북쪽(뭍 쪽) 끝 위, 눈높이 1.6 m
        var dock = GameObject.Find("Dock");
        Bounds db = new Bounds(new Vector3(-9.6f, 0.5f, 38f), Vector3.zero); bool any = false;
        if (dock != null) foreach (var r in dock.GetComponentsInChildren<Renderer>()) { if (!any) { db = r.bounds; any = true; } else db.Encapsulate(r.bounds); }
        log.AppendLine(string.Format("  dock bounds min {0} max {1}", db.min.ToString("F2"), db.max.ToString("F2")));
        var eye = new Vector3(db.center.x, db.max.y + 1.6f, db.max.z - 0.6f);
        // 유성: 화면 오른쪽 위에서 오른쪽 아래로 (방위 188→203°, 고도 46→38°)
        var pts = new Vector3[32];
        for (int i = 0; i < pts.Length; i++) pts[i] = met.center + Vector3.Slerp(D(40f, 189f), D(33f, 204f), i / (float)(pts.Length - 1)) * met.radius;
        var shots = new (string n, float h, float pitch, float az)[]
        {
            ("N13_2100_cl1", 21.0f, 12f, 180f),
            ("N14_2100_cl2", 21.0f, 12f, 180f),
            ("N15_2100_cl3", 21.0f, 12f, 180f),
        };
        bool headWas = head.gameObject.activeSelf;
        try
        {
            head.gameObject.SetActive(true);
            // 인게임에선 크리스탈 주변 반딧불(LightFX/FF_*, 파티클)이 크리스탈을 밝힌다. 에디터 렌더는 파티클이 멈춰 있어 비어 보였다 → 미리 돌려 둔다
            var lfx = GameObject.Find("LightFX");
            var ffs = lfx != null ? lfx.GetComponentsInChildren<ParticleSystem>() : new ParticleSystem[0];
            var cluster = new Vector3(-13.0f, 3.9f, -73.0f);
            foreach (var l in Object.FindObjectsOfType<Light>(true))
                if (l.type != LightType.Directional && Vector3.Distance(l.transform.position, cluster) < 20f)
                    log.AppendLine(string.Format("  light {0} en {1} act {2} int {3:0.00} range {4:0.0} mode {5} col {6}", l.name, l.enabled, l.gameObject.activeInHierarchy, l.intensity, l.range, l.lightmapBakeType, l.color.ToString("F2")));
            foreach (var s in shots)
            {
                cyc.ResetCache(); cyc.EvaluateAt(s.h);
                foreach (var ps in ffs) ps.Simulate(8f, true, true, true);
                foreach (var l in Object.FindObjectsOfType<Light>(true))
                    if (l.type != LightType.Directional && Vector3.Distance(l.transform.position, cluster) < 20f)
                        log.AppendLine(string.Format("  after Eval {0} en {1} int {2:0.00} range {3:0.0} pos {4} cullMask {5} renderMode {6}", l.name, l.enabled, l.intensity, l.range, l.transform.position.ToString("F1"), l.cullingMask, l.renderMode));
                var shard = GameObject.Find("Crystals/Landmark_1_PyriteCluster_C");
                if (shard != null) foreach (var r in shard.GetComponentsInChildren<Renderer>()) log.AppendLine(string.Format("  rend {0} lmIdx {1} layer {2} probes {3} mat {4}", r.name, r.lightmapIndex, r.gameObject.layer, r.lightProbeUsage, r.sharedMaterial != null ? r.sharedMaterial.name : "-"));
                // 인게임 스샷의 크리스탈 앞면 밝기(평균 ~45, 따뜻한 색)에 맞추려고 썸네일에서만 크리스탈 불빛을 키운다. 끝나면 EvaluateAt 으로 원래대로
                float boostI = s.n.EndsWith("cl1") ? 1f : s.n.EndsWith("cl2") ? 2.5f : 4f;
                float boostR = s.n.EndsWith("cl1") ? 1f : s.n.EndsWith("cl2") ? 1.8f : 2.4f;
                foreach (var l in Object.FindObjectsOfType<Light>(true))
                    if (l.name.StartsWith("CrystalLight") && Vector3.Distance(l.transform.position, cluster) < 20f) { l.intensity *= boostI; l.range *= boostR; log.AppendLine("  boost " + l.name + " int " + l.intensity.ToString("0.00") + " range " + l.range.ToString("0.0")); }
                log.AppendLine("  fireflies " + ffs.Length + " particles " + System.Linq.Enumerable.Sum(ffs, p => p.particleCount));
                tr.emitting = true; tr.Clear(); tr.AddPositions(pts); head.position = pts[pts.Length - 1];
                cam.fieldOfView = 60f; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(D(s.pitch, s.az)));
                const int W = 1200, H = 900;
                bool hdr = s.n.EndsWith("_hdr"); bool hdr0 = cam.allowHDR; cam.allowHDR = hdr;
                var rt = new RenderTexture(W, H, 24, hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default); rt.antiAliasing = 8; cam.targetTexture = rt; cam.Render(); cam.allowHDR = hdr0;
                var ldr = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); Graphics.Blit(rt, ldr);
                RenderTexture.active = ldr; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(OUT + s.n + ".png", tx.EncodeToPNG());
                foreach (var l in Object.FindObjectsOfType<Light>(true))
                    if (l.name.StartsWith("CrystalLight") && Vector3.Distance(l.transform.position, cluster) < 20f) l.range /= boostR;   // range 는 EvaluateAt 이 안 되돌린다
                cam.targetTexture = null; Object.DestroyImmediate(tx); RenderTexture.ReleaseTemporary(ldr); rt.Release(); Object.DestroyImmediate(rt);
                log.AppendLine(string.Format("  {0} hour {1} eye {2} pitch {3} sunEl {4:0.0} hdr {5}", s.n, s.h, eye.ToString("F2"), s.pitch, cyc.sunElNow, hdr0));
            }
            log.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally
        {
            tr.emitting = false; tr.Clear(); head.gameObject.SetActive(headWas);
            var lfx2 = GameObject.Find("LightFX"); if (lfx2 != null) foreach (var ps in lfx2.GetComponentsInChildren<ParticleSystem>()) ps.Clear(true);
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        AssetDatabase.Refresh();
        File.AppendAllText("Logs/pyrite_sfx.txt", log.ToString());
    }
    // Z39c. 썸네일 구도 가운데 물체 진단 — 인게임에선 빛나는 크리스탈이 에디터 렌더에선 어둡다
    [MenuItem("Tools/Pyrite2/Z39c. Thumbnail Center Probe", false, 132)]
    public static void Probe()
    {
        var log = new System.Text.StringBuilder("[Z39c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        cyc.ResetCache(); cyc.EvaluateAt(21f);
        var eye = new Vector3(-11.22f, 3.15f, 42.11f);
        System.Func<float, float, Vector3> D = (el, az) => { float e = el * Mathf.Deg2Rad, a = az * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)); };
        var go = new GameObject("_probeCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 60f; cam.aspect = 4f / 3f;
        go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(D(12f, 180f)));
        try
        {
            foreach (var vy in new[] { 0.36f, 0.33f, 0.30f })
            {
                var ray = cam.ViewportPointToRay(new Vector3(0.5f, vy, 0f));
                log.AppendLine("ray vy " + vy + " dir " + ray.direction.ToString("F3"));
                var hits = new System.Collections.Generic.List<(float d, Renderer r)>();
                foreach (var r in Object.FindObjectsOfType<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (r.bounds.size.magnitude > 400f) continue;
                    if (r.bounds.IntersectRay(ray, out float d) && d < 400f) hits.Add((d, r));
                }
                hits.Sort((a, b) => a.d.CompareTo(b.d));
                foreach (var h in hits.GetRange(0, Mathf.Min(6, hits.Count)))
                {
                    var r = h.r; string path = r.name; var t = r.transform.parent; while (t != null) { path = t.name + "/" + path; t = t.parent; }
                    log.AppendLine(string.Format("  {0:0.0} m  {1}  bounds c{2} s{3}", h.d, path, r.bounds.center.ToString("F1"), r.bounds.size.ToString("F1")));
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string em = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor").ToString("F2") : "-";
                        log.AppendLine("      mat " + m.name + " shader " + m.shader.name + " emis " + em + (m.IsKeywordEnabled("_EMISSION") ? " KW" : ""));
                    }
                }
            }
            var c = hits0(eye);
            foreach (var l in Object.FindObjectsOfType<Light>(true))
            {
                if (l.type == LightType.Directional) continue;
                float dist = Vector3.Distance(l.transform.position, c);
                if (dist < 45f) log.AppendLine(string.Format("  light {0} {1} en {2} act {3} int {4:0.00} range {5:0.0} mode {6} dist {7:0.0} col {8}", l.name, l.type, l.enabled, l.gameObject.activeInHierarchy, l.intensity, l.range, l.lightmapBakeType, dist, l.color.ToString("F2")));
            }
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally { Object.DestroyImmediate(go); cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        File.AppendAllText("Logs/pyrite_sfx.txt", log.ToString());
    }
    static Vector3 hits0(Vector3 eye) { return eye + new Vector3(0f, -3f, -60f); }
}
#endif
