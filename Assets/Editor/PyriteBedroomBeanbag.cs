// PyriteBedroomBeanbag.cs — 침실 빈백 2개 (Z51l 빌드 / Z51m 되돌리기). 재실행 안전
//  2026-09-29 관리자: 3번 = 러그 위 빈백 2개, TV(−X 벽, 화면 중심 (−2.67, 1.20, −0.90)) 를 보게, 앉기 = 캠프 의자 Station 복사(없으면 기본 앉기) + PyriteCarrySeat
//  루트 TentBedroom/Beanbags. 메시 = 코드로 만든 눌린 구(등받이 솟음 · 앞쪽 좌석 파임 · 바닥 퍼짐), 24×16, 머티리얼 2색
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteBedroomBeanbag
{
    const string DIR = "Assets/Bedroom/Beanbag/";
    const string PREV = "Assets/_preview/bedroom/";
    const string ROOT = "Beanbags";
    static readonly Vector3 TV = new Vector3(-2.67f, 1.20f, -0.90f);
    static readonly Vector3[] POS = { new Vector3(-1.05f, 0f, 0.95f), new Vector3(-0.05f, 0f, 1.30f) };
    static readonly Color[] COL = { new Color(0.52f, 0.38f, 0.17f), new Color(0.13f, 0.29f, 0.31f) };   // 머스터드 · 딥 틸
    const float R = 0.42f, H = 0.62f, SEAT_Y = 0.30f;
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z51l. Bedroom Beanbag Build", false, 5112)]
    public static void Build()
    {
        sb = new StringBuilder("[Z51l] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51m. Bedroom Beanbag Revert", false, 5113)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z51m] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom"); var t = room ? room.transform.Find(ROOT) : null;
        if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(ROOT + " 삭제"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

    static bool Inner()
    {
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return false; }
        Directory.CreateDirectory(DIR);
        var old = room.transform.Find(ROOT); if (old) Object.DestroyImmediate(old.gameObject);

        // 앉기 원본: 침실 밖 Station 중 이름에 Chair/Seat/Bench, 없으면 기본(애니메이터 null = VRChat 기본 앉기)
        var all = Object.FindObjectsOfType<VRC.SDK3.Components.VRCStation>(true).Where(s => !s.transform.IsChildOf(room.transform)).ToArray();
        sb.AppendLine("씬 Station " + all.Length + ": " + string.Join(", ", all.Take(12).Select(s => Path(s.transform) + "[" + (s.animatorController ? s.animatorController.name : "기본") + "]")));
        var src = all.FirstOrDefault(s => s.transform.parent && s.transform.parent.name.StartsWith("CarryChair"));   // 캠프 의자 앉기 우선
        if (src == null) src = all.FirstOrDefault(s => System.Text.RegularExpressions.Regex.IsMatch(s.name + " " + (s.transform.parent ? s.transform.parent.name : ""), "Chair|Seat|Bench", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        sb.AppendLine("앉기 원본: " + (src ? Path(src.transform) + " (" + (src.animatorController ? src.animatorController.name : "기본 앉기") + ")" : "없음 → 기본 앉기"));

        var root = new GameObject(ROOT).transform; root.SetParent(room.transform, false);
        var mesh = SaveMesh(BagMesh(), "Beanbag");
        int tris = mesh.triangles.Length / 3;
        for (int i = 0; i < POS.Length; i++)
        {
            var d = TV - POS[i]; d.y = 0f;
            var bag = new GameObject("Beanbag_" + (i + 1)).transform; bag.SetParent(root, false);
            bag.localPosition = POS[i]; bag.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var body = new GameObject("Body"); body.transform.SetParent(bag, false); body.layer = PyriteBedroomV3.LAYER;
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mat("M_Beanbag_" + (i + 1), COL[i]);
            mr.lightProbeUsage = LightProbeUsage.Off; mr.shadowCastingMode = ShadowCastingMode.On;

            // Station (판정 상자 = Pickup 레이어 13, 몸에 안 걸림 — 눕기 Lie_* 와 같은 방식)
            var seat = new GameObject("Seat"); seat.layer = 13; seat.transform.SetParent(bag, false);
            var bc = seat.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, H * 0.5f, 0f); bc.size = new Vector3(R * 1.7f, H, R * 1.7f);
            var st = seat.AddComponent<VRC.SDK3.Components.VRCStation>();
            if (src) EditorUtility.CopySerialized(src, st);
            st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize; st.seated = true; st.disableStationExit = false; st.canUseStationFromStation = true;
            var ep0 = new GameObject("SitPoint").transform; ep0.SetParent(seat.transform, false);
            ep0.localPosition = new Vector3(0f, SEAT_Y, -0.02f); ep0.localRotation = Quaternion.identity;
            var ex = new GameObject("ExitPoint").transform; ex.SetParent(seat.transform, false);
            ex.localPosition = new Vector3(0f, 0.02f, R + 0.35f); ex.localRotation = Quaternion.identity;
            st.stationEnterPlayerLocation = ep0; st.stationExitPlayerLocation = ex;
            EditorUtility.SetDirty(st);
            var cs = UdonSharpUndo.AddComponent<PyriteCarrySeat>(seat);
            cs.station = st; cs.pickup = null;
            UdonSharpEditorUtility.CopyProxyToUdon(cs);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs);
            if (ub != null) { ub.interactText = "Sit"; ub.proximity = 2f; EditorUtility.SetDirty(ub); }

            float dist = new Vector2(d.x, d.z).magnitude;
            var eye = POS[i] + Vector3.up * (SEAT_Y + 0.62f);
            float pitch = Mathf.Atan2(TV.y - eye.y, dist) * Mathf.Rad2Deg;
            sb.AppendLine("빈백 " + (i + 1) + " " + V(POS[i]) + " yaw " + bag.localEulerAngles.y.ToString("F0") + ", TV 까지 " + dist.ToString("F2") + " m (화면 2.0 m → 가로 시야 " + (2f * Mathf.Atan(1f / dist) * Mathf.Rad2Deg).ToString("F0") + "°), 눈높이 추정 " + eye.y.ToString("F2") + " m · 올려봄 " + pitch.ToString("F0") + "°");
        }
        sb.AppendLine("메시 삼각형 " + tris + " × " + POS.Length + " = " + tris * POS.Length + ", 크기 지름 " + (R * 2).ToString("F2") + " m · 높이 " + H + " m");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    // 눌린 구: 앞 = 로컬 +Z. 바닥 평평 · 아래쪽 퍼짐, 뒤쪽 위 솟음(등받이), 앞쪽 위 파임(좌석)
    static Mesh BagMesh()
    {
        const int NU = 24, NV = 16;
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
        for (int j = 0; j <= NV; j++)
        {
            float th = Mathf.PI * j / NV;               // 0 = 위, π = 아래
            float sy = Mathf.Cos(th), sr = Mathf.Sin(th);
            for (int i = 0; i <= NU; i++)
            {
                float ph = 2f * Mathf.PI * i / NU;
                float dx = Mathf.Sin(ph) * sr, dz = Mathf.Cos(ph) * sr;   // i=0 → +Z(앞)
                float up = Mathf.Max(0f, sy), low = Mathf.Max(0f, -sy);
                float rad = R * (1f + 0.16f * low - 0.10f * up * up);     // 아래로 퍼지고 위로 좁아짐
                float y = H * 0.5f * (1f + sy);
                y = Mathf.Max(y, 0.015f + 0.03f * sr);                      // 바닥 눌림
                float back = Mathf.Max(0f, -dz / Mathf.Max(sr, 1e-4f)) * Mathf.Min(1f, sr * 1.2f);
                float front = Mathf.Max(0f, dz / Mathf.Max(sr, 1e-4f));
                y += 0.20f * back * up;                                    // 등받이
                y -= 0.24f * Mathf.Pow(up, 1.5f) * (0.35f + 0.65f * front); // 좌석 파임 (가운데도 조금)
                float wob = 1f + 0.025f * Mathf.Sin(ph * 3f + th * 2f);      // 천 주름 약간
                vs.Add(new Vector3(dx * rad * wob, y, dz * rad * wob));
                uv.Add(new Vector2((float)i / NU * 3f, (float)j / NV * 2f));
            }
        }
        int row = NU + 1;
        for (int j = 0; j < NV; j++)
            for (int i = 0; i < NU; i++)
            {
                int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                ts.Add(a); ts.Add(c); ts.Add(b); ts.Add(b); ts.Add(c); ts.Add(d);
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(ts, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Material Mat(string name, Color c)
    {
        string path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c; m.SetFloat("_Glossiness", 0.18f); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void Renders()
    {
        var room = Root("TentBedroom").transform;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var tvT = room.Find("BedroomPanel/BedroomTV"); bool tv0 = tvT && tvT.gameObject.activeSelf;
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            if (tvT) tvT.gameObject.SetActive(true);
            Vector3 W(Vector3 v) => room.TransformPoint(v);
            cam.fieldOfView = 60f;
            Shot(cam, W(new Vector3(1.6f, 1.6f, 2.2f)), W(new Vector3(-1.2f, 0.4f, 0.4f)), "bb_room");
            Shot(cam, W(new Vector3(-2.2f, 1.1f, -0.2f)), W(new Vector3(-0.6f, 0.3f, 1.1f)), "bb_close");
            for (int i = 0; i < POS.Length; i++)
                Shot(cam, W(POS[i] + Vector3.up * (SEAT_Y + 0.62f)), W(TV), "bb_sit" + (i + 1));
        }
        finally
        {
            if (tvT) tvT.gameObject.SetActive(tv0);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
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
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag);
        Object.DestroyImmediate(tex);
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_beanbag.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
