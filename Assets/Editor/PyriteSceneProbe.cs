// B1 준비 — 시간대 스위처가 참조해야 할 것들을 한 번에 조사한다.
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteSceneProbe
{
    static void DumpMat(string label, Material m)
    {
        if (m == null) { Debug.Log("[Probe] " + label + " = 없음"); return; }
        var path = AssetDatabase.GetAssetPath(m);
        Debug.Log(string.Format("[Probe] {0} = {1}\n  경로 {2}\n  셰이더 {3}", label, m.name, path, m.shader.name));
        var sh = m.shader;
        int n = ShaderUtil.GetPropertyCount(sh);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n; i++)
        {
            var pn = ShaderUtil.GetPropertyName(sh, i);
            var pt = ShaderUtil.GetPropertyType(sh, i);
            string v = "?";
            if (pt == ShaderUtil.ShaderPropertyType.Float || pt == ShaderUtil.ShaderPropertyType.Range)
                v = m.GetFloat(pn).ToString("0.###");
            else if (pt == ShaderUtil.ShaderPropertyType.Color)
            { var c = m.GetColor(pn); v = string.Format("({0:0.##},{1:0.##},{2:0.##},{3:0.##})", c.r, c.g, c.b, c.a); }
            else if (pt == ShaderUtil.ShaderPropertyType.Vector)
                v = m.GetVector(pn).ToString("0.###");
            else if (pt == ShaderUtil.ShaderPropertyType.TexEnv)
            { var t = m.GetTexture(pn); v = t == null ? "null" : t.name; }
            sb.Append("    ").Append(pn).Append(" (").Append(pt).Append(") = ").Append(v).Append('\n');
        }
        Debug.Log("[Probe] " + label + " 속성\n" + sb);
    }

    [MenuItem("Tools/Pyrite/I2. Probe Scene For TimeOfDay &8")]
    public static void Probe()
    {
        DumpMat("Skybox", RenderSettings.skybox);

        Debug.Log(string.Format(
            "[Probe] RenderSettings — ambientMode {0} / ambientIntensity {1:0.###} / ambientLight {2}\n"
            + "  ambientSky {3} / ambientEquator {4} / ambientGround {5}\n"
            + "  fog {6} / fogColor {7} / fogMode {8} / fogDensity {9:0.####}\n"
            + "  reflectionIntensity {10:0.###} / defaultReflectionMode {11}",
            RenderSettings.ambientMode, RenderSettings.ambientIntensity, RenderSettings.ambientLight,
            RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
            RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogMode, RenderSettings.fogDensity,
            RenderSettings.reflectionIntensity, RenderSettings.defaultReflectionMode));

        foreach (var l in Object.FindObjectsOfType<Light>(true))
            Debug.Log(string.Format("[Probe] Light {0} — {1} / {2} / 색 {3} / 강도 {4:0.###} / rot {5}",
                l.name, l.type, l.lightmapBakeType, l.color, l.intensity, l.transform.rotation.eulerAngles));

        foreach (var rp in Object.FindObjectsOfType<ReflectionProbe>(true))
            Debug.Log(string.Format("[Probe] ReflectionProbe {0} — mode {1} / size {2} / bakedTexture {3}",
                rp.name, rp.mode, rp.size, rp.bakedTexture == null ? "null" : rp.bakedTexture.name));

        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name != "FlowerField" && r.name != "LightFX" && r.name != "Camp") continue;
            var mats = r.GetComponentsInChildren<Renderer>(true)
                        .Where(x => x.sharedMaterial != null)
                        .Select(x => x.sharedMaterial.name).Distinct().ToList();
            Debug.Log("[Probe] " + r.name + " 머티리얼: " + string.Join(", ", mats));
        }

        // 꽃 Night 머티리얼이 에셋에 있는지
        foreach (var g in AssetDatabase.FindAssets("t:Material T_Flower"))
            Debug.Log("[Probe] 에셋 머티리얼: " + AssetDatabase.GUIDToAssetPath(g));

        // 캠프 테이블 위치 (다이얼 놓을 자리)
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
            if (t.name.StartsWith("camp03_table"))
            {
                var rr = t.GetComponentInChildren<Renderer>();
                Debug.Log(string.Format("[Probe] 테이블 {0} pos {1} yaw {2:0.0} / 바운즈 c{3} s{4}",
                    t.name, t.position, t.rotation.eulerAngles.y,
                    rr != null ? rr.bounds.center.ToString() : "?", rr != null ? rr.bounds.size.ToString() : "?"));
            }
    }
}
#endif
