// Tools ▸ Pyrite ▸ Z24a. Camp Table Audit (projector placement)
//  설정 프로젝터 배치 전 실측: 테이블·다이얼·의자·모닥불·ProTV·타프 기둥 위치/크기, 테이블 → 호수 방향
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteCampAudit2
{
    [MenuItem("Tools/Pyrite/Z24a. Camp Table Audit (projector)", false, 57)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z24a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var camp = GameObject.Find("Camp");
        foreach (var t in camp.GetComponentsInChildren<Transform>(true).Where(t => t.parent == camp.transform))
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            var b = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : new Bounds(t.position, Vector3.zero);
            sb.AppendLine(string.Format("  {0,-26} pos {1} rot {2} | bounds min {3} max {4}", t.name, t.position.ToString("F2"), t.eulerAngles.ToString("F0"), b.min.ToString("F2"), b.max.ToString("F2")));
        }
        foreach (var n in new[] { "TimeDial", "DialPointer", "TimePanel", "MediaPlayer", "LakeMirrorSwitch", "Mirror" })
        {
            var g = GameObject.Find(n) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == n && x.scene.IsValid());
            if (g == null) { sb.AppendLine("  " + n + " 없음"); continue; }
            var rs = g.GetComponentsInChildren<Renderer>(true);
            var b = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : new Bounds(g.transform.position, Vector3.zero);
            sb.AppendLine(string.Format("  {0,-26} pos {1} active {2} | bounds min {3} max {4} | parent {5}", n, g.transform.position.ToString("F2"), g.activeInHierarchy, b.min.ToString("F2"), b.max.ToString("F2"), g.transform.parent ? g.transform.parent.name : "-"));
        }
        var terr = Terrain.activeTerrain;
        var table = GameObject.Find("camp03_table");
        if (table != null)
        {
            var b = table.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            var d = new Vector3(0f - b.center.x, 0, -14f - b.center.z).normalized;
            sb.AppendLine(string.Format("table top y {0:0.000}, center {1}, ground {2:0.000}, dir to lake center {3} (yaw {4:0.0})", b.max.y, b.center.ToString("F2"),
                terr ? terr.SampleHeight(b.center) + terr.transform.position.y : 0f, d.ToString("F3"), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg));
            for (float s = 1f; s <= 5f; s += 1f)
            {
                var p = b.center + d * s;
                sb.AppendLine(string.Format("   +{0} m toward lake: ground {1:0.00}", s, terr ? terr.SampleHeight(p) + terr.transform.position.y : 0f));
            }
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_projector.txt", sb.ToString());
    }
}
#endif
