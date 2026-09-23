// Tools ▸ Pyrite2 ▸ Z27b. Build Tarp Mirror  /  Z27c. Tarp Mirror Revert
//  타프 왼쪽 앞 줄(왼쪽 기둥 꼭대기 (-11.71, 4.19, 57.60) → 말뚝 (-14.30, 1.70, 55.45))에 둥근 손거울을 매단다
//   손거울을 누르면 가로 거울 2.4 × 1.35 m 가 옆에 켜진다 (로컬, PyriteMirrorToggle — 거울은 보는 사람만 렌더)
//   가로 거울 = 예전 캠프 거울 MirrorSurface 복제(같은 반사 레이어·해상도) + 나무 테, 캠프 쪽을 본다
//   설정 패널 반사 칸의 '거울' 토글도 이 거울을 켠다 (Z25a 가 연결)
//  렌더 Assets/_preview/tarp/mirror_*.png
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
using VRC.SDK3.Components;

public static class PyriteTarpMirror
{
    const string ROOT = "TarpMirror";
    const string DIR = "Assets/Materials/Projector/";
    static readonly Vector3 ROPE_TOP = new Vector3(-11.71f, 4.19f, 57.60f);
    static readonly Vector3 ROPE_PEG = new Vector3(-14.30f, 1.70f, 55.45f);
    static readonly Vector3 CAMP = new Vector3(-10.5f, 0f, 53.5f);
    static readonly Vector3 BIG_AT = new Vector3(-15.2f, 0f, 55.0f);       // 가로 거울 중심 (예전 거울 자리 근처, 타프 밖)
    const float BIG_W = 2.4f, BIG_H = 1.35f, BIG_Y = 1.45f;                 // 지면 위 중심 높이
    const float HANG_Y = 1.50f;                                             // 손거울 걸린 줄 높이 = 지면 + 1.50

    [MenuItem("Tools/Pyrite2/Z27b. Build Tarp Mirror", false, 11)]
    public static void Build()
    {
        var sb = new StringBuilder("[Z27b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = Find(ROOT); if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("기존 " + ROOT + " 삭제"); }
        var src = Object.FindObjectsOfType<VRCMirrorReflection>(true).FirstOrDefault(m => m.name == "MirrorSurface");
        if (src == null) { sb.AppendLine("MirrorSurface 없음"); Flush(sb); return; }
        var terr = Terrain.activeTerrain;
        float G(Vector3 p) => terr ? terr.SampleHeight(p) + terr.transform.position.y : 1.81f;

        // 줄 위 걸 점: y = 지면 + HANG_Y 인 곳
        float gy = G((ROPE_TOP + ROPE_PEG) * 0.5f);
        float t = Mathf.Clamp01((ROPE_TOP.y - (gy + HANG_Y)) / (ROPE_TOP.y - ROPE_PEG.y));
        var hang = Vector3.Lerp(ROPE_TOP, ROPE_PEG, t);
        var toCamp = CAMP - hang; toCamp.y = 0; toCamp.Normalize();

        // 머티리얼
        var std = Shader.Find("Standard");
        var mWood = Mat("M_TarpMirror_Wood", std, m => { m.color = new Color(0.42f, 0.28f, 0.16f); m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.35f); });
        var mString = Mat("M_TarpMirror_String", std, m => { m.color = new Color(0.78f, 0.74f, 0.66f); m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.1f); });
        var mGlassOff = Mat("M_TarpMirror_Glass", std, m => { m.color = new Color(0.75f, 0.78f, 0.82f); m.SetFloat("_Metallic", 1f); m.SetFloat("_Glossiness", 0.95f); m.DisableKeyword("_EMISSION"); });
        var mGlassOn = Mat("M_TarpMirror_GlassOn", std, m =>
        {
            m.color = new Color(0.9f, 0.85f, 0.7f); m.SetFloat("_Metallic", 1f); m.SetFloat("_Glossiness", 0.95f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.5f) * 1.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        });

        // 손거울 (루트 = 걸 점, 캠프 쪽을 본다)
        var root = new GameObject(ROOT);
        Undo.RegisterCreatedObjectUndo(root, "tarp mirror");
        root.transform.SetPositionAndRotation(hang, Quaternion.LookRotation(-toCamp, Vector3.up));   // forward = 캠프 반대 (VRChat 거울 규약과 같게)
        const float strLen = 0.14f, rimD = 0.24f;
        Prim(PrimitiveType.Cylinder, "String", root.transform, new Vector3(0f, -strLen * 0.5f, 0f), Quaternion.identity, new Vector3(0.006f, strLen * 0.5f, 0.006f), mString);
        Prim(PrimitiveType.Sphere, "Knot", root.transform, Vector3.zero, Quaternion.identity, Vector3.one * 0.018f, mString);
        var cy = -strLen - rimD * 0.5f;
        var xr = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Cylinder, "Rim", root.transform, new Vector3(0f, cy, 0f), xr, new Vector3(rimD, 0.012f, rimD), mWood);
        Prim(PrimitiveType.Cylinder, "Loop", root.transform, new Vector3(0f, -strLen + 0.004f, 0f), Quaternion.identity, new Vector3(0.03f, 0.006f, 0.03f), mWood);
        var glass = Prim(PrimitiveType.Cylinder, "Glass", root.transform, new Vector3(0f, cy, -0.013f), xr, new Vector3(rimD * 0.82f, 0.002f, rimD * 0.82f), mGlassOff);
        var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0f, cy + 0.03f, 0f); col.size = new Vector3(0.34f, 0.44f, 0.12f);

        // 가로 거울 (컨테이너: 균일 크기, 표면 + 나무 테)
        var bigPos = BIG_AT; bigPos.y = G(BIG_AT) + BIG_Y;
        var bigToCamp = CAMP - bigPos; bigToCamp.y = 0; bigToCamp.Normalize();
        var big = new GameObject("BigMirror");
        big.transform.SetParent(root.transform, false);
        big.transform.SetPositionAndRotation(bigPos, Quaternion.LookRotation(-bigToCamp, Vector3.up));
        var surf = Object.Instantiate(src.gameObject, big.transform);
        surf.name = "Surface"; surf.SetActive(true);
        foreach (var c in surf.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        surf.transform.localPosition = Vector3.zero; surf.transform.localRotation = Quaternion.identity; surf.transform.localScale = new Vector3(BIG_W, BIG_H, 1f);
        const float fw = 0.05f, fd = 0.04f;
        Prim(PrimitiveType.Cube, "FrameTop", big.transform, new Vector3(0f, BIG_H * 0.5f + fw * 0.5f, fd * 0.5f), Quaternion.identity, new Vector3(BIG_W + fw * 2f, fw, fd), mWood);
        Prim(PrimitiveType.Cube, "FrameBottom", big.transform, new Vector3(0f, -BIG_H * 0.5f - fw * 0.5f, fd * 0.5f), Quaternion.identity, new Vector3(BIG_W + fw * 2f, fw, fd), mWood);
        Prim(PrimitiveType.Cube, "FrameL", big.transform, new Vector3(-BIG_W * 0.5f - fw * 0.5f, 0f, fd * 0.5f), Quaternion.identity, new Vector3(fw, BIG_H, fd), mWood);
        Prim(PrimitiveType.Cube, "FrameR", big.transform, new Vector3(BIG_W * 0.5f + fw * 0.5f, 0f, fd * 0.5f), Quaternion.identity, new Vector3(fw, BIG_H, fd), mWood);
        Prim(PrimitiveType.Cube, "Back", big.transform, new Vector3(0f, 0f, fd + 0.005f), Quaternion.identity, new Vector3(BIG_W + fw * 2f, BIG_H + fw * 2f, 0.01f), mWood);
        // 다리 두 개 (거울 아래 → 지면)
        float legLen = BIG_Y - BIG_H * 0.5f - fw;
        foreach (var sx in new[] { -1f, 1f })
            Prim(PrimitiveType.Cylinder, "Leg", big.transform, new Vector3(sx * (BIG_W * 0.5f - 0.2f), -BIG_H * 0.5f - fw - legLen * 0.5f, fd * 0.5f), Quaternion.identity, new Vector3(0.05f, legLen * 0.5f, 0.05f), mWood);
        big.SetActive(false);

        foreach (var tr in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(tr.gameObject, 0);
        foreach (var rr in root.GetComponentsInChildren<Renderer>(true)) { rr.shadowCastingMode = ShadowCastingMode.On; rr.lightProbeUsage = LightProbeUsage.BlendProbes; }

        // U# (로컬 토글)
        PyriteMirrorToggle tg;
        try { tg = UdonSharpUndo.AddComponent<PyriteMirrorToggle>(root); }
        catch (System.Exception e) { sb.AppendLine("AddComponent 실패: " + e.Message); Flush(sb); return; }
        tg.mirror = big; tg.lamp = glass.GetComponent<Renderer>(); tg.lampOff = mGlassOff; tg.lampOn = mGlassOn; tg.startOn = false;
        UdonSharpEditorUtility.CopyProxyToUdon(tg); EditorUtility.SetDirty(tg);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tg); if (ub != null) { ub.interactText = "Mirror"; EditorUtility.SetDirty(ub); }

        sb.AppendLine(string.Format("hang {0} (rope t {1:0.00}, ground {2:0.00}) | big mirror {3} {4}x{5} m, top {6:0.00} above ground | camp dist {7:0.0} m",
            hang.ToString("F2"), t, gy, bigPos.ToString("F2"), BIG_W, BIG_H, BIG_Y + BIG_H * 0.5f, Vector3.Distance(new Vector3(bigPos.x, 0, bigPos.z), CAMP)));

        Shots(root, big, sb);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite2/Z27c. Tarp Mirror Revert", false, 12)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z27c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = Find(ROOT); if (old != null) { Object.DestroyImmediate(old); sb.AppendLine(ROOT + " 삭제"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Shots(GameObject root, GameObject big, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var hang = root.transform.position;
        var eyeCamp = new Vector3(-10.8f, 3.45f, 52.9f);
        var eyeNear = hang - root.transform.forward * 0.9f + Vector3.up * 0.05f + root.transform.right * 0.25f;
        Directory.CreateDirectory("Assets/_preview/tarp/");
        big.SetActive(true);
        try
        {
            if (cyc != null) cyc.ResetCache();
            foreach (var h in new[] { 21f, 12f })
            {
                if (cyc != null) cyc.EvaluateAt(h);
                Shot(cam, eyeCamp, (hang + big.transform.position) * 0.5f, 65f, string.Format("Assets/_preview/tarp/mirror_{0:00}_camp.png", (int)h));
                Shot(cam, eyeNear, hang - Vector3.up * 0.26f, 45f, string.Format("Assets/_preview/tarp/mirror_{0:00}_near.png", (int)h));
            }
            sb.AppendLine("  shots mirror_{21,12}_{camp,near}");
        }
        finally
        {
            big.SetActive(false);
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (cyc != null) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1280, H = 720;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 1000f;
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

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 lp, Quaternion lr, Vector3 ls, Material mat)
    {
        var g = GameObject.CreatePrimitive(t); g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localRotation = lr; g.transform.localScale = ls;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    static Material Mat(string name, Shader sh, System.Action<Material> set)
    {
        var path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh; set(m); EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Find(string n) => GameObject.Find(n) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == n && x.scene.IsValid());
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_tarp.txt", sb.ToString()); }
}
#endif
