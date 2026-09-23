// Tools ▸ Pyrite ▸ Y6. Lake Water (depth + grid + shader)
//  [E2] 지형 높이맵 → 수심 텍스처 T_LakeDepth.png (R = 수심/4m). 셰이더가 물가 투명·물결 감쇠에 쓴다.
//  [F1] 호수 격자 메시 1m 간격 (Unity Plane 은 정점 간격 11.6m 라 물결이 안 된다). 물·물가 근처 칸만 남긴다.
//  [F2/F3] 머티리얼 3종(노을/밤/새벽)을 Pyrite/Water 로 새로 만들어 시간대 스크립트에 연결.
//         원본 M_LakeWater* 는 그대로 둔다 → 되돌리려면 Y6 Revert.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteLakeWater
{
    const float X0 = -64f, Z0 = -78f, W = 128f;   // 수심 텍스처·격자 범위 (호수 중심 (0,-14) R56 을 덮음)
    const int   RES = 256;
    const float DEPTH_MAX = 4f;
    const string TEX  = "Assets/TerrainAssets/T_LakeDepth.png";
    const string MESH = "Assets/Meshes/PyriteLakeGrid.asset";
    static readonly string[] MATS = { "Assets/Materials/M_Water_Dusk.mat", "Assets/Materials/M_Water_Night.mat", "Assets/Materials/M_Water_Dawn.mat" };

    static MeshFilter LakeFilter(out GameObject water)
    {
        water = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Water");
        if (water == null) return null;
        var fs = water.GetComponentsInChildren<MeshFilter>(true);
        return fs.FirstOrDefault(f => f.sharedMesh != null && (f.sharedMesh.name == "Plane" || f.sharedMesh.name == "PyriteLakeGrid"))
               ?? fs.FirstOrDefault();
    }

    [MenuItem("Tools/Pyrite/Y6. Lake Water (depth + grid + shader)", false, 295)]
    public static void Run()
    {
        var log = new StringBuilder("[Y6] ");
        var t = Terrain.activeTerrain;
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        GameObject water;
        var mf = LakeFilter(out water);
        var sh = Shader.Find("Pyrite/Water");
        if (t == null || tod == null || mf == null || sh == null) { Debug.LogError("[Y6] Terrain/ToD/Water/Pyrite/Water 셰이더 중 없음"); return; }
        float waterY = mf.transform.position.y;
        float G(float x, float z) => t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;

        // ── 1. 수심 텍스처
        var tex = new Texture2D(RES, RES, TextureFormat.RGBA32, false, true);
        var px = new Color[RES * RES];
        for (int j = 0; j < RES; j++)
            for (int i = 0; i < RES; i++)
            {
                float x = X0 + (i + 0.5f) / RES * W, z = Z0 + (j + 0.5f) / RES * W;
                float d = Mathf.Clamp01((waterY - G(x, z)) / DEPTH_MAX);
                px[j * RES + i] = new Color(d, d, d, 1f);
            }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(TEX, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TEX, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(TEX);
        ti.sRGBTexture = false; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = RES; ti.alphaSource = TextureImporterAlphaSource.None;
        ti.SaveAndReimport();
        var depthTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX);
        log.Append("depth ").Append(RES).Append("² | ");

        // ── 2. 격자 메시 (1m) — 네 모서리 중 하나라도 수면+0.3m 아래인 칸만
        int N = Mathf.RoundToInt(W);
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tans = new List<Vector4>(); var uvs = new List<Vector2>();
        var idx = new Dictionary<int, int>(); var tris = new List<int>();
        var tr = mf.transform;
        Vector3 nLocal = tr.InverseTransformDirection(Vector3.up).normalized;
        Vector3 tLocal = tr.InverseTransformDirection(Vector3.right).normalized;
        int V(int i, int j)
        {
            int key = j * (N + 1) + i; int v;
            if (idx.TryGetValue(key, out v)) return v;
            float x = X0 + i, z = Z0 + j;
            v = verts.Count; idx[key] = v;
            verts.Add(tr.InverseTransformPoint(new Vector3(x, waterY, z)));
            norms.Add(nLocal); tans.Add(new Vector4(tLocal.x, tLocal.y, tLocal.z, 1f)); uvs.Add(new Vector2(x / 10f, z / 10f));
            return v;
        }
        var wet = new bool[N + 1, N + 1];
        for (int j = 0; j <= N; j++) for (int i = 0; i <= N; i++) wet[i, j] = G(X0 + i, Z0 + j) < waterY + 0.3f;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                if (!(wet[i, j] || wet[i + 1, j] || wet[i, j + 1] || wet[i + 1, j + 1])) continue;
                int a = V(i, j), b = V(i + 1, j), c = V(i, j + 1), d = V(i + 1, j + 1);
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MESH);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "PyriteLakeGrid";
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTangents(tans); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        var bb = mesh.bounds; bb.Expand(new Vector3(0f, 0.4f, 0f)); mesh.bounds = bb;
        if (isNew) AssetDatabase.CreateAsset(mesh, MESH);
        EditorUtility.SetDirty(mesh);
        string oldMesh = mf.sharedMesh != null ? mf.sharedMesh.name : "(none)";
        Undo.RecordObject(mf, "lake grid");
        mf.sharedMesh = mesh;
        log.Append("grid ").Append(verts.Count).Append("v ").Append(tris.Count / 3).Append("t (was ").Append(oldMesh).Append(") | ");

        // ── 3. 머티리얼 3종
        var src = tod.waterMat ?? new Material[0];
        var outM = new Material[3];
        for (int k = 0; k < 3; k++)
        {
            var s = k < src.Length && src[k] != null ? src[k] : (src.Length > 0 ? src[0] : null);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MATS[k]);
            if (m == null)
            {
                if (s == null) { Debug.LogError("[Y6] 원본 물 머티리얼 없음"); return; }
                // 이미 Pyrite/Water 인 걸 원본으로 잡으면 안 된다 (재실행 대비) — M_LakeWater* 에서만 복사
                m = new Material(s); m.shader = sh; AssetDatabase.CreateAsset(m, MATS[k]);
            }
            else if (m.shader != sh) m.shader = sh;
            m.SetTexture("_DepthTex", depthTex);
            m.SetVector("_DepthRect", new Vector4(X0, Z0, 1f / W, 1f / W));
            m.SetFloat("_DepthMax", DEPTH_MAX);
            m.SetFloat("_NormalScale", 0.35f);
            m.SetFloat("_Alpha", 1f);
            m.SetFloat("_FresnelPower", 3f);
            m.SetFloat("_WaveNormal", 1.8f);    // 3 이면 먼 수면에 평행 줄무늬(블라인드)가 생겼다
            m.SetFloat("_ShoreFade", 0.6f);
            m.SetFloat("_ShoreAlpha", 0.25f);
            EditorUtility.SetDirty(m);
            outM[k] = m;
        }
        var mr = mf.GetComponent<MeshRenderer>();
        Undo.RecordObject(mr, "lake mat");
        mr.sharedMaterial = outM[0];
        tod.waterMat = outM;
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        log.Append("mats ").Append(string.Join(",", outM.Select(x => x.name))).Append(" | ");

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(log.ToString());
        Directory.CreateDirectory("Assets/_preview/");
        File.WriteAllText("Assets/_preview/lakewater.txt", log.ToString());
        PyriteViews.CaptureSet("shoreline shore camp_lake camp_left", new[] { 0, 1 });
    }

    [MenuItem("Tools/Pyrite/Y6b. Lake Water Revert (Plane + M_LakeWater)", false, 296)]
    public static void Revert()
    {
        GameObject water; var mf = LakeFilter(out water);
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (mf == null || tod == null) return;
        var plane = Resources.GetBuiltinResource<Mesh>("New-Plane.fbx");
        Undo.RecordObject(mf, "revert"); mf.sharedMesh = plane;
        var a = AssetDatabase.FindAssets("M_LakeWater t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(x => x != null).ToList();
        Material Get(string n) => a.FirstOrDefault(x => x.name == n);
        var m0 = Get("M_LakeWater");
        tod.waterMat = new[] { m0, Get("M_LakeWater_Night") ?? m0, Get("M_LakeWater_Dawn") ?? m0 };
        mf.GetComponent<MeshRenderer>().sharedMaterial = m0;
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Y6b] 되돌림 — Plane + M_LakeWater*");
    }
}
#endif
