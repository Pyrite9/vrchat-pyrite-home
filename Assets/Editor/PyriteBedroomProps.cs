// PyriteBedroomProps.cs — 텐트 침실 소품 1차 (Z51b 빌드 / Z51c 되돌리기 / Z51d 렌더). 재실행 안전
//  2026-09-29 관리자: "순서대로 시행" 1번 = 테이블 소품 + 러그. 전부 정적 장식(콜라이더·Udon 없음), 레이어 24, 라이트 프로브 끔(침실 규칙)
//  루트 TentBedroom/Props: Rug(2.2×1.6 m, 킬림 무늬 텍스처 코드 생성 + 양끝 술) · Books(3권 쌓음) · Mug(캠프 Mug_0 재사용) · PyriteCluster(정육면체 4개) · WaterBottle(머리맡 오른쪽 바닥)
//  에셋 Assets/Bedroom/Props/ (텍스처·머티리얼·메시 — 전부 우리 소유)
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

public static class PyriteBedroomProps
{
    const string DIR = "Assets/Bedroom/Props/";
    const string PREV = "Assets/_preview/bedroom/";
    const string ROOT = "Props";
    const float TABLE_Y = 0.35f, TABLE_Z = 2.52f;
    static StringBuilder sb;
    static Transform root;

    [MenuItem("Tools/Pyrite3/Z51b. Bedroom Props Build", false, 5102)]
    public static void Build()
    {
        sb = new StringBuilder("[Z51b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51c. Bedroom Props Revert", false, 5103)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z51c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom"); var t = room ? room.transform.Find(ROOT) : null;
        if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(ROOT + " 삭제 (에셋은 남김)"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51d. Bedroom Props Renders", false, 5104)]
    public static void RenderOnly()
    {
        sb = new StringBuilder("[Z51d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
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
        root = new GameObject(ROOT).transform; root.SetParent(room.transform, false);

        BuildRug();
        BuildBooks();
        BuildMug();
        BuildPyrite();
        BuildBottle();

        int tris = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.gameObject.layer = PyriteBedroomV3.LAYER;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
            var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            foreach (var c in r.GetComponents<Collider>()) Object.DestroyImmediate(c);
        }
        sb.AppendLine("소품 삼각형 합계 " + tris);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    // ───────────── 공통 ─────────────
    static Material Mat(string name, Color c, float gloss, float metal = 0f, Texture tex = null, bool fade = false)
    {
        string path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metal); m.mainTexture = tex;
        if (fade)
        {
            m.SetFloat("_Mode", 2); m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0); m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center; g.transform.localRotation = rot ?? Quaternion.identity; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material m, Vector3 pos, Quaternion rot)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = rot;
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    static Texture2D SaveTex(Texture2D t, string name, bool linear = false)
    {
        string path = DIR + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.sRGBTexture = !linear; imp.mipmapEnabled = true; imp.anisoLevel = 4; imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────────── 러그 ─────────────
    const float RUG_W = 2.2f, RUG_D = 1.6f, RUG_Z = 1.10f;
    static void BuildRug()
    {
        var tex = SaveTex(RugTexture(1024, 744), "T_Rug");
        var mRug = Mat("M_Rug", Color.white, 0.08f, 0f, tex);
        var mFringe = Mat("M_RugFringe", new Color(0.84f, 0.78f, 0.64f), 0.05f);
        var rug = new GameObject("Rug").transform; rug.SetParent(root, false); rug.localPosition = new Vector3(0f, 0f, RUG_Z);
        Box(rug, "Body", new Vector3(0f, 0.004f, 0f), new Vector3(RUG_W, 0.008f, RUG_D), mRug);
        // 술: 짧은 두 변(x = ±W/2) 에 가닥 34개씩, 폭 1.2 cm · 길이 6~8 cm, 바닥에 눕힘
        var vs = new List<Vector3>(); var ts = new List<int>(); var ns = new List<Vector3>(); var uv = new List<Vector2>();
        var rnd = new System.Random(7);
        foreach (float side in new[] { -1f, 1f })
            for (int i = 0; i < 34; i++)
            {
                float z = -RUG_D / 2f + 0.03f + i * (RUG_D - 0.06f) / 33f;
                float len = 0.06f + (float)rnd.NextDouble() * 0.02f, w = 0.012f, y = 0.0025f + (float)rnd.NextDouble() * 0.001f;
                float x0 = side * RUG_W / 2f, x1 = x0 + side * len;
                float bend = ((float)rnd.NextDouble() - 0.5f) * 0.02f;
                int b = vs.Count;
                vs.Add(new Vector3(x0, y, z - w / 2)); vs.Add(new Vector3(x0, y, z + w / 2)); vs.Add(new Vector3(x1, y, z + w / 2 + bend)); vs.Add(new Vector3(x1, y, z - w / 2 + bend));
                for (int k = 0; k < 4; k++) { ns.Add(Vector3.up); uv.Add(Vector2.zero); }
                if (side > 0) { ts.Add(b); ts.Add(b + 1); ts.Add(b + 2); ts.Add(b); ts.Add(b + 2); ts.Add(b + 3); }
                else { ts.Add(b); ts.Add(b + 2); ts.Add(b + 1); ts.Add(b); ts.Add(b + 3); ts.Add(b + 2); }
            }
        var fm = new Mesh(); fm.SetVertices(vs); fm.SetNormals(ns); fm.SetUVs(0, uv); fm.SetTriangles(ts, 0); fm.RecalculateBounds();
        MeshObj(rug, "Fringe", SaveMesh(fm, "RugFringe"), mFringe, Vector3.zero, Quaternion.identity);
        sb.AppendLine("러그 " + RUG_W + "×" + RUG_D + " m, 중심 z " + RUG_Z + ", 술 68가닥, 텍스처 1024×744");
    }

    // 킬림: 테두리 띠 3겹 + 가운데 마름모 3개(겹 윤곽) + 바탕 작은 계단 무늬 + 털실 결
    static Texture2D RugTexture(int W, int H)
    {
        Color red = new Color(0.50f, 0.12f, 0.09f), mustard = new Color(0.80f, 0.58f, 0.20f), cream = new Color(0.88f, 0.82f, 0.68f),
              indigo = new Color(0.16f, 0.21f, 0.34f), charcoal = new Color(0.16f, 0.13f, 0.12f), rust = new Color(0.66f, 0.28f, 0.12f);
        var t = new Texture2D(W, H, TextureFormat.RGB24, true);
        var px = new Color[W * H];
        var rnd = new System.Random(3);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * RUG_W, v = (y + 0.5f) / H * RUG_D;     // m 단위
                float e = Mathf.Min(Mathf.Min(u, RUG_W - u), Mathf.Min(v, RUG_D - v));
                Color c;
                if (e < 0.035f) c = charcoal;
                else if (e < 0.055f) c = cream;
                else if (e < 0.14f)
                {
                    // 테두리 띠: 톱니(삼각) 무늬
                    float along = (u < 0.14f || u > RUG_W - 0.14f) ? v : u;
                    float saw = Mathf.Abs(Mathf.Repeat(along / 0.08f, 1f) - 0.5f) * 2f;   // 0..1
                    c = (e - 0.055f) / 0.085f < saw ? mustard : indigo;
                }
                else if (e < 0.16f) c = cream;
                else
                {
                    c = red;
                    // 가운데 마름모 3개 (가로로)
                    float cy = RUG_D / 2f;
                    for (int k = -1; k <= 1; k++)
                    {
                        float cx = RUG_W / 2f + k * 0.58f;
                        float d = Mathf.Abs(u - cx) / 0.30f + Mathf.Abs(v - cy) / 0.46f;   // 마름모 거리 (1 = 가장자리)
                        if (d < 1f)
                        {
                            if (d > 0.88f) c = cream;
                            else if (d > 0.74f) c = indigo;
                            else if (d > 0.60f) c = mustard;
                            else if (d > 0.36f) c = rust;
                            else if (d > 0.22f) c = cream;
                            else c = indigo;
                        }
                    }
                    // 바탕 계단 무늬 (마름모 밖)
                    if (c == red)
                    {
                        float gu = Mathf.Repeat(u, 0.10f), gv = Mathf.Repeat(v, 0.10f);
                        if (Mathf.Abs(gu - 0.05f) + Mathf.Abs(gv - 0.05f) < 0.018f) c = mustard;
                    }
                }
                // 털실 결: 가로 씨실 줄무늬 + 작은 잡음
                float weft = 0.93f + 0.07f * Mathf.Sin(v * 2400f);
                float n = 0.94f + (float)rnd.NextDouble() * 0.10f;
                px[y * W + x] = c * weft * n;
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    // ───────────── 책 ─────────────
    static void BuildBooks()
    {
        var pages = Mat("M_BookPages", new Color(0.90f, 0.86f, 0.76f), 0.05f);
        var covers = new[] { Mat("M_BookCover_0", new Color(0.16f, 0.30f, 0.22f), 0.25f), Mat("M_BookCover_1", new Color(0.14f, 0.18f, 0.32f), 0.25f), Mat("M_BookCover_2", new Color(0.42f, 0.11f, 0.10f), 0.25f) };
        var books = new GameObject("Books").transform; books.SetParent(root, false); books.localPosition = new Vector3(-0.17f, TABLE_Y, TABLE_Z);
        // (가로, 두께, 세로, 회전°) 아래부터
        var spec = new[] { (0.22f, 0.034f, 0.155f, 4f), (0.20f, 0.028f, 0.140f, -7f), (0.18f, 0.024f, 0.128f, 12f) };
        float y = 0f;
        for (int i = 0; i < spec.Length; i++)
        {
            var (w, th, d, ang) = spec[i];
            var b = new GameObject("Book_" + i).transform; b.SetParent(books, false);
            b.localPosition = new Vector3(0f, y, 0f); b.localRotation = Quaternion.Euler(0f, ang, 0f);
            float c = 0.003f;   // 표지 두께
            Box(b, "CoverBottom", new Vector3(0f, c / 2f, 0f), new Vector3(w, c, d), covers[i]);
            Box(b, "CoverTop", new Vector3(0f, th - c / 2f, 0f), new Vector3(w, c, d), covers[i]);
            Box(b, "Spine", new Vector3(-w / 2f + c / 2f, th / 2f, 0f), new Vector3(c, th, d), covers[i]);
            Box(b, "Pages", new Vector3(c / 2f + 0.002f, th / 2f, 0f), new Vector3(w - c - 0.006f, th - 2f * c, d - 0.006f), pages);
            y += th;
        }
        sb.AppendLine("책 3권, 쌓은 높이 " + (y * 100f).ToString("F1") + " cm, 위치 x −0.17");
    }

    // ───────────── 머그 ─────────────
    static void BuildMug()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Props/Meshes/Mug_0.asset");
        if (mesh == null) { sb.AppendLine("!! Mug_0 메시 없음"); return; }
        var src = Object.FindObjectsOfType<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh == mesh);
        var mats = src ? src.GetComponent<MeshRenderer>().sharedMaterials : new[] { Mat("M_MugFallback", new Color(0.8f, 0.8f, 0.78f), 0.5f) };
        var g = new GameObject("Mug"); g.transform.SetParent(root, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterials = mats;
        var b = mesh.bounds;
        g.transform.localRotation = Quaternion.Euler(0f, 205f, 0f);
        g.transform.localPosition = new Vector3(0.06f, TABLE_Y - b.min.y, TABLE_Z - 0.05f);
        sb.AppendLine("머그: Mug_0 (" + mesh.triangles.Length / 3 + " tris, 크기 " + b.size.ToString("F2") + ", 머티리얼 " + string.Join("/", mats.Select(m => m ? m.name : "-")) + (src ? " ← " + src.name : " (원본 못 찾음)") + ")");
    }

    // ───────────── 황철석 결정 (정육면체 쌍정) ─────────────
    static void BuildPyrite()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Pyrite.mat");
        if (m == null) { sb.AppendLine("!! M_Pyrite 없음"); return; }
        var cl = new GameObject("PyriteCluster").transform; cl.SetParent(root, false); cl.localPosition = new Vector3(0.21f, TABLE_Y, TABLE_Z + 0.06f);
        // (한 변, 중심 오프셋, 회전) — 가장 큰 것이 바닥에 앉고 나머지가 파고든 모양
        var spec = new[] {
            (0.046f, new Vector3(0f, 0.023f, 0f), new Vector3(0f, 18f, 0f)),
            (0.034f, new Vector3(0.024f, 0.036f, 0.010f), new Vector3(28f, 40f, 12f)),
            (0.028f, new Vector3(-0.020f, 0.030f, 0.018f), new Vector3(-15f, 65f, 30f)),
            (0.020f, new Vector3(0.006f, 0.052f, -0.016f), new Vector3(35f, 10f, -20f)),
        };
        for (int i = 0; i < spec.Length; i++) Box(cl, "Cube_" + i, spec[i].Item2, Vector3.one * spec[i].Item1, m, Quaternion.Euler(spec[i].Item3));
        sb.AppendLine("황철석 결정 정육면체 4개 (4.6/3.4/2.8/2.0 cm), M_Pyrite");
    }

    // ───────────── 물병 ─────────────
    static void BuildBottle()
    {
        var body = Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.034f, 0f), new Vector2(0.036f, 0.006f), new Vector2(0.036f, 0.170f), new Vector2(0.030f, 0.190f), new Vector2(0.024f, 0.200f), new Vector2(0.001f, 0.200f) }, 24);
        var water = Lathe(new[] { new Vector2(0.001f, 0.004f), new Vector2(0.033f, 0.004f), new Vector2(0.033f, 0.120f), new Vector2(0.001f, 0.120f) }, 24);
        var cap = Lathe(new[] { new Vector2(0.001f, 0.198f), new Vector2(0.027f, 0.198f), new Vector2(0.027f, 0.228f), new Vector2(0.024f, 0.232f), new Vector2(0.001f, 0.232f) }, 20);
        var g = new GameObject("WaterBottle").transform; g.SetParent(root, false); g.localPosition = new Vector3(1.30f, 0f, -2.05f); g.localRotation = Quaternion.Euler(0f, 30f, 0f);
        MeshObj(g, "Water", SaveMesh(water, "BottleWater"), Mat("M_BottleWater", new Color(0.45f, 0.66f, 0.85f, 0.60f), 0.9f, 0f, null, true), Vector3.zero, Quaternion.identity);
        MeshObj(g, "Body", SaveMesh(body, "BottleBody"), Mat("M_BottleBody", new Color(0.62f, 0.80f, 0.95f, 0.50f), 0.92f, 0f, null, true), Vector3.zero, Quaternion.identity);
        MeshObj(g, "Cap", SaveMesh(cap, "BottleCap"), Mat("M_BottleCap", new Color(0.12f, 0.22f, 0.38f), 0.45f), Vector3.zero, Quaternion.identity);
        sb.AppendLine("물병 (1.30, 0, −2.05) 높이 23 cm, 물 12 cm");
    }

    // 회전체: 프로필은 높이가 커지는 쪽으로 (법선 바깥 — 00 문서 함정)
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

    // ───────────── 렌더 ─────────────
    static void Renders()
    {
        var room = Root("TentBedroom").transform;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            Vector3 W(float x, float y, float z) => room.TransformPoint(new Vector3(x, y, z));
            cam.fieldOfView = 60f;
            Shot(cam, W(0.05f, 0.75f, 1.75f), W(0.02f, 0.38f, 2.55f), "pr_table");
            Shot(cam, W(-1.8f, 1.6f, -0.3f), W(0.3f, 0.2f, 1.3f), "pr_room");
            Shot(cam, W(0f, 2.9f, 1.1f), W(0f, 0f, 1.12f), "pr_rug_top");
            Shot(cam, W(0.4f, 0.8f, -1.2f), W(1.3f, 0.12f, -2.05f), "pr_bottle");
            Shot(cam, W(-0.28f, 0.45f, -1.9f), W(0f, 0.3f, 2.4f), "pr_lie");
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
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag);
        Object.DestroyImmediate(tex);
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_props.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
