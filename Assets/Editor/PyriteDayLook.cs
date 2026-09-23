// Tools ▸ Pyrite ▸ Z23b. Day Look Grid (cliff tone × clouds, 12:00)
//  낮이 칙칙함 — 인게임 정오 절벽 밝기 24~43 (거의 검정). 절벽 색 3 × 구름량 2 를 12:00 에서 렌더(값은 끝나면 원래대로)
//   절벽 _Color: 0.227(현재) / 0.30 / 0.36 (같은 색상비) · 구름 _CloudCoverage: 0.4(현재) / 0.2
//  렌더 Assets/_preview/day/look_c{절벽}_k{구름}_{시점}.png, 로그 Logs/pyrite_day.txt (영역 밝기)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteDayLook
{
    [MenuItem("Tools/Pyrite/Z23b. Day Look Grid (cliff x clouds)", false, 56)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z23b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var terr = Terrain.activeTerrain;
        var views = new (string n, Vector3 e, Vector3 l, float f)[]
        {
            ("camp_lake", new Vector3(-10f, 0f, 47f), new Vector3(-10f, 3f, -40f), 70f),
            ("crystal_r", new Vector3(-40f, 0f, 42f), new Vector3(-52f, 3f, 26f), 50f),
        };
        Directory.CreateDirectory("Assets/_preview/day/");
        try
        {
            cyc.ResetCache(); cyc.EvaluateAt(12f);
            var baseCol = cyc.cliffMat.GetColor("_Color"); float baseCloud = cyc.sky.GetFloat("_CloudCoverage");
            sb.AppendLine("noon cliff " + baseCol + " cloud " + baseCloud);
            foreach (var cl in new[] { 0.227f, 0.30f, 0.36f })
                foreach (var ck in new[] { 0.4f, 0.2f })
                {
                    var c = baseCol * (cl / 0.227f); c.a = 1f;
                    cyc.cliffMat.SetColor("_Color", c);
                    cyc.sky.SetFloat("_CloudCoverage", ck);
                    sb.Append(string.Format("  cliff {0:0.000} cloud {1:0.0}", cl, ck));
                    foreach (var v in views)
                    {
                        var eye = v.e; if (terr != null) eye.y = Mathf.Max(0f, terr.SampleHeight(eye) + terr.transform.position.y) + 1.7f;
                        var px = Shot(cam, eye, v.l, v.f, string.Format("Assets/_preview/day/look_c{0:000}_k{1:0}_{2}.png", cl * 1000, ck * 10, v.n));
                        // 텍스처 행 0 = 아래. 위 1/3 = 하늘, 가운데 1/3 = 절벽
                        float sky = 0, mid = 0; int ns = 0, nm = 0; int W = 960, H = 540;
                        for (int y = 0; y < H; y += 3) for (int x = 0; x < W; x += 3)
                        {
                            float g = px[y * W + x].grayscale;
                            if (y > 2 * H / 3) { sky += g; ns++; } else if (y > H / 3) { mid += g; nm++; }
                        }
                        sb.Append(string.Format(" | {0} sky {1:0} mid {2:0}", v.n, 255 * sky / ns, 255 * mid / nm));
                    }
                    sb.AppendLine();
                }
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
            AssetDatabase.SaveAssets();
        }
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_day.txt", sb.ToString());
    }

    static Color[] Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
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
        var px = tx.GetPixels();
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
        return px;
    }
}
#endif
