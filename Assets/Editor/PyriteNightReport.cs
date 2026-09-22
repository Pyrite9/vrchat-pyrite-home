using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteNightReport
{
    [MenuItem("Tools/Pyrite/U. Night Report", false, 293)]
    public static void Report()
    {
        var L = new List<string>();

        L.Add("── 머티리얼 ──");
        foreach (var g in AssetDatabase.FindAssets("t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var n = System.IO.Path.GetFileNameWithoutExtension(p);
            if (!(n.StartsWith("M_Pyrite") || n.StartsWith("M_TS_") || n.StartsWith("M_Bedrock") || n.StartsWith("M_Dock"))) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            var sb = new System.Text.StringBuilder();
            sb.Append("  " + n + "  [" + m.shader.name + "]  ");
            foreach (var prop in new[] { "_Color", "_BaseColor", "_MainColor", "_Metallic", "_Glossiness", "_Smoothness", "_EmissionColor", "_BumpScale" })
                if (m.HasProperty(prop))
                {
                    if (prop.Contains("Color")) sb.Append(prop + "=" + m.GetColor(prop).ToString("F2") + " ");
                    else sb.Append(prop + "=" + m.GetFloat(prop).ToString("F2") + " ");
                }
            if (m.HasProperty("_BumpMap")) sb.Append("_BumpMap=" + (m.GetTexture("_BumpMap") ? m.GetTexture("_BumpMap").name : "없음") + " ");
            sb.Append("GI=" + m.globalIlluminationFlags + " kw=" + string.Join(",", m.shaderKeywords));
            L.Add(sb.ToString());
        }

        L.Add("── 조명 ──");
        foreach (var l in Object.FindObjectsOfType<Light>(true).OrderBy(x => x.name))
            L.Add(string.Format("  {0,-22} {1,-11} bake={2,-8} I={3:F2} range={4:F1} en={5} parent={6}",
                l.name, l.type, l.lightmapBakeType, l.intensity, l.range, l.enabled,
                l.transform.parent ? l.transform.parent.name : "-"));

        var t = Terrain.activeTerrain;
        if (t != null)
            L.Add(string.Format("── 터레인 ──\n  mat={0} [{1}]  drawInstanced={2}  basemapDistance={3}  lightmapIndex={4}  reflProbe={5}",
                t.materialTemplate ? t.materialTemplate.name : "없음(기본)",
                t.materialTemplate ? t.materialTemplate.shader.name : "-",
                t.drawInstanced, t.basemapDistance, t.lightmapIndex, t.reflectionProbeUsage));

        L.Add("── 꽃 렌더러 샘플 ──");
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true).Where(r => r.name.StartsWith("P_TS_")).Take(2))
            L.Add(string.Format("  {0} lightmapIndex={1} probes={2} mat={3}", r.name, r.lightmapIndex, r.lightProbeUsage,
                r.sharedMaterial ? r.sharedMaterial.name : "-"));

        var txt = string.Join("\n", L);
        System.IO.Directory.CreateDirectory("Assets/_preview");
        System.IO.File.WriteAllText("Assets/_preview/night_report.txt", txt);
        Debug.Log("[NIGHT]\n" + txt);
    }
}
