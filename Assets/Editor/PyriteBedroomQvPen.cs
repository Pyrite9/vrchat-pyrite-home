// PyriteBedroomQvPen.cs — 침실 전용 QvPen 소형 세트 (Z51f 빌드 / Z51g 되돌리기). 재실행 안전
//  2026-09-29 관리자: 2번 (가) = 침실 전용 소형 세트. QvPen.prefab 을 풀어서(Unpack) 펜 3(노랑·시안·흰색) · 지우개 1 만 남김
//  위치: +X 벽 창 쪽 빈 자리(z ≈ 1.45), 펜 높이 0.8 m, 앞면(로컬 −Z)이 방 안(−X)을 보게. 루트 TentBedroom/BedroomQvPen (씬 루트 이름 "QvPen" 과 안 겹침 → 캠프 소환 기능 영향 없음)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomQvPen
{
    const string PREFAB = "Packages/net.ureishi.qvpen/QvPen.prefab";
    const string NAME = "BedroomQvPen";
    const string PREV = "Assets/_preview/bedroom/";
    const int KEEP_PENS = 3, KEEP_ERASERS = 1;
    static readonly int[] KEEP = { 3, 7, 13 };   // 노랑 · 시안 · 흰색 (관리자 지정 없음 → 어두운 방에서 잘 보이는 색)
    const float SET_Z = 1.45f, SET_Y = 0.80f, WALL_GAP = 0.14f;
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z51f. Bedroom QvPen Build", false, 5106)]
    public static void Build()
    {
        sb = new StringBuilder("[Z51f] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51g. Bedroom QvPen Revert", false, 5107)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z51g] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom"); var t = room ? room.transform.Find(NAME) : null;
        if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(NAME + " 삭제"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static bool Inner()
    {
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return false; }
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB); if (asset == null) { sb.AppendLine("!! " + PREFAB + " 없음"); return false; }
        var old = room.transform.Find(NAME); if (old) Object.DestroyImmediate(old.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, room.transform);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = NAME;

        // 펜 색 = 펜 몸통 "Mesh" 렌더러 머티리얼 색 (Z51h 실측: 0 검정 · 1 빨강 · 3 노랑 · 7 시안 · 13 흰색 …). 어두운 텐트에서 잘 보이는 흰·노랑·시안
        var pens = go.transform.Find("Pens"); var erasers = go.transform.Find("Erasers");
        var list = new List<(Transform t, Color c)>();
        foreach (Transform pm in pens)
        {
            var mr = pm.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Mesh");
            var c = mr && mr.sharedMaterial && mr.sharedMaterial.HasProperty("_Color") ? mr.sharedMaterial.color : Color.gray;
            list.Add((pm, c));
        }
        sb.AppendLine("펜 " + list.Count + "개 색: " + string.Join(" ", list.Select((p, i) => i + ":" + ColorUtility.ToHtmlStringRGB(p.c))));
        var keep = KEEP.Where(i => i < list.Count).ToList();
        sb.AppendLine("남길 펜 " + string.Join(", ", keep.Select(i => i + "(" + ColorUtility.ToHtmlStringRGB(list[i].c) + ")")));
        for (int i = list.Count - 1; i >= 0; i--) if (!keep.Contains(i)) Object.DestroyImmediate(list[i].t.gameObject);
        // 남은 펜을 0.1 m 간격으로 다시 줄세움
        int k = 0; foreach (Transform pm in pens) { pm.localPosition = new Vector3(k * 0.1f, 0f, 0f); k++; }
        for (int i = erasers.childCount - 1; i >= KEEP_ERASERS; i--) Object.DestroyImmediate(erasers.GetChild(i).gameObject);
        erasers.localPosition = new Vector3(KEEP_PENS * 0.1f + 0.05f, 0f, 0f);

        // 위치: 루트 로컬 −Z = 방 쪽(−X) → 루트 +Z = +X, 세트 가로(로컬 +X) = 방 −Z 쪽으로 늘어섬 → 가운데 맞춤
        float width = KEEP_PENS * 0.1f + 0.05f;   // 펜 줄 + 지우개
        float x = PyriteBedroomBuild.SurfX(SET_Z, SET_Y) - WALL_GAP;
        go.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        go.transform.localPosition = new Vector3(x, SET_Y, SET_Z) - go.transform.localRotation * new Vector3(width / 2f, 0f, 0f);
        go.transform.localScale = Vector3.one;

        int udon = go.GetComponentsInChildren<Component>(true).Count(c => c && c.GetType().Name == "UdonBehaviour");
        int sync = go.GetComponentsInChildren<Component>(true).Count(c => c && c.GetType().Name == "VRCObjectSync");
        int tris = go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).Sum(f => f.sharedMesh.triangles.Length / 3);
        var rs = go.GetComponentsInChildren<Renderer>(false).Where(r => !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
        var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero); foreach (var r in rs) b.Encapsulate(r.bounds);
        var o = room.transform.position;
        sb.AppendLine("세트: 펜 " + pens.childCount + " · 지우개 " + erasers.childCount + ", UdonBehaviour " + udon + ", ObjectSync " + sync + ", 메시 삼각형 " + tris);
        sb.AppendLine("위치 " + V(go.transform.localPosition) + " yaw " + go.transform.localEulerAngles.y.ToString("F0") + " (벽 x " + (x + WALL_GAP).ToString("F2") + "), 보이는 bounds " + V(b.min - o) + " .. " + V(b.max - o));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    // 색상환 거리(채도 가중)로 서로 가장 다른 n개 — 첫째는 가장 채도 높은 것
    static List<int> PickDistinct(List<Color> cs, int n)
    {
        var hsv = cs.Select(c => { Color.RGBToHSV(c, out float h, out float s, out float v); return (h, s, v); }).ToList();
        var pick = new List<int> { Enumerable.Range(0, cs.Count).OrderByDescending(i => hsv[i].s * hsv[i].v).First() };
        while (pick.Count < Mathf.Min(n, cs.Count))
        {
            int best = -1; float bestD = -1f;
            for (int i = 0; i < cs.Count; i++)
            {
                if (pick.Contains(i)) continue;
                float d = pick.Min(j =>
                {
                    float dh = Mathf.Abs(hsv[i].h - hsv[j].h); dh = Mathf.Min(dh, 1f - dh);
                    return dh * Mathf.Min(hsv[i].s, hsv[j].s) + 0.5f * Mathf.Abs(hsv[i].v - hsv[j].v) + 0.3f * Mathf.Abs(hsv[i].s - hsv[j].s);
                });
                if (d > bestD) { bestD = d; best = i; }
            }
            pick.Add(best);
        }
        pick.Sort();
        return pick;
    }

    static void Renders()
    {
        var room = Root("TentBedroom").transform;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            Vector3 W(float x, float y, float z) => room.TransformPoint(new Vector3(x, y, z));
            cam.fieldOfView = 60f;
            Shot(cam, W(1.6f, 1.2f, 1.2f), W(3.1f, 0.8f, 1.45f), "pq_close");
            Shot(cam, W(-1.6f, 1.6f, 0.4f), W(2.6f, 0.7f, 1.6f), "pq_room");
            Shot(cam, W(-0.28f, 0.45f, -1.9f), W(3.0f, 0.8f, 1.45f), "pq_lie");
        }
        finally
        {
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
        File.WriteAllText("Logs/pyrite_bedroom_qvpen.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
