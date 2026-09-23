// Tools ▸ Pyrite ▸ Z21b. Tarnish Crystal → Mayonnaise  /  Z21c. Revert
//  관리자 요청: 구리색 결정(M_Pyrite_Tarnish, 색 0.66/0.46/0.30, 광택 0.62)을 밝은 마요네즈 색으로.
//   색 (0.96, 0.91, 0.70), 광택 0.80 — DayCycle 은 광택을 "기준 × 시간대 배율"로 매번 다시 쓰므로 기준값(crystalBaseGloss)도 바꾼다.
//   예전 ToD 의 배열도 같이 바꿔 Z18b 를 다시 돌려도 유지되게 한다.
//  전후 렌더 12:00 / 18:20 / 21:00 × 랜드마크 2곳 → Assets/_preview/crystal/mayo_*.png
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteCrystalMayo
{
    const string MAT = "Assets/Materials/M_Pyrite_Tarnish.mat";
    static readonly Color MAYO = new Color(0.96f, 0.91f, 0.70f, 1f);
    const float MAYO_GLOSS = 0.80f;
    static readonly Color OLD = new Color(0.66f, 0.46f, 0.30f, 1f);
    const float OLD_GLOSS = 0.62f;
    static readonly Color OLD_EM = new Color(0.62f, 0.40f, 0.22f, 1f);

    [MenuItem("Tools/Pyrite/Z21b. Tarnish Crystal -> Mayonnaise", false, 50)]
    public static void Run() { Apply(MAYO, MAYO_GLOSS, MAYO, true, "[Z21b]"); }

    [MenuItem("Tools/Pyrite/Z21c. Tarnish Crystal Revert", false, 51)]
    public static void Revert() { Apply(OLD, OLD_GLOSS, OLD_EM, false, "[Z21c]"); }

    static void Apply(Color col, float gloss, Color em, bool render, string tag)
    {
        var sb = new StringBuilder(tag + " " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        if (mat == null || cyc == null) { sb.AppendLine("mat/cycle 없음"); Flush(sb); return; }
        int k = System.Array.IndexOf(cyc.crystalMats, mat);
        sb.AppendLine(string.Format("before: _Color {0} baseGloss {1} (index {2})", mat.GetColor("_Color"), k >= 0 ? cyc.crystalBaseGloss[k] : -1, k));

        if (render) Shots(cyc, "before", sb);

        mat.SetColor("_Color", col);
        EditorUtility.SetDirty(mat);
        if (k >= 0)
        {
            cyc.crystalBaseGloss[k] = gloss; cyc.crystalBaseEmission[k] = em;
            UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
        }
        if (tod != null)
        {
            int j = System.Array.IndexOf(tod.crystalMats, mat);
            if (j >= 0) { tod.crystalBaseGloss[j] = gloss; tod.crystalBaseEmission[j] = em; UdonSharpEditorUtility.CopyProxyToUdon(tod); EditorUtility.SetDirty(tod); }
        }
        cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine(string.Format("after: _Color {0} _Glossiness(18:20) {1}", mat.GetColor("_Color"), mat.GetFloat("_Glossiness")));

        if (render) Shots(cyc, "after", sb);
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Shots(PyriteDayCycle cyc, string tag, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var lm = Object.FindObjectsOfType<Transform>(true).Where(t => t.name.StartsWith("Landmark_")).OrderBy(t => t.name).Take(2).ToArray();
        var terr = Terrain.activeTerrain;
        Directory.CreateDirectory("Assets/_preview/crystal/");
        try
        {
            cyc.ResetCache();
            foreach (var h in new[] { 12f, PyriteDayCycleSetup.EDITOR_HOUR, 21f })
            {
                cyc.EvaluateAt(h);
                int i = 0;
                foreach (var t in lm)
                {
                    var tgt = t.position + Vector3.up * 1.2f;
                    var eye = t.position + new Vector3(0f, 0f, 9f); if (terr != null) eye.y = terr.SampleHeight(eye) + terr.transform.position.y + 1.7f;
                    Shot(cam, eye, tgt, 60f, string.Format("Assets/_preview/crystal/mayo_{0}_{1:00}_{2}.png", tag, (int)h, ++i));
                }
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

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_crystal.txt", sb.ToString()); }
}
#endif
