// Tools ▸ Pyrite2 ▸ Z31a. Camp Audit (props layout)
//  마시멜로·망원경·돗자리·스토브·주전자·머그 배치 전 실측: 캠프 물건 bounds + 위에서 본 렌더 (씬 변경 없음)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteCampAudit4
{
    [MenuItem("Tools/Pyrite2/Z31a. Camp Audit (props layout)", false, 50)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z31a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var names = new[] { "Camp", "SettingsProjector", "VideoProjector", "TarpMirror", "BigMirror", "CarryChair", "CarryChair_1", "CarryChair_2" };
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!names.Contains(root.name) && !root.name.StartsWith("Pick_")) continue;
            var list = root.name == "Camp" ? root.transform.Cast<Transform>().ToArray() : new[] { root.transform };
            foreach (var t in list)
            {
                var rs = t.GetComponentsInChildren<Renderer>(false).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (rs.Length == 0) { sb.AppendLine(string.Format("{0} pos {1} (no renderer) active {2}", P(t), t.position.ToString("F2"), t.gameObject.activeInHierarchy)); continue; }
                var b = rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                sb.AppendLine(string.Format("{0} pos {1} yaw {2:0} | bounds {3} .. {4} | cols {5} active {6}", P(t), t.position.ToString("F2"), t.eulerAngles.y,
                    b.min.ToString("F2"), b.max.ToString("F2"), t.GetComponentsInChildren<Collider>().Length, t.gameObject.activeInHierarchy));
            }
        }
        // 타프 밑 물건 (돗자리 자리 찾기): 렌더러 bounds 가 x -15..-3, z 54..62 에 걸치는 루트·자식
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            var bb = r.bounds;
            if (bb.max.x < -15f || bb.min.x > -3f || bb.max.z < 54f || bb.min.z > 62f) continue;
            if (bb.size.x > 6f || bb.size.z > 6f || r is ParticleSystemRenderer) continue;
            if (bb.min.y > 2.6f) continue;
            sb.AppendLine(string.Format("  tarp-area {0} | {1} .. {2}", P(r.transform), bb.min.ToString("F2"), bb.max.ToString("F2")));
        }
        // 테이블 상판 메시 정점 (가장 높은 면)
        var table = GameObject.Find("Camp/camp03_table");
        if (table)
        {
            foreach (var mf in table.GetComponentsInChildren<MeshFilter>())
            {
                var v = mf.sharedMesh.vertices.Select(p => mf.transform.TransformPoint(p)).ToArray();
                float top = v.Max(p => p.y);
                var tv = v.Where(p => p.y > top - 0.01f).ToArray();
                sb.AppendLine(string.Format("table mesh {0} lrot {1} top y {2:F3} top rect x {3:F2}..{4:F2} z {5:F2}..{6:F2} ({7} v)", mf.name, mf.transform.eulerAngles.ToString("F0"), top,
                    tv.Min(p => p.x), tv.Max(p => p.x), tv.Min(p => p.z), tv.Max(p => p.z), tv.Length));
                sb.AppendLine("   table right(+local X) world " + mf.transform.right.ToString("F2") + " fwd " + mf.transform.forward.ToString("F2"));
            }
        }
        var terr = Terrain.activeTerrain;
        if (terr) for (int i = 0; i < 8; i++)
        {
            var p = new Vector3(-10.5f, 0, 51.5f) + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 3.5f;
            sb.AppendLine(string.Format("ground at fire+3.5m dir {0}: ({1:F1},{2:F1}) y {3:F2}", i * 45, p.x, p.z, terr.SampleHeight(p) + terr.transform.position.y));
        }
        // 위에서 본 렌더
        Directory.CreateDirectory("Assets/_preview/props/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView; bool o0 = cam.orthographic; float s0 = cam.orthographicSize;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            cam.orthographic = true; cam.orthographicSize = 5f;
            cam.transform.SetPositionAndRotation(new Vector3(-10.8f, 30f, 53.8f), Quaternion.Euler(90f, 0f, 0f));
            Shot(cam, "Assets/_preview/props/audit_top.png");
            cam.orthographic = false; cam.fieldOfView = 55f;
            var eye = new Vector3(-10.8f, 3.6f, 59.5f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(new Vector3(-10.8f, 1.9f, 53f) - eye));
            Shot(cam, "Assets/_preview/props/audit_persp.png");
        }
        finally
        {
            cam.orthographic = o0; cam.orthographicSize = s0; cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_props.txt", sb.ToString());
    }

    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1200, 1200, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(1200, 1200, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    static string P(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
