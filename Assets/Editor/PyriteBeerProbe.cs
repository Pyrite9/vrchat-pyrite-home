// PyriteBeerProbe.cs — 캠프 맥주 박스 자리 실측 (Z53a). 읽기 전용: 임시 회색 상자는 렌더 동안만 두고 지운다
//  2026-09-30 관리자: 폴딩 컨테이너(침실 것 복제)에 병맥주 12 + 얼음, 캠프 탁자 오른쪽(−X, 스토브·주전자 쪽) 끝 옆 땅
//  후보 A1 = 긴 변(0.62)을 z 로, 탁자 끝면에 붙임 / A2 = 긴 변을 x 로, 탁자 끝에서 바깥으로
//  로그 Logs/pyrite_beer_probe.txt, 렌더 Assets/_preview/beer/probe_*.jpg
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBeerProbe
{
    const string PREV = "Assets/_preview/beer/";
    const float BW = 0.40f, BL = 0.62f, BH = 0.36f;     // 침실 컨테이너 바깥 치수 (x 0.40 · z 0.62 · 높이 0.36)
    const float GAP = 0.05f;                            // 탁자 끝면과 틈

    [MenuItem("Tools/Pyrite4/Z53a. Beer Box Spot Probe", false, 5301)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z53a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var temp = new List<GameObject>();
        try
        {
            Physics.SyncTransforms();
            // 1) 탁자
            var table = GameObject.Find("Camp/camp03_table");
            if (table == null) { sb.AppendLine("!! Camp/camp03_table 없음"); return; }
            var tb = WB(table);
            sb.AppendLine("table bounds " + BS(tb));
            var tcols = Object.FindObjectsOfType<Collider>(true).Where(c => c.name.ToLower().Contains("table") && c.gameObject.activeInHierarchy).ToArray();
            foreach (var c in tcols) sb.AppendLine("  table collider " + Path(c.transform) + " " + BS(c.bounds) + " layer " + c.gameObject.layer);
            float top = tb.max.y;

            // 2) 탁자 주변 물건 (탁자 bounds ± 1.5 m 수평, 렌더러 기준)
            var zone = new Bounds(tb.center, new Vector3(tb.size.x + 3f, 6f, tb.size.z + 3f));
            sb.AppendLine("\n탁자 ±1.5 m 안 루트/주요 물체 (렌더러 합친 bounds):");
            var roots = new Dictionary<string, Bounds>();
            foreach (var r in Object.FindObjectsOfType<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!r.bounds.Intersects(zone)) continue;
                if (r.bounds.size.x > 20f || r.bounds.size.z > 20f) continue;      // 지형·꽃 덩어리·절벽 제외
                var key = Key(r.transform);
                if (roots.ContainsKey(key)) { var b = roots[key]; b.Encapsulate(r.bounds); roots[key] = b; } else roots[key] = r.bounds;
            }
            foreach (var kv in roots.OrderBy(k => k.Value.center.x)) sb.AppendLine("  " + kv.Key + " " + BS(kv.Value));

            // 3) 지면 높이 격자 (탁자 오른쪽 끝 바깥 x tb.min.x−1.2 .. tb.min.x+0.2, z tb.min.z−0.5 .. tb.max.z+0.5, 10 cm)
            sb.AppendLine("\n지면 높이 격자 (위→아래 레이, 트리거 무시, 모든 레이어) — 행 = z, 열 = x (값 = 지면 y − 1.81 cm, 지형 아닌 콜라이더에 맞으면 *이름)");
            var hitNames = new HashSet<string>();
            var xs = new List<float>(); for (float x = tb.min.x - 1.2f; x <= tb.min.x + 0.21f; x += 0.1f) xs.Add(x);
            sb.AppendLine("  x: " + string.Join(" ", xs.Select(x => x.ToString("F1").PadLeft(6))));
            for (float z = tb.max.z + 0.5f; z >= tb.min.z - 0.51f; z -= 0.1f)
            {
                var row = new StringBuilder("  z " + z.ToString("F2") + ": ");
                foreach (var x in xs)
                {
                    RaycastHit h;
                    if (Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out h, 60f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        bool terr = h.collider is TerrainCollider;
                        if (!terr) hitNames.Add(Path(h.collider.transform) + " L" + h.collider.gameObject.layer);
                        row.Append(((terr ? "" : "*") + ((h.point.y - 1.81f) * 100f).ToString("F0")).PadLeft(6));
                    }
                    else row.Append("     -");
                }
                sb.AppendLine(row.ToString());
            }
            sb.AppendLine("  지형 아닌 콜라이더: " + (hitNames.Count == 0 ? "없음" : string.Join(" | ", hitNames)));

            // 4) 후보 자리
            var cands = new[] {
                ("A1", new Vector3(tb.min.x - GAP - BW * 0.5f, 0f, tb.center.z), 0f),     // 긴 변 z
                ("A2", new Vector3(tb.min.x - GAP - BL * 0.5f, 0f, tb.center.z), 90f),    // 긴 변 x
            };
            var terrain = Terrain.activeTerrain;
            foreach (var (name, c, yaw) in cands)
            {
                float g = Ground(c);
                var center = new Vector3(c.x, g + BH * 0.5f, c.z);
                var size = yaw == 0f ? new Vector3(BW, BH, BL) : new Vector3(BL, BH, BW);
                // 네 모서리 지면 차
                var cs = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) }
                    .Select(s => Ground(new Vector3(c.x + s.x * size.x * 0.5f, 0, c.z + s.z * size.z * 0.5f))).ToArray();
                sb.AppendLine(string.Format("\n후보 {0}: 중심 ({1:F2}, {2:F3}, {3:F2}) yaw {4} 크기 {5} | 모서리 지면 {6} (차 {7:F1} cm) | 상판 높이 차 {8:F1} cm (상자 윗면 {9:F3} vs 탁자 {10:F3})",
                    name, c.x, g, c.z, yaw, size.ToString("F2"), string.Join("/", cs.Select(v => v.ToString("F3"))), (cs.Max() - cs.Min()) * 100f,
                    (g + BH - top) * 100f, g + BH, top));
                var probe = new Bounds(center, size + new Vector3(0.2f, 0f, 0.2f));
                var over = Physics.OverlapBox(center, size * 0.5f + new Vector3(0.1f, -0.02f, 0.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide)
                    .Where(o => !(o is TerrainCollider)).Select(o => Path(o.transform) + (o.isTrigger ? "(트리거)" : "") + " L" + o.gameObject.layer).Distinct().ToArray();
                sb.AppendLine("  겹치는 콜라이더(±10 cm): " + (over.Length == 0 ? "없음" : string.Join(" | ", over)));
                var rov = roots.Where(kv => kv.Value.Intersects(probe)).Select(kv => kv.Key).ToArray();
                sb.AppendLine("  겹치는 렌더러 묶음(±10 cm): " + (rov.Length == 0 ? "없음" : string.Join(" | ", rov)));
                // 가까운 의자·통로
                foreach (var kv in roots.Where(k => k.Key.Contains("Chair") || k.Key.Contains("Mat") || k.Key.Contains("Stove") || k.Key.Contains("Kettle") || k.Key.Contains("Projector") || k.Key.Contains("Screen") || k.Key.Contains("Fire")))
                {
                    var b = kv.Value; var p = new Vector2(Mathf.Clamp(c.x, b.min.x, b.max.x), Mathf.Clamp(c.z, b.min.z, b.max.z));
                    float dx = Mathf.Max(0f, Mathf.Abs(c.x - b.center.x) - (b.extents.x + size.x * 0.5f));
                    float dz = Mathf.Max(0f, Mathf.Abs(c.z - b.center.z) - (b.extents.z + size.z * 0.5f));
                    sb.AppendLine(string.Format("  ↔ {0}: 수평 틈 {1:F2} m", kv.Key, Mathf.Sqrt(dx * dx + dz * dz)));
                }
                // 임시 회색 상자 (렌더용)
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "_BeerProbe_" + name; box.hideFlags = HideFlags.DontSave;
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.transform.position = center; box.transform.localScale = size;
                var m = new Material(Shader.Find("Standard")); m.color = name == "A1" ? new Color(0.9f, 0.3f, 0.2f) : new Color(0.2f, 0.5f, 0.9f);
                box.GetComponent<Renderer>().sharedMaterial = m; box.SetActive(false);
                temp.Add(box);
            }

            // 5) 렌더: 위 정사영(격자 0.5 m) + 서서 보는 눈높이 두 곳, 후보마다
            Directory.CreateDirectory(PREV);
            var stove = GameObject.Find("CampProps/Stove");
            foreach (var box in temp)
            {
                box.SetActive(true);
                string n = box.name.Substring(12);
                Top(PREV + "probe_top_" + n + ".jpg", tb, stove != null ? stove.transform.position : tb.center);
                var eyeC = new Vector3(tb.center.x, 0, tb.min.z - 0.9f); float eg = Ground(eyeC);   // 모닥불 쪽(−z)에서 탁자를 봄
                Shot(PREV + "probe_front_" + n + ".jpg", new Vector3(eyeC.x, eg + 1.55f, eyeC.z), box.transform.position + Vector3.up * 0.1f, 60f);
                var side = new Vector3(tb.min.x - 1.6f, 0, tb.center.z - 0.9f); float sg = Ground(side);
                Shot(PREV + "probe_side_" + n + ".jpg", new Vector3(side.x, sg + 1.55f, side.z), new Vector3(tb.min.x, top, tb.center.z), 60f);
                box.SetActive(false);
            }
            sb.AppendLine("\nshots " + PREV + "probe_{top,front,side}_{A1,A2}.jpg (top: 오른쪽 +x, 위 +z, 흰 1 m · 회색 0.5 m, 자홍 점 = 스토브 중심)");
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            foreach (var g in temp) if (g != null) Object.DestroyImmediate(g);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/pyrite_beer_probe.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }

    static void Top(string path, Bounds tb, Vector3 mark)
    {
        var go = new GameObject("_TopCam"); go.hideFlags = HideFlags.DontSave;
        var cam = go.AddComponent<Camera>(); cam.CopyFrom(Camera.main);
        cam.orthographic = true; cam.orthographicSize = 1.6f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
        var c = new Vector3(tb.min.x - 0.3f, tb.max.y + 8f, tb.center.z);
        go.transform.SetPositionAndRotation(c, Quaternion.Euler(90f, 0f, 0f));
        int w = 1000, h = 1000;
        var tex = Render(cam, w, h);
        float ppm = w / (cam.orthographicSize * 2f);
        // 격자: 월드 0.5 m 선 (x·z 값이 0.5 배수), 스크린 좌표로
        for (float gx = Mathf.Floor((c.x - 1.6f) * 2f) / 2f; gx <= c.x + 1.6f; gx += 0.5f)
        {
            int px = Mathf.RoundToInt(cam.WorldToScreenPoint(new Vector3(gx, 0, c.z)).x);
            bool one = Mathf.Abs(gx - Mathf.Round(gx)) < 0.01f;
            for (int i = 0; i < h; i += one ? 1 : 3) if (px >= 0 && px < w) tex.SetPixel(px, i, one ? Color.white : Color.gray);
        }
        for (float gz = Mathf.Floor((c.z - 1.6f) * 2f) / 2f; gz <= c.z + 1.6f; gz += 0.5f)
        {
            int py = Mathf.RoundToInt(cam.WorldToScreenPoint(new Vector3(c.x, 0, gz)).y);
            bool one = Mathf.Abs(gz - Mathf.Round(gz)) < 0.01f;
            for (int i = 0; i < w; i += one ? 1 : 3) if (py >= 0 && py < h) tex.SetPixel(i, py, one ? Color.white : Color.gray);
        }
        var ms = cam.WorldToScreenPoint(mark);
        for (int dx = -6; dx <= 6; dx++) for (int dy = -6; dy <= 6; dy++) if (dx * dx + dy * dy <= 36) tex.SetPixel((int)ms.x + dx, (int)ms.y + dy, Color.magenta);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToJPG(88));
        Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }

    static void Shot(string path, Vector3 pos, Vector3 look, float fov)
    {
        var go = new GameObject("_ShotCam"); go.hideFlags = HideFlags.DontSave;
        var cam = go.AddComponent<Camera>(); cam.CopyFrom(Camera.main);
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        var tex = Render(cam, 1280, 720);
        File.WriteAllBytes(path, tex.EncodeToJPG(88));
        Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }

    static Texture2D Render(Camera cam, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    static float Ground(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out hit, 120f, 1 | (1 << 11), QueryTriggerInteraction.Ignore)) return hit.point.y;
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.transform.position.y : 1.81f;
    }

    static Bounds WB(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>(false);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    // 묶음 이름: 씬 루트 바로 아래 한 단계까지 (CampProps/Stove, Camp/camp03_table …)
    static string Key(Transform t)
    {
        var chain = new List<Transform>(); while (t != null) { chain.Insert(0, t); t = t.parent; }
        return chain.Count >= 2 ? chain[0].name + "/" + chain[1].name : chain[0].name;
    }

    static string BS(Bounds b) => string.Format("x {0:F2}~{1:F2} y {2:F2}~{3:F2} z {4:F2}~{5:F2}", b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z);
    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
