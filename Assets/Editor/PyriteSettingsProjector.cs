// Tools ▸ Pyrite ▸ Z24b. Build Settings Projector  /  Z24c. Revert
//  테이블 위 미니 빔 프로젝터 → 누르면 빛줄기 + 호수 쪽 공중 패널 (로컬 토글, 기본 꺼짐). 1단계: 외형만 (UI 기능은 다음 단계)
//   몸체 0.18×0.065×0.14 m (흑연 + 금 테 · 윗판 · 허리띠 · 통풍 슬릿 · 버튼 · 상태 LED), 테이블의 호수 쪽 가장자리, 방향 = 테이블 → 호수 중심 (yaw ≈171)
//   패널 3.0×1.69 m (16:9), 렌즈 앞 3.2 m, 중심 높이 = 테이블 지면 + 1.6 m, 수직 · 방향 = 호수 중심에서 왼쪽 10° (yaw ≈161)
//   빛줄기 = 렌즈 사각형 → 패널 모서리 사각뿔 (Pyrite/ProjectorBeam), 렌즈 옆 작은 포인트 라이트
//   TimeDial 은 끈다 (Z24c 가 되돌림). 재실행 안전: 기존 SettingsProjector 를 지우고 다시 만든다
//  렌더 12:00 / 21:00 × (테이블 뒤, 옆, 근접) — 빛줄기·패널을 잠시 켜서 찍고 다시 끈다 → Assets/_preview/projector/*.png
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

public static class PyriteSettingsProjector
{
    const string ROOT = "SettingsProjector";
    const string DIR = "Assets/Materials/Projector/";
    const float PANEL_W = 3.0f, PANEL_H = 1.6875f, THROW = 3.2f, PANEL_Y = 1.6f;
    const float YAW_OFFSET = -10f;   // 관리자: 빔을 왼쪽으로 10° (테이블에서 호수를 볼 때 왼쪽 = yaw 감소)
    static readonly Vector3 BODY = new Vector3(0.18f, 0.065f, 0.14f);
    const float FOOT = 0.01f;

    [MenuItem("Tools/Pyrite/Z24b. Build Settings Projector", false, 58)]
    public static void Build()
    {
        var sb = new StringBuilder("[Z24b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var table = GameObject.Find("camp03_table");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (table == null) { sb.AppendLine("camp03_table 없음"); Flush(sb); return; }

        var old = Find(ROOT); if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("기존 " + ROOT + " 삭제"); }
        var dial = Find("TimeDial");
        if (dial != null) { dial.SetActive(false); sb.AppendLine("TimeDial off"); }

        var terr = Terrain.activeTerrain;
        var tb = table.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
        var dir = Quaternion.Euler(0f, YAW_OFFSET, 0f) * new Vector3(0f - tb.center.x, 0f, -14f - tb.center.z).normalized;
        // 테이블 중심에서 dir 로 상자 가장자리까지 거리
        float tEdge = Mathf.Min(Mathf.Abs(dir.x) > 1e-4f ? tb.extents.x / Mathf.Abs(dir.x) : 99f, Mathf.Abs(dir.z) > 1e-4f ? tb.extents.z / Mathf.Abs(dir.z) : 99f);
        var foot = tb.center + dir * Mathf.Max(0f, tEdge - 0.13f);
        float top = tb.max.y;
        // 테이블에 콜라이더가 없어 레이가 지면에 맞는다 → 상판 높이 = 렌더러 bounds 최상단 (TimeDial 끈 뒤)
        if (Physics.Raycast(new Vector3(foot.x, tb.max.y + 0.5f, foot.z), Vector3.down, out var hit, 1.5f) && hit.point.y > tb.center.y) top = hit.point.y;
        float ground = terr ? terr.SampleHeight(tb.center) + terr.transform.position.y : tb.min.y;
        sb.AppendLine(string.Format("table center {0} top(bounds) {1:0.000} top(ray) {2:0.000} ground {3:0.000} edge {4:0.00} dir {5} yaw {6:0.0}",
            tb.center.ToString("F2"), tb.max.y, top, ground, tEdge, dir.ToString("F3"), Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg));

        // 머티리얼
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Projector")) AssetDatabase.CreateFolder("Assets/Materials", "Projector");
        var std = Shader.Find("Standard");
        var mBody = Mat("M_Proj_Body", std, m => { m.color = new Color(0.075f, 0.075f, 0.08f); m.SetFloat("_Metallic", 0.55f); m.SetFloat("_Glossiness", 0.55f); });
        var mTrim = Mat("M_Proj_Trim", std, m => { m.color = new Color(0.93f, 0.74f, 0.38f); m.SetFloat("_Metallic", 1f); m.SetFloat("_Glossiness", 0.78f); });
        var mLensOff = Mat("M_Proj_LensOff", std, m => { m.color = new Color(0.02f, 0.025f, 0.03f); m.SetFloat("_Metallic", 0.2f); m.SetFloat("_Glossiness", 0.95f); m.DisableKeyword("_EMISSION"); });
        var mLensOn = Mat("M_Proj_LensOn", std, m =>
        {
            m.color = new Color(0.9f, 0.85f, 0.75f); m.SetFloat("_Glossiness", 0.95f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.72f) * 4f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        });
        var mBeam = Mat("M_Proj_Beam", Shader.Find("Pyrite/ProjectorBeam"), m => { m.SetColor("_Color", new Color(1f, 0.82f, 0.58f)); m.SetFloat("_Intensity", 0.05f); m.SetFloat("_Dust", 0.12f); m.SetFloat("_EdgePow", 1.6f); });
        var mTop = Mat("M_Proj_Top", std, m => { m.color = new Color(0.13f, 0.13f, 0.14f); m.SetFloat("_Metallic", 0.4f); m.SetFloat("_Glossiness", 0.35f); });
        var mVent = Mat("M_Proj_Vent", std, m => { m.color = new Color(0.015f, 0.015f, 0.018f); m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.2f); });
        var mLedOff = Mat("M_Proj_LedOff", std, m => { m.color = new Color(0.25f, 0.16f, 0.05f); m.SetFloat("_Glossiness", 0.9f); m.DisableKeyword("_EMISSION"); });
        var mLedOn = Mat("M_Proj_LedOn", std, m => { m.color = new Color(1f, 0.7f, 0.3f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.2f) * 6f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; });
        var mPanel = Mat("M_Proj_Panel", Shader.Find("Pyrite/HoloPanel"), m => { m.SetFloat("_Aspect", PANEL_W / PANEL_H); });
        if (mBeam.shader == null || mPanel.shader == null || mBeam.shader.name != "Pyrite/ProjectorBeam" || mPanel.shader.name != "Pyrite/HoloPanel")
        { sb.AppendLine("셰이더 없음 (Refresh 먼저)"); Flush(sb); return; }

        // 루트 = 몸체 중심 (콜라이더 + U#)
        var root = new GameObject(ROOT);
        Undo.RegisterCreatedObjectUndo(root, "projector");
        root.transform.SetPositionAndRotation(new Vector3(foot.x, top + FOOT + BODY.y * 0.5f, foot.z), Quaternion.LookRotation(dir, Vector3.up));
        var col = root.AddComponent<BoxCollider>(); col.size = BODY + new Vector3(0.02f, 0.02f, 0.02f);

        Prim(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, Quaternion.identity, BODY, mBody);
        Prim(PrimitiveType.Cube, "TrimFront", root.transform, new Vector3(0f, 0f, BODY.z * 0.5f + 0.002f), Quaternion.identity, new Vector3(BODY.x + 0.004f, BODY.y + 0.004f, 0.004f), mTrim);
        Prim(PrimitiveType.Cube, "TrimTop", root.transform, new Vector3(0f, BODY.y * 0.5f + 0.001f, -0.02f), Quaternion.identity, new Vector3(BODY.x * 0.7f, 0.002f, 0.006f), mTrim);
        var xr = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Cylinder, "LensBarrel", root.transform, new Vector3(0.035f, 0.004f, BODY.z * 0.5f + 0.014f), xr, new Vector3(0.05f, 0.012f, 0.05f), mTrim);
        var lens = Prim(PrimitiveType.Cylinder, "LensGlass", root.transform, new Vector3(0.035f, 0.004f, BODY.z * 0.5f + 0.027f), xr, new Vector3(0.038f, 0.002f, 0.038f), mLensOff);
        // 디테일: 윗판(한 톤 밝은 흑연, 베벨처럼 보이게), 금색 허리띠, 양옆 통풍 슬릿, 윗면 버튼, 상태 LED
        Prim(PrimitiveType.Cube, "TopPlate", root.transform, new Vector3(0f, BODY.y * 0.5f + 0.0015f, 0f), Quaternion.identity, new Vector3(BODY.x - 0.012f, 0.003f, BODY.z - 0.012f), mTop);
        Prim(PrimitiveType.Cube, "Belt", root.transform, new Vector3(0f, -BODY.y * 0.5f + 0.012f, 0f), Quaternion.identity, new Vector3(BODY.x + 0.003f, 0.004f, BODY.z + 0.003f), mTrim);
        foreach (var sx in new[] { -1f, 1f })
            for (int k = 0; k < 6; k++)
                Prim(PrimitiveType.Cube, "Vent", root.transform, new Vector3(sx * (BODY.x * 0.5f + 0.0008f), 0.006f, -0.045f + k * 0.013f), Quaternion.identity, new Vector3(0.002f, 0.026f, 0.005f), mVent);
        Prim(PrimitiveType.Cylinder, "Button", root.transform, new Vector3(-0.05f, BODY.y * 0.5f + 0.005f, -0.035f), Quaternion.identity, new Vector3(0.02f, 0.0025f, 0.02f), mTrim);
        Prim(PrimitiveType.Cube, "LedBase", root.transform, new Vector3(-0.065f, BODY.y * 0.5f + 0.0035f, 0.045f), Quaternion.identity, new Vector3(0.008f, 0.002f, 0.004f), mLedOff);
        var led = Prim(PrimitiveType.Cube, "LedOn", root.transform, new Vector3(-0.065f, BODY.y * 0.5f + 0.004f, 0.045f), Quaternion.identity, new Vector3(0.0085f, 0.0022f, 0.0045f), mLedOn);
        led.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (var fx in new[] { -1f, 1f }) foreach (var fz in new[] { -1f, 1f })
            Prim(PrimitiveType.Cylinder, "Foot", root.transform, new Vector3(fx * (BODY.x * 0.5f - 0.02f), -BODY.y * 0.5f - FOOT * 0.5f, fz * (BODY.z * 0.5f - 0.02f)), Quaternion.identity, new Vector3(0.018f, FOOT * 0.5f, 0.018f), mTrim);

        var lensW = lens.transform.position + root.transform.forward * 0.003f;

        // 패널 (수직, 앞면이 테이블 쪽)
        var panelC = lensW + dir * THROW; panelC.y = ground + PANEL_Y;
        var panel = Prim(PrimitiveType.Quad, "Panel", root.transform, root.transform.InverseTransformPoint(panelC), Quaternion.identity, new Vector3(PANEL_W, PANEL_H, 1f), mPanel);
        panel.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        var pr = panel.GetComponent<MeshRenderer>(); pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; pr.lightProbeUsage = LightProbeUsage.Off; pr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // 빛줄기
        var beam = new GameObject("Beam"); beam.transform.SetParent(root.transform, false);
        var mesh = BeamMesh(root.transform, lensW, 0.013f, 0.009f, panelC, panel.transform.right, panel.transform.up, PANEL_W * 0.5f * 0.97f, PANEL_H * 0.5f * 0.97f);
        var meshPath = DIR + "SettingsBeam.asset";
        var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath); if (oldMesh != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        beam.AddComponent<MeshFilter>().sharedMesh = mesh;
        var br = beam.AddComponent<MeshRenderer>(); br.sharedMaterial = mBeam;
        br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false; br.lightProbeUsage = LightProbeUsage.Off; br.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // 렌즈 앞 작은 빛 (테이블 위를 살짝 밝힘)
        var lg = new GameObject("LensGlow"); lg.transform.SetParent(root.transform, false);
        lg.transform.position = lensW + root.transform.forward * 0.05f;
        var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.range = 0.7f; li.intensity = 0.5f; li.color = new Color(1f, 0.88f, 0.7f);
        li.shadows = LightShadows.None; li.lightmapBakeType = LightmapBakeType.Realtime; li.renderMode = LightRenderMode.Auto;

        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        // U#
        PyriteProjector pj;
        try { pj = UdonSharpUndo.AddComponent<PyriteProjector>(root); }
        catch (System.Exception e) { sb.AppendLine("AddComponent 실패 (H 로 프로그램 에셋 먼저): " + e.Message); Flush(sb); return; }
        pj.onObjects = new[] { beam, panel, lg, led };
        pj.lensRenderer = lens.GetComponent<Renderer>();
        pj.lensOn = mLensOn; pj.lensOff = mLensOff; pj.isOn = false;
        UdonSharpEditorUtility.CopyProxyToUdon(pj); EditorUtility.SetDirty(pj);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pj); if (ub != null) { ub.interactText = "Settings"; EditorUtility.SetDirty(ub); }

        var tris = root.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh ? f.sharedMesh.triangles.Length / 3 : 0);
        sb.AppendLine(string.Format("root {0} lens {1} panel {2} (above ground {3:0.00}) beam len {4:0.00} rise {5:0.0}° | tris {6}",
            root.transform.position.ToString("F3"), lensW.ToString("F3"), panelC.ToString("F2"), panelC.y - ground, (panelC - lensW).magnitude,
            Mathf.Asin((panelC.y - lensW.y) / (panelC - lensW).magnitude) * Mathf.Rad2Deg, tris));

        // 렌더 (잠시 켬)
        SetOn(pj, true);
        try { Shots(cyc, root.transform, panelC, lensW, dir, ground, sb); }
        finally { SetOn(pj, false); }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite/Z24c. Settings Projector Revert", false, 59)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z24c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = Find(ROOT); if (old != null) { Object.DestroyImmediate(old); sb.AppendLine(ROOT + " 삭제"); }
        var dial = Find("TimeDial"); if (dial != null) { dial.SetActive(true); sb.AppendLine("TimeDial on"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void SetOn(PyriteProjector pj, bool on)
    {
        foreach (var o in pj.onObjects) if (o != null) o.SetActive(on);
        pj.lensRenderer.sharedMaterial = on ? pj.lensOn : pj.lensOff;
    }

    static Mesh BeamMesh(Transform root, Vector3 lensW, float lhx, float lhy, Vector3 pc, Vector3 right, Vector3 up, float phx, float phy)
    {
        var rr = root.right; var ru = root.up;
        // 모서리 순서: 좌하, 우하, 우상, 좌상
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
        var m = new Mesh { name = "SettingsBeam", vertices = v, normals = n, uv = uv, triangles = tri };
        m.RecalculateBounds();
        return m;
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 lp, Quaternion lr, Vector3 ls, Material mat)
    {
        var g = GameObject.CreatePrimitive(t); g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localRotation = lr; g.transform.localScale = ls;
        var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
        return g;
    }

    static Material Mat(string name, Shader sh, System.Action<Material> set)
    {
        var path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        if (sh != null) m.shader = sh;
        set(m); EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Find(string n) => GameObject.Find(n) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == n && x.scene.IsValid());

    static void Shots(PyriteDayCycle cyc, Transform root, Vector3 panelC, Vector3 lensW, Vector3 dir, float ground, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var side = Vector3.Cross(Vector3.up, dir).normalized;
        var mid = (lensW + panelC) * 0.5f;
        var views = new (string n, Vector3 e, Vector3 l, float f)[]
        {
            ("behind", lensW - dir * 2.3f + side * 0.4f + Vector3.up * (ground + 1.7f - lensW.y), panelC, 65f),
            ("side", mid + side * 4.2f - dir * 0.6f + Vector3.up * (ground + 1.6f - mid.y), mid, 60f),
            ("close", lensW - dir * 0.55f + side * 0.35f + Vector3.up * 0.22f, lensW + dir * 0.05f, 45f),
            ("front", lensW + dir * 0.45f + side * 0.38f + Vector3.up * 0.16f, root.position, 40f),
        };
        Directory.CreateDirectory("Assets/_preview/projector/");
        try
        {
            if (cyc != null) cyc.ResetCache();
            foreach (var h in new[] { 12f, 21f })
            {
                if (cyc != null) cyc.EvaluateAt(h);
                foreach (var v in views) Shot(cam, v.e, v.l, v.f, string.Format("Assets/_preview/projector/proj_{0:00}_{1}.png", (int)h, v.n));
            }
            sb.AppendLine("  shots 12/21 × behind/side/close/front");
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (cyc != null) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 960, H = 540;
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

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_projector.txt", sb.ToString()); }
}
#endif
