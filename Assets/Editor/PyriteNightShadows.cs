// Tools ▸ Pyrite ▸ Z7. Night Light Report (읽기 전용)
//  밤 캠프 그림자 진단 — 캠프 조명·결정 조명·태양의 그림자/범위/강도, 밤 프리셋 값
//  → Logs/pyrite_nightlights.txt, Assets/_preview/night_camp.png (밤 프리셋 근접 렌더)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteNightShadows
{
    [MenuItem("Tools/Pyrite/Z7. Night Light Report", false, 12)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        sb.AppendLine("pixelLightCount " + QualitySettings.pixelLightCount);
        if (tod != null)
        {
            sb.AppendLine("sunIntensity " + string.Join(" / ", tod.sunIntensity) + "  sunColor[1] " + tod.sunColor[1]);
            sb.AppendLine("campLightMul " + string.Join(" / ", tod.campLightMul ?? new float[0]));
            foreach (var l in tod.campLights ?? new Light[0])
                if (l != null) sb.AppendLine("camp  " + Path(l.transform) + "  type=" + l.type + " range=" + l.range + " int=" + l.intensity + " shadows=" + l.shadows + " str=" + l.shadowStrength
                    + " res=" + l.shadowResolution + " render=" + l.renderMode + " mode=" + l.lightmapBakeType + " pos=" + l.transform.position.ToString("F1") + " active=" + l.gameObject.activeInHierarchy);
        }
        foreach (var l in Object.FindObjectsOfType<Light>())
            if (tod == null || tod.campLights == null || !tod.campLights.Contains(l))
                sb.AppendLine("other " + Path(l.transform) + " type=" + l.type + " range=" + l.range + " int=" + l.intensity + " shadows=" + l.shadows + " render=" + l.renderMode + " mode=" + l.lightmapBakeType);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_nightlights.txt", sb.ToString());
        Debug.Log("[Z7] Logs/pyrite_nightlights.txt");
    }
    static string Path(Transform t) { var s = t.name; for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s; return s; }
}
#endif
