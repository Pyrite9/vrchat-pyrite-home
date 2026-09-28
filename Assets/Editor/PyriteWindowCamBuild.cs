// PyriteWindowCamBuild.cs — 침실 창밖 실시간 카메라 (Z49w 빌드 / Z49x 되돌리기). 재실행 안전
//  루트 BedroomWindowCam: 캠프 텐트 앞 (−4.9, 2.55, 54.6), yaw 210(호수 + 오른쪽 캠프), 수직 화각 100° · 가로 125°, RT 1536×960
//  v2 (19:02): 카메라가 머리를 따라간다(침실 머리 위치·방향 → 캠프). 창에 붙어 옆을 봐도 카메라가 같이 옆을 봐서 경계가 시야 밖으로
//  M_Backdrop: _Live(RT) 를 화면 안이면 우선, 밖·가장자리는 파노라마. 침실 +Z(창) ↔ 월드 yaw 210 (_Yaw 210)
//  U# PyriteWindowCam: 로컬 플레이어가 침실 12 m 안이면 카메라 켬 (Z49u 실측: 1024×512 1회 3.6~3.9 ms, 꽃 끄면 2.6 ms)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteWindowCamBuild
{
    const string DIR = "Assets/Bedroom";
    const string RT_PATH = DIR + "/RT_WindowCam.renderTexture";
    const string PREV = "Assets/_preview/bedroom/";
    const string ROOT = "BedroomWindowCam";
    // 19:17 관리자 "카메라 바로 앞 랜턴 스탠드가 거슬림" — 스탠드(camp07_lantern_stand, 앞 2.76 m·오른쪽 1.6 m)가 창 면(앞 2.55 m) 바로 뒤라 창에 크게 붙어 보였다
    //  → 뒤(yaw 30 방향)로 2.0 m: (−4.9, 54.6) → (−3.9, 56.33). 텐트 안쪽이 되지만 창 면 앞은 near 로 잘린다. 스탠드는 창 뒤 2.2 m
    static readonly Vector3 POS = new Vector3(-3.9f, 2.55f, 56.33f);
    const float YAW = 210f, VFOV = 100f, PLANE_Z = 2.55f;
    const int W = 1536, H = 960;
    static readonly Vector3 EYE_LOCAL = new Vector3(0f, 0.74f, 0f);   // 침실 이 점 ↔ 캠프 POS (머리 따라가기의 기준)
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z49w. Window Live Cam Build", false, 4921)]
    public static void Build()
    {
        sb = new StringBuilder("[Z49w] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49x. Window Live Cam Revert", false, 4922)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z49x] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var r = Root(ROOT); if (r) { Object.DestroyImmediate(r); sb.AppendLine(ROOT + " 삭제"); }
        var m = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_Backdrop.mat");
        if (m) { m.SetFloat("_LiveOn", 0f); m.SetFloat("_Yaw", 180f); m.SetTexture("_Live", null); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); sb.AppendLine("M_Backdrop _LiveOn 0, _Yaw 180"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (RT 에셋은 남김)");
        Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static bool EnsureProgram(string name)
    {
        string asset = "Assets/Udon/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset) != null) return true;
        var paType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Udon/" + name + ".cs");
        if (paType == null || ms == null) { sb.AppendLine("!! 프로그램 에셋 생성 실패 " + name + " (cs " + (ms != null) + ")"); return false; }
        var pa = ScriptableObject.CreateInstance(paType);
        AssetDatabase.CreateAsset(pa, asset);
        var so = new SerializedObject(pa); so.FindProperty("sourceCsScript").objectReferenceValue = ms; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Ctrl+R 후 Z49w 다시");
        return false;
    }

    static bool Inner()
    {
        if (!EnsureProgram("PyriteWindowCam")) return false;
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return false; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_Backdrop.mat"); if (mat == null) { sb.AppendLine("!! M_Backdrop 없음"); return false; }
        if (!mat.HasProperty("_LiveOn")) { sb.AppendLine("!! Pyrite/Backdrop 에 _LiveOn 없음 (셰이더 갱신 전?)"); return false; }

        // 관리자가 씬에서 옮긴 자리·방향이 있으면 그걸 유지 (U# 도 Start 에서 이 오브젝트 자리를 기준으로 삼는다)
        var old = Root(ROOT);
        Vector3 anchor = POS; float yawA = YAW;
        if (old) { anchor = old.transform.position; yawA = old.transform.eulerAngles.y; sb.AppendLine("기존 자리 유지 " + V(anchor) + " yaw " + yawA.ToString("F1")); Object.DestroyImmediate(old); }
        var go = new GameObject(ROOT);
        go.transform.SetPositionAndRotation(anchor, Quaternion.Euler(0f, yawA, 0f));

        if (AssetDatabase.LoadAssetAtPath<RenderTexture>(RT_PATH) != null) AssetDatabase.DeleteAsset(RT_PATH);
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "RT_WindowCam", antiAliasing = 1, useMipMap = false, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        AssetDatabase.CreateAsset(rt, RT_PATH);

        var main = Camera.main;
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);   // ⚠ CopyFrom 은 transform·layer 까지 메인 카메라로 덮는다 → 위치는 그 뒤에
        go.layer = 0;
        go.transform.SetPositionAndRotation(anchor, Quaternion.Euler(0f, yawA, 0f));
        cam.targetTexture = rt;
        cam.fieldOfView = VFOV; cam.nearClipPlane = 0.1f; cam.farClipPlane = main.farClipPlane;
        int drop = (1 << 5) | (1 << 10) | (1 << 12) | (1 << 18) | (1 << PyriteBedroomV3.LAYER) | (1 << 25);   // 25 = WindowCamHide (Z49y, 캠프 텐트)
        cam.cullingMask = main.cullingMask & ~drop;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.allowMSAA = false; cam.depth = -10; cam.useOcclusionCulling = true;
        cam.enabled = false;

        var wc = UdonSharpUndo.AddComponent<PyriteWindowCam>(go);
        wc.cam = cam; wc.room = room.transform; wc.mat = mat; wc.radius = 12f; wc.allowLive = true;
        wc.followHead = true; wc.camPos = anchor; wc.camYaw = yawA; wc.eyeLocal = EYE_LOCAL; wc.planeZ = PLANE_Z;
        UdonSharpEditorUtility.CopyProxyToUdon(wc); EditorUtility.SetDirty(wc);

        float tanV = Mathf.Tan(VFOV * 0.5f * Mathf.Deg2Rad), tanH = tanV * W / (float)H;
        var t = go.transform;
        mat.SetTexture("_Live", rt);
        mat.SetFloat("_LiveOn", 0f);
        mat.SetVector("_LiveFwd", t.forward); mat.SetVector("_LiveRight", t.right); mat.SetVector("_LiveUp", t.up);
        mat.SetVector("_LiveTan", new Vector4(tanH, tanV, 0, 0));
        mat.SetFloat("_Yaw", yawA);
        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();

        sb.AppendLine("카메라 " + V(POS) + " yaw " + YAW + ", 화각 수직 " + VFOV + "° · 가로 " + (2f * Mathf.Atan(tanH) * Mathf.Rad2Deg).ToString("F0") + "°, RT " + W + "×" + H + ", cullingMask " + cam.cullingMask + " (뺀 레이어 UI·PlayerLocal·UiMenu·MirrorReflection·Bedroom)");
        sb.AppendLine("M_Backdrop: _Yaw 180 → " + YAW + " (창 가운데 = 월드 yaw " + YAW + "), _LiveTan (" + tanH.ToString("F2") + ", " + tanV.ToString("F2") + ")");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    // 확인 렌더: 캠프에 사람 대역(캡슐 3) 세우고 창 카메라 1회 → 침실 시점. 파노라마만(끔) / 실시간(켬) 비교
    static void Renders()
    {
        var room = Root("TentBedroom"); var wcGo = Root(ROOT); if (room == null || wcGo == null) return;
        Directory.CreateDirectory(PREV);
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var wcam = wcGo.GetComponent<Camera>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_Backdrop.mat");
        var anchor0 = wcGo.transform.position; var rot0 = wcGo.transform.rotation;
        sAnchor = anchor0; sYaw = rot0.eulerAngles.y;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var stand = new System.Collections.Generic.List<GameObject>();
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            var skin = new Material(Shader.Find("Standard")); skin.color = new Color(0.85f, 0.85f, 0.9f);
            foreach (var p in new[] { new Vector3(-9.2f, 0, 52.4f), new Vector3(-11.8f, 0, 52.9f), new Vector3(-8.0f, 0, 50.6f) })
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Capsule); c.name = "_StandIn"; c.hideFlags = HideFlags.DontSave;
                float gy = Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 40f) ? hit.point.y : 1.81f;
                c.transform.position = new Vector3(p.x, gy + 0.85f, p.z); c.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
                Object.DestroyImmediate(c.GetComponent<Collider>()); c.GetComponent<Renderer>().sharedMaterial = skin; stand.Add(c);
            }
            var o = room.transform;
            System.Func<float, float, float, Vector3> Wp = (x, y, z) => o.TransformPoint(new Vector3(x, y, z));
            cam.fieldOfView = 75f;
            mat.SetFloat("_LiveOn", 1f);
            // 머리 따라가기 흉내 (U# PostLateUpdate 와 같은 식)
            var views = new[] {
                new { e = Wp(-0.28f, 0.42f, -2.05f), a = Wp(0, 1.2f, PyriteBedroomBuild.B), n = "lie" },
                new { e = Wp(0.3f, 1.6f, -1.2f), a = Wp(0, 1.2f, PyriteBedroomBuild.B), n = "stand" },
                new { e = Wp(1.6f, 1.3f, 2.05f), a = Wp(-2.0f, 1.0f, 2.6f), n = "near_left" },
                new { e = Wp(-1.4f, 1.3f, 2.05f), a = Wp(2.5f, 1.2f, 2.6f), n = "near_right" },
                new { e = Wp(0f, 1.2f, 2.1f), a = Wp(0f, 2.8f, 2.4f), n = "near_up" },
            };
            foreach (var v in views)
            {
                var rot = Quaternion.LookRotation(v.a - v.e);
                Aim(wcam, o, v.e, rot, mat);
                wcam.Render();
                Shot(cam, v.e, v.a, "wl_follow_" + v.n);
                sb.AppendLine("    cam " + V(wcam.transform.position) + " near " + wcam.nearClipPlane.ToString("F2"));
            }
            // 카메라 원본 화면
            var a = RenderTexture.active; var rt = wcam.targetTexture; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = a; File.WriteAllBytes(PREV + "wl_rt.jpg", tex.EncodeToJPG(85)); Object.DestroyImmediate(tex);
            sb.AppendLine("  wl_rt (카메라 원본)");
            Object.DestroyImmediate(skin);
        }
        finally
        {
            foreach (var g in stand) if (g) Object.DestroyImmediate(g);
            mat.SetFloat("_LiveOn", 0f);
            wcam.transform.SetPositionAndRotation(anchor0, rot0); wcam.nearClipPlane = 0.1f;
            var tt = wcam.transform; mat.SetVector("_LiveFwd", tt.forward); mat.SetVector("_LiveRight", tt.right); mat.SetVector("_LiveUp", tt.up);
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static Vector3 sAnchor; static float sYaw;
    static void Aim(Camera wcam, Transform room, Vector3 eye, Quaternion rot, Material mat)
    {
        Vector3 local = room.InverseTransformPoint(eye);
        Quaternion lrot = Quaternion.Inverse(room.rotation) * rot;
        Quaternion yaw = Quaternion.Euler(0f, sYaw, 0f);
        wcam.transform.SetPositionAndRotation(sAnchor + yaw * (local - EYE_LOCAL), yaw * lrot);
        Vector3 fwdLocal = lrot * Vector3.forward; float d = PLANE_Z - local.z; float near = 0.05f;
        if (d > 0f && fwdLocal.z > 0.2f) near = Mathf.Max(0.05f, (d * fwdLocal.z - 0.35f) * 0.9f);
        wcam.nearClipPlane = near;
        var t = wcam.transform;
        mat.SetVector("_LiveFwd", t.forward); mat.SetVector("_LiveRight", t.right); mat.SetVector("_LiveUp", t.up);
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double s = 0; foreach (var p in px) s += (p.r + p.g + p.b) / 3.0;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(82));
        sb.AppendLine("  shot " + tag + " mean " + (s / px.Length).ToString("F1"));
        Object.DestroyImmediate(tex);
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_windowcam.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }
}
#endif
