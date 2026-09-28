// PyriteWindowCamProbe.cs — 침실 창밖 실시간 카메라 실측 (Z49u). 씬은 안 바꿈
//  후보 자리(텐트 앞)에서 방향 3개 × 1024×512 렌더 + 시간(ReadPixels 로 GPU 대기 포함), 꽃 끔·원거리 200 m 변형
#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteWindowCamProbe
{
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;
    static readonly Vector3 POS = new Vector3(-4.9f, 2.55f, 54.6f);

    [MenuItem("Tools/Pyrite3/Z49u. Window Cam Probe", false, 4920)]
    public static void Run()
    {
        sb = new StringBuilder("[Z49u] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        File.WriteAllText("Logs/pyrite_windowcam.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }

    static void Inner()
    {
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
        var tent = GameObject.Find("Camp/camp01_tent_BRN");
        if (tent) { var b = tent.GetComponent<Renderer>().bounds; sb.AppendLine("tent bounds " + b.min.ToString("F2") + ".." + b.max.ToString("F2")); }
        var fire = GameObject.Find("Camp/Fire_Light") ?? GameObject.FindObjectsOfType<Light>().Where(l => l.name == "Fire_Light").Select(l => l.gameObject).FirstOrDefault();
        if (fire) { var d = fire.transform.position - POS; sb.AppendLine("fire " + fire.transform.position.ToString("F2") + " from cam: dist " + new Vector2(d.x, d.z).magnitude.ToString("F2") + " yaw " + (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg).ToString("F0")); }
        // 발밑 지면 높이
        if (Physics.Raycast(POS + Vector3.up * 3f, Vector3.down, out var hit, 10f)) sb.AppendLine("ground under cam y " + hit.point.y.ToString("F2") + " (" + hit.collider.name + ") → eye +" + (POS.y - hit.point.y).ToString("F2"));

        var go = new GameObject("_WinCamProbe"); go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>();
        var main = Camera.main;
        cam.CopyFrom(main);
        cam.fieldOfView = 70f; cam.aspect = 2f; cam.nearClipPlane = 0.1f;
        var rt = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var flowers = GameObject.Find("FlowerField");
        var fr = flowers ? flowers.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray() : new Renderer[0];
        sb.AppendLine("main cam far " + main.farClipPlane + " mask " + main.cullingMask + " hdr " + main.allowHDR + " msaa " + main.allowMSAA + "  flower renderers " + fr.Length);
        try
        {
            foreach (var yaw in new[] { 180f, 210f, 240f })
                Shot(cam, rt, yaw, "wc_yaw" + yaw.ToString("0"));
            cam.farClipPlane = 200f; Shot(cam, rt, 210f, "wc_far200"); cam.farClipPlane = main.farClipPlane;
            foreach (var r in fr) r.enabled = false;
            Shot(cam, rt, 210f, "wc_noflower");
            foreach (var r in fr) r.enabled = true;
            // 기준: 메인 카메라 같은 해상도 캠프 눈높이
            Shot(cam, rt, 210f, "wc_yaw210_again");
        }
        finally
        {
            foreach (var r in fr) r.enabled = true;
            foreach (var r in psr) r.enabled = true;
            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, RenderTexture rt, float yaw, string tag)
    {
        cam.transform.SetPositionAndRotation(POS, Quaternion.Euler(4f, yaw, 0));
        var a = RenderTexture.active;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        // 데우기 1번 → 측정 6번 (ReadPixels 로 GPU 완료까지 기다림)
        cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        var sw = Stopwatch.StartNew(); int n = 6;
        for (int i = 0; i < n; i++) { cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); }
        sw.Stop();
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        RenderTexture.active = a;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        var px = tex.GetPixels32(); double s = 0; foreach (var p in px) s += (p.r + p.g + p.b) / 3.0;
        sb.AppendLine("  " + tag + " yaw " + yaw + " : " + (sw.Elapsed.TotalMilliseconds / n).ToString("F2") + " ms/render, mean " + (s / px.Length).ToString("F1"));
        Object.DestroyImmediate(tex);
    }
}
#endif
