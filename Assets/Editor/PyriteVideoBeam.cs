// Tools ▸ Pyrite2 ▸ Z26b. Build Video Beam  /  Z26c. Video Beam Revert
//  테이블 오른쪽에 영상 프로젝터(설정 프로젝터와 같은 몸체 복제) → 빛줄기 → 스크린 3.2×1.8 m (ProTV Main Screen 을 옮긴다)
//   방향: 영상 프로젝터에서 yaw 208° (설정 패널 오른쪽), 거리 4.5 m, 스크린 중심 = 지면 + 1.9 m, 수직
//   스크린 뒤 테두리 = 설정 패널과 같은 흑요석 유리 + 금선 (M_Proj_Panel)
//   ProTV: Main Screen·Speakers·MediaControls 를 스크린 쪽으로 옮기고(조작 패널은 스크린 아래, 2배), ScreenRig·ControlPost 는 끈다
//   켜고 끄기는 모두에게 동기화 (PyriteVideoProjector, Manual). 기본 꺼짐
//   되돌리기 위치·회전·크기와 끈 오브젝트는 Logs/pyrite_video_revert.txt
//  렌더 Assets/_preview/projector/video_*.png
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

public static class PyriteVideoBeam
{
    const string ROOT = "VideoProjector";
    const string REVERT = "Logs/pyrite_video_revert.txt";
    const float YAW = 208f, DIST = 4.5f, SW = 3.2f, SH = 1.8f, SCREEN_Y = 1.9f, SIDE = 0.30f;
    static readonly string[] BODY_PARTS = { "Body", "TrimFront", "TrimTop", "LensBarrel", "LensGlass", "Foot", "TopPlate", "Belt", "Vent", "Button", "LedBase", "LedOn", "LensGlow" };

    [MenuItem("Tools/Pyrite2/Z26b. Build Video Beam", false, 2)]
    public static void Build()
    {
        var sb = new StringBuilder("[Z26b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var sp = GameObject.Find("SettingsProjector");
        var mp = Find("MediaPlayer");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (sp == null || mp == null) { sb.AppendLine("SettingsProjector/MediaPlayer 없음"); Flush(sb); return; }
        var screen = mp.transform.Find("Room/Main Screen");
        var speakers = mp.transform.Find("Room/Speakers");
        var controls = mp.transform.Find("Room/MediaControls (Mono)");
        var rig = mp.transform.Find("Room/ScreenRig");
        var post = mp.transform.Find("Room/ControlPost");
        if (screen == null || speakers == null || controls == null) { sb.AppendLine("ProTV 하위(Main Screen/Speakers/MediaControls) 없음"); Flush(sb); return; }

        // 재실행: 먼저 되돌린다 (원래 위치 기준으로 다시 계산)
        if (Find(ROOT) != null || File.Exists(REVERT)) { Revert(); sb.AppendLine("이전 빌드 되돌림 후 다시"); }

        var revert = new List<string>();
        void Save(Transform tr) { revert.Add(string.Format("T|{0}|{1}|{2}|{3}", PathOf(tr), V(tr.position), Q(tr.rotation), V(tr.localScale))); }
        void Off(GameObject g) { if (g != null && g.activeSelf) { g.SetActive(false); revert.Add("A|" + PathOf(g.transform)); } }

        var terr = Terrain.activeTerrain;
        var spt = sp.transform;
        var lakeDir = Quaternion.Euler(0f, 10f, 0f) * spt.forward;          // 설정 프로젝터는 호수 방향에서 왼쪽 10°
        var right = Vector3.Cross(Vector3.up, lakeDir).normalized;
        var rootPos = spt.position + right * SIDE - lakeDir * 0.02f;
        var dir = Quaternion.Euler(0f, YAW, 0f) * Vector3.forward;

        // 1) 몸체 (설정 프로젝터에서 복제)
        var root = new GameObject(ROOT);
        Undo.RegisterCreatedObjectUndo(root, "video projector");
        root.transform.SetPositionAndRotation(rootPos, Quaternion.LookRotation(dir, Vector3.up));
        var col = root.AddComponent<BoxCollider>(); col.size = spt.GetComponent<BoxCollider>().size;
        GameObject lens = null, led = null, glow = null;
        foreach (Transform ch in spt)
        {
            if (!BODY_PARTS.Contains(ch.name)) continue;
            var c = Object.Instantiate(ch.gameObject, root.transform);
            c.name = ch.name;
            c.transform.localPosition = ch.localPosition; c.transform.localRotation = ch.localRotation; c.transform.localScale = ch.localScale;
            if (ch.name == "LensGlass") lens = c;
            if (ch.name == "LedOn") led = c;
            if (ch.name == "LensGlow") glow = c;
        }
        var lensW = lens.transform.position + root.transform.forward * 0.003f;
        if (glow != null) glow.transform.position = lensW + root.transform.forward * 0.05f;

        // 2) 스크린 위치
        var sc = rootPos + dir * DIST;
        float groundY = terr ? terr.SampleHeight(sc) + terr.transform.position.y : rootPos.y - 0.4f;
        sc.y = groundY + SCREEN_Y;
        var toAud = -dir;                                                    // 스크린에서 관객(테이블) 쪽
        var faceRot = Quaternion.LookRotation(dir, Vector3.up);              // 설정 패널과 같은 규약: forward = 관객 반대쪽

        // 3) ProTV Main Screen 이동: 보이는 면(메시 법선)이 관객을 향하게, 크기 SW×SH
        Save(screen); Save(speakers); Save(controls);
        var mf = screen.GetComponent<MeshFilter>();
        var nLocal = mf.sharedMesh.normals.Length > 0 ? mf.sharedMesh.normals[0] : Vector3.back;
        var nOld = screen.TransformDirection(nLocal); nOld.y = 0; nOld.Normalize();
        var oldCenter = screen.GetComponent<Renderer>().bounds.center;
        var delta = Quaternion.FromToRotation(nOld, toAud);
        var ms = mf.sharedMesh.bounds.size;
        var ls = screen.lossyScale;
        float curW = Mathf.Abs(ms.x * ls.x), curH = Mathf.Abs(ms.y * ls.y);
        bool xz = curH < 0.01f;                                              // 평면(Plane)형 메시면 세로가 z
        if (xz) curH = Mathf.Abs(ms.z * ls.z);
        screen.rotation = delta * screen.rotation;
        var sScale = screen.localScale;
        screen.localScale = xz ? new Vector3(sScale.x * SW / curW, sScale.y, sScale.z * SH / curH) : new Vector3(sScale.x * SW / curW, sScale.y * SH / curH, sScale.z);
        screen.position += sc - screen.GetComponent<Renderer>().bounds.center;
        sb.AppendLine(string.Format("screen: old {0:0.00}x{1:0.00} center {2} → {3} normal {4} → {5}", curW, curH, oldCenter.ToString("F2"), sc.ToString("F2"), nOld.ToString("F2"), toAud.ToString("F2")));

        // 스피커: 스크린과 같이 이동·회전
        var spOld = speakers.position;
        speakers.rotation = delta * speakers.rotation;
        speakers.position = sc + delta * (spOld - oldCenter);

        // 조작 패널: 스크린 아래, 관객을 보게, 2배
        controls.SetPositionAndRotation(sc - Vector3.up * (SH * 0.5f + 0.42f) + toAud * 0.02f, faceRot);
        controls.localScale = controls.localScale * 2f;
        var cRect = controls.GetComponent<RectTransform>();
        sb.AppendLine("controls size " + (cRect ? (cRect.sizeDelta.x * controls.lossyScale.x).ToString("0.00") + " x " + (cRect.sizeDelta.y * controls.lossyScale.y).ToString("0.00") + " m" : "?"));

        Off(rig != null ? rig.gameObject : null);
        Off(post != null ? post.gameObject : null);

        // 4) 테두리 (설정 패널과 같은 유리)
        var panelMat = spt.Find("Panel").GetComponent<Renderer>().sharedMaterial;
        var frame = GameObject.CreatePrimitive(PrimitiveType.Quad); frame.name = "ScreenFrame";
        Object.DestroyImmediate(frame.GetComponent<Collider>());
        frame.transform.SetParent(root.transform, false);
        frame.transform.SetPositionAndRotation(sc + dir * 0.02f, faceRot);
        frame.transform.localScale = new Vector3(SW + 0.16f, SH + 0.09f, 1f);
        var fr = frame.GetComponent<MeshRenderer>(); fr.sharedMaterial = panelMat;
        fr.shadowCastingMode = ShadowCastingMode.Off; fr.receiveShadows = false; fr.lightProbeUsage = LightProbeUsage.Off; fr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // 5) 빛줄기
        var beamMat = spt.Find("Beam").GetComponent<Renderer>().sharedMaterial;
        var beam = new GameObject("Beam"); beam.transform.SetParent(root.transform, false);
        var right2 = faceRot * Vector3.right;
        var mesh = BeamMesh(root.transform, lensW, 0.013f, 0.009f, sc, right2, Vector3.up, SW * 0.5f * 0.98f, SH * 0.5f * 0.98f);
        var meshPath = "Assets/Materials/Projector/VideoBeam.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        beam.AddComponent<MeshFilter>().sharedMesh = mesh;
        var br = beam.AddComponent<MeshRenderer>(); br.sharedMaterial = beamMat;
        br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false; br.lightProbeUsage = LightProbeUsage.Off; br.reflectionProbeUsage = ReflectionProbeUsage.Off;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        // 6) U#
        var spj = spt.GetComponent<PyriteProjector>();
        PyriteVideoProjector vp;
        try { vp = UdonSharpUndo.AddComponent<PyriteVideoProjector>(root); }
        catch (System.Exception e) { sb.AppendLine("AddComponent 실패 (H 로 프로그램 에셋 먼저): " + e.Message); Flush(sb); return; }
        vp.onObjects = new[] { beam, frame, screen.gameObject, controls.gameObject, glow, led }.Where(o => o != null).ToArray();
        vp.lensRenderer = lens.GetComponent<Renderer>();
        vp.lensOn = spj.lensOn; vp.lensOff = spj.lensOff; vp.isOn = false;
        UdonSharpEditorUtility.CopyProxyToUdon(vp); EditorUtility.SetDirty(vp);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(vp); if (ub != null) { ub.interactText = "Video"; EditorUtility.SetDirty(ub); }

        Directory.CreateDirectory("Logs"); File.WriteAllLines(REVERT, revert);
        sb.AppendLine(string.Format("root {0} yaw {1:0.0} | lens {2} | screen {3} (ground {4:0.00}) dist {5:0.00} | onObjects {6}",
            rootPos.ToString("F3"), YAW, lensW.ToString("F3"), sc.ToString("F2"), groundY, (sc - lensW).magnitude, vp.onObjects.Length));

        // 7) 렌더 (둘 다 켬)
        Shots(vp, spj, cyc, sb);
        foreach (var o in vp.onObjects) o.SetActive(false);
        vp.lensRenderer.sharedMaterial = vp.lensOff;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite2/Z26c. Video Beam Revert", false, 3)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z26c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = Find(ROOT); if (old != null) { Object.DestroyImmediate(old); sb.AppendLine(ROOT + " 삭제"); }
        if (File.Exists(REVERT))
        {
            var all = Object.FindObjectsOfType<Transform>(true);
            foreach (var line in File.ReadAllLines(REVERT))
            {
                var p = line.Split('|');
                var tr = all.FirstOrDefault(x => PathOf(x) == p[1]); if (tr == null) continue;
                if (p[0] == "A") tr.gameObject.SetActive(true);
                if (p[0] == "T") { tr.position = PV(p[2]); tr.rotation = PQ(p[3]); tr.localScale = PV(p[4]); tr.gameObject.SetActive(true); }
                sb.AppendLine("  restore " + p[0] + " " + p[1]);
            }
            File.Delete(REVERT);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static Mesh BeamMesh(Transform root, Vector3 lensW, float lhx, float lhy, Vector3 pc, Vector3 right, Vector3 up, float phx, float phy)
    {
        var rr = root.right; var ru = root.up;
        var L = new[] { lensW - rr * lhx - ru * lhy, lensW + rr * lhx - ru * lhy, lensW + rr * lhx + ru * lhy, lensW - rr * lhx + ru * lhy };
        var P = new[] { pc - right * phx - up * phy, pc + right * phx - up * phy, pc + right * phx + up * phy, pc - right * phx + up * phy };
        var axisMid = (lensW + pc) * 0.5f;
        var v = new Vector3[16]; var n = new Vector3[16]; var uv = new Vector2[16]; var tri = new int[24];
        for (int f = 0; f < 4; f++)
        {
            int a = f, b = (f + 1) % 4, k = f * 4;
            v[k] = L[a]; v[k + 1] = L[b]; v[k + 2] = P[b]; v[k + 3] = P[a];
            uv[k] = new Vector2(0, 0); uv[k + 1] = new Vector2(1, 0); uv[k + 2] = new Vector2(1, 1); uv[k + 3] = new Vector2(0, 1);
            var nn = Vector3.Cross(v[k + 1] - v[k], v[k + 3] - v[k]).normalized;
            var fc = (v[k] + v[k + 1] + v[k + 2] + v[k + 3]) * 0.25f;
            if (Vector3.Dot(nn, fc - axisMid) < 0) nn = -nn;
            for (int j = 0; j < 4; j++) { n[k + j] = root.InverseTransformDirection(nn); v[k + j] = root.InverseTransformPoint(v[k + j]); }
            tri[f * 6] = k; tri[f * 6 + 1] = k + 1; tri[f * 6 + 2] = k + 2; tri[f * 6 + 3] = k; tri[f * 6 + 4] = k + 2; tri[f * 6 + 5] = k + 3;
        }
        var m = new Mesh { name = "VideoBeam", vertices = v, normals = n, uv = uv, triangles = tri };
        m.RecalculateBounds();
        return m;
    }

    static void Shots(PyriteVideoProjector vp, PyriteProjector spj, PyriteDayCycle cyc, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var terr = Terrain.activeTerrain;
        var lakeDir = Quaternion.Euler(0f, 10f, 0f) * spj.transform.forward;
        var stand = spj.transform.position - lakeDir * 1.0f + Vector3.Cross(Vector3.up, lakeDir) * 0.15f;
        stand.y = (terr ? terr.SampleHeight(stand) + terr.transform.position.y : stand.y) + 1.6f;
        var look = stand + (Quaternion.Euler(0f, 17f, 0f) * lakeDir) * 5f; look.y = stand.y + 0.3f;
        var side = vp.transform.position + (Quaternion.Euler(0f, 208f + 70f, 0f) * Vector3.forward) * 4.2f; side.y = stand.y;
        var scr = vp.onObjects.First(o => o.name == "Main Screen").GetComponent<Renderer>().bounds.center;
        var near = scr - (Quaternion.Euler(0f, 208f, 0f) * Vector3.forward) * 3.2f; near.y = scr.y - 0.3f;
        foreach (var o in vp.onObjects) o.SetActive(true); vp.lensRenderer.sharedMaterial = vp.lensOn;
        foreach (var o in spj.onObjects) if (o != null) o.SetActive(true); spj.lensRenderer.sharedMaterial = spj.lensOn;
        Directory.CreateDirectory("Assets/_preview/projector/");
        try
        {
            if (cyc != null) cyc.ResetCache();
            foreach (var h in new[] { 21f, 12f })
            {
                if (cyc != null) cyc.EvaluateAt(h);
                Shot(cam, stand, look, 70f, string.Format("Assets/_preview/projector/video_{0:00}_table.png", (int)h));
                Shot(cam, side, (scr + vp.transform.position) * 0.5f, 60f, string.Format("Assets/_preview/projector/video_{0:00}_side.png", (int)h));
                Shot(cam, near, scr, 55f, string.Format("Assets/_preview/projector/video_{0:00}_screen.png", (int)h));
            }
            sb.AppendLine("  shots video_{21,12}_{table,side,screen}");
        }
        finally
        {
            foreach (var o in spj.onObjects) if (o != null) o.SetActive(false); spj.lensRenderer.sharedMaterial = spj.lensOff;
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
        Canvas.ForceUpdateCanvases();
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

    static string V(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2}", v.x, v.y, v.z);
    static string Q(Quaternion q) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3}", q.x, q.y, q.z, q.w);
    static float[] Fs(string s) => s.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    static Vector3 PV(string s) { var f = Fs(s); return new Vector3(f[0], f[1], f[2]); }
    static Quaternion PQ(string s) { var f = Fs(s); return new Quaternion(f[0], f[1], f[2], f[3]); }
    static GameObject Find(string n) => GameObject.Find(n) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == n && x.scene.IsValid());
    static string PathOf(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_video.txt", sb.ToString()); }
}
#endif
