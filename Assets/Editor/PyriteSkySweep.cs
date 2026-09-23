// Tools ▸ Pyrite ▸ Z9c. Night Sky Exposure Sweep (preview only)
//  밤 Sorafield 하늘이 너무 어두워 절벽 실루엣이 사라졌다(하늘 평균 34 → 8.6).
//  M_Sky_PyriteNightAtmos 의 _Exposure 후보별로 렌더만 한다. 끝나면 원래 값으로 돌려놓는다(씬·에셋 변경 없음).
//  → Assets/_preview/sky/exp_<값>_<시점>.png, Logs/pyrite_sky_sweep.txt
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteSkySweep
{
    const string MAT = "Assets/Materials/M_Sky_PyriteNightAtmos.mat";
    static readonly float[] EXPO = { 1f, 1.8f, 2.6f, 3.4f, 4f };

    [MenuItem("Tools/Pyrite/Z9c. Night Sky Exposure Sweep (preview)", false, 16)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z9c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var cam = Camera.main;
        if (m == null || tod == null || cam == null) { sb.AppendLine("준비 안 됨"); File.WriteAllText("Logs/pyrite_sky_sweep.txt", sb.ToString()); return; }
        float e0 = m.GetFloat("_Exposure");
        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(tod, false);
        tod.index = 1; tod.Apply();
        var t = Terrain.activeTerrain;
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        Directory.CreateDirectory("Assets/_preview/sky/");
        try
        {
            foreach (var e in EXPO)
            {
                m.SetFloat("_Exposure", e);
                foreach (var v in new[] { ("up", new Vector3(-20f, 40f, -10f), 80f), ("lake", new Vector3(-10f, 3f, -40f), 70f) })
                {
                    var eye = new Vector3(-10f, 1.7f, 47f);
                    if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(eye) + t.transform.position.y);
                    string p = "Assets/_preview/sky/exp_" + e.ToString("0.0") + "_" + v.Item1 + ".png";
                    sb.AppendLine(string.Format("  exp {0:0.0} {1,-4} {2}", e, v.Item1, Shot(cam, eye, v.Item2, v.Item3, p)));
                }
            }
        }
        finally
        {
            m.SetFloat("_Exposure", e0);
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            tod.index = 0; tod.Apply();
        }
        sb.AppendLine("복원 _Exposure " + e0);
        AssetDatabase.Refresh();
        File.WriteAllText("Logs/pyrite_sky_sweep.txt", sb.ToString());
    }

    // 하늘(위 1/3) 평균 + 절벽 띠(가운데 1/3) 평균 + 밝은 점 수
    static string Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        var px = tex.GetPixels32();
        double top = 0, mid = 0; int nt = 0, nm = 0, bright = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var c = px[y * W + x]; float v = (c.r + c.g + c.b) / 3f;
                if (y >= 2 * H / 3) { top += v; nt++; if (v > 120) bright++; }
                else if (y >= H / 3) { mid += v; nm++; }
            }
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        return string.Format("top {0:0.0}  mid {1:0.0}  stars>120 {2}", top / nt, mid / nm, bright);
    }
}
#endif
