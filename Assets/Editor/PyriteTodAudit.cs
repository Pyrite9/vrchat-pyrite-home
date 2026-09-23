// Tools ▸ Pyrite ▸ Z18a. Time-of-Day Audit (continuous prep)
//  연속 시간대로 가기 전 실측: 프리셋마다 무엇이 다른지 전부 뽑는다.
//   1) PyriteTimeOfDay 공개 필드 전부 (배열 값)
//   2) 교체되는 머티리얼 묶음(skybox / cliff / water / flower) — 셰이더·키워드·프리셋 간에 다른 프로퍼티
//   3) 값만 바꾸는 머티리얼(crystal / night / shimmer) 셰이더
//   4) 라이트맵을 쓰는 렌더러의 셰이더별 개수 (전역 간접광 색조를 어디에 넣어야 하는지)
//   5) 씬의 라이트 / PP 볼륨 / 리플렉션 프로브
//  씬은 건드리지 않는다. 결과: Logs/pyrite_tod_audit.txt
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteTodAudit
{
    const string LOG = "Logs/pyrite_tod_audit.txt";

    [MenuItem("Tools/Pyrite/Z18a. Time-of-Day Audit (continuous prep)", false, 40)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z18a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null) { sb.AppendLine("PyriteTimeOfDay 없음"); Flush(sb); return; }
        sb.AppendLine("object " + tod.gameObject.name + "  index " + tod.index);

        // 1) 공개 필드
        sb.AppendLine("\n== 1. PyriteTimeOfDay fields ==");
        foreach (var f in typeof(PyriteTimeOfDay).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var v = f.GetValue(tod);
            sb.AppendLine(string.Format("{0} ({1}) = {2}", f.Name, f.FieldType.Name, Fmt(v)));
        }

        // 2) 교체 머티리얼 묶음
        sb.AppendLine("\n== 2. swapped material sets ==");
        DiffSet(sb, "skybox", tod.skybox);
        DiffSet(sb, "cliffMat", tod.cliffMat);
        DiffSet(sb, "waterMat", tod.waterMat);
        DiffSet(sb, "flowerMat", tod.flowerMat);
        sb.AppendLine("cliffRenderers: " + string.Join(", ", (tod.cliffRenderers ?? new Renderer[0]).Select(r => r == null ? "null" : r.name + "[" + r.GetType().Name + " lm" + r.lightmapIndex + "]")));
        sb.AppendLine("waterRenderers: " + string.Join(", ", (tod.waterRenderers ?? new Renderer[0]).Select(r => r == null ? "null" : r.name)));
        sb.AppendLine("flowerRenderers: " + (tod.flowerRenderers == null ? 0 : tod.flowerRenderers.Length) + " 개");

        // 3) 값만 바꾸는 머티리얼
        sb.AppendLine("\n== 3. value-driven materials ==");
        foreach (var m in (tod.crystalMats ?? new Material[0]).Concat(tod.nightMats ?? new Material[0]).Concat(new[] { tod.shimmerMat }))
            if (m != null) sb.AppendLine(string.Format("  {0}  shader {1}  kw [{2}]", AssetDatabase.GetAssetPath(m), m.shader.name, string.Join(" ", m.shaderKeywords)));

        // Sorafield 계열 스카이 셰이더 프로퍼티 전부
        var skyShaders = (tod.skybox ?? new Material[0]).Where(m => m != null).Select(m => m.shader).Distinct();
        foreach (var sh in skyShaders)
        {
            sb.AppendLine("\n-- sky shader " + sh.name + " properties --");
            int n = ShaderUtil.GetPropertyCount(sh);
            for (int i = 0; i < n; i++)
            {
                var t = ShaderUtil.GetPropertyType(sh, i);
                string rng = t == ShaderUtil.ShaderPropertyType.Range ? string.Format(" [{0}..{1}]", ShaderUtil.GetRangeLimits(sh, i, 1), ShaderUtil.GetRangeLimits(sh, i, 2)) : "";
                sb.AppendLine(string.Format("  {0} {1}{2}  \"{3}\"", ShaderUtil.GetPropertyName(sh, i), t, rng, ShaderUtil.GetPropertyDescription(sh, i)));
            }
        }

        // 4) 라이트맵 렌더러 셰이더
        sb.AppendLine("\n== 4. lightmapped renderers by shader ==");
        var byShader = new Dictionary<string, int>();
        var byShaderTris = new Dictionary<string, long>();
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (r.lightmapIndex < 0 || r.lightmapIndex >= 65534) continue;
            foreach (var m in r.sharedMaterials)
            {
                string k = m == null ? "(null)" : m.shader.name;
                byShader[k] = byShader.TryGetValue(k, out var c) ? c + 1 : 1;
            }
        }
        foreach (var kv in byShader.OrderByDescending(k => k.Value)) sb.AppendLine(string.Format("  {0,5}  {1}", kv.Value, kv.Key));
        foreach (var t in Object.FindObjectsOfType<Terrain>())
            sb.AppendLine(string.Format("  terrain {0} lm{1} template {2}", t.name, t.lightmapIndex, t.materialTemplate == null ? "null" : t.materialTemplate.shader.name + " (" + t.materialTemplate.name + ")"));
        sb.AppendLine(string.Format("  lightmaps {0}  mode {1}", LightmapSettings.lightmaps.Length, LightmapSettings.lightmapsMode));

        // 5) 라이트 / 볼륨 / 프로브
        sb.AppendLine("\n== 5. lights ==");
        foreach (var l in Object.FindObjectsOfType<Light>(true).OrderBy(l => l.type))
            sb.AppendLine(string.Format("  {0,-28} {1,-11} {2,-9} int {3:0.00} range {4:0.0} shadows {5} on {6}", l.name, l.type, l.lightmapBakeType, l.intensity, l.range, l.shadows, l.enabled && l.gameObject.activeInHierarchy));
        sb.AppendLine("== PP volumes ==");
        var ppType = System.Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime");
        if (ppType != null)
            foreach (Component v in Object.FindObjectsOfType(ppType, true))
            {
                var so = new SerializedObject(v);
                sb.AppendLine(string.Format("  {0} global {1} weight {2:0.00} priority {3} profile {4}", v.name, so.FindProperty("isGlobal").boolValue, so.FindProperty("weight").floatValue, so.FindProperty("priority").floatValue, so.FindProperty("sharedProfile").objectReferenceValue == null ? "null" : so.FindProperty("sharedProfile").objectReferenceValue.name));
            }
        sb.AppendLine("== reflection probes ==");
        foreach (var p in Object.FindObjectsOfType<ReflectionProbe>(true))
            sb.AppendLine(string.Format("  {0} mode {1} res {2} importance {3} size {4} custom {5}", p.name, p.mode, p.resolution, p.importance, p.size, p.customBakedTexture == null ? "null" : p.customBakedTexture.name));
        sb.AppendLine(string.Format("RenderSettings: sun {0}, ambientMode {1}, skybox {2}, fog {3} {4:0.0000}", RenderSettings.sun == null ? "null" : RenderSettings.sun.name, RenderSettings.ambientMode, RenderSettings.skybox == null ? "null" : RenderSettings.skybox.name, RenderSettings.fog, RenderSettings.fogDensity));
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void DiffSet(StringBuilder sb, string label, Material[] mats)
    {
        sb.AppendLine("-- " + label + " --");
        if (mats == null || mats.Length == 0) { sb.AppendLine("  (empty)"); return; }
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            sb.AppendLine(m == null ? "  [" + i + "] null" : string.Format("  [{0}] {1}  shader {2}  kw [{3}]", i, AssetDatabase.GetAssetPath(m), m.shader.name, string.Join(" ", m.shaderKeywords)));
        }
        var valid = mats.Where(m => m != null).ToArray();
        if (valid.Length < 2) return;
        if (valid.Select(m => m.shader).Distinct().Count() > 1) { sb.AppendLine("  !! 셰이더가 서로 다름"); }
        var names = new HashSet<string>();
        foreach (var m in valid)
        {
            var sh = m.shader; int n = ShaderUtil.GetPropertyCount(sh);
            for (int i = 0; i < n; i++) names.Add(ShaderUtil.GetPropertyName(sh, i) + "|" + (int)ShaderUtil.GetPropertyType(sh, i));
        }
        int same = 0;
        foreach (var key in names.OrderBy(k => k))
        {
            var parts = key.Split('|'); string p = parts[0]; var t = (ShaderUtil.ShaderPropertyType)int.Parse(parts[1]);
            var vals = mats.Select(m => m == null || !m.HasProperty(p) ? "-" : Val(m, p, t)).ToArray();
            if (vals.Distinct().Count() > 1) sb.AppendLine(string.Format("  ≠ {0} ({1}): {2}", p, t, string.Join("  |  ", vals)));
            else same++;
        }
        sb.AppendLine("  (같은 값 " + same + "개)");
    }

    static string Val(Material m, string p, ShaderUtil.ShaderPropertyType t)
    {
        switch (t)
        {
            case ShaderUtil.ShaderPropertyType.Color: { var c = m.GetColor(p); return string.Format("({0:0.###},{1:0.###},{2:0.###},{3:0.###})", c.r, c.g, c.b, c.a); }
            case ShaderUtil.ShaderPropertyType.Vector: { var v = m.GetVector(p); return string.Format("({0:0.###},{1:0.###},{2:0.###},{3:0.###})", v.x, v.y, v.z, v.w); }
            case ShaderUtil.ShaderPropertyType.TexEnv: { var tx = m.GetTexture(p); return tx == null ? "null" : tx.name; }
            default: return m.GetFloat(p).ToString("0.####");
        }
    }

    static string Fmt(object v)
    {
        if (v == null) return "null";
        if (v is string s) return "\"" + s + "\"";
        if (v is Object o) return o == null ? "null" : o.name;
        if (v is Color c) return string.Format("({0:0.###},{1:0.###},{2:0.###},{3:0.###})", c.r, c.g, c.b, c.a);
        if (v is Vector3 v3) return string.Format("({0:0.##},{1:0.##},{2:0.##})", v3.x, v3.y, v3.z);
        if (v is float fl) return fl.ToString("0.####");
        if (v is IEnumerable e && !(v is string))
        {
            var items = new List<string>();
            foreach (var x in e) items.Add(Fmt(x));
            return "[" + items.Count + "] " + string.Join("; ", items.Count > 40 ? items.Take(40).Append("…") : items);
        }
        return v.ToString();
    }

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.WriteAllText(LOG, sb.ToString()); }
}
#endif
