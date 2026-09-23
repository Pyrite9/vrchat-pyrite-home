// Tools ▸ Pyrite ▸ Z19a. Day Look Diagnostic (12:00)
//  낮이 칙칙한 원인 가르기 — 12:00 에서 누적 비교 (씬·에셋은 끝나면 원래대로)
//   a 현재
//   b + 정오 반사 큐브맵 (Assets/Reflections/<probe>_noon.exr 로 굽는다 — 이후 작업에 그대로 쓴다)
//   c + 낮 후처리 (임시 프로필: 노을 프로필 복사 → 색온도 0, 틴트 0, 채도 +10)
//   d + 간접광 1.4배 (nightMats _NightTint ×1.4, 절벽 _Color ×1.25)
//  렌더 Assets/_preview/day/, 로그 Logs/pyrite_day.txt (영역별 밝기·채도)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public static class PyriteDayDiag
{
    const string LOG = "Logs/pyrite_day.txt";
    struct V { public string n; public Vector3 eye, look; public float fov; }
    static readonly V[] Views =
    {
        new V{ n="camp_lake",   eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f, 3.0f, -40.0f), fov=70f },
        new V{ n="far_to_camp", eye=new Vector3(  5.0f, 1.7f, -40.0f), look=new Vector3( -5.0f, 6.0f, 78.0f), fov=70f },
        new V{ n="tent_side",   eye=new Vector3( -4.0f, 1.7f, 58.0f), look=new Vector3(-16.0f, 1.5f, 56.0f), fov=70f },
    };

    [MenuItem("Tools/Pyrite/Z19a. Day Look Diagnostic (12:00)", false, 45)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z19a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc == null) { sb.AppendLine("DayCycle 없음"); Flush(sb); return; }
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var vol = Object.FindObjectsOfType<PostProcessVolume>().FirstOrDefault(v => v.isGlobal);
        var prof0 = vol != null ? vol.sharedProfile : null;
        var probes = cyc.probes;
        var cube0 = probes.Select(p => p.customBakedTexture).ToArray();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var tint0 = cyc.nightMats.Select(m => m != null ? m.GetColor("_NightTint") : Color.white).ToArray();
        Directory.CreateDirectory("Assets/_preview/day/"); Directory.CreateDirectory("Assets/Reflections/");
        PostProcessProfile dayProf = null;
        try
        {
            cyc.ResetCache(); cyc.EvaluateAt(12f);
            Shots(cam, "a", sb);

            // b — 정오 큐브맵 굽기 (Custom 모드 그대로 BakeReflectionProbe 는 파일로 렌더만 한다)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int k = 0; k < probes.Length; k++)
            {
                string path = "Assets/Reflections/" + probes[k].name + "_noon.exr";
                var mode0 = probes[k].mode; probes[k].mode = ReflectionProbeMode.Baked;
                bool ok = Lightmapping.BakeReflectionProbe(probes[k], path);
                probes[k].mode = mode0;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
                probes[k].customBakedTexture = tex;
                sb.AppendLine(string.Format("  bake {0} {1}", path, ok));
            }
            sb.AppendLine(string.Format("  cubes {0:0.0}s", sw.Elapsed.TotalSeconds));
            Shots(cam, "b", sb);

            // c — 낮 후처리
            if (prof0 != null)
            {
                dayProf = Object.Instantiate(prof0);
                if (dayProf.TryGetSettings<ColorGrading>(out var cg))
                {
                    sb.AppendLine(string.Format("  dusk CG temp {0} tint {1} sat {2} contrast {3} postExp {4}", cg.temperature.value, cg.tint.value, cg.saturation.value, cg.contrast.value, cg.postExposure.value));
                    cg.temperature.Override(0f); cg.tint.Override(0f); cg.saturation.Override(10f);
                }
                vol.sharedProfile = dayProf;
            }
            Shots(cam, "c", sb);

            // d — 간접광 1.4배
            for (int k = 0; k < cyc.nightMats.Length; k++) if (cyc.nightMats[k] != null) cyc.nightMats[k].SetColor("_NightTint", tint0[k] * 1.4f);
            var cc = cyc.cliffMat.GetColor("_Color");
            cyc.cliffMat.SetColor("_Color", cc * 1.25f);
            Shots(cam, "d", sb);
            cyc.cliffMat.SetColor("_Color", cc);
        }
        finally
        {
            for (int k = 0; k < cyc.nightMats.Length; k++) if (cyc.nightMats[k] != null) cyc.nightMats[k].SetColor("_NightTint", tint0[k]);
            if (vol != null) vol.sharedProfile = prof0;
            if (dayProf != null) Object.DestroyImmediate(dayProf);
            for (int k = 0; k < probes.Length; k++) probes[k].customBakedTexture = cube0[k];
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
            AssetDatabase.SaveAssets();
        }
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Shots(Camera cam, string tag, StringBuilder sb)
    {
        var t = Terrain.activeTerrain;
        foreach (var v in Views)
        {
            var eye = v.eye;
            if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0, eye.z)) + t.transform.position.y);
            string p = string.Format("Assets/_preview/day/{0}_{1}.png", tag, v.n);
            Shot(cam, eye, v.look, v.fov, p);
        }
        sb.AppendLine("  shots " + tag);
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

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString()); }
}
#endif
