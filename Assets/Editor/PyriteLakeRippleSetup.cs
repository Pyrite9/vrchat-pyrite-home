// Tools ▸ Pyrite ▸ Z16. Lake Ripples (player wakes)  /  Z16b. Revert  /  Z16c. Ripple Preview (synthetic)
//  레퍼런스: 움직이면 물에 파문이 생긴다. (선행: Z15 잔잔한 수면, T7 화이트리스트 확인)
//   RT_LakeRipple   CustomRenderTexture 1024² RGHalf, 이중 버퍼, OnDemand(Udon 이 60 Hz 로 Update)
//   M_LakeRippleSim Pyrite/LakeRippleSim, _DepthTex = 물 머티리얼의 수심 텍스처(땅에서 0)
//   LakeRipple      루트 오브젝트 + PyriteLakeRipple(U#) — 플레이어 물결 발생, 전역 _UdonLakeRipple 설정
//   물 3종 + M_WaterMirror _PlayerRipple 4
//  에디터에선 Udon 이 안 돈다 → Z16c 는 합성 파문(걸어간 자취)을 전역에 넣고 렌더만 한다(거울 오버레이는 잠시 끔).
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteLakeRippleSetup
{
    const string CRT = "Assets/Textures/RT_LakeRipple.asset";
    const string SIM = "Assets/Materials/M_LakeRippleSim.mat";
    const string ROOT = "LakeRipple";
    const string LOG = "Logs/pyrite_ripple.txt";
    static readonly string[] WATER = { "Assets/Materials/M_Water_Dusk.mat", "Assets/Materials/M_Water_Night.mat", "Assets/Materials/M_Water_Dawn.mat", "Assets/Materials/M_WaterMirror.mat" };

    [MenuItem("Tools/Pyrite/Z16. Lake Ripples (player wakes)", false, 32)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z16] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var sh = Shader.Find("Pyrite/LakeRippleSim");
        if (sh == null) { sb.AppendLine("Pyrite/LakeRippleSim 없음"); Flush(sb); return; }
        var wmat = AssetDatabase.LoadAssetAtPath<Material>(WATER[0]);

        var sim = AssetDatabase.LoadAssetAtPath<Material>(SIM);
        if (sim == null) { sim = new Material(sh); AssetDatabase.CreateAsset(sim, SIM); }
        sim.shader = sh;
        if (wmat != null && wmat.HasProperty("_DepthTex")) sim.SetTexture("_DepthTex", wmat.GetTexture("_DepthTex"));
        EditorUtility.SetDirty(sim);

        var crt = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(CRT);
        if (crt == null)
        {
            crt = new CustomRenderTexture(1024, 1024, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
            AssetDatabase.CreateAsset(crt, CRT);
        }
        crt.material = sim;
        crt.initializationMode = CustomRenderTextureUpdateMode.OnLoad;
        crt.initializationColor = Color.clear;
        crt.updateMode = CustomRenderTextureUpdateMode.OnDemand;
        crt.doubleBuffered = true;
        crt.wrapMode = TextureWrapMode.Clamp;
        crt.filterMode = FilterMode.Bilinear;
        EditorUtility.SetDirty(crt);
        sb.AppendLine(string.Format("CRT {0} {1}x{2} {3} double {4} update {5}", CRT, crt.width, crt.height, crt.format, crt.doubleBuffered, crt.updateMode));

        foreach (var p in WATER)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null || !m.HasProperty("_PlayerRipple")) { sb.AppendLine("  _PlayerRipple 없음: " + p); continue; }
            m.SetFloat("_PlayerRipple", 4f); EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();

        foreach (var g in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == ROOT).ToArray()) Undo.DestroyObjectImmediate(g);
        var go = new GameObject(ROOT);
        Undo.RegisterCreatedObjectUndo(go, "lake ripple");
        var rip = UdonSharpUndo.AddComponent<PyriteLakeRipple>(go);
        rip.crt = crt; rip.sim = sim;
        UdonSharpEditorUtility.CopyProxyToUdon(rip);
        EditorUtility.SetDirty(rip);
        sb.AppendLine("LakeRipple + PyriteLakeRipple (stepHz 60, maxSteps 4, dropAmp 0.004, radius 0.35, band ±0.6)");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite/Z16b. Lake Ripples Revert", false, 33)]
    public static void Revert()
    {
        foreach (var g in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == ROOT).ToArray()) Undo.DestroyObjectImmediate(g);
        foreach (var p in WATER) { var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m != null && m.HasProperty("_PlayerRipple")) { m.SetFloat("_PlayerRipple", 0f); EditorUtility.SetDirty(m); } }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Flush(new StringBuilder("[Z16b] reverted (_PlayerRipple 0, LakeRipple 제거)\n"));
    }

    // ── 합성 파문 미리보기 ─────────────────────────────────────────
    //  부두 끝 옆 물 위를 (−8, 28) → (−1, 23) 으로 1.4 m/s 로 걸어간 자취: 0.35 m 마다 떨어진 물결이
    //  나이 × 1.7 m/s 반경의 고리로 퍼지고 1.2 s 로 줄어든다. 높이 단위는 시뮬레이션과 같은 "m" 가정(최대 ~2 cm).
    [MenuItem("Tools/Pyrite/Z16c. Ripple Preview (synthetic)", false, 34)]
    public static void Preview()
    {
        var sb = new StringBuilder("[Z16c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        const int N = 1024;
        var tex = new Texture2D(N, N, TextureFormat.RFloat, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var h = new float[N * N];
        Vector2 a = new Vector2(-8f, 28f), b = new Vector2(-1f, 23f);
        float L = Vector2.Distance(a, b), speed = 1.4f;
        for (float s = 0; s <= L; s += 0.35f)
        {
            var c = Vector2.Lerp(a, b, s / L);
            float age = (L - s) / speed;
            float R = age * 1.7f, A = 0.02f * Mathf.Exp(-age / 1.2f);
            float reach = R + 1.5f;
            int x0 = Mathf.Max(0, (int)((c.x - reach + 64f) / 128f * N)), x1 = Mathf.Min(N - 1, (int)((c.x + reach + 64f) / 128f * N));
            int y0 = Mathf.Max(0, (int)((c.y - reach + 78f) / 128f * N)), y1 = Mathf.Min(N - 1, (int)((c.y + reach + 78f) / 128f * N));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float wx = -64f + (x + 0.5f) / N * 128f, wz = -78f + (y + 0.5f) / N * 128f;
                    float r = Mathf.Sqrt((wx - c.x) * (wx - c.x) + (wz - c.y) * (wz - c.y));
                    float q = (r - R) / 0.45f;
                    h[y * N + x] += A * Mathf.Cos((r - R) * 2f * Mathf.PI / 0.55f) * Mathf.Exp(-q * q);
                }
        }
        tex.SetPixelData(h, 0); tex.Apply(false);
        var black = new Texture2D(1, 1, TextureFormat.RFloat, false, true); black.SetPixel(0, 0, Color.clear); black.Apply();

        var mirror = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "LakeMirror");
        bool mOn = mirror != null && mirror.activeSelf;
        if (mirror != null) mirror.SetActive(false);                        // 에디터에선 VRChat 거울이 안 그려진다
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null && tod != null) f.SetValue(tod, false);
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        Directory.CreateDirectory("Assets/_preview/ripple/");
        try
        {
            foreach (int i in new[] { 0, 1 })
            {
                if (tod != null) { tod.index = i; tod.Apply(); }
                foreach (var on in new[] { false, true })
                {
                    Shader.SetGlobalTexture("_UdonLakeRipple", on ? (Texture)tex : black);
                    string path = string.Format("Assets/_preview/ripple/ripple_{0}_{1}.png", on ? "on" : "off", i);
                    Shot(cam, new Vector3(-4.5f, 2.2f, 37.5f), new Vector3(-4.5f, 0f, 24f), 60f, path);
                    sb.AppendLine("  " + path);
                }
            }
        }
        finally
        {
            Shader.SetGlobalTexture("_UdonLakeRipple", black);
            if (mirror != null) mirror.SetActive(mOn);
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (tod != null) { tod.index = 0; tod.Apply(); }
        }
        float mx = h.Max(), mn = h.Min();
        sb.AppendLine(string.Format("  합성 높이 {0:0.000} .. {1:0.000} m", mn, mx));
        AssetDatabase.Refresh();
        Flush(sb);
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(W, H, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, t.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(t); rt.Release(); Object.DestroyImmediate(rt);
    }

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.WriteAllText(LOG, sb.ToString()); }
}
#endif
