// PyriteBedroomLightProbe.cs — 침실 조명 실측 (Z49m). 바꾸는 것 없음(렌더 중 켬/끔 후 원복)
//  보는 것: 환경광 설정·SH(방 중심), 침실 렌더러의 라이트맵/프로브/반사 프로브, 광원별 기여(켬/끔 렌더 평균 RGB)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteBedroomLightProbe
{
    const string PREV = "Assets/_preview/bedroom/";
    const string LOG = "Logs/pyrite_bedroom_light.txt";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z49m. Bedroom Light Probe", false, 4912)]
    public static void Run()
    {
        sb = new StringBuilder("[Z49m] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }

    static void Inner()
    {
        var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
        var o = room.transform;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }

        sb.AppendLine("ambientMode " + RenderSettings.ambientMode + " intensity " + RenderSettings.ambientIntensity.ToString("F2")
            + " sky " + C(RenderSettings.ambientSkyColor) + " eq " + C(RenderSettings.ambientEquatorColor) + " gnd " + C(RenderSettings.ambientGroundColor)
            + " ambLight " + C(RenderSettings.ambientLight));
        sb.AppendLine("reflection mode " + RenderSettings.defaultReflectionMode + " intensity " + RenderSettings.reflectionIntensity.ToString("F2")
            + " customRefl " + (RenderSettings.customReflectionTexture ? RenderSettings.customReflectionTexture.name : "-") + " skybox " + (RenderSettings.skybox ? RenderSettings.skybox.name : "-"));
        sb.AppendLine("fog " + RenderSettings.fog + " " + RenderSettings.fogMode + " col " + C(RenderSettings.fogColor) + " dens " + RenderSettings.fogDensity.ToString("F4")
            + " lin " + RenderSettings.fogStartDistance.ToString("F0") + ".." + RenderSettings.fogEndDistance.ToString("F0"));

        // 방 중심 SH
        var center = o.TransformPoint(new Vector3(0, 1.0f, 0));
        SphericalHarmonicsL2 sh; LightProbes.GetInterpolatedProbe(center, null, out sh);
        var dirs = new[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.right };
        var outc = new Color[4];
        sh.Evaluate(dirs, outc);
        sb.AppendLine("probe SH @room center  up " + C(outc[0]) + " down " + C(outc[1]) + " fwd " + C(outc[2]) + " right " + C(outc[3])
            + "  (probes in scene " + (LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0) + ")");
        var sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (sun) sb.AppendLine("sun int " + sun.intensity.ToString("F2") + " col " + C(sun.color) + " mask " + sun.cullingMask + " (24 in? " + ((sun.cullingMask & (1 << 24)) != 0) + ")");

        // 렌더러
        foreach (var n in new[] { "Floor", "Dome", "Beds/Mattress", "Beds/BlanketCover", "Furniture/LowTable" })
        {
            var t = o.Find(n); if (t == null) { sb.AppendLine("  " + n + " 없음"); continue; }
            var r = t.GetComponentInChildren<Renderer>(true); if (r == null) continue;
            var rp = new System.Collections.Generic.List<ReflectionProbeBlendInfo>(); r.GetClosestReflectionProbes(rp);
            sb.AppendLine("  " + n + " layer " + r.gameObject.layer + " static " + r.gameObject.isStatic + " lmIdx " + r.lightmapIndex
                + " probe " + r.lightProbeUsage + " refl " + r.reflectionProbeUsage + " rp[" + string.Join(",", rp.Select(b => b.probe.name + ":" + b.weight.ToString("F2"))) + "]"
                + " mat " + (r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name + " " + (r.sharedMaterial.HasProperty("_Color") ? C(r.sharedMaterial.color) : "") : "-"));
        }
        // 광원
        var lights = room.GetComponentsInChildren<Light>(true);
        foreach (var l in lights)
            sb.AppendLine("  light " + Path(l.transform, o) + " " + l.type + " int " + l.intensity.ToString("F2") + " range " + l.range.ToString("F1")
                + " col " + C(l.color) + " sh " + l.shadows + " mode " + l.lightmapBakeType + " render " + l.renderMode + " en " + (l.enabled && l.gameObject.activeInHierarchy));
        sb.AppendLine("QualitySettings pixelLightCount " + QualitySettings.pixelLightCount + " shadowDist " + QualitySettings.shadowDistance);

        // 켬/끔 렌더
        Directory.CreateDirectory(PREV);
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled && !r.transform.IsChildOf(o)).ToArray();
        foreach (var r in psr) r.enabled = false;
        var en0 = lights.Select(l => l.enabled).ToArray();
        float amb0 = RenderSettings.ambientIntensity, refl0 = RenderSettings.reflectionIntensity;
        try
        {
            cam.fieldOfView = 75f;
            var eye = o.TransformPoint(new Vector3(1.9f, 1.75f, 2.1f));
            var at = o.TransformPoint(new Vector3(-0.3f, 0.3f, -1.3f));
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
            void Set(System.Func<Light, bool> on) { for (int i = 0; i < lights.Length; i++) lights[i].enabled = on(lights[i]); }
            Set(l => true); Shot(cam, "lp_all");
            Set(l => false); Shot(cam, "lp_none");
            Set(l => l.transform.IsChildOf(o.Find("Lights/HangLantern") ?? o)); Shot(cam, "lp_hang");
            Set(l => l.transform.parent && l.transform.parent.name.StartsWith("CandleLantern")); Shot(cam, "lp_candle");
            Set(l => false); RenderSettings.ambientIntensity = 0f; RenderSettings.reflectionIntensity = 0f; Shot(cam, "lp_black");
            RenderSettings.ambientIntensity = amb0; RenderSettings.reflectionIntensity = 0f; Shot(cam, "lp_none_norefl");
            RenderSettings.reflectionIntensity = refl0;
            // 바닥 한 점 직접 보기(누운 눈높이에서 바닥)
            Set(l => true);
            eye = o.TransformPoint(new Vector3(2.4f, 0.5f, 0.5f)); at = o.TransformPoint(new Vector3(2.4f, 0f, 1.5f));
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye)); Shot(cam, "lp_floor_all");
            Set(l => false); Shot(cam, "lp_floor_none");
        }
        finally
        {
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = en0[i];
            RenderSettings.ambientIntensity = amb0; RenderSettings.reflectionIntensity = refl0;
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static string Path(Transform t, Transform root) { string s = t.name; while (t.parent && t.parent != root) { t = t.parent; s = t.name + "/" + s; } return s; }
    static string C(Color c) => "(" + c.r.ToString("F3") + "," + c.g.ToString("F3") + "," + c.b.ToString("F3") + ")";

    static void Shot(Camera cam, string tag)
    {
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double r = 0, g = 0, b = 0;
        foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
        int n = px.Length;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(82));
        sb.AppendLine("  shot " + tag + " mean " + ((r + g + b) / 3 / n).ToString("F1") + " RGB " + (r / n).ToString("F1") + "/" + (g / n).ToString("F1") + "/" + (b / n).ToString("F1"));
        Object.DestroyImmediate(tex);
    }
}
#endif
