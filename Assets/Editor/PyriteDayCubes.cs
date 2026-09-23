// Tools ▸ Pyrite ▸ Z23a. Bake Day Reflection Sets (morning/noon/afternoon)
//  낮에도 노을(세트 0)·새벽(세트 2) 큐브맵을 쓰고 있었다 → 결정·금속에 주황 하늘이 비친다.
//  사이클을 07:30 / 12:00 / 16:00 으로 맞춰 프로브 5개를 굽고 세트 3·4·5 로 붙인다.
//   probeCubes 는 [세트 × 프로브 수 + 프로브] 로 펼친 배열 (예전 ToD 배열을 늘리고 Z18b 가 그대로 복사)
//   키프레임: 아침 → 3, 정오 → 4, 오후 → 5 (Z18b 가 세트 수를 보고 고른다)
//  전후 렌더 12:00 × (캠프→호수, 건너편, 오른쪽 결정) → Assets/_preview/day/cube_*.png
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteDayCubes
{
    const string DIR = "Assets/Reflections/";
    static readonly (float h, string label)[] SETS = { (7.5f, "Morning"), (12f, "Noon"), (16f, "Afternoon") };

    [MenuItem("Tools/Pyrite/Z23a. Bake Day Reflection Sets", false, 55)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z23a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (tod == null || cyc == null || tod.probes == null) { sb.AppendLine("tod/cycle/probes 없음"); Flush(sb); return; }
        var probes = tod.probes;
        int np = probes.Length;
        sb.AppendLine("probes: " + string.Join(", ", probes.Select(p => p.name)) + "  existing cubes " + tod.probeCubes.Length);

        Shots(cyc, "before", sb);

        var cubes = tod.probeCubes.Take(3 * np).ToList();         // 기존 노을·밤·새벽
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            cyc.ResetCache();
            int si = 3;
            foreach (var (h, label) in SETS)
            {
                cyc.EvaluateAt(h);
                for (int k = 0; k < np; k++)
                {
                    string path = DIR + probes[k].name + "_" + si + "_" + label + ".exr";
                    var mode0 = probes[k].mode; probes[k].mode = ReflectionProbeMode.Baked;
                    bool ok = Lightmapping.BakeReflectionProbe(probes[k], path);
                    probes[k].mode = mode0;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    cubes.Add(AssetDatabase.LoadAssetAtPath<Texture>(path));
                    sb.AppendLine(string.Format("  {0} {1}", path, ok));
                }
                si++;
            }
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        sb.AppendLine(string.Format("  baked {0:0.0}s, total cubes {1}", sw.Elapsed.TotalSeconds, cubes.Count));

        tod.probeCubes = cubes.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(tod); EditorUtility.SetDirty(tod);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        // 사이클 다시 셋업 (키프레임 세트 번호 갱신)
        PyriteDayCycleSetup.Setup();
        sb.AppendLine("  Z18b re-run: keyCubeSet " + string.Join(",", cyc.keyCubeSet) + " probeCubes " + cyc.probeCubes.Length);

        Shots(cyc, "after", sb);
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Shots(PyriteDayCycle cyc, string tag, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var terr = Terrain.activeTerrain;
        Directory.CreateDirectory("Assets/_preview/day/");
        var views = new (string n, Vector3 e, Vector3 l, float f)[]
        {
            ("camp_lake", new Vector3(-10f, 0f, 47f), new Vector3(-10f, 3f, -40f), 70f),
            ("far_to_camp", new Vector3(5f, 0f, -40f), new Vector3(-5f, 6f, 78f), 70f),
            ("crystal_r", new Vector3(-40f, 0f, 42f), new Vector3(-52f, 3f, 26f), 50f),
        };
        try
        {
            cyc.ResetCache();
            foreach (var h in new[] { 7.5f, 12f, 16f })
            {
                cyc.EvaluateAt(h);
                foreach (var v in views)
                {
                    var eye = v.e; if (terr != null) eye.y = terr.SampleHeight(eye) + terr.transform.position.y + 1.7f;
                    Shot(cam, eye, v.l, v.f, string.Format("Assets/_preview/day/cube_{0}_{1:00}_{2}.png", tag, (int)h, v.n));
                }
            }
            sb.AppendLine("  shots " + tag);
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_day.txt", sb.ToString()); }
}
#endif
