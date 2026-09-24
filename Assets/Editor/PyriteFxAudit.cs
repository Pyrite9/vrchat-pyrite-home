// Tools ▸ Pyrite2 ▸ Z34a. FX Audit (water collider, fog, dock)
//  별똥별·물안개·불티·물수제비 전 실측 (씬 변경 없음)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteFxAudit
{
    [MenuItem("Tools/Pyrite2/Z34a. FX Audit (water collider, fog, dock)", false, 80)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z34a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        foreach (var c in Object.FindObjectsOfType<Collider>().Where(c => c.name.ToLower().Contains("water") || (c.transform.parent && c.transform.parent.name == "Water")))
            sb.AppendLine(string.Format("collider {0} ({1}) layer {2}={3} trigger {4} bounds {5}..{6} | ignore vs Pickup(13) {7} Default(0) {8}",
                P(c.transform), c.GetType().Name, c.gameObject.layer, LayerMask.LayerToName(c.gameObject.layer), c.isTrigger, c.bounds.min.ToString("F2"), c.bounds.max.ToString("F2"),
                Physics.GetIgnoreLayerCollision(13, c.gameObject.layer), Physics.GetIgnoreLayerCollision(0, c.gameObject.layer)));
        for (int l = 0; l < 32; l++) { var n = LayerMask.LayerToName(l); if (!string.IsNullOrEmpty(n)) sb.Append(l + ":" + n + (Physics.GetIgnoreLayerCollision(13, l) ? "(x13) " : " ")); }
        sb.AppendLine();
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc)
        {
            sb.AppendLine("keyHour " + string.Join("/", cyc.keyHour.Select(h => h.ToString("0.0"))));
            sb.AppendLine("fogDensity " + string.Join("/", cyc.fogDensity.Select(h => h.ToString("0.0000"))));
            sb.AppendLine("dayMinutes " + cyc.dayMinutes + " autoFlow " + cyc.autoFlow);
        }
        var cam = Camera.main; sb.AppendLine("main cam far " + cam.farClipPlane + " | fog mode " + RenderSettings.fogMode);
        var ripple = Object.FindObjectOfType<PyriteLakeRipple>(); sb.AppendLine("ripple " + (ripple ? P(ripple.transform) + " surfaceY " + ripple.surfaceY + " dropAmp " + ripple.dropAmp : "없음"));
        var dock = GameObject.Find("Dock");
        if (dock) foreach (Transform t in dock.transform)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
            var b = rs.Select(r => r.bounds).Aggregate((a, c2) => { a.Encapsulate(c2); return a; });
            if (t.name.StartsWith("Boat")) continue;
            sb.AppendLine(string.Format("dock {0} {1}..{2}", t.name, b.min.ToString("F2"), b.max.ToString("F2")));
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_fx.txt", sb.ToString());
    }
    static string P(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
