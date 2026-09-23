// Tools ▸ Pyrite ▸ Z22a. Crystal Material Audit
//  골드 비율이 너무 높다 — 일부를 실버로. 결정 렌더러·머티리얼·위치를 전부 기록(씬 변경 없음)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteCrystalAudit
{
    [MenuItem("Tools/Pyrite/Z22a. Crystal Material Audit", false, 52)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z22a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var rs = Object.FindObjectsOfType<Renderer>(true)
            .Where(r => r.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name == "Pyrite/PyriteMetal")).ToArray();
        sb.AppendLine("renderers with PyriteMetal: " + rs.Length);
        var byMat = rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).GroupBy(m => m.name).Select(g => g.Key + " x" + g.Count());
        sb.AppendLine("material slots: " + string.Join(", ", byMat));
        foreach (var r in rs.OrderBy(r => Path(r.transform)))
        {
            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf ? mf.sharedMesh : null;
            sb.AppendLine(string.Format("  {0} | mats [{1}] | sub {2} verts {3} | center {4} size {5} | static {6}",
                Path(r.transform), string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")),
                mesh ? mesh.subMeshCount : 0, mesh ? mesh.vertexCount : 0, r.bounds.center.ToString("F1"), r.bounds.size.ToString("F1"),
                GameObjectUtility.GetStaticEditorFlags(r.gameObject)));
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_crystal_audit.txt", sb.ToString());
    }

    static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
