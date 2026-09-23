// Tools ▸ Pyrite2 ▸ Z28a. Camp Audit (fire, chairs)
//  화로 불빛 줄이기·회전의자 삭제·들고 다니는 의자 전 실측 (씬 변경 없음)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteCampAudit3
{
    [MenuItem("Tools/Pyrite2/Z28a. Camp Audit (fire, chairs)", false, 20)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z28a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc != null)
        {
            sb.AppendLine("campBase " + string.Join("/", cyc.campBase) + " | campLightMul " + string.Join("/", cyc.campLightMul.Select(x => x.ToString("0.00"))) + " | keyHour " + string.Join("/", cyc.keyHour));
            for (int i = 0; i < cyc.campLights.Length; i++)
            {
                var l = cyc.campLights[i];
                if (l == null) { sb.AppendLine("  campLights[" + i + "] null"); continue; }
                sb.AppendLine(string.Format("  campLights[{0}] {1} | int {2:0.00} range {3:0.0} shadows {4} mode {5} bake {6} color {7} | pos {8}",
                    i, Path(l.transform), l.intensity, l.range, l.shadows, l.renderMode, l.lightmapBakeType, l.color, l.transform.position.ToString("F2")));
            }
        }
        foreach (var l in Object.FindObjectsOfType<Light>(true).Where(l => l.transform.position.y > 1f && Vector3.Distance(l.transform.position, new Vector3(-10.5f, 2f, 51.5f)) < 3f))
            sb.AppendLine(string.Format("  near fire: {0} int {1:0.00} range {2:0.0} active {3}", Path(l.transform), l.intensity, l.range, l.gameObject.activeInHierarchy));

        // 의자 후보
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && (g.name.ToLower().Contains("chair") || g.name.ToLower().Contains("swivel") || g.name.ToLower().Contains("seat") || g.name.ToLower().Contains("stool")) && (g.transform.parent == null || !g.transform.parent.name.ToLower().Contains("chair"))))
        {
            var rs = g.GetComponentsInChildren<Renderer>(true);
            var b = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : new Bounds(g.transform.position, Vector3.zero);
            sb.AppendLine(string.Format("chair? {0} active {1} pos {2} rot {3} | bounds {4} .. {5}", Path(g.transform), g.activeInHierarchy, g.transform.position.ToString("F2"), g.transform.eulerAngles.ToString("F0"), b.min.ToString("F2"), b.max.ToString("F2")));
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
            {
                var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name + (c is Renderer r ? "(" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")) + ")" : "")).ToArray();
                var mf = t.GetComponent<MeshFilter>();
                sb.AppendLine(string.Format("    {0} [{1}] mesh {2} lpos {3} lrot {4}", t == g.transform ? "." : t.name, string.Join(",", comps), mf && mf.sharedMesh ? mf.sharedMesh.name + " tris " + mf.sharedMesh.triangles.Length / 3 : "-", t.localPosition.ToString("F2"), t.localEulerAngles.ToString("F0")));
            }
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_camp3.txt", sb.ToString());
    }

    static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
