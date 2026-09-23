// Tools ▸ Pyrite ▸ Z22b. Silver Crystal Mix  /  Z22c. Revert
//  관리자: 골드 결정 비율이 너무 높고 오른쪽 더미(Landmark_3)가 너무 통일돼 보인다 → 일부를 실버로.
//   M_Pyrite_Silver = M_Pyrite 복사, 색 (0.80, 0.82, 0.86), 광택 0.90
//   골드 9 → 5: Landmark_3 Shard_01·04, Landmark_1 Shard_02, Landmark_2 Shard_04
//   (1차는 Landmark_3 Shard_02 였는데 캠프 쪽에서 큰 Shard_01 뒤에 가려 안 보였다)
//   DayCycle·예전 ToD 의 crystalMats / crystalBaseEmission / crystalBaseGloss / nightMats 에 추가 (Z18b 재실행에도 유지)
//  전후 렌더 12:00 / 18:20 / 21:00 × (오른쪽 더미, 캠프→호수) → Assets/_preview/crystal/silver_*.png
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

public static class PyriteCrystalSilver
{
    const string GOLD = "Assets/Materials/M_Pyrite.mat";
    const string SILVER = "Assets/Materials/M_Pyrite_Silver.mat";
    static readonly Color SILVER_COL = new Color(0.80f, 0.82f, 0.86f, 1f);
    const float SILVER_GLOSS = 0.90f;
    static readonly string[] TARGETS =
    {
        "Crystals/Landmark_3_PyriteCluster_B/Shard_01", "Crystals/Landmark_3_PyriteCluster_B/Shard_04",
        "Crystals/Landmark_1_PyriteCluster_C/Shard_02", "Crystals/Landmark_2_PyriteCluster_C/Shard_04",
    };

    [MenuItem("Tools/Pyrite/Z22b. Silver Crystal Mix", false, 53)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z22b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var gold = AssetDatabase.LoadAssetAtPath<Material>(GOLD);
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        var rs = Targets(sb);
        if (gold == null || cyc == null || rs.Count == 0) { sb.AppendLine("gold/cycle/targets 없음"); Flush(sb); return; }

        Shots(cyc, "before", sb);

        var silver = AssetDatabase.LoadAssetAtPath<Material>(SILVER);
        if (silver == null) { silver = new Material(gold); AssetDatabase.CreateAsset(silver, SILVER); }
        silver.shader = gold.shader;
        silver.CopyPropertiesFromMaterial(gold);
        silver.SetColor("_Color", SILVER_COL);
        silver.SetFloat("_Glossiness", SILVER_GLOSS);
        EditorUtility.SetDirty(silver);

        // 목록 밖 랜드마크 실버(이전 실행분)는 골드로
        foreach (var r in LandmarkRenderers().Where(r => r.sharedMaterial == silver && !rs.Contains(r)))
        { r.sharedMaterial = gold; EditorUtility.SetDirty(r); sb.AppendLine("  back to gold: " + PathOf(r.transform)); }
        foreach (var r in rs) { Undo.RecordObject(r, "silver"); r.sharedMaterial = silver; EditorUtility.SetDirty(r); }

        // 시간대 시스템에 등록
        int gi = System.Array.IndexOf(cyc.crystalMats, gold);
        var em = gi >= 0 ? cyc.crystalBaseEmission[gi] : new Color(0.85f, 0.7f, 0.3f);
        Register(cyc, silver, em, sb);
        UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
        if (tod != null)
        {
            if (!tod.crystalMats.Contains(silver))
            {
                tod.crystalMats = tod.crystalMats.Append(silver).ToArray();
                tod.crystalBaseEmission = tod.crystalBaseEmission.Append(new Color(0.80f, 0.82f, 0.86f)).ToArray();
                tod.crystalBaseGloss = tod.crystalBaseGloss.Append(SILVER_GLOSS).ToArray();
            }
            if (!tod.nightMats.Contains(silver)) tod.nightMats = tod.nightMats.Append(silver).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(tod); EditorUtility.SetDirty(tod);
        }
        cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Shots(cyc, "after", sb);
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Register(PyriteDayCycle cyc, Material silver, Color goldEm, StringBuilder sb)
    {
        if (!cyc.crystalMats.Contains(silver))
        {
            cyc.crystalMats = cyc.crystalMats.Append(silver).ToArray();
            cyc.crystalBaseEmission = cyc.crystalBaseEmission.Append(new Color(0.80f, 0.82f, 0.86f)).ToArray();
            cyc.crystalBaseGloss = cyc.crystalBaseGloss.Append(SILVER_GLOSS).ToArray();
        }
        if (!cyc.nightMats.Contains(silver)) cyc.nightMats = cyc.nightMats.Append(silver).ToArray();
        sb.AppendLine("cycle crystalMats: " + string.Join(", ", cyc.crystalMats.Select(m => m ? m.name : "null")) + " | nightMats " + cyc.nightMats.Length);
    }

    [MenuItem("Tools/Pyrite/Z22c. Silver Crystal Revert", false, 54)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z22c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var gold = AssetDatabase.LoadAssetAtPath<Material>(GOLD);
        var silver = AssetDatabase.LoadAssetAtPath<Material>(SILVER);
        foreach (var r in LandmarkRenderers().Where(r => silver != null && r.sharedMaterial == silver)) { r.sharedMaterial = gold; EditorUtility.SetDirty(r); sb.AppendLine("  gold: " + PathOf(r.transform)); }
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        if (silver != null)
        {
            if (cyc != null)
            {
                int i = System.Array.IndexOf(cyc.crystalMats, silver);
                if (i >= 0)
                {
                    cyc.crystalMats = cyc.crystalMats.Where((m, k) => k != i).ToArray();
                    cyc.crystalBaseEmission = cyc.crystalBaseEmission.Where((m, k) => k != i).ToArray();
                    cyc.crystalBaseGloss = cyc.crystalBaseGloss.Where((m, k) => k != i).ToArray();
                }
                cyc.nightMats = cyc.nightMats.Where(m => m != silver).ToArray();
                UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
                cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
            }
            if (tod != null)
            {
                int i = System.Array.IndexOf(tod.crystalMats, silver);
                if (i >= 0)
                {
                    tod.crystalMats = tod.crystalMats.Where((m, k) => k != i).ToArray();
                    tod.crystalBaseEmission = tod.crystalBaseEmission.Where((m, k) => k != i).ToArray();
                    tod.crystalBaseGloss = tod.crystalBaseGloss.Where((m, k) => k != i).ToArray();
                }
                tod.nightMats = tod.nightMats.Where(m => m != silver).ToArray();
                UdonSharpEditorUtility.CopyProxyToUdon(tod); EditorUtility.SetDirty(tod);
            }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static Renderer[] LandmarkRenderers() => Object.FindObjectsOfType<Renderer>(true).Where(r => PathOf(r.transform).StartsWith("Crystals/Landmark_")).ToArray();

    static List<Renderer> Targets(StringBuilder sb)
    {
        var all = Object.FindObjectsOfType<Renderer>(true);
        var list = new List<Renderer>();
        foreach (var p in TARGETS)
        {
            var r = all.FirstOrDefault(x => PathOf(x.transform) == p);
            sb.AppendLine("  target " + p + (r == null ? " 없음" : " (" + r.sharedMaterial.name + ")"));
            if (r != null) list.Add(r);
        }
        return list;
    }

    static void Shots(PyriteDayCycle cyc, string tag, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var terr = Terrain.activeTerrain;
        Directory.CreateDirectory("Assets/_preview/crystal/");
        try
        {
            cyc.ResetCache();
            var c3 = new Vector3(-52f, 3f, 26f);
            var eye3 = new Vector3(-40f, 0f, 42f); if (terr != null) eye3.y = terr.SampleHeight(eye3) + terr.transform.position.y + 1.7f;
            var eyeC = new Vector3(-10f, 0f, 47f); if (terr != null) eyeC.y = terr.SampleHeight(eyeC) + terr.transform.position.y + 1.7f;
            foreach (var h in new[] { 12f, PyriteDayCycleSetup.EDITOR_HOUR, 21f })
            {
                cyc.EvaluateAt(h);
                Shot(cam, eye3, c3, 50f, string.Format("Assets/_preview/crystal/silver_{0}_{1:00}_right.png", tag, (int)h));
                Shot(cam, eyeC, new Vector3(-10f, 3f, -40f), 70f, string.Format("Assets/_preview/crystal/silver_{0}_{1:00}_lake.png", tag, (int)h));
            }
            sb.AppendLine("  shots " + tag);
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    static string PathOf(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_crystal.txt", sb.ToString()); }
}
#endif
