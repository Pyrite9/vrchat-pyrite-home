// PyriteBedroomDayProbe.cs — 낮에 침실이 밝은 원인 실측 (Z50d, 읽기 전용). 시각 × 조건별 렌더 평균 밝기
//  조건: A 그대로 / B 수면 100%(광원 ×0.05, _Dim 0.4) / C B + 환경광(Trilight)을 21시 값으로 / D C + 반사 세기 21시 값 / E D + 안개 21시
//  결과 Logs/pyrite_bedroom_dayprobe.txt, 렌더 Assets/_preview/bedroom/dp_*.jpg. 끝나면 전부 원래대로
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomDayProbe
{
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z50d. Bedroom Day Brightness Probe", false, 5004)]
    public static void Run()
    {
        sb = new StringBuilder("[Z50d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        var lights = room.transform.Find("Lights").GetComponentsInChildren<Light>(true);
        var baseI = lights.Select(l => l.intensity).ToArray();
        var bd = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat");
        float dim0 = bd.GetFloat("_Dim");
        try
        {
            Directory.CreateDirectory(PREV);
            cam.fieldOfView = 70f;
            cyc.ResetCache(); cyc.EvaluateAt(21f);
            var nSky = RenderSettings.ambientSkyColor; var nEq = RenderSettings.ambientEquatorColor; var nGr = RenderSettings.ambientGroundColor;
            float nRefl = RenderSettings.reflectionIntensity; bool nFog = RenderSettings.fog; var nFogC = RenderSettings.fogColor; float nFogD = RenderSettings.fogDensity;
            sb.AppendLine("21h 환경광 sky " + nSky + " eq " + nEq + " gr " + nGr + " 반사 " + nRefl.ToString("F2") + " 안개 " + nFog + " " + nFogD.ToString("F4"));
            foreach (float h in new[] { 9f, 12f, 16f, 21f })
            {
                cyc.ResetCache(); cyc.EvaluateAt(h);
                sb.AppendLine(h.ToString("00") + "h 환경광 sky " + RenderSettings.ambientSkyColor + " eq " + RenderSettings.ambientEquatorColor + " gr " + RenderSettings.ambientGroundColor + " ×" + RenderSettings.ambientIntensity.ToString("F2") + " 반사 " + RenderSettings.reflectionIntensity.ToString("F2") + " 안개 " + RenderSettings.fog + " " + RenderSettings.fogDensity.ToString("F4"));
                for (int c = 0; c < 5; c++)
                {
                    cyc.ResetCache(); cyc.EvaluateAt(h);
                    for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i] * (c >= 1 ? 0.05f : 1f);
                    bd.SetFloat("_Dim", c >= 1 ? 0.4f : 1f);
                    if (c >= 2) { RenderSettings.ambientSkyColor = nSky; RenderSettings.ambientEquatorColor = nEq; RenderSettings.ambientGroundColor = nGr; }
                    if (c >= 3) RenderSettings.reflectionIntensity = nRefl;
                    if (c >= 4) { RenderSettings.fog = nFog; RenderSettings.fogColor = nFogC; RenderSettings.fogDensity = nFogD; }
                    string tag = "dp_" + h.ToString("00") + "_" + "ABCDE"[c];
                    var a = Shot(cam, room.transform.TransformPoint(new Vector3(1.6f, 1.5f, 1.9f)), room.transform.TransformPoint(new Vector3(-0.6f, 0.7f, -1.6f)), tag, c == 0 || c == 4);
                    var b = Shot(cam, room.transform.TransformPoint(new Vector3(-0.28f, 0.45f, -1.9f)), room.transform.TransformPoint(new Vector3(0.3f, 1.4f, 0.8f)), tag + "_lie", false);
                    sb.AppendLine("  " + "ABCDE"[c] + " 방 " + a + " | 누움 " + b);
                }
            }
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i];
            bd.SetFloat("_Dim", dim0);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        sb.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_dayprobe.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }

    // 평균 (R,G,B) 과 밝기
    static string Shot(Camera cam, Vector3 eye, Vector3 at, string tag, bool save)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 480, h = 270;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var act = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double r = 0, g = 0, b = 0;
        foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
        r /= px.Length; g /= px.Length; b /= px.Length;
        if (save) File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        Object.DestroyImmediate(tex);
        return ((r + g + b) / 3).ToString("F1") + " (" + r.ToString("F0") + "/" + g.ToString("F0") + "/" + b.ToString("F0") + ")";
    }
}
#endif
