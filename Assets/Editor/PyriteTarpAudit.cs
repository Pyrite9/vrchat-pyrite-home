// Tools ▸ Pyrite2 ▸ Z27a. Tarp Rope Audit
//  타프 줄에 손거울을 걸기 전 실측: 타프 계층·머티리얼, 줄(가는 긴 부분) 끝점 = 기둥 꼭대기 ↔ 땅 말뚝, 확인 렌더
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteTarpAudit
{
    [MenuItem("Tools/Pyrite2/Z27a. Tarp Rope Audit", false, 10)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z27a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var tarp = GameObject.Find("camp02_hexa_tarp_GRN");
        if (tarp == null) { sb.AppendLine("tarp 없음"); Flush(sb); return; }
        foreach (var r in tarp.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null;
            sb.AppendLine(string.Format("renderer {0} | mats [{1}] | mesh {2} verts {3} sub {4} | bounds {5} .. {6}", Path(r.transform),
                string.Join(",", r.sharedMaterials.Select(x => x ? x.name : "null")), m ? m.name : "-", m ? m.vertexCount : 0, m ? m.subMeshCount : 0,
                r.bounds.min.ToString("F2"), r.bounds.max.ToString("F2")));
            if (m == null) continue;
            Vector3[] verts;
            try { verts = m.vertices; } catch (System.Exception e) { sb.AppendLine("  vertices 실패: " + e.Message); continue; }
            if (verts == null || verts.Length == 0) { sb.AppendLine("  vertices 비어 있음 (readable " + m.isReadable + ")"); continue; }
            var tr = r.transform;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var idx = m.GetIndices(s).Distinct().ToArray();
                var vs = idx.Select(i => tr.TransformPoint(verts[i])).ToArray();
                if (vs.Length == 0) continue;
                var b = new Bounds(vs[0], Vector3.zero); foreach (var v in vs) b.Encapsulate(v);
                sb.AppendLine(string.Format("  sub {0} mat {1} verts {2} bounds {3} .. {4}", s, s < r.sharedMaterials.Length && r.sharedMaterials[s] ? r.sharedMaterials[s].name : "-", vs.Length, b.min.ToString("F2"), b.max.ToString("F2")));
                // 땅 가까운 점(말뚝) 군집, 높은 점(기둥 꼭대기) 군집
                sb.AppendLine("    low  (y<2.0): " + Clusters(vs.Where(v => v.y < 2.0f), 0.4f));
                sb.AppendLine("    high (top 3%): " + Clusters(vs.OrderByDescending(v => v.y).Take(Mathf.Max(4, vs.Length / 33)), 0.5f));
            }
        }
        // 확인 렌더: 캠프 쪽에서 타프
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        Directory.CreateDirectory("Assets/_preview/tarp/");
        try
        {
            foreach (var (n, e, l) in new[] { ("front", new Vector3(-9.3f, 3.4f, 50.0f), new Vector3(-9.3f, 2.8f, 57.6f)), ("left", new Vector3(-17.5f, 3.2f, 53.0f), new Vector3(-12.0f, 2.8f, 57.0f)) })
            {
                cam.transform.SetPositionAndRotation(e, Quaternion.LookRotation(l - e));
                cam.fieldOfView = 65f;
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var tx = new Texture2D(1280, 720, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tx.Apply(); RenderTexture.active = null;
                File.WriteAllBytes("Assets/_preview/tarp/tarp_" + n + ".png", tx.EncodeToPNG());
                cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
        finally { cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null; }
        Flush(sb);
    }

    static string Clusters(IEnumerable<Vector3> pts, float rad)
    {
        var cs = new List<(Vector3 sum, int n)>();
        foreach (var p in pts)
        {
            int k = cs.FindIndex(c => Vector3.Distance(c.sum / c.n, p) < rad);
            if (k < 0) cs.Add((p, 1)); else cs[k] = (cs[k].sum + p, cs[k].n + 1);
        }
        return string.Join("  ", cs.Select(c => (c.sum / c.n).ToString("F2") + "x" + c.n));
    }

    static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_tarp.txt", sb.ToString()); }
}
#endif
