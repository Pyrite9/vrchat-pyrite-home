// PyriteBedroomLayoutProbe.cs — 침실 배치 실측 (Z51q). 읽기 전용 (씬 변경 없음)
//  2026-09-30 관리자: 침대 오른편에 트렁크 + 협탁, 폴라로이드 줄, 바닥 쿠션, 빈백 양털 담요 → 배치 전에 빈 공간 실측
//  로그 Logs/pyrite_bedroom_layout.txt, 렌더 Assets/_preview/bedroom/lay_top.jpg (위에서 정사영, 0.5 m 격자)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomLayoutProbe
{
    const string PREV = "Assets/_preview/bedroom/";

    [MenuItem("Tools/Pyrite3/Z51q. Bedroom Layout Probe", false, 5117)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z51q] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
            if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
            var rt = room.transform;
            sb.AppendLine("room 위치 " + V(rt.position) + " rot " + V(rt.eulerAngles) + " (아래 좌표는 모두 room 로컬, bounds = min~max)");
            Dump(rt, rt, 0, sb);

            // 침대 이름을 가진 것들 상세
            foreach (var t in rt.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Bed") || t.name.StartsWith("Lie") || t.name.Contains("Mattress") || t.name.Contains("Pillow")))
                sb.AppendLine("  [침대류] " + P(t, rt) + " active " + t.gameObject.activeInHierarchy + " pos " + V(rt.InverseTransformPoint(t.position)) + " fwd " + V(rt.InverseTransformDirection(t.forward)) + BoundsStr(t, rt));

            TopShot(rt, sb);
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/pyrite_bedroom_layout.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }

    static void Dump(Transform t, Transform rt, int depth, StringBuilder sb)
    {
        foreach (Transform c in t)
        {
            sb.AppendLine(new string(' ', depth * 2 + 2) + c.name + (c.gameObject.activeInHierarchy ? "" : " (꺼짐)") + " pos " + V(rt.InverseTransformPoint(c.position)) + BoundsStr(c, rt)
                          + " r" + c.GetComponentsInChildren<Renderer>(true).Length);
            if (depth < 1) Dump(c, rt, depth + 1, sb);
        }
    }

    static string BoundsStr(Transform t, Transform rt)
    {
        var rs = t.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return "";
        Vector3 mn = Vector3.one * 1e9f, mx = -Vector3.one * 1e9f;
        foreach (var r in rs)
        {
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var p = rt.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
            }
        }
        return " · bounds x " + mn.x.ToString("F2") + "~" + mx.x.ToString("F2") + " y " + mn.y.ToString("F2") + "~" + mx.y.ToString("F2") + " z " + mn.z.ToString("F2") + "~" + mx.z.ToString("F2");
    }

    // 위에서 정사영 + 0.5 m 격자 (x 오른쪽, z 위쪽). 지붕을 잠시 끈다
    static void TopShot(Transform rt, StringBuilder sb)
    {
        var hidden = rt.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.bounds.min.y - rt.position.y > 1.6f).ToArray();
        foreach (var r in hidden) r.enabled = false;
        var go = new GameObject("_TopCam"); go.hideFlags = HideFlags.DontSave;
        var cam = go.AddComponent<Camera>(); var main = Camera.main;
        cam.CopyFrom(main); cam.orthographic = true; cam.orthographicSize = 3.2f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        go.transform.position = rt.TransformPoint(new Vector3(0, 6f, 0));
        go.transform.rotation = rt.rotation * Quaternion.Euler(90f, 0f, 0f);
        int w = 1200, h = 1200;
        var rtex = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rtex; cam.Render(); cam.targetTexture = null;
        var a = RenderTexture.active; RenderTexture.active = rtex;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rtex);
        // 격자: 0.5 m 마다 회색, 1 m 마다 흰색, 원점 빨강
        float ppm = w / (cam.orthographicSize * 2f);
        for (int k = -6; k <= 6; k++)
        {
            float m = k * 0.5f; int px = Mathf.RoundToInt(w / 2f + m * ppm), py = Mathf.RoundToInt(h / 2f + m * ppm);
            var col = k == 0 ? Color.red : (k % 2 == 0 ? new Color(1, 1, 1, 1) : new Color(0.5f, 0.5f, 0.5f));
            for (int i = 0; i < w; i += (k % 2 == 0 ? 1 : 3))
            {
                if (px >= 0 && px < w) tex.SetPixel(px, i, col);
                if (py >= 0 && py < h) tex.SetPixel(i, py, col);
            }
        }
        tex.Apply();
        Directory.CreateDirectory(PREV);
        File.WriteAllBytes(PREV + "lay_top.jpg", tex.EncodeToJPG(88));
        Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        foreach (var r in hidden) r.enabled = true;
        sb.AppendLine("shot lay_top (정사영 6.4 m 폭, 오른쪽 +x, 위쪽 +z, 흰 선 1 m · 회색 0.5 m · 빨강 원점, 지붕 렌더러 " + hidden.Length + "개 숨김)");
    }

    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
    static string P(Transform t, Transform stop) { var s = t.name; while (t.parent && t.parent != stop) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
