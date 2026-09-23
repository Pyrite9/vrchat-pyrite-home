// Tools ▸ Pyrite2 ▸ Z29a. Lantern + TV + Chair Audit
//  1) 씬의 모든 랜턴: 계층·컴포넌트·조명·콜라이더  2) ProTV TVManager 의 autoplay/loop 관련 직렬화 필드
//  3) CarryChair 메시 높이 분포(앉는 면·등받이 판정용)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteLanternAudit
{
    [MenuItem("Tools/Pyrite2/Z29a. Lantern + TV + Chair Audit", false, 30)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z29a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var all = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid()).ToArray();
        foreach (var g in all.Where(g => g.name.ToLower().Contains("lantern") && (g.transform.parent == null || !g.transform.parent.name.ToLower().Contains("lantern"))))
        {
            sb.AppendLine(string.Format("LANTERN {0} active {1} tag {2} layer {3} pos {4} rot {5}", Path(g.transform), g.activeInHierarchy, g.tag, g.layer, g.transform.position.ToString("F2"), g.transform.eulerAngles.ToString("F0")));
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
            {
                var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c =>
                {
                    if (c is Light l) return string.Format("Light(int {0:0.00} range {1:0.0} shadows {2})", l.intensity, l.range, l.shadows);
                    if (c is BoxCollider b) return string.Format("Box(c {0} s {1} trig {2})", b.center.ToString("F2"), b.size.ToString("F2"), b.isTrigger);
                    if (c is Rigidbody rb) return string.Format("Rigidbody(kin {0} grav {1})", rb.isKinematic, rb.useGravity);
                    if (c is Renderer r) return c.GetType().Name + "(" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")) + ")";
                    return c.GetType().Name;
                }).ToArray();
                sb.AppendLine(string.Format("   {0} [{1}] lpos {2}", t == g.transform ? "." : t.name, string.Join(", ", comps), t.localPosition.ToString("F2")));
            }
        }
        var stand = all.FirstOrDefault(g => g.name == "camp07_lantern_stand");
        if (stand != null)
        {
            var rs = stand.GetComponentsInChildren<Renderer>(true);
            var b = rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            sb.AppendLine("lantern stand bounds " + b.min.ToString("F2") + " .. " + b.max.ToString("F2"));
        }

        // ProTV
        var tvm = all.Select(g => g.GetComponent("TVManager")).FirstOrDefault(c => c != null);
        if (tvm != null)
        {
            var so = new SerializedObject(tvm);
            var it = so.GetIterator(); bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                var n = it.name.ToLower();
                if (n.Contains("auto") || n.Contains("loop") || n.Contains("url") || n.Contains("play") || n.Contains("start") || n.Contains("volume") || n.Contains("sync"))
                {
                    string v = it.propertyType == SerializedPropertyType.Boolean ? it.boolValue.ToString() :
                        it.propertyType == SerializedPropertyType.Float ? it.floatValue.ToString() :
                        it.propertyType == SerializedPropertyType.Integer ? it.intValue.ToString() :
                        it.propertyType == SerializedPropertyType.String ? "\"" + it.stringValue + "\"" :
                        it.propertyType == SerializedPropertyType.ObjectReference ? (it.objectReferenceValue ? it.objectReferenceValue.name : "null") :
                        it.propertyType.ToString();
                    if (it.propertyType == SerializedPropertyType.Generic) { var u = it.FindPropertyRelative("url"); if (u != null) v = "VRCUrl \"" + u.stringValue + "\""; }
                    sb.AppendLine("TV " + it.propertyPath + " (" + it.type + ") = " + v);
                }
            }
        }
        else sb.AppendLine("TVManager 없음");

        // 의자 메시 높이 분포
        var chair = all.FirstOrDefault(g => g.name == "CarryChair");
        if (chair != null)
        {
            var mf = chair.GetComponentInChildren<MeshFilter>();
            var vs = mf.sharedMesh.vertices.Select(v => chair.transform.InverseTransformPoint(mf.transform.TransformPoint(v))).ToArray();
            sb.AppendLine(string.Format("chair local bounds x {0:0.00}..{1:0.00} y {2:0.00}..{3:0.00} z {4:0.00}..{5:0.00}", vs.Min(v => v.x), vs.Max(v => v.x), vs.Min(v => v.y), vs.Max(v => v.y), vs.Min(v => v.z), vs.Max(v => v.z)));
            for (float z0 = -0.3f; z0 < 0.3f; z0 += 0.1f)
            {
                var band = vs.Where(v => v.z >= z0 && v.z < z0 + 0.1f && Mathf.Abs(v.x) < 0.2f).ToArray();
                if (band.Length == 0) continue;
                var hist = band.GroupBy(v => Mathf.Floor(v.y / 0.05f)).OrderByDescending(gp => gp.Count()).Take(3).Select(gp => (gp.Key * 0.05f).ToString("0.00") + "x" + gp.Count());
                sb.AppendLine(string.Format("  z {0:0.0}..{1:0.0}: n {2} y {3:0.00}..{4:0.00} top bins {5}", z0, z0 + 0.1f, band.Length, band.Min(v => v.y), band.Max(v => v.y), string.Join(" ", hist)));
            }
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_lantern.txt", sb.ToString());
    }

    static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
