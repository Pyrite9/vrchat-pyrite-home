// Tools ▸ Pyrite ▸ Z18e. Sky Dot Probe
//  1차 연속 시간대 렌더에서 하늘에 고정된 빛점(캠프→호수 시점 기준 방위 180°, 고도 ~24.5°, 낮·밤 모두)이 보였다.
//  정체 확인: (a) 그대로 (b) SkyDiscs 끔 (c) 스카이박스 없음(단색) 세 장 + 그 방향 ±3° 안의 렌더러·라이트 목록.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteDotProbe
{
    [MenuItem("Tools/Pyrite/Z18e. Sky Dot Probe", false, 44)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z18e] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc != null) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView; var cf0 = cam.clearFlags; var bg0 = cam.backgroundColor;
        var t = Terrain.activeTerrain;
        var eye = new Vector3(-10f, 1.7f, 47f); if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(eye) + t.transform.position.y);
        var look = new Vector3(-10f, 3f, -40f);
        float el = 24.5f * Mathf.Deg2Rad, az = 180f * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        sb.AppendLine("eye " + eye + " dir " + dir);

        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            var c = r.bounds.center - eye; if (c.sqrMagnitude < 1f) continue;
            float ang = Vector3.Angle(c, dir);
            if (ang < 3f) sb.AppendLine(string.Format("  renderer {0} ({1}) ang {2:0.0} dist {3:0} mat {4} active {5}", r.name, r.transform.parent ? r.transform.parent.name : "-", ang, c.magnitude, r.sharedMaterial ? r.sharedMaterial.shader.name : "null", r.enabled && r.gameObject.activeInHierarchy));
        }
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            var c = l.transform.position - eye; float ang = Vector3.Angle(c, dir);
            if (ang < 5f) sb.AppendLine(string.Format("  light {0} ang {1:0.0} dist {2:0} on {3} flare {4}", l.name, ang, c.magnitude, l.enabled && l.gameObject.activeInHierarchy, l.flare != null));
        }
        if (Physics.Raycast(eye, dir, out var hit, 2000f)) sb.AppendLine("  raycast hit " + hit.collider.name + " dist " + hit.distance.ToString("0"));

        var discs = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "SkyDiscs");
        var sky0 = RenderSettings.skybox;
        Directory.CreateDirectory("Assets/_preview/cycle/");
        try
        {
            Shot(cam, eye, look, "Assets/_preview/cycle/dot_a_normal.png");
            if (discs != null) discs.SetActive(false);
            Shot(cam, eye, look, "Assets/_preview/cycle/dot_b_nodiscs.png");
            if (discs != null) discs.SetActive(true);
            RenderSettings.skybox = null; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.3f, 0.5f);
            Shot(cam, eye, look, "Assets/_preview/cycle/dot_c_nosky.png");
            var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
            foreach (var r in psr) r.enabled = false;
            Shot(cam, eye, look, "Assets/_preview/cycle/dot_d_noparticles.png");
            foreach (var r in psr) r.enabled = true;
            sb.AppendLine("  particle renderers toggled: " + psr.Length);
        }
        finally
        {
            RenderSettings.skybox = sky0; if (discs != null) discs.SetActive(true);
            cam.clearFlags = cf0; cam.backgroundColor = bg0;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (cyc != null) cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        // 빛점 주변 밝기 (478,98 주변 9x9 최대 - 주변 평균)
        foreach (var n in new[] { "a_normal", "b_nodiscs", "c_nosky", "d_noparticles" })
        {
            var tex = new Texture2D(2, 2); tex.LoadImage(File.ReadAllBytes("Assets/_preview/cycle/dot_" + n + ".png"));
            float mx = 0, sum = 0; int cnt = 0;
            for (int y = 540 - 98 - 12; y <= 540 - 98 + 12; y++)
                for (int x = 478 - 12; x <= 478 + 12; x++)
                {
                    float v = tex.GetPixel(x, y).grayscale;
                    if (Mathf.Abs(x - 478) <= 3 && Mathf.Abs(y - (540 - 98)) <= 3) mx = Mathf.Max(mx, v); else { sum += v; cnt++; }
                }
            sb.AppendLine(string.Format("  {0}: peak {1:0.000} ring {2:0.000}", n, mx, sum / cnt));
            Object.DestroyImmediate(tex);
        }
        AssetDatabase.Refresh();
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_cycle.txt", sb.ToString());
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
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
}
#endif
