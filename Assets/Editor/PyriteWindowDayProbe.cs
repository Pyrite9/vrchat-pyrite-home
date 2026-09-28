// PyriteWindowDayProbe.cs — 창 카메라(BedroomWindowCam) 화면 밝기를 시각별로 실측 (Z50e, 읽기 전용). Logs/pyrite_window_dayprobe.txt
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteWindowDayProbe
{
    [MenuItem("Tools/Pyrite3/Z50e. Window Cam Day Probe", false, 5005)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z50e] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var go = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "BedroomWindowCam");
        var cam = go ? go.GetComponentInChildren<Camera>(true) : null;
        if (cam == null) { sb.AppendLine("!! BedroomWindowCam 카메라 없음"); Flush(sb); return; }
        var prevRT = cam.targetTexture;
        try
        {
            foreach (float h in new[] { 6f, 7.5f, 9f, 12f, 16f, 18.33f, 19f, 19.5f, 20f, 21f, 0f })
            {
                cyc.ResetCache(); cyc.EvaluateAt(h);
                var rt = RenderTexture.GetTemporary(384, 240, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt; cam.Render();
                var act = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(384, 240, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 384, 240), 0, 0); tex.Apply();
                RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
                double sum = 0; var px = tex.GetPixels32(); foreach (var p in px) sum += p.r + p.g + p.b;
                Object.DestroyImmediate(tex);
                var s = RenderSettings.ambientSkyColor;
                sb.AppendLine(h.ToString("00.00") + "h 창 카메라 평균 " + (sum / px.Length / 3).ToString("F1") + " | 환경광 sky 합 " + (s.r + s.g + s.b).ToString("F3") + " | 해 " + cyc.sunElNow.ToString("F1") + "°");
            }
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            cam.targetTexture = prevRT;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Flush(StringBuilder sb)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_window_dayprobe.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
