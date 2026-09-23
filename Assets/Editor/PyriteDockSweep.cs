// Tools ▸ Pyrite ▸ Z13c. Dock Look Sweep (preview only)
//  Z13(노멀·AO) 후에도 밤 렌더가 거의 같았다(평균 차 0.48/255). 밤 부두는 간접광이 균일하고 직접광이 거의 없어
//  노멀맵이 드러날 빛이 없다. 무엇이 밤에 판자를 살리는지 값별로 렌더만 해본다(끝나면 원래 값으로 복원).
//   gloss  : smoothness — 밤하늘·달 반사가 판자 면마다 달리 맺히는지
//   bump   : 노멀 세기
//   ao     : AO 세기(1 = 현재 맵)
//  → Assets/_preview/dock/sweep_<g>_<b>_<a>_<시간대>.png (부두 판자 부분만 잘라 1장씩), Logs/pyrite_dock_sweep.txt
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteDockSweep
{
    [MenuItem("Tools/Pyrite/Z13c. Dock Look Sweep (preview)", false, 27)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z13c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_DockWood.mat");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var cam = Camera.main;
        float g0 = m.GetFloat("_NightAmbTilt"), b0 = m.GetFloat("_BumpScale"), a0 = m.GetFloat("_OcclusionStrength");
        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(tod, false);
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var t = Terrain.activeTerrain;
        var eye = new Vector3(-10.5f, 1.7f, 44.5f);
        eye.y += Mathf.Max(0f, t.SampleHeight(eye) + t.transform.position.y);
        var look = new Vector3(-10.6f, 0.2f, 33.0f);
        Directory.CreateDirectory("Assets/_preview/dock/");
        try
        {
            foreach (int i in new[] { 1, 0 })
            {
                tod.index = i; tod.Apply();
                foreach (var g in new[] { 0f, 0.8f, 1.5f })
                foreach (var b in new[] { 1f, 2f })
                foreach (var a in new[] { 0f, 1f })
                {
                    if (i == 0 && !(b == 1f && a == 1f && g == 0f)) continue;   // 노을은 기준 1장
                    m.SetFloat("_NightAmbTilt", g); m.SetFloat("_BumpScale", b); m.SetFloat("_OcclusionStrength", a);
                    string p = string.Format("Assets/_preview/dock/sweep_t{0:0.0}_b{1:0.0}_a{2:0}_{3}.png", g, b, a, i);
                    sb.AppendLine(string.Format("  tod{0} tilt {1:0.0} bump {2:0.0} ao {3:0}  {4}", i, g, b, a, Shot(cam, eye, look, p)));
                }
            }
        }
        finally
        {
            m.SetFloat("_NightAmbTilt", g0); m.SetFloat("_BumpScale", b0); m.SetFloat("_OcclusionStrength", a0);
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            tod.index = 0; tod.Apply();
        }
        AssetDatabase.Refresh();
        File.WriteAllText("Logs/pyrite_dock_sweep.txt", sb.ToString());
    }

    // 판자 영역(화면 아래 가운데)만 잘라 저장 + 통계: 평균, 표준편차, 판자 방향 행 평균의 표준편차(= 판자 간 톤 차)
    static string Shot(Camera cam, Vector3 eye, Vector3 look, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = 60f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        int x0 = 560, w = 480, h = 420, y0 = H - h;          // ReadPixels 는 위가 0 (D3D) — 화면 아래 가운데 = 판자
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(x0, y0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        var px = tex.GetPixels32();
        double s = 0, s2 = 0; int n = 0; var rows = new double[h];
        for (int y = 0; y < h; y++) { double rs = 0; for (int x = 0; x < w; x++) { var c = px[y * w + x]; double v = (c.r + c.g + c.b) / 3.0; s += v; s2 += v * v; n++; rs += v; } rows[y] = rs / w; }
        double m = s / n, rm = 0, rv = 0; foreach (var r in rows) rm += r; rm /= h; foreach (var r in rows) rv += (r - rm) * (r - rm); rv = System.Math.Sqrt(rv / h);
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        return string.Format("mean {0:0.0} sd {1:0.0} rowSd {2:0.0}", m, System.Math.Sqrt(s2 / n - m * m), rv);
    }
}
#endif
