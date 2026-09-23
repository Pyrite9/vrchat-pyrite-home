// Tools ▸ Pyrite ▸ Z10. Mirror Axis Probe (temporary objects, deleted after)
//  호수 실시간 반사 전 실측. VRCMirrorReflection 이 반사 평면 법선을 transform 의 어느 축으로 잡는지 확인한다.
//   A: Quad (90,0,0)   — 앞면이 위, transform.forward = 아래, transform.up = +Z
//   C: Plane (0,0,0)   — 앞면이 위, transform.forward = +Z, transform.up = 위
//  -forward 를 쓰면 A 만, up 을 쓰면 C 만 제대로 비친다. 에디터에서 거울이 안 그려지면 둘 다 흰색(_ReflectionTex 기본값).
//  → Assets/_preview/mirror_probe.png, Logs/pyrite_mirror_probe.txt. 씬은 저장하지 않는다(임시 오브젝트는 지운다).
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteMirrorProbe
{
    [MenuItem("Tools/Pyrite/Z10. Mirror Axis Probe", false, 17)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z10] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var sh = Shader.Find("FX/MirrorReflection");
        sb.AppendLine("shader FX/MirrorReflection " + (sh != null));
        var mat = new Material(sh);
        var root = new GameObject("__MirrorProbe");
        System.Type mt = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        { mt = asm.GetType("VRC.SDK3.Components.VRCMirrorReflection"); if (mt != null) break; }
        sb.AppendLine("type " + (mt != null ? mt.FullName : "null"));
        try
        {
            var a = GameObject.CreatePrimitive(PrimitiveType.Quad);
            a.name = "A_QuadUp"; a.transform.SetParent(root.transform);
            a.transform.SetPositionAndRotation(new Vector3(-7f, 0.05f, 22f), Quaternion.Euler(90f, 0f, 0f));
            a.transform.localScale = new Vector3(8f, 8f, 1f);
            var c = GameObject.CreatePrimitive(PrimitiveType.Plane);
            c.name = "C_Plane"; c.transform.SetParent(root.transform);
            c.transform.SetPositionAndRotation(new Vector3(7f, 0.05f, 22f), Quaternion.identity);
            c.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            foreach (var g in new[] { a, c })
            {
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                if (mt != null) g.AddComponent(mt);
                sb.AppendLine(string.Format("  {0} fwd {1} up {2}", g.name, g.transform.forward, g.transform.up));
            }

            var cam = Camera.main;
            var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            var eye = new Vector3(0f, 3.5f, 38f); var look = new Vector3(0f, 0f, 20f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            cam.fieldOfView = 60f;
            const int W = 1600, H = 900;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render(); cam.Render();                          // 거울은 첫 프레임에 텍스처를 만든다
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory("Assets/_preview/");
            File.WriteAllBytes("Assets/_preview/mirror_probe.png", tex.EncodeToPNG());
            foreach (var g in new[] { a, c })
            {
                var sp = cam.WorldToScreenPoint(g.transform.position);
                int cx = (int)sp.x, cy = (int)sp.y; double s = 0, s2 = 0; int n = 0;
                for (int y = cy - 25; y < cy + 25; y++) for (int x = cx - 60; x < cx + 60; x++)
                    { if (x < 0 || y < 0 || x >= W || y >= H) continue; var col = tex.GetPixel(x, y); float v = (col.r + col.g + col.b) / 3f * 255f; s += v; s2 += v * v; n++; }
                double m = s / n;
                sb.AppendLine(string.Format("  {0} screen ({1},{2}) mean {3:0.0} sd {4:0.0}  (흰색≈255/sd0 = 반사 텍스처 없음)", g.name, cx, cy, m, System.Math.Sqrt(s2 / n - m * m)));
            }
            cam.targetTexture = null; cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mat);
        }
        AssetDatabase.Refresh();
        File.WriteAllText("Logs/pyrite_mirror_probe.txt", sb.ToString());
    }
    // ── 인게임 확인용 배치 ──────────────────────────────────────────
    //  에디터에선 VRChat 거울이 안 그려진다(Z10 결과: 둘 다 흰색). 부두 옆 물 위에 두 장을 놓고 Build & Test 로 본다.
    //  물가에서 호수(−Z)를 볼 때 부두 왼쪽 = A(Quad 90,0,0), 오른쪽 = C(Plane 0,0,0). 머리 위에 글자.
    //  제대로 비치는 쪽(절벽·하늘이 거꾸로 보이는 쪽)이 호수에 쓸 축이다. Z10c 로 지운다.
    const string TEST = "__MirrorAxisTest";

    [MenuItem("Tools/Pyrite/Z10b. Place Mirror Axis Test (in-game)", false, 18)]
    public static void Place()
    {
        Remove();
        var sh = Shader.Find("FX/MirrorReflection");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_MirrorProbe.mat");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, "Assets/Materials/M_MirrorProbe.mat"); }
        System.Type mt = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        { mt = asm.GetType("VRC.SDK3.Components.VRCMirrorReflection"); if (mt != null) break; }
        var root = new GameObject(TEST);
        var a = GameObject.CreatePrimitive(PrimitiveType.Quad);
        a.name = "A_QuadUp"; a.transform.SetParent(root.transform);
        a.transform.SetPositionAndRotation(new Vector3(-3.5f, 0.06f, 36f), Quaternion.Euler(90f, 0f, 0f));
        a.transform.localScale = new Vector3(5f, 5f, 1f);
        var c = GameObject.CreatePrimitive(PrimitiveType.Plane);
        c.name = "C_Plane"; c.transform.SetParent(root.transform);
        c.transform.SetPositionAndRotation(new Vector3(-17.5f, 0.06f, 36f), Quaternion.identity);
        c.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        foreach (var g in new[] { a, c })
        {
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mt != null) g.AddComponent(mt);
            var lab = new GameObject("Label"); lab.transform.SetParent(root.transform);
            lab.transform.position = g.transform.position + new Vector3(0f, 2.2f, 0f);
            lab.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));   // 물가(+Z)에서 읽히게
            var tm = lab.AddComponent<TextMesh>();
            tm.text = g.name.Substring(0, 1); tm.characterSize = 0.35f; tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(1f, 0.85f, 0.3f);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        File.WriteAllText("Logs/pyrite_mirror_probe.txt", "[Z10b] placed " + (mt != null) + "\n");
    }

    [MenuItem("Tools/Pyrite/Z10c. Remove Mirror Axis Test", false, 19)]
    public static void Remove()
    {
        foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == TEST) Object.DestroyImmediate(g);
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_MirrorProbe.mat") != null)
            AssetDatabase.DeleteAsset("Assets/Materials/M_MirrorProbe.mat");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    }
}
#endif
