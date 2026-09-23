// Tools ▸ Pyrite2 ▸ Z32a. Moon + Lake Depth Audit
//  "하늘에 달이 없다" 실측: Sky_Moon 오브젝트·머티리얼·DayCycle 연결, 시각별 고도·색, 달 방향 렌더
//  + 보트 옮길 자리 호수 바닥 높이 (씬 변경 없음)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteMoonAudit
{
    [MenuItem("Tools/Pyrite2/Z32a. Moon + Lake Depth Audit", false, 60)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z32a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var moons = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && g.name.ToLower().Contains("moon")).ToArray();
        foreach (var g in moons)
        {
            var r = g.GetComponent<Renderer>();
            sb.AppendLine(string.Format("obj {0} activeSelf {1} inHier {2} tag {3} layer {4} pos {5} scale {6} | renderer {7} mat {8} shader {9} q {10}",
                P(g.transform), g.activeSelf, g.activeInHierarchy, g.tag, g.layer, g.transform.position.ToString("F1"), g.transform.lossyScale.ToString("F1"),
                r ? r.enabled.ToString() : "-", r && r.sharedMaterial ? r.sharedMaterial.name : "-", r && r.sharedMaterial ? r.sharedMaterial.shader.name : "-", r && r.sharedMaterial ? r.sharedMaterial.renderQueue : 0));
        }
        if (cyc == null) { sb.AppendLine("DayCycle 없음"); Write(sb); return; }
        sb.AppendLine("cyc.moonDisc " + (cyc.moonDisc ? cyc.moonDisc.name : "NULL") + " | moonDiscColor " + cyc.moonDiscColor + " | autoFlow " + cyc.autoFlow
            + " | hideObjects " + (cyc.hideObjects == null ? "null" : string.Join(",", cyc.hideObjects.Select(o => o ? o.name : "null"))));
        var cam = Camera.main;
        sb.AppendLine("main cam far " + cam.farClipPlane + " near " + cam.nearClipPlane + " cull " + cam.cullingMask);
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        Directory.CreateDirectory("Assets/_preview/moon/");
        try
        {
            foreach (var h in new[] { 18.5f, 19.5f, 21f, 23f, 2f, 5f, 6.5f })
            {
                cyc.ResetCache(); cyc.EvaluateAt(h);
                var c = cyc.moonDisc ? cyc.moonDisc.GetColor("_Color") : Color.clear;
                var d = cyc.moonDisc ? (Vector3)cyc.moonDisc.GetVector("_Dir") : Vector3.zero;
                sb.AppendLine(string.Format("  {0:00.0}h moonEl {1,5:0.0} az {2,4:0} sunEl {3,5:0.0} | _Color {4} | _Dir {5}", h, cyc.moonElNow, cyc.MoonAz(h), cyc.sunElNow, c, d.ToString("F2")));
                if (h == 21f || h == 23f || h == 2f)
                {
                    var eye = new Vector3(-10.6f, 3.4f, 53.8f);
                    cam.fieldOfView = 60f;
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(d.sqrMagnitude > 0.1f ? d : Vector3.back));
                    Shot(cam, string.Format("Assets/_preview/moon/moon_{0:00}.png", h));
                }
            }
        }
        finally { cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }

        var terr = Terrain.activeTerrain;
        if (terr)
            foreach (var x in new[] { -12.6f, -12.13f, -11.6f })
                sb.AppendLine(string.Format("lake bottom x {0}: ", x) + string.Join(" ", Enumerable.Range(0, 12).Select(i => { float z = 31f + i; return z.ToString("0") + ":" + (terr.SampleHeight(new Vector3(x, 0, z)) + terr.transform.position.y).ToString("F2"); })));
        var boat = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == "Boat");
        if (boat) sb.AppendLine("boat pos " + boat.transform.position.ToString("F2") + " static " + GameObjectUtility.GetStaticEditorFlags(boat) + " | bounds " + boat.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }).min.ToString("F2") + " .. " + boat.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }).max.ToString("F2"));
        Write(sb);
    }

    // Z32b: Sky_Moon 이 꺼져 있었다(activeSelf False, Z32a 실측) → 켠다. 옛 ToD(PyriteTimeOfDay)가 켜져 있으면 알린다
    [MenuItem("Tools/Pyrite2/Z32b. Moon Restore", false, 61)]
    public static void Restore()
    {
        var sb = new StringBuilder("[Z32b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && g.name == "Sky_Moon"))
        {
            sb.AppendLine("Sky_Moon activeSelf " + g.activeSelf + " → True");
            g.SetActive(true); EditorUtility.SetDirty(g);
        }
        foreach (var t in Object.FindObjectsOfType<PyriteTimeOfDay>(true))
        {
            var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(t);
            sb.AppendLine("old ToD " + P(t.transform) + " proxy enabled " + t.enabled + " udon enabled " + (ub ? ub.enabled.ToString() : "-") + " skyObjects " + (t.skyObjects == null ? "null" : string.Join(",", t.skyObjects.Select(o => o ? o.name + ":" + o.activeSelf : "null"))));
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Write(sb);
    }

    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(1280, 720, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }
    static void Write(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_moon.txt", sb.ToString()); }
    static string P(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
