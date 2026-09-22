using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 물 반사 진단 — 물이 리플렉션 프로브를 쓰는지, 자체 큐브맵을 쓰는지
public static class PyriteReflectReport
{
    // customReflection 은 큐브맵이 아닐 때 접근만 해도 ArgumentException 을 던진다
    static string SafeCustomRefl()
    {
        try { var c = RenderSettings.customReflection; return c ? c.name : "없음"; }
        catch (System.Exception) { return "없음(큐브맵 아님)"; }
    }

    [MenuItem("Tools/Pyrite/S. Reflection Report", false, 290)]
    public static void Report()
    {
        var L = new List<string>();

        L.Add("── 리플렉션 프로브 ──");
        foreach (var p in Object.FindObjectsOfType<ReflectionProbe>(true))
        {
            L.Add(string.Format("  {0,-12} mode={1} importance={2} res={3} size={4} center={5}\n      baked={6}  custom={7}  boxProj={8}",
                p.name, p.mode, p.importance, p.resolution, p.size.ToString("F0"), p.transform.position.ToString("F1"),
                p.bakedTexture ? p.bakedTexture.name : "없음",
                p.customBakedTexture ? p.customBakedTexture.name : "없음",
                p.boxProjection));
        }

        L.Add("── 물 렌더러 ──");
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r.sharedMaterials.All(m => m == null || m.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) < 0
                                                      && m.name.IndexOf("Lake", System.StringComparison.OrdinalIgnoreCase) < 0))
                continue;
            L.Add(string.Format("  {0}  reflectionProbeUsage={1}  anchor={2}", r.name, r.reflectionProbeUsage,
                r.probeAnchor ? r.probeAnchor.name : "없음"));
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                L.Add("    머티리얼 " + m.name + "  셰이더 " + m.shader.name);
                int n = ShaderUtil.GetPropertyCount(m.shader);
                for (int i = 0; i < n; i++)
                {
                    var t = ShaderUtil.GetPropertyType(m.shader, i);
                    var pn = ShaderUtil.GetPropertyName(m.shader, i);
                    if (t == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        var tex = m.GetTexture(pn);
                        L.Add(string.Format("      tex  {0,-24} {1}  ({2})", pn,
                            tex ? tex.name : "비어있음", tex ? tex.GetType().Name : "-"));
                    }
                    else if (pn.IndexOf("Refl", System.StringComparison.OrdinalIgnoreCase) >= 0
                          || pn.IndexOf("Cube", System.StringComparison.OrdinalIgnoreCase) >= 0
                          || pn.IndexOf("Sky",  System.StringComparison.OrdinalIgnoreCase) >= 0
                          || pn.IndexOf("Fresnel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        L.Add(string.Format("      {0,-4} {1,-24} {2}", t.ToString().Substring(0, 4), pn,
                            t == ShaderUtil.ShaderPropertyType.Color ? m.GetColor(pn).ToString() : m.GetFloat(pn).ToString("F3")));
                }
                // 셰이더 키워드
                L.Add("      키워드: " + string.Join(" ", m.shaderKeywords));
            }
        }

        L.Add("── 환경 ──");
        L.Add("  skybox=" + (RenderSettings.skybox ? RenderSettings.skybox.name : "없음")
              + "  defaultReflectionMode=" + RenderSettings.defaultReflectionMode
              + "  customReflection=" + SafeCustomRefl()
              + "  reflectionIntensity=" + RenderSettings.reflectionIntensity.ToString("F2"));

        Debug.Log("[REFL]\n" + string.Join("\n", L));
    }
}
