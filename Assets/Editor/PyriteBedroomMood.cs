// PyriteBedroomMood.cs — 침실 분위기 조명 (Z51i 빌드 / Z51j 되돌리기 / Z51k 렌더). 재실행 안전
//  2026-09-29 관리자: 4번 = 별 무드등 + 줄전구, 수면 모드 연동
//  루트 TentBedroom/Mood
//   StarLamp: 머리맡 왼쪽 바닥 작은 돔 기기 + 위를 보는 스폿 광원(150°, 쿠키 = 코드로 만든 별 512²) → 천장·벽에 별. PyriteBedroomPanel 이 수면 모드로 세기 15%→100%, 1.5°/s 회전
//   StringLights: 두 폴(X자)을 따라 0.32 m 마다 전구, 클립 사이 처짐. 광원 없음(발광 머티리얼) → PyriteBedroomPanel 이 수면 모드로 발광 100%→30%
//  텐트 천 셰이더(Pyrite/TentCanvas)는 Standard 표면 셰이더라 스폿 쿠키를 받음
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

public static class PyriteBedroomMood
{
    public const string DIR = "Assets/Bedroom/Mood/";
    const string PREV = "Assets/_preview/bedroom/";
    const string ROOT = "Mood";
    public const float STAR_MAX = 6.0f;   // 2.0 → 천장(3.2 m) 별이 거의 안 보임(md_sleep 9.4) → 6
    static readonly Vector3 LAMP_POS = new Vector3(-1.50f, 0f, -2.10f);   // 매트 옆(x −1.13) · 담요 끝(x −1.26) 에서 비켜
    public static readonly Color STRING_EMIT = new Color(1.0f, 0.70f, 0.36f) * 2.2f;
    // 폴 경로 = 돔 표면 θ 45°/135° 초타원 방향 (Z51a: 폴 bounds x ±3.03 · z ±2.50 · y 3.29)
    const float PA = 3.03f, PB = 2.49f, PH = 3.29f;
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z51i. Bedroom Mood Lights Build", false, 5109)]
    public static void Build()
    {
        sb = new StringBuilder("[Z51i] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51j. Bedroom Mood Lights Revert", false, 5110)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z51j] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom"); var t = room ? room.transform.Find(ROOT) : null;
        if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(ROOT + " 삭제"); }
        Wire(room ? room.transform : null);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51k. Bedroom Mood Renders", false, 5111)]
    public static void RenderOnly()
    {
        sb = new StringBuilder("[Z51k] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static bool Inner()
    {
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return false; }
        Directory.CreateDirectory(DIR);
        var old = room.transform.Find(ROOT); if (old) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(ROOT).transform; root.SetParent(room.transform, false);
        BuildStarLamp(root);
        BuildStringLights(root);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.gameObject.layer = PyriteBedroomV3.LAYER; r.lightProbeUsage = LightProbeUsage.Off;
            foreach (var c in r.GetComponents<Collider>()) Object.DestroyImmediate(c);
        }
        Wire(room.transform);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    // 침실 패널 U# 에 연결 (Z50a 도 같은 방식으로 연결함)
    public static void Wire(Transform room)
    {
        if (room == null) return;
        var p = room.Find("BedroomPanel"); var pb = p ? p.GetComponent<PyriteBedroomPanel>() : null;
        if (pb == null) { if (sb != null) sb.AppendLine("  (BedroomPanel 없음 — Z50a 때 연결됨)"); return; }
        var lt = room.Find("Mood/StarLamp/StarLight");
        pb.starLight = lt ? lt.GetComponent<Light>() : null;
        pb.stringMat = room.Find("Mood/StringLights") ? AssetDatabase.LoadAssetAtPath<Material>(DIR + "M_StringBulb.mat") : null;
        pb.starMax = STAR_MAX; pb.stringEmit = STRING_EMIT;
        UdonSharpEditorUtility.CopyProxyToUdon(pb); EditorUtility.SetDirty(pb);
        if (sb != null) sb.AppendLine("  패널 연결: starLight " + (pb.starLight != null) + ", stringMat " + (pb.stringMat != null));
    }

    // ───────────── 별 무드등 ─────────────
    static void BuildStarLamp(Transform root)
    {
        var lamp = new GameObject("StarLamp").transform; lamp.SetParent(root, false); lamp.localPosition = LAMP_POS;
        var mBase = Mat("M_StarLampBase", new Color(0.10f, 0.11f, 0.14f), 0.45f, 0.3f);
        var mDome = Mat("M_StarLampDome", new Color(0.20f, 0.26f, 0.42f), 0.85f, 0f, emit: new Color(0.25f, 0.35f, 0.65f) * 0.6f);
        var baseMesh = SaveMesh(Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.070f, 0f), new Vector2(0.072f, 0.004f), new Vector2(0.068f, 0.034f), new Vector2(0.060f, 0.036f), new Vector2(0.001f, 0.036f) }, 32), "StarLampBase");
        var dome = new List<Vector2>();
        for (int i = 0; i <= 10; i++) { float a = Mathf.PI / 2f * i / 10f; dome.Add(new Vector2(Mathf.Max(0.001f, 0.058f * Mathf.Cos(a)), 0.036f + 0.058f * Mathf.Sin(a))); }
        var domeMesh = SaveMesh(Lathe(dome.ToArray(), 32), "StarLampDome");
        MeshObj(lamp, "Base", baseMesh, mBase);
        MeshObj(lamp, "Dome", domeMesh, mDome);

        var cookie = StarCookie(512);
        var lgo = new GameObject("StarLight"); lgo.transform.SetParent(lamp, false);
        lgo.transform.localPosition = new Vector3(0f, 0.10f, 0f);
        lgo.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Spot; l.spotAngle = 110f; l.innerSpotAngle = 90f; l.range = 6.0f;   // 150° 는 천장(±33°)에 쿠키 3% 만 → 별 ~16개. 110° → ~20%
        l.color = new Color(0.78f, 0.86f, 1.0f); l.intensity = STAR_MAX * 0.15f;
        l.cookie = cookie; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        sb.AppendLine("별 무드등 " + V(LAMP_POS) + ", 스폿 110° · 6 m · 최대 " + STAR_MAX + ", 쿠키 512² (별 " + starCount + "개)");
    }

    static int starCount;
    // 스폿 쿠키: 알파 = 밝기. 가장자리 10% 는 검정(클램프 번짐 방지), 밝은 별 몇 개는 작은 십자 빛
    static Texture2D StarCookie(int n)
    {
        var px = new float[n * n];
        var rnd = new System.Random(11);
        starCount = 0;
        void Dot(float cx, float cy, float r, float a)
        {
            int x0 = Mathf.Max(0, (int)(cx - r * 3)), x1 = Mathf.Min(n - 1, (int)(cx + r * 3)), y0 = Mathf.Max(0, (int)(cy - r * 3)), y1 = Mathf.Min(n - 1, (int)(cy + r * 3));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                float d2 = ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) / (r * r);
                px[y * n + x] = Mathf.Max(px[y * n + x], a * Mathf.Exp(-d2 * 1.6f));
            }
        }
        for (int i = 0; i < 700; i++)
        {
            float cx = (float)rnd.NextDouble() * n, cy = (float)rnd.NextDouble() * n;
            float big = (float)rnd.NextDouble();
            float r = big > 0.96f ? 2.6f : big > 0.8f ? 1.7f : 1.1f;
            float a = big > 0.96f ? 1f : 0.45f + 0.5f * (float)rnd.NextDouble();
            Dot(cx, cy, r, a); starCount++;
            if (big > 0.96f) for (int k = 1; k <= 6; k++) { float f = 0.5f / k; Dot(cx + k * 1.5f, cy, 0.8f, f); Dot(cx - k * 1.5f, cy, 0.8f, f); Dot(cx, cy + k * 1.5f, 0.8f, f); Dot(cx, cy - k * 1.5f, 0.8f, f); }
        }
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var cols = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
            float edge = Mathf.Clamp01((0.92f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))) / 0.12f);
            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(px[y * n + x]) * edge * 255f);
            cols[y * n + x] = new Color32(a, a, a, a);
        }
        t.SetPixels32(cols); t.Apply();
        string path = DIR + "T_StarCookie.png";
        File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Cookie; imp.alphaSource = TextureImporterAlphaSource.FromGrayScale;
        imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false; imp.textureCompression = TextureImporterCompression.Uncompressed;
        var so = new SerializedObject(imp); var lt = so.FindProperty("m_LightType"); if (lt != null) { lt.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); }   // 0 = Spot
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────────── 줄전구 ─────────────
    static Vector3 PolePoint(int pole, float phi)   // phi 0 → 한쪽 끝 바닥, π → 반대쪽 바닥
    {
        float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
        return pole == 0 ? new Vector3(PA * c, PH * s, PB * c) : new Vector3(-PA * c, PH * s, PB * c);
    }

    static void BuildStringLights(Transform root)
    {
        var mBulb = Mat("M_StringBulb", new Color(1f, 0.86f, 0.62f), 0.6f, 0f, emit: STRING_EMIT);
        var mWire = Mat("M_StringWire", new Color(0.06f, 0.05f, 0.05f), 0.3f);
        var sl = new GameObject("StringLights").transform; sl.SetParent(root, false);
        int bulbsTotal = 0, trisTotal = 0;
        for (int pole = 0; pole < 2; pole++)
        {
            // 폴을 따라 촘촘히 → 호 길이 표
            const int N = 400; float phi0 = 25f * Mathf.Deg2Rad, phi1 = 155f * Mathf.Deg2Rad;
            var pts = new Vector3[N + 1]; var len = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float phi = Mathf.Lerp(phi0, phi1, i / (float)N);
                var p = PolePoint(pole, phi);
                var inward = -new Vector3(p.x, 0f, p.z).normalized;
                pts[i] = p + inward * 0.045f + Vector3.down * 0.035f;       // 폴 안쪽·아래로 비켜서
                if (i > 0) len[i] = len[i - 1] + Vector3.Distance(pts[i], pts[i - 1]);
            }
            float L = len[N];
            Vector3 At(float d) { int i = System.Array.FindIndex(len, x => x >= d); if (i <= 0) return pts[0]; float t = (d - len[i - 1]) / Mathf.Max(1e-5f, len[i] - len[i - 1]); return Vector3.Lerp(pts[i - 1], pts[i], t); }
            const float CLIP = 0.96f, SAG = 0.05f, STEP = 0.32f;
            Vector3 Wire(float d) { float f = Mathf.Repeat(d, CLIP) / CLIP; return At(d) + Vector3.down * SAG * 4f * f * (1f - f); }   // 클립 사이 포물선 처짐

            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var tw = new List<int>(); var tb = new List<int>();
            // 전선: 사각 튜브 3 mm
            int segs = Mathf.CeilToInt(L / 0.06f); float w = 0.0015f;
            for (int i = 0; i <= segs; i++)
            {
                float d = L * i / segs; var p = Wire(d); var tan = (Wire(Mathf.Min(L, d + 0.01f)) - Wire(Mathf.Max(0f, d - 0.01f))).normalized;
                var a = Vector3.Cross(tan, Vector3.up).normalized; var b = Vector3.Cross(a, tan).normalized;
                foreach (var o in new[] { a + b, -a + b, -a - b, a - b }) { vs.Add(p + o * w); ns.Add(o.normalized); }
                if (i > 0) { int s0 = (i - 1) * 4, s1 = i * 4; for (int k = 0; k < 4; k++) { int k1 = (k + 1) % 4; tw.Add(s0 + k); tw.Add(s1 + k); tw.Add(s0 + k1); tw.Add(s0 + k1); tw.Add(s1 + k); tw.Add(s1 + k1); } }
            }
            // 전구: 전선에서 1.2 cm 아래 달린 계란형 (8면 × 5층)
            int bulbs = 0;
            for (float d = STEP * 0.5f; d < L; d += STEP)
            {
                var c = Wire(d) + Vector3.down * 0.022f;
                int b0 = vs.Count, rings = 5, sides = 8;
                for (int r = 0; r <= rings; r++)
                {
                    float th = Mathf.PI * r / rings, ry = Mathf.Cos(th), rr = Mathf.Sin(th);
                    for (int s = 0; s <= sides; s++)
                    {
                        float a = 2f * Mathf.PI * s / sides;
                        var n = new Vector3(Mathf.Cos(a) * rr, ry, Mathf.Sin(a) * rr);
                        vs.Add(c + new Vector3(n.x * 0.011f, n.y * 0.016f, n.z * 0.011f)); ns.Add(n);
                    }
                }
                for (int r = 0; r < rings; r++) for (int s = 0; s < sides; s++)
                {
                    int i0 = b0 + r * (sides + 1) + s, i1 = i0 + 1, i2 = i0 + sides + 1, i3 = i2 + 1;
                    tb.Add(i0); tb.Add(i1); tb.Add(i2); tb.Add(i1); tb.Add(i3); tb.Add(i2);
                }
                bulbs++;
            }
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.SetVertices(vs); m.SetNormals(ns); m.subMeshCount = 2; m.SetTriangles(tw, 0); m.SetTriangles(tb, 1); m.RecalculateBounds();
            var go = new GameObject("Pole_" + pole); go.transform.SetParent(sl, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m, "StringLights_" + pole);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = new[] { mWire, mBulb }; mr.shadowCastingMode = ShadowCastingMode.Off;
            bulbsTotal += bulbs; trisTotal += (tw.Count + tb.Count) / 3;
            sb.AppendLine("  폴 " + pole + ": 길이 " + L.ToString("F2") + " m, 전구 " + bulbs + ", 클립 간격 " + CLIP + " m · 처짐 " + SAG * 100 + " cm");
        }
        sb.AppendLine("줄전구 전구 " + bulbsTotal + "개, 삼각형 " + trisTotal + ", 발광 " + STRING_EMIT);
    }

    // ───────────── 공통 ─────────────
    static Material Mat(string name, Color c, float gloss, float metal = 0f, Color? emit = null)
    {
        string path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metal);
        if (emit.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    static Mesh Lathe(Vector2[] prof, int seg)
    {
        var vs = new List<Vector3>(); var ts = new List<int>();
        for (int i = 0; i < prof.Length; i++)
            for (int s = 0; s <= seg; s++)
            {
                float a = 2f * Mathf.PI * s / seg;
                vs.Add(new Vector3(Mathf.Cos(a) * prof[i].x, prof[i].y, Mathf.Sin(a) * prof[i].x));
            }
        int row = seg + 1;
        for (int i = 0; i < prof.Length - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                ts.Add(a); ts.Add(c); ts.Add(b); ts.Add(b); ts.Add(c); ts.Add(d);
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetTriangles(ts, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ───────────── 렌더: 깨어 있음(수면 0) / 수면 100% 흉내 ─────────────
    static void Renders()
    {
        var room = Root("TentBedroom").transform;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var lights = room.Find("Lights").GetComponentsInChildren<Light>(true); var baseI = lights.Select(x => x.intensity).ToArray();
        var star = room.Find(ROOT + "/StarLamp/StarLight").GetComponent<Light>();
        var bulb = AssetDatabase.LoadAssetAtPath<Material>(DIR + "M_StringBulb.mat");
        var bd = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat"); float dim0 = bd ? bd.GetFloat("_Dim") : 1f;
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            Vector3 W(float x, float y, float z) => room.TransformPoint(new Vector3(x, y, z));
            cam.fieldOfView = 70f;
            foreach (var (tag, s) in new[] { ("awake", 0f), ("sleep", 1f) })
            {
                float k = Mathf.Lerp(1f, 0.05f, s);
                for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i] * k;
                star.intensity = STAR_MAX * Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.7f, s)));
                bulb.SetColor("_EmissionColor", STRING_EMIT * Mathf.Lerp(1f, 0.3f, s));
                if (bd) bd.SetFloat("_Dim", Mathf.Lerp(1f, 0.4f, s));
                Shot(cam, W(-0.28f, 0.45f, -1.7f), W(0.1f, 2.6f, 0.6f), "md_" + tag + "_lie_up");
                Shot(cam, W(1.8f, 1.5f, 2.1f), W(-0.4f, 1.4f, -1.2f), "md_" + tag + "_room");
                Shot(cam, W(-0.9f, 1.1f, -1.3f), room.TransformPoint(LAMP_POS + new Vector3(0f, 0.05f, 0f)), "md_" + tag + "_lamp");
            }
        }
        finally
        {
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i];
            star.intensity = STAR_MAX * 0.15f; bulb.SetColor("_EmissionColor", STRING_EMIT); if (bd) bd.SetFloat("_Dim", dim0);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double sum = 0; foreach (var q in px) sum += q.r + q.g + q.b;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag + " 평균 " + (sum / px.Length / 3).ToString("F1"));
        Object.DestroyImmediate(tex);
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_mood.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
