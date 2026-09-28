// PyriteBedroomPropsProbe.cs — 침실 소품 배치 전 실측 (Z51a, 읽기 전용). Logs/pyrite_bedroom_props_probe.txt
//  침실 자식별 bounds(방 로컬)·삼각형, Furniture/Lights 트리, 매트·Lie 위치, 캠프 머그·스토브 등 재사용 후보 메시(에셋 경로), 바닥 빈 칸 지도(0.25 m, 렌더러 bounds 기준)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomPropsProbe
{
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z51a. Bedroom Props Probe", false, 5101)]
    public static void Run()
    {
        sb = new StringBuilder("[Z51a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        sb.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_props_probe.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
    static int Tris(Renderer r) { var mf = r.GetComponent<MeshFilter>(); var sm = r as SkinnedMeshRenderer; var m = mf ? mf.sharedMesh : sm ? sm.sharedMesh : null; return m ? (int)(m.triangles.Length / 3) : 0; }

    static void Inner()
    {
        var room = Root("TentBedroom").transform;
        Vector3 L(Vector3 w) => w - room.position;
        int total = 0;
        sb.AppendLine("== 침실 자식 (활성 렌더러 삼각형 / bounds 방 로컬)");
        foreach (Transform c in room)
        {
            var rs = c.GetComponentsInChildren<Renderer>(false).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            int t = rs.Sum(Tris); total += t;
            if (rs.Length == 0) { sb.AppendLine("  " + c.name + " (렌더러 없음) " + V(c.localPosition) + (c.gameObject.activeSelf ? "" : " 꺼짐")); continue; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            sb.AppendLine("  " + c.name + (c.gameObject.activeSelf ? "" : " 꺼짐") + " tris " + t + " min " + V(L(b.min)) + " max " + V(L(b.max)));
        }
        sb.AppendLine("  합계(활성) " + total);

        foreach (var n in new[] { "Furniture", "Lights", "Beds" })
        {
            var t = room.Find(n); if (!t) continue;
            sb.AppendLine("== " + n);
            foreach (var tr in t.GetComponentsInChildren<Transform>(true))
            {
                if (tr == t) continue;
                int d = 0; for (var p = tr.parent; p != t; p = p.parent) d++;
                if (d > 2) continue;
                var r = tr.GetComponent<Renderer>();
                string extra = r ? " bounds min " + V(L(r.bounds.min)) + " max " + V(L(r.bounds.max)) + " tris " + Tris(r) + " mat " + (r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "-") : " pos " + V(L(tr.position));
                var st = tr.GetComponent("VRCStation") ? " [Station]" : "";
                sb.AppendLine("  " + new string(' ', d * 2) + tr.name + st + extra);
            }
        }

        sb.AppendLine("== 재사용 후보 메시 (씬 전체에서 이름으로)");
        foreach (var key in new[] { "Mug", "Kettle", "Stove", "Book", "Bottle", "Crystal", "Rug", "Mat", "Lantern" })
        {
            var hits = Object.FindObjectsOfType<MeshFilter>(true).Where(mf => mf.sharedMesh && mf.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0 && !mf.transform.IsChildOf(room)).Take(4);
            foreach (var mf in hits)
            {
                var r = mf.GetComponent<Renderer>();
                sb.AppendLine("  [" + key + "] " + Path(mf.transform) + " mesh " + mf.sharedMesh.name + " (" + (AssetDatabase.GetAssetPath(mf.sharedMesh) is string ap && ap.Length > 0 ? ap : "씬 내장") + ") tris " + mf.sharedMesh.triangles.Length / 3 + " size " + mf.sharedMesh.bounds.size.ToString("F2") + " mat " + (r && r.sharedMaterial ? r.sharedMaterial.name + " ← " + AssetDatabase.GetAssetPath(r.sharedMaterial) : "-"));
            }
        }
        sb.AppendLine("== 황철석 머티리얼");
        foreach (var guid in AssetDatabase.FindAssets("t:Material M_Pyrite")) sb.AppendLine("  " + AssetDatabase.GUIDToAssetPath(guid));

        // 바닥 빈 칸: x −3.2..3.2, z −2.6..2.6 을 0.25 m 로, 바닥 위 0.02~1.2 m 를 차지하는 렌더러 bounds 와 겹치면 #, 돔 밖이면 공백, 비면 .
        sb.AppendLine("== 바닥 지도 (위 = +Z 창, 왼쪽 = −X 입구·TV, # 막힘, . 빈 곳, 0.25 m 칸)");
        var blockers = room.GetComponentsInChildren<Renderer>(false).Where(r => !(r is ParticleSystemRenderer) && r.name != "Floor" && r.name != "Dome" && !r.name.StartsWith("Pole") && r.transform.parent && !r.transform.IsChildOf(room.Find("Backdrop") ?? room.Find("__none__")) && !r.transform.IsChildOf(room.Find("Window") ?? room.Find("__none__"))).ToArray();
        var boxes = blockers.Select(r => { var b = r.bounds; b.center = L(b.center); return b; }).Where(b => b.max.y > 0.02f && b.min.y < 1.2f && b.size.x < 20f).ToArray();
        for (float z = 2.625f; z > -2.7f; z -= 0.25f)
        {
            var line = new StringBuilder("  " + z.ToString("+0.00;-0.00") + " ");
            for (float x = -3.125f; x < 3.2f; x += 0.25f)
            {
                bool inside = PyriteBedroomBuild.SurfZ(x, 0.3f) > Mathf.Abs(z) + 0.1f;
                if (!inside) { line.Append(' '); continue; }
                var cell = new Bounds(new Vector3(x, 0.5f, z), new Vector3(0.25f, 0.9f, 0.25f));
                line.Append(boxes.Any(b => b.Intersects(cell)) ? '#' : '.');
            }
            sb.AppendLine(line.ToString());
        }
        sb.AppendLine("  x 열: −3.125 부터 0.25 m 간격 (가운데 x 0 = 13번째 칸)");
    }

    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
