// PyritePolaroids.cs — 머리맡 벽 폴라로이드 줄 (Z51v 빌드 / Z51w 되돌림). 재실행 안전
//  2026-09-30 관리자: 침대 주변 채우기 C = 폴라로이드 줄
//  루트 TentBedroom/Polaroids: 머리맡 벽(−z)을 따라 끈(황마색) 한 줄, 가운데 처짐, 사진 6장을 나무 집게로 걺
//  사진 = README 월드 렌더(docs/images) 6장을 정사각으로 잘라 인화 느낌을 입힌 아틀라스 Assets/Bedroom/Polaroid/T_Polaroids.png (768×624, 3×2, 칸 256×312)
//  벽 곡면 = PyriteBedroomBuild.SurfZ(x, y). 패널(위 끝 1.23 m)·아이콘(1.04 m)보다 위
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

public static class PyritePolaroids
{
    const string ROOT = "Polaroids";
    const string DIR = "Assets/Bedroom/Polaroid/";
    const string TEX = DIR + "T_Polaroids.png";
    const string PREV = "Assets/_preview/bedroom/";
    const float X0 = -1.05f, X1 = 1.05f, Y_END = 1.58f, SAG = 0.10f, GAP = 0.035f;
    const float PW = 0.110f, PH = 0.134f;   // 폴라로이드 88×107 mm 의 1.25 배 (VR 에서 보이게)
    static readonly float[] PX = { -0.78f, -0.47f, -0.16f, 0.16f, 0.47f, 0.78f };
    static readonly float[] ROLL = { 4f, -3f, 2f, -5f, 3f, -2f };
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z51v. Bedroom Polaroids Build", false, 5122)]
    public static void Build()
    {
        sb = new StringBuilder("[Z51v] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51w. Bedroom Polaroids Revert", false, 5123)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z51w] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom"); var t = room ? room.transform.Find(ROOT) : null;
        if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(ROOT + " 삭제"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    // 끈 위의 점 (방 로컬). t 0 → 왼쪽 끝, 1 → 오른쪽 끝
    static Vector3 Rope(float t)
    {
        float x = Mathf.Lerp(X0, X1, t);
        float y = Y_END - SAG * 4f * t * (1f - t);
        return new Vector3(x, y, -PyriteBedroomBuild.SurfZ(x, y) + GAP);
    }

    static bool Inner()
    {
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return false; }
        var o = room.transform;
        var old = o.Find(ROOT); if (old) Object.DestroyImmediate(old.gameObject);
        var tex = ImportTex(); if (tex == null) { sb.AppendLine("!! 텍스처 없음 " + TEX); return false; }
        var root = new GameObject(ROOT).transform; root.SetParent(o, false);

        var mPhoto = Mat("M_Polaroid", Color.white, 0.32f); mPhoto.mainTexture = tex;
        var mTwine = Mat("M_Twine", new Color(0.50f, 0.40f, 0.27f), 0.08f);
        var lt = o.Find("Furniture/LowTable"); var ltr = lt ? lt.GetComponentsInChildren<Renderer>(true).FirstOrDefault() : null;
        var mClip = ltr ? ltr.sharedMaterial : Mat("M_ClipWood", new Color(0.55f, 0.40f, 0.24f), 0.2f);
        int tris = 0;

        // 끈: 지름 3 mm 사각 튜브, 60 구간 + 양 끝 핀
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var ts = new List<int>(); const int S = 60; float w = 0.0015f;
            for (int i = 0; i <= S; i++)
            {
                float t = i / (float)S; var p = Rope(t);
                var tan = (Rope(Mathf.Min(1f, t + 0.01f)) - Rope(Mathf.Max(0f, t - 0.01f))).normalized;
                var a = Vector3.Cross(tan, Vector3.up).normalized; var b = Vector3.Cross(a, tan).normalized;
                foreach (var d in new[] { a + b, -a + b, -a - b, a - b }) { vs.Add(p + d * w); ns.Add(d.normalized); }
                if (i > 0) { int s0 = (i - 1) * 4, s1 = i * 4; for (int k = 0; k < 4; k++) { int k1 = (k + 1) % 4; ts.Add(s0 + k); ts.Add(s1 + k); ts.Add(s0 + k1); ts.Add(s0 + k1); ts.Add(s1 + k); ts.Add(s1 + k1); } }
            }
            var m = new Mesh { name = "PolaroidTwine" }; m.SetVertices(vs); m.SetNormals(ns); m.SetTriangles(ts, 0); m.RecalculateBounds();
            var g = new GameObject("Twine"); g.transform.SetParent(root, false);
            g.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m, "PolaroidTwine");
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mTwine; mr.shadowCastingMode = ShadowCastingMode.Off;
            tris += ts.Count / 3;
            foreach (var t in new[] { 0f, 1f }) { Box(root, "Pin", Rope(t) + new Vector3(0, 0.004f, -0.012f), new Vector3(0.010f, 0.010f, 0.024f), mClip); tris += 12; }
        }

        // 사진: 위 가운데(집게 자리)가 피벗, 앞(+z, 방 쪽)을 봄
        var quad = SaveMesh(QuadTopPivot(PW, PH), "PolaroidQuad");
        for (int i = 0; i < PX.Length; i++)
        {
            float t = Mathf.InverseLerp(X0, X1, PX[i]); var p = Rope(t);
            var g = new GameObject("Photo_" + (i + 1)); g.transform.SetParent(root, false);
            g.transform.localPosition = p + new Vector3(0f, -0.004f, 0.003f);
            g.transform.localRotation = Quaternion.Euler(0f, 180f, ROLL[i]);   // 쿼드는 −z 를 봄 → 180° 돌려 방 쪽
            g.AddComponent<MeshFilter>().sharedMesh = CellMesh(quad, i);
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mPhoto; mr.shadowCastingMode = ShadowCastingMode.Off;
            tris += 2;
            Box(g.transform, "Clip", new Vector3(0f, -0.004f, -0.004f), new Vector3(0.009f, 0.026f, 0.007f), mClip); tris += 12;
            sb.AppendLine("  사진 " + (i + 1) + " " + V(p) + " 기울기 " + ROLL[i] + "°, 아래 끝 y " + (p.y - PH).ToString("F2") + " · 그 높이 벽 z " + (-PyriteBedroomBuild.SurfZ(p.x, p.y - PH)).ToString("F3") + " (끈 z " + p.z.ToString("F3") + ")");
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { r.gameObject.layer = PyriteBedroomV3.LAYER; r.lightProbeUsage = LightProbeUsage.Off; }
        sb.AppendLine("끈 " + V(Rope(0f)) + " ~ 가운데 " + V(Rope(0.5f)) + " ~ " + V(Rope(1f)) + " · 벽에서 " + GAP + " m · 처짐 " + SAG + " m");
        sb.AppendLine("사진 " + PX.Length + "장 " + (PW * 1000).ToString("F0") + "×" + (PH * 1000).ToString("F0") + " mm, 아래 끝 최저 " + (Rope(0.5f).y - PH).ToString("F2") + " m (패널 위 끝 1.23 m) · 삼각형 " + tris);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    static Texture2D ImportTex()
    {
        if (!File.Exists(TEX)) return null;
        AssetDatabase.ImportAsset(TEX, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(TEX);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.mipmapEnabled = true; imp.maxTextureSize = 1024;
        imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TEX);
    }

    // 위 가운데 피벗 쿼드 (−z 를 봄, Unity Quad 와 같은 감김)
    static Mesh QuadTopPivot(float w, float h)
    {
        var m = new Mesh { name = "PolaroidQuad" };
        m.SetVertices(new List<Vector3> { new Vector3(-w / 2, -h, 0), new Vector3(w / 2, -h, 0), new Vector3(-w / 2, 0, 0), new Vector3(w / 2, 0, 0) });
        m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
        m.SetTriangles(new[] { 0, 3, 1, 3, 0, 2 }, 0);
        m.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
        m.RecalculateBounds();
        return m;
    }

    // 아틀라스 칸 i (3×2, 위 줄부터) 로 UV 를 옮긴 사본
    static Mesh CellMesh(Mesh q, int i)
    {
        int col = i % 3, row = i / 3;   // row 0 = 이미지 위 줄 = UV v 위쪽
        float u0 = col / 3f, u1 = (col + 1) / 3f, v1 = 1f - row / 2f, v0 = v1 - 0.5f;
        var m = Object.Instantiate(q); m.name = "PolaroidCell_" + (i + 1);
        m.SetUVs(0, new List<Vector2> { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u0, v1), new Vector2(u1, v1) });
        return SaveMesh(m, "PolaroidCell_" + (i + 1));
    }

    static Transform Box(Transform p, string n, Vector3 center, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = n; g.transform.SetParent(p, false); g.transform.localPosition = center; g.transform.localScale = size;
        var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
        return g.transform;
    }

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
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

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
            Shot(cam, W(0.0f, 1.00f, -0.55f), W(0.0f, 1.20f, -2.55f), "pl_bed");        // 매트 발치에 앉아서 머리맡 벽
            Shot(cam, W(0.35f, 1.35f, -1.75f), W(0.10f, 1.45f, -2.50f), "pl_close");     // 가까이
            Shot(cam, W(1.6f, 1.55f, 2.1f), W(0f, 1.0f, -2.2f), "pl_room");              // 방 전경
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
        File.WriteAllText("Logs/pyrite_polaroids.txt", sb.ToString(), new UTF8Encoding(false));
    }
}
#endif
