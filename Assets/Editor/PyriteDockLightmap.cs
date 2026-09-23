// Tools ▸ Pyrite ▸ Z6c. Dock Back To Lightmap (+ Bake + T + Capture)
//               Z6d. Dock Capture Only
//  인게임에서 부두 판자가 밋밋했다. Z6 때 Dock 을 라이트맵에서 빼서 라이트 프로브 한 점 밝기로
//  20 m 부두 전체가 한 톤이 됐기 때문. 부두만 라이트맵으로 되돌린다(캠프·침대는 실시간 그림자 유지).
//   1) 현재 상태 렌더(before) → Assets/_preview/dock/
//   2) Dock 렌더러에 ContributeGI 복구 + receiveGI=Lightmaps, Z6 목록에서 제거
//   3) 라이트맵 베이크(F) → 끝나면 반사 큐브맵(T) → after 렌더 → Logs/pyrite_dock.txt
//  되돌리기: Z6 을 다시 돌리면 Dock 도 다시 실시간으로 간다.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteDockLightmap
{
    const string OUT = "Assets/_preview/dock/";
    const string LOG = "Logs/pyrite_dock.txt";
    const string MARKS = "Assets/Editor/PyriteRealtimeProps.list.txt";
    static readonly StringBuilder log = new StringBuilder();
    static System.Diagnostics.Stopwatch sw;

    struct V { public string n; public Vector3 eye, look; public float fov; }
    static readonly V[] Views =
    {
        // 관리자 스크린샷과 비슷한 시점 — 물가 꽃밭에서 부두 쪽
        new V{ n="shore", eye=new Vector3( -2.0f, 1.7f, 47.0f), look=new Vector3(-10.5f, 0.3f, 33.0f), fov=65f },
        // 부두 뿌리에서 끝 쪽으로 판자 내려다보기
        new V{ n="deck",  eye=new Vector3(-10.5f, 1.7f, 44.5f), look=new Vector3(-10.6f, 0.2f, 33.0f), fov=60f },
        // 옆에서 — 기둥·판자 옆면
        new V{ n="side",  eye=new Vector3(  0.0f, 1.2f, 38.0f), look=new Vector3(-10.5f, 0.4f, 38.0f), fov=55f },
    };

    [MenuItem("Tools/Pyrite/Z6c. Dock Back To Lightmap (+Bake+T+Capture)", false, 12)]
    public static void Run()
    {
        if (Lightmapping.isRunning) { Debug.LogWarning("[Z6c] 베이크 중"); return; }
        log.Clear();
        log.AppendLine("[Z6c] " + System.DateTime.Now.ToString("HH:mm:ss"));

        Capture("before");

        var dock = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Dock");
        if (dock == null) { log.AppendLine("Dock 루트 없음 — 중단"); Flush(); return; }

        var marks = File.Exists(MARKS) ? File.ReadAllLines(MARKS).Where(s => s.Length > 0).ToList() : new System.Collections.Generic.List<string>();
        int k = 0;
        foreach (var r in dock.GetComponentsInChildren<MeshRenderer>(true))
        {
            var go = r.gameObject;
            string path = PathOf(go);
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            bool gi = (flags & StaticEditorFlags.ContributeGI) != 0;
            log.AppendLine(string.Format("  {0}  GI {1}  marked {2}  cast {3}  scale {4}",
                path, gi, marks.Contains(path), r.shadowCastingMode, r.scaleInLightmap));
            if (!marks.Contains(path) || gi) continue;
            Undo.RecordObject(go, "dock lm"); Undo.RecordObject(r, "dock lm");
            GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ContributeGI);
            r.receiveGI = ReceiveGI.Lightmaps;
            marks.Remove(path);
            k++;
        }
        File.WriteAllLines(MARKS, marks.OrderBy(x => x));
        AssetDatabase.ImportAsset(MARKS);
        log.AppendLine("복구 " + k + "개, 목록 남은 항목 " + marks.Count);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Flush();
        if (k == 0) { log.AppendLine("바뀐 게 없어 베이크 생략"); Flush(); return; }

        sw = System.Diagnostics.Stopwatch.StartNew();
        Lightmapping.bakeCompleted -= AfterBake;
        Lightmapping.bakeCompleted += AfterBake;
        PyriteBakeRun.Bake();
        log.AppendLine("베이크 시작"); Flush();
    }

    static void AfterBake()
    {
        Lightmapping.bakeCompleted -= AfterBake;
        log.AppendLine(string.Format("베이크 완료 {0:0.0}분, 라이트맵 {1}장", sw.Elapsed.TotalMinutes, LightmapSettings.lightmaps.Length));
        var dock = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Dock");
        if (dock != null)
            foreach (var r in dock.GetComponentsInChildren<MeshRenderer>(true))
                log.AppendLine("  " + PathOf(r.gameObject) + " lightmapIndex " + r.lightmapIndex);
        Flush();
        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.SaveOpenScenes();
            PyriteReflectionSets.Bake();
            log.AppendLine("T 완료");
            Capture("after");
            EditorSceneManager.SaveOpenScenes();
            log.AppendLine("RESULT: DONE");
            Flush();
        };
    }

    [MenuItem("Tools/Pyrite/Z6d. Dock Capture Only", false, 13)]
    public static void CaptureOnly() { log.Clear(); Capture("now"); Flush(); }

    // 다른 도구(Z13 등)가 전후 렌더를 찍을 때 — 로그는 Logs/pyrite_dock.txt 에 덧붙인다
    public static string CaptureTagged(string tag)
    {
        log.Clear(); Capture(tag);
        string s = log.ToString();
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, "[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n" + s);
        return s;
    }

    static void Capture(string tag)
    {
        Directory.CreateDirectory(OUT);
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var cam = Camera.main;
        if (cam == null) { log.AppendLine("Main Camera 없음"); return; }
        var t = Terrain.activeTerrain;
        if (tod != null)
        {
            var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(tod, false);
        }
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        float n0 = cam.nearClipPlane, fa0 = cam.farClipPlane;
        try
        {
            foreach (int i in new[] { 0, 1 })
            {
                if (tod != null) { tod.index = i; tod.Apply(); }
                foreach (var v in Views)
                {
                    var eye = v.eye;
                    if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0f, eye.z)) + t.transform.position.y);
                    string p = OUT + tag + "_" + v.n + "_" + i + ".png";
                    var st = Render(cam, eye, v.look, v.fov, p);
                    log.AppendLine("  " + p + "  " + st);
                }
            }
        }
        finally
        {
            cam.transform.SetPositionAndRotation(p0, r0);
            cam.fieldOfView = f0; cam.targetTexture = null; cam.nearClipPlane = n0; cam.farClipPlane = fa0;
            if (tod != null) { tod.index = 0; tod.Apply(); }
        }
        AssetDatabase.Refresh();
    }

    // 렌더 + 화면 중앙 1/3 영역 밝기 통계(0~255) — 판자 톤 차이 측정용
    static string Render(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        var px = tex.GetPixels32();
        var vals = new System.Collections.Generic.List<float>();
        for (int y = H / 3; y < 2 * H / 3; y += 3)
            for (int x = W / 3; x < 2 * W / 3; x += 3)
            { var c = px[y * W + x]; vals.Add((c.r + c.g + c.b) / 3f); }
        vals.Sort();
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        float mean = vals.Average();
        float sd = Mathf.Sqrt(vals.Select(v => (v - mean) * (v - mean)).Average());
        return string.Format("mean {0:0.0} sd {1:0.0} p10 {2:0} p90 {3:0}", mean, sd, vals[vals.Count / 10], vals[vals.Count * 9 / 10]);
    }

    static string PathOf(GameObject g) { var s = g.name; for (var t = g.transform.parent; t != null; t = t.parent) s = t.name + "/" + s; return s; }
    static void Flush() { Directory.CreateDirectory("Logs"); File.WriteAllText(LOG, log.ToString()); }
}
#endif
