// PyriteBedroomSoft.cs — 침실 천 소품 (재실행 안전)
//  2026-09-30 관리자: 침대 주변 채우기 D = 바닥 쿠션, 6 = 빈백 담요는 "양털" (침대 체크 · 러그 킬림과 다른 무늬 없는 결)
//  Z51z 쿠션 빌드 / Z52a 쿠션 되돌림  — TentBedroom/Cushions: TV 벽(−x) 쪽 바닥, 2장 쌓고 1장 따로. 올리브 · 오트밀 · 테라코타 (현대 캠핑 톤)
//  Z52b 양털 빌드 / Z52c 양털 되돌림  — TentBedroom/Beanbags/Beanbag_1/Sheepskin: 머스터드 빈백 등받이를 감싸는 양털 러그
//     빈백 표면을 중심에서 방사 레이캐스트로 따라가 1.4 cm 띄운 패치. 가장자리는 불규칙한 양털 윤곽(알파 컷아웃)
//  장식용 (앉기·충돌 없음)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteBedroomSoft
{
    const string DIR = "Assets/Bedroom/Soft/";
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;

    // 쿠션: (방 로컬 위치, yaw, 색 번호). 두 번째는 첫 번째 위에 쌓임
    const float CW = 0.50f, CH = 0.11f;
    static readonly (Vector3 p, float yaw, int col)[] CUSH =
    {
        (new Vector3(-1.86f, 0f, -1.38f), 12f, 0),
        (new Vector3(-1.83f, CH * 0.82f, -1.33f), -9f, 1),
        (new Vector3(-1.98f, 0f, -0.70f), -22f, 2),
    };
    static readonly Color[] CCOL = { new Color(0.33f, 0.35f, 0.24f), new Color(0.78f, 0.72f, 0.61f), new Color(0.62f, 0.30f, 0.20f) };   // 올리브 · 오트밀 · 테라코타
    static readonly string[] CNAME = { "Olive", "Oatmeal", "Terracotta" };

    // ─────────────── 쿠션 ───────────────
    [MenuItem("Tools/Pyrite3/Z51z. Bedroom Cushions Build", false, 5126)]
    public static void CushionsBuild() => Run("Z51z", () =>
    {
        var room = Room(); if (room == null) return false;
        var old = room.Find("Cushions"); if (old) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Cushions").transform; root.SetParent(room, false);
        var weave = WeaveTex(); var mesh = SaveMesh(CushionMesh(), "Cushion");
        int tris = 0;
        for (int i = 0; i < CUSH.Length; i++)
        {
            var c = CUSH[i];
            var m = Mat("M_Cushion_" + CNAME[c.col], CCOL[c.col], 0.06f); m.mainTexture = weave; m.mainTextureScale = new Vector2(3f, 3f);
            var g = new GameObject("Cushion_" + (i + 1) + "_" + CNAME[c.col]); g.transform.SetParent(root, false);
            g.transform.localPosition = c.p; g.transform.localRotation = Quaternion.Euler(0f, c.yaw, 0f);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.lightProbeUsage = LightProbeUsage.Off; mr.shadowCastingMode = ShadowCastingMode.On;
            g.layer = PyriteBedroomV3.LAYER;
            tris += mesh.triangles.Length / 3;
            sb.AppendLine("  쿠션 " + (i + 1) + " " + CNAME[c.col] + " " + V(c.p) + " yaw " + c.yaw);
        }
        sb.AppendLine("쿠션 " + CUSH.Length + "장 " + CW + " × " + CW + " × " + CH + " m, 삼각형 " + tris + " · TV 벽(x −2.67)에서 약 " + (2.67f + CUSH[0].p.x).ToString("F2") + " m");
        Save();
        Shots(room, "cu");
        return true;
    });

    [MenuItem("Tools/Pyrite3/Z52a. Bedroom Cushions Revert", false, 5127)]
    public static void CushionsRevert() => Run("Z52a", () => { var room = Room(); var t = room ? room.Find("Cushions") : null; if (t) Object.DestroyImmediate(t.gameObject); Save(); return true; });

    // 둥근 모서리 방석 (초타원, 윗면 가운데 단추 자리 살짝 눌림)
    static Mesh CushionMesh()
    {
        const int NU = 40, NV = 16; float a = CW / 2f, b = CH / 2f, e1 = 0.28f, e2 = 0.22f;
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
        float SP(float x, float e) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);
        for (int j = 0; j <= NV; j++)
        {
            float v = -Mathf.PI / 2f + Mathf.PI * j / NV;
            for (int i = 0; i <= NU; i++)
            {
                float u = -Mathf.PI + 2f * Mathf.PI * i / NU;
                float cv = SP(Mathf.Cos(v), e1), sv = SP(Mathf.Sin(v), e1);
                float x = a * cv * SP(Mathf.Cos(u), e2), z = a * cv * SP(Mathf.Sin(u), e2), y = b * sv;
                // 윗면 가운데로 갈수록 살짝 부풀고, 정중앙은 단추로 눌림
                float r = Mathf.Sqrt(x * x + z * z) / a;
                if (y > 0f) y += 0.012f * (1f - r * r) - 0.016f * Mathf.Exp(-r * r / 0.012f);
                vs.Add(new Vector3(x, y + b, z));
                uv.Add(new Vector2(x / CW + 0.5f, z / CW + 0.5f));
            }
        }
        int row = NU + 1;
        for (int j = 0; j < NV; j++)
            for (int i = 0; i < NU; i++)
            {
                int p = j * row + i, q = p + 1, r = p + row, s = r + 1;
                ts.Add(p); ts.Add(r); ts.Add(q); ts.Add(q); ts.Add(r); ts.Add(s);
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(ts, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 천 결 (회색 → 재질 색이 곱해짐)
    static Texture2D WeaveTex()
    {
        int n = 128; var px = new Color[n * n]; var rnd = new System.Random(3);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float w = 0.92f + 0.05f * Mathf.Sin(x * Mathf.PI) * Mathf.Sin(y * Mathf.PI * 0.5f) + 0.04f * ((x + y) % 2 == 0 ? 1f : -1f) + 0.03f * (float)(rnd.NextDouble() - 0.5);
            px[y * n + x] = new Color(w, w, w);
        }
        return SaveTex(px, n, n, "T_Weave.png", true);
    }

    // ─────────────── 양털 ───────────────
    [MenuItem("Tools/Pyrite3/Z52b. Beanbag Sheepskin Build", false, 5128)]
    public static void SheepBuild() => Run("Z52b", () =>
    {
        var room = Room(); if (room == null) return false;
        var bag = room.Find("Beanbags/Beanbag_1"); if (bag == null) { sb.AppendLine("!! Beanbags/Beanbag_1 없음"); return false; }
        var old = bag.Find("Sheepskin"); if (old) Object.DestroyImmediate(old.gameObject);
        var body = bag.Find("Body"); var bmf = body ? body.GetComponent<MeshFilter>() : null;
        if (bmf == null) { sb.AppendLine("!! Beanbag_1/Body 없음"); return false; }
        var mc = body.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = bmf.sharedMesh;
        Mesh mesh;
        int hit = 0, miss = 0;
        try
        {
            // 등받이(로컬 −Z) 중심 방사 패치: 극각 th (위 0 → 옆 π/2 넘어서) · 방위 ph (뒤 π 중심)
            const int NU = 28, NV = 22; float cy = 0.26f;   // 방사 중심 높이 (빈백 높이 0.62)
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
            var center = body.TransformPoint(new Vector3(0f, cy, 0.02f));
            for (int j = 0; j <= NV; j++)
            {
                float tv = j / (float)NV;
                float th = Mathf.Lerp(0.06f, 0.74f, tv) * Mathf.PI;          // 위에서 등 아래쪽까지
                for (int i = 0; i <= NU; i++)
                {
                    float tu = i / (float)NU;
                    float ph = Mathf.PI + Mathf.Lerp(-0.34f, 0.34f, tu) * Mathf.PI * Mathf.Lerp(0.55f, 1f, Mathf.Sin(th));   // 위쪽은 좁게
                    var dirL = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    var dirW = body.TransformDirection(dirL).normalized;
                    Vector3 pW;
                    if (mc.Raycast(new Ray(center + dirW * 2f, -dirW), out var h, 3f)) { pW = h.point + h.normal * 0.014f; hit++; }
                    else { pW = center + dirW * 0.45f; miss++; }
                    vs.Add(bag.InverseTransformPoint(pW));
                    uv.Add(new Vector2(tu, 1f - tv));
                }
            }
            int row = NU + 1;
            for (int j = 0; j < NV; j++)
                for (int i = 0; i < NU; i++)
                {
                    int p = j * row + i, q = p + 1, r = p + row, s = r + 1;
                    ts.Add(p); ts.Add(q); ts.Add(r); ts.Add(q); ts.Add(s); ts.Add(r);
                }
            mesh = new Mesh { name = "Sheepskin" }; mesh.SetVertices(vs); mesh.SetUVs(0, uv); mesh.SetTriangles(ts, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // 바깥을 보게: 법선이 안쪽이면 감김 뒤집기
            var nrm = mesh.normals; var cen = bag.InverseTransformPoint(center); int inward = 0;
            for (int k = 0; k < vs.Count; k++) if (Vector3.Dot(nrm[k], vs[k] - cen) < 0f) inward++;
            if (inward > vs.Count / 2) { var t2 = mesh.triangles; for (int k = 0; k < t2.Length; k += 3) { int tmp = t2[k + 1]; t2[k + 1] = t2[k + 2]; t2[k + 2] = tmp; } mesh.triangles = t2; mesh.RecalculateNormals(); sb.AppendLine("  (감김 뒤집음)"); }
        }
        finally { Object.DestroyImmediate(mc); }

        var mat = Mat("M_Sheepskin", new Color(0.93f, 0.89f, 0.81f), 0.04f);
        mat.mainTexture = SheepTex();
        mat.SetFloat("_Mode", 1f); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON"); mat.DisableKeyword("_ALPHABLEND_ON"); mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
        var go = new GameObject("Sheepskin"); go.transform.SetParent(bag, false); go.layer = PyriteBedroomV3.LAYER;
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "Sheepskin");
        var r2 = go.AddComponent<MeshRenderer>(); r2.sharedMaterial = mat; r2.lightProbeUsage = LightProbeUsage.Off; r2.shadowCastingMode = ShadowCastingMode.On;
        sb.AppendLine("양털 " + bag.name + " 등받이 패치: 레이캐스트 적중 " + hit + " / 빗나감 " + miss + ", 삼각형 " + mesh.triangles.Length / 3 + ", bounds " + mesh.bounds.size.ToString("F2"));
        Save();
        Shots(room, "sk");
        return true;
    });

    [MenuItem("Tools/Pyrite3/Z52c. Beanbag Sheepskin Revert", false, 5129)]
    public static void SheepRevert() => Run("Z52c", () => { var room = Room(); var t = room ? room.Find("Beanbags/Beanbag_1/Sheepskin") : null; if (t) Object.DestroyImmediate(t.gameObject); Save(); return true; });

    // 양털: 크림색 털 뭉치(저주파 얼룩 + 고주파 결) · 알파 = 불규칙한 양털 윤곽 (가장자리 물결 + 털끝)
    static Texture2D SheepTex()
    {
        int w = 512, h = 512; var px = new Color[w * h]; var rnd = new System.Random(9);
        float[] ph = Enumerable.Range(0, 8).Select(_ => (float)rnd.NextDouble() * 6.28f).ToArray();
        float Noise(float x, float y, float f) => 0.5f + 0.25f * Mathf.Sin(x * f + ph[0] + Mathf.Sin(y * f * 0.7f + ph[1]) * 2f) + 0.25f * Mathf.Sin(y * f * 1.3f + ph[2] + Mathf.Sin(x * f * 0.9f + ph[3]) * 2f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = x / (float)w, v = y / (float)h;
            float clump = Noise(u, v, 40f), fine = Noise(u * 3.1f, v * 2.7f, 90f), grain = (float)rnd.NextDouble();
            float lum = 0.86f + 0.10f * clump + 0.05f * fine - 0.05f * grain;
            // 윤곽: 가장자리까지 거리 + 물결(양털 러그 모양) — 위아래(v) 끝은 둥글게, 좌우(u) 는 가죽 다리 자국처럼 두 번 파임
            float du = Mathf.Min(u, 1f - u), dv = Mathf.Min(v, 1f - v);
            float wave = 0.035f * Mathf.Sin(v * 19f + ph[4]) + 0.02f * Mathf.Sin(v * 43f + ph[5]) + 0.03f * Mathf.Exp(-Mathf.Pow((v - 0.3f) / 0.07f, 2f)) + 0.03f * Mathf.Exp(-Mathf.Pow((v - 0.72f) / 0.07f, 2f));
            float waveV = 0.03f * Mathf.Sin(u * 17f + ph[6]) + 0.02f * Mathf.Sin(u * 37f + ph[7]);
            float edge = Mathf.Min(du - 0.03f - wave, dv - 0.03f - waveV);
            float tuft = 0.012f * (fine - 0.5f) + 0.01f * (grain - 0.5f);
            float a = edge + tuft > 0f ? 1f : 0f;
            float tip = Mathf.Clamp01(edge / 0.04f);                       // 가장자리 털끝은 조금 어둡고 누렇게
            px[y * w + x] = new Color(lum * (0.97f + 0.03f * tip), lum * (0.95f + 0.05f * tip), lum * (0.88f + 0.12f * tip), a);
        }
        return SaveTex(px, w, h, "T_Sheepskin.png", false, true);
    }

    // ─────────────── 공통 ───────────────
    static void Run(string tag, System.Func<bool> body)
    {
        sb = new StringBuilder("[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (body()) sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_soft.txt", sb.ToString(), new UTF8Encoding(false));
    }

    static Transform Room()
    {
        var r = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        if (r == null) sb.AppendLine("!! TentBedroom 없음");
        return r ? r.transform : null;
    }

    static void Save() { EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes(); }
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static Material Mat(string name, Color c, float gloss)
    {
        Directory.CreateDirectory(DIR);
        string path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        Directory.CreateDirectory(DIR);
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Texture2D SaveTex(Color[] px, int w, int h, string file, bool repeat, bool alpha = false)
    {
        Directory.CreateDirectory(DIR);
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true); t.SetPixels(px); t.Apply();
        string path = DIR + file; File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; imp.alphaIsTransparency = alpha; imp.mipmapEnabled = true;
        if (alpha) imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = 0.5f;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Shots(Transform room, string tag)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            Vector3 W(float x, float y, float z) => room.TransformPoint(new Vector3(x, y, z));
            cam.fieldOfView = 55f;
            if (tag == "cu")
            {
                Shot(cam, W(-0.6f, 1.25f, -0.2f), W(-1.9f, 0.1f, -1.05f), "cu_close");
                Shot(cam, W(1.6f, 1.55f, 2.1f), W(-1.2f, 0.3f, -0.8f), "cu_room");
            }
            else
            {
                Shot(cam, W(0.2f, 1.15f, 1.9f), W(-1.05f, 0.45f, 0.95f), "sk_side");     // 러그 쪽에서
                Shot(cam, W(-2.2f, 1.1f, 1.9f), W(-1.05f, 0.45f, 0.95f), "sk_back");     // 등 뒤쪽에서
                Shot(cam, W(1.6f, 1.55f, 2.1f), W(-0.8f, 0.4f, 0.9f), "sk_room");
            }
        }
        finally
        {
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag);
        Object.DestroyImmediate(tex);
    }
}
#endif
