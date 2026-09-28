// PyriteBedroomProbe2.cs — 침실 조명·창문 준비 실측 (읽기 전용)
// Tools ▸ Pyrite3 ▸ Z49h. Bedroom Probe 2 → 클립보드 + Logs/pyrite_bedroom_probe2.txt
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteBedroomProbe2
{
    [MenuItem("Tools/Pyrite3/Z49h. Bedroom Probe 2", false, 4907)]
    public static void Run()
    {
        var sb = new StringBuilder("# probe2 " + System.DateTime.Now.ToString("HH:mm:ss") + "\n## layers\n");
        for (int i = 0; i < 32; i++) sb.AppendLine(i + " '" + LayerMask.LayerToName(i) + "' objs " + Object.FindObjectsOfType<GameObject>(true).Count(g => g.layer == i));
        sb.AppendLine("## lights");
        foreach (var l in Object.FindObjectsOfType<Light>(true))
            sb.AppendLine(P(l.transform) + " " + l.type + " " + l.lightmapBakeType + " I " + l.intensity + " R " + l.range + " col " + l.color + " sh " + l.shadows + " mask " + l.cullingMask + " active " + l.gameObject.activeInHierarchy);
        sb.AppendLine("## lantern/candle/lamp/flame renderers");
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            string n = P(r.transform).ToLower();
            if (!(n.Contains("lantern") || n.Contains("candle") || n.Contains("lamp") || n.Contains("flame") || n.Contains("light"))) continue;
            var mf = r.GetComponent<MeshFilter>();
            sb.AppendLine(P(r.transform) + " | " + r.GetType().Name + " mesh " + (mf && mf.sharedMesh ? mf.sharedMesh.name + " (" + AssetDatabase.GetAssetPath(mf.sharedMesh) + ")" : "-")
                + " | mats " + string.Join(",", r.sharedMaterials.Where(m => m).Select(m => m.name + "/" + m.shader.name))
                + " | size " + r.bounds.size.ToString("F2") + " active " + r.gameObject.activeInHierarchy);
        }
        sb.AppendLine("## crystals (first level)");
        var cr = GameObject.Find("Crystals");
        if (cr) foreach (Transform c in cr.transform)
            {
                sb.AppendLine(P(c) + " children " + c.childCount);
                foreach (Transform cc in c) { var r = cc.GetComponent<Renderer>(); if (r) sb.AppendLine("   " + cc.name + " size " + r.bounds.size.ToString("F2") + " mat " + (r.sharedMaterial ? r.sharedMaterial.name : "-")); }
            }
        sb.AppendLine("## camp roots near tent");
        foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var rs = g.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.size.magnitude > 500) continue;
            float d = Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(-3.4f, 55.6f));
            if (d < 25f) sb.AppendLine(g.name + " center " + b.center.ToString("F1") + " size " + b.size.ToString("F1") + " dist " + d.ToString("F1") + " active " + g.activeSelf);
        }
        sb.AppendLine("## terrain height along tent → lake");
        var t = Terrain.activeTerrain;
        for (float z = 56f; z >= 30f; z -= 4f) { var p = new Vector3(-3.4f, 0, z); sb.AppendLine("z " + z + " h " + (t ? t.SampleHeight(p) + t.transform.position.y : 0).ToString("F2")); }
        sb.AppendLine("## shaders");
        foreach (var s in new[] { "Pyrite/StandardNight", "Pyrite/AddGlow", "Pyrite/SoftAlpha", "Standard" }) sb.AppendLine(s + " " + (Shader.Find(s) != null));
        foreach (var g in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" })) sb.AppendLine("  " + AssetDatabase.GUIDToAssetPath(g));
        File.WriteAllText("Logs/pyrite_bedroom_probe2.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
    static string P(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
