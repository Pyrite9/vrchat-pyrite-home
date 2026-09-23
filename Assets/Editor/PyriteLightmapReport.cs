// Tools ▸ Pyrite ▸ Z4. Lightmap Usage Report (읽기 전용)
//  렌더러별 라이트맵 점유율(lightmapScaleOffset.xy 넓이) 을 루트별로 합산 → Logs/pyrite_lightmap.txt
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteLightmapReport
{
    [MenuItem("Tools/Pyrite/Z4. Lightmap Usage Report", false, 8)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var ls = Lightmapping.lightingSettings;
        sb.AppendLine("lightmaps " + LightmapSettings.lightmaps.Length + " res " + ls.lightmapResolution + " max " + ls.lightmapMaxSize + " dir " + ls.directionalityMode + " compress " + ls.lightmapCompression);
        var rows = Object.FindObjectsOfType<MeshRenderer>()
            .Where(r => r.lightmapIndex >= 0 && r.lightmapIndex < 65534)
            .Select(r => new { r, area = r.lightmapScaleOffset.x * r.lightmapScaleOffset.y, root = r.transform.root.name,
                               scale = new SerializedObject(r).FindProperty("m_ScaleInLightmap").floatValue })
            .ToList();
        foreach (var g in rows.GroupBy(x => x.root).OrderByDescending(g => g.Sum(x => x.area)))
            sb.AppendLine(g.Key.PadRight(28) + " maps " + g.Sum(x => x.area).ToString("F2") + "  renderers " + g.Count() + "  scale " + string.Join(",", g.Select(x => x.scale.ToString("0.###")).Distinct().Take(5)));
        sb.AppendLine("-- top renderers");
        foreach (var x in rows.OrderByDescending(x => x.area).Take(25))
            sb.AppendLine(x.root + "/" + x.r.name + "  " + x.area.ToString("F3") + "  scale " + x.scale + "  idx " + x.r.lightmapIndex);
        var t = Terrain.activeTerrain;
        if (t != null) sb.AppendLine("terrain idx " + t.lightmapIndex + " so " + t.lightmapScaleOffset + " scale " + new SerializedObject(t).FindProperty("m_ScaleInLightmap").floatValue);
        foreach (var lm in LightmapSettings.lightmaps)
            sb.AppendLine("map " + (lm.lightmapColor ? lm.lightmapColor.width + "x" + lm.lightmapColor.height : "-") + " dir " + (lm.lightmapDir ? "y" : "n") + " mask " + (lm.shadowMask ? "y" : "n"));
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_lightmap.txt", sb.ToString());
        Debug.Log("[Z4] Logs/pyrite_lightmap.txt");
    }
}
#endif
