// Tools ▸ Pyrite4 ▸ Z53f. Font Check (읽기 전용) — 씬의 TMP 글자 중 글꼴이 끊긴 것 개수 + 침실 탁상시계 가까이 렌더(21시)
//  2026-09-30: Z25a 가 글꼴 에셋을 지우고 새로 만들 때 침실 글자 20개(머리맡 패널 · 탁상시계)가 글꼴을 잃었다 → Z25a 에 다시 연결을 넣었고, 그 확인용
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteFontCheck
{
    [MenuItem("Tools/Pyrite4/Z53f. Font Check (TMP + desk clock)", false, 5306)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z53f] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var all = Object.FindObjectsOfType<TMP_Text>(true);
            var broken = all.Where(t => t.font == null).ToArray();
            sb.AppendLine("TMP 글자 " + all.Length + " · 글꼴 끊김 " + broken.Length);
            foreach (var b in broken.Take(20)) sb.AppendLine("  !! " + b.transform.root.name + "/…/" + b.name);
            foreach (var g in all.Where(t => t.font != null).GroupBy(t => t.font.name)) sb.AppendLine("  글꼴 " + g.Key + ": " + g.Count());

            var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == "TentBedroom");
            if (room != null)
            {
                var o = room.transform;
                var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
                var cyc = Object.FindObjectOfType<PyriteDayCycle>();
                try
                {
                    if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
                    cam.fieldOfView = 50f;
                    var eye = o.TransformPoint(new Vector3(1.05f, 0.70f, -1.55f)); var at = o.TransformPoint(new Vector3(1.45f, 0.44f, -2.01f));
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
                    int w = 960, h = 540;
                    var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                    var a = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                    RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
                    Directory.CreateDirectory("Assets/_preview/beer/");
                    File.WriteAllBytes("Assets/_preview/beer/fontcheck_clock.jpg", tex.EncodeToJPG(88));
                    Object.DestroyImmediate(tex);
                    sb.AppendLine("shot Assets/_preview/beer/fontcheck_clock.jpg");
                }
                finally
                {
                    cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
                    if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
                }
            }
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_fontcheck.txt", sb.ToString(), new UTF8Encoding(false));
    }
}
#endif
