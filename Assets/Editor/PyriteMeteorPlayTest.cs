// Tools ▸ Pyrite2 ▸ Z34f. Meteor Play Test (Ctrl+Alt+Shift+6) — Play 모드(ClientSim)에서 실제 PyriteMeteors 가 그은 별똥별을
//  오클루전 컬링 켬/끔 두 카메라로 같은 프레임에 찍어 비교한다. 에디터 정지 상태 렌더는 오클루전을 안 쓰므로 이걸로만 확인 가능
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteMeteorPlayTest
{
    static double t0; static int shots; static StringBuilder sb;

    [MenuItem("Tools/Pyrite2/Z34f. Meteor Play Test %&#6", false, 85)]
    public static void Run()
    {
        sb = new StringBuilder("=== Meteor Play Test " + System.DateTime.Now.ToString("HH:mm:ss") + " ===\n");
        if (!Application.isPlaying) { sb.AppendLine("Play 모드 아님"); Flush(); return; }
        t0 = EditorApplication.timeSinceStartup; shots = 0;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        sb.AppendLine("waiting for meteor (max 40 s)"); Flush();
    }

    static void Tick()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup - t0 > 40) { sb.AppendLine("timeout/stop, shots " + shots); Flush(); EditorApplication.update -= Tick; return; }
        var headGo = GameObject.Find("AmbientFX/Meteors/Head");
        if (headGo == null || !headGo.activeInHierarchy) return;
        var tr = headGo.GetComponent<TrailRenderer>();
        if (!tr.emitting || tr.positionCount < 30) return;   // 한창 그을 때   // 한창 그을 때(또는 끝난 직후) 찍는다
        var eye = new Vector3(-10.6f, 3.65f, 53.8f);
        var go = new GameObject("__MeteorTestCam"); var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.allowHDR = true;
        go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(tr.bounds.center - eye));
        sb.AppendLine(string.Format("shot {0}: head {1} dist {2:0} m, positionCount {3}, emitting {4}, isVisible {5}, allowOcclusionWhenDynamic {6}, bounds {7}",
            shots, headGo.transform.position.ToString("F0"), Vector3.Distance(eye, headGo.transform.position), tr.positionCount, tr.emitting, tr.isVisible, tr.allowOcclusionWhenDynamic, tr.bounds));
        foreach (var occ in new[] { true, false })
        {
            cam.useOcclusionCulling = occ;
            var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tx = new Texture2D(960, 540, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tx.Apply(); RenderTexture.active = null;
            Directory.CreateDirectory("Assets/_preview/fx/");
            File.WriteAllBytes("Assets/_preview/fx/play_meteor_" + shots + (occ ? "_occ" : "_noocc") + ".png", tx.EncodeToPNG());
            cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
        }
        Object.DestroyImmediate(go);
        shots++;
        Flush();
        if (shots >= 1) { EditorApplication.update -= Tick; sb.AppendLine("done"); Flush(); }
        else t0 = EditorApplication.timeSinceStartup;
    }

    static void Flush() { File.WriteAllText("Logs/pyrite_meteor_play.txt", sb.ToString()); }
}
#endif
