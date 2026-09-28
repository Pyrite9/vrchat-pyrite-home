// PyriteBedroomPPProbe.cs — 침실에서 보이는 PP(시간대 볼륨) 실측 (Z49n). 바꾸는 것 없음
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class PyriteBedroomPPProbe
{
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z49n. Bedroom PP Probe", false, 4913)]
    public static void Run()
    {
        sb = new StringBuilder("[Z49n] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        File.WriteAllText("Logs/pyrite_bedroom_pp.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }

    static string V(ParameterOverride p)
    {
        if (!p.overrideState) return null;
        var f = p.GetType().GetField("value"); var v = f != null ? f.GetValue(p) : null;
        if (v is float x) return x.ToString("F3");
        if (v is Color c) return "(" + c.r.ToString("F2") + "," + c.g.ToString("F2") + "," + c.b.ToString("F2") + ")";
        if (v is Vector4 q) return "(" + q.x.ToString("F2") + "," + q.y.ToString("F2") + "," + q.z.ToString("F2") + "," + q.w.ToString("F2") + ")";
        return v == null ? "null" : v.ToString();
    }

    static void Inner()
    {
        var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        var o = room.transform;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
        foreach (var v in Object.FindObjectsOfType<PostProcessVolume>(true))
        {
            sb.AppendLine("VOL " + v.name + " global " + v.isGlobal + " prio " + v.priority + " weight " + v.weight.ToString("F2") + " en " + (v.enabled && v.gameObject.activeInHierarchy)
                + " profile " + (v.sharedProfile ? v.sharedProfile.name : "-"));
            if (v.sharedProfile == null) continue;
            foreach (var s in v.sharedProfile.settings)
            {
                if (!s.enabled.value) { sb.AppendLine("   " + s.GetType().Name + " (off)"); continue; }
                var parts = s.GetType().GetFields().Where(f => typeof(ParameterOverride).IsAssignableFrom(f.FieldType) && f.Name != "enabled")
                    .Select(f => { var val = V((ParameterOverride)f.GetValue(s)); return val == null ? null : f.Name + "=" + val; }).Where(x => x != null);
                sb.AppendLine("   " + s.GetType().Name + ": " + string.Join(" ", parts));
            }
        }
        var cam = Camera.main; var layer = cam.GetComponent<PostProcessLayer>();
        sb.AppendLine("PostProcessLayer " + (layer ? "on " + layer.enabled + " volumeLayer " + layer.volumeLayer.value + " trigger " + (layer.volumeTrigger ? layer.volumeTrigger.name : "-") : "없음"));

        var lights = room.GetComponentsInChildren<Light>(true); var en0 = lights.Select(l => l.enabled).ToArray();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled && !r.transform.IsChildOf(o)).ToArray();
        foreach (var r in psr) r.enabled = false;
        bool l0 = layer && layer.enabled;
        try
        {
            cam.fieldOfView = 75f;
            var eye = o.TransformPoint(new Vector3(1.9f, 1.75f, 2.1f)); var at = o.TransformPoint(new Vector3(-0.3f, 0.3f, -1.3f));
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
            if (layer) layer.enabled = false;
            Shot(cam, "pp_off_all");
            foreach (var l in lights) l.enabled = false; Shot(cam, "pp_off_none");
            foreach (var l in lights) l.enabled = true;
            if (layer) layer.enabled = l0;
            Shot(cam, "pp_on_all");
        }
        finally
        {
            if (layer) layer.enabled = l0;
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = en0[i];
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

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
