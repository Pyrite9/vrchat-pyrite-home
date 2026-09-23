// Tools ▸ Pyrite2 ▸ Z30b. Boat + Dock Audit
//  보트 프리팹(크기·방향·삼각형·머티리얼·텍스처) + 부두(판자·계선주 위치) + 수면 높이 실측, 보트 단독 확인 렌더
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteBoatAudit
{
    [MenuItem("Tools/Pyrite2/Z30b. Boat + Dock Audit", false, 41)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z30b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WoodBoat/WoodBoat.prefab");
        if (pf != null)
        {
            foreach (var t in pf.GetComponentsInChildren<Transform>(true))
            {
                var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name).ToArray();
                var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                sb.AppendLine(string.Format("  {0} [{1}] lpos {2} lrot {3} lscale {4}", t.name, string.Join(",", comps), t.localPosition.ToString("F3"), t.localEulerAngles.ToString("F0"), t.localScale.ToString("F3")));
                if (mf && mf.sharedMesh)
                {
                    var m = mf.sharedMesh;
                    sb.AppendLine(string.Format("     mesh {0} verts {1} tris {2} sub {3} bounds c {4} s {5}", m.name, m.vertexCount, m.triangles.Length / 3, m.subMeshCount, m.bounds.center.ToString("F3"), m.bounds.size.ToString("F3")));
                }
                if (mr) foreach (var mat in mr.sharedMaterials)
                    sb.AppendLine(string.Format("     mat {0} shader {1} tex {2} color {3}", mat ? mat.name : "null", mat && mat.shader ? mat.shader.name : "-",
                        mat && mat.mainTexture ? mat.mainTexture.name + " " + mat.mainTexture.width + "px" : "-", mat && mat.HasProperty("_Color") ? mat.color.ToString() : "-"));
            }
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            var b = tmp.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            sb.AppendLine("  prefab world bounds at origin: c " + b.center.ToString("F3") + " size " + b.size.ToString("F3"));
            // 단독 렌더
            Directory.CreateDirectory("Assets/_preview/boat/");
            tmp.transform.position = new Vector3(0f, 500f, 0f);
            var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            var bb = tmp.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            foreach (var (n, d) in new[] { ("iso", new Vector3(1f, 0.7f, 1f)), ("side", new Vector3(1f, 0.1f, 0f)), ("front", new Vector3(0f, 0.2f, 1f)), ("top", new Vector3(0.01f, 1f, 0f)) })
            {
                var eye = bb.center + d.normalized * bb.size.magnitude * 1.3f;
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(bb.center - eye));
                cam.fieldOfView = 40f;
                var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var tx = new Texture2D(960, 540, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tx.Apply(); RenderTexture.active = null;
                File.WriteAllBytes("Assets/_preview/boat/boat_" + n + ".png", tx.EncodeToPNG());
                cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
            }
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0;
            Object.DestroyImmediate(tmp);
        }
        else sb.AppendLine("WoodBoat.prefab 없음");

        // 부두
        var dock = GameObject.Find("Dock");
        if (dock != null)
        {
            foreach (var t in dock.GetComponentsInChildren<Transform>(true).Where(t => t.parent == dock.transform))
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                var b = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : new Bounds(t.position, Vector3.zero);
                sb.AppendLine(string.Format("dock {0} pos {1} | bounds {2} .. {3}", t.name, t.position.ToString("F2"), b.min.ToString("F2"), b.max.ToString("F2")));
            }
        }
        var water = GameObject.Find("Water");
        if (water != null) foreach (var t in water.GetComponentsInChildren<Transform>(true).Where(t => t.parent == water.transform))
            sb.AppendLine("water " + t.name + " pos " + t.position.ToString("F3") + (t.GetComponent<Collider>() ? " collider top " + t.GetComponent<Collider>().bounds.max.y.ToString("F3") : ""));
        var terr = Terrain.activeTerrain;
        if (terr) foreach (var p in new[] { new Vector3(-8.5f, 0, 32f), new Vector3(-12.9f, 0, 32f), new Vector3(-8.5f, 0, 35f), new Vector3(-12.9f, 0, 35f) })
            sb.AppendLine("lake bottom at " + p + " = " + (terr.SampleHeight(p) + terr.transform.position.y).ToString("F2"));
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_boat.txt", sb.ToString());
    }
}
#endif
