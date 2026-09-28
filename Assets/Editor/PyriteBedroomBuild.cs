// PyriteBedroomBuild.cs — 텐트 침실 2단계: 빈 돔 + 텔레포트 왕복
// Tools ▸ Pyrite3 ▸ Z49b. Bedroom Shell Build  /  Z49c. Bedroom Revert  /  Z49d. Bedroom Renders
//  루트 TentBedroom (2000, 0, 0) — 돔 텐트(지름 6 m, 꼭대기 3.5 m), 바닥, X자 폴 2개, 입구 천막(−Z, 귀환), Spawn(입구 안쪽, +Z 를 봄)
//  루트 TentDoor — 캠프 텐트 camp01_tent_BRN 자리에 트리거 BoxCollider + PyriteTeleportDoor (기존 텐트 오브젝트는 건드리지 않음: 네트워크 ID 함정)
//  임시 조명 TempLight (4단계에서 교체). 정적 플래그 없음 → 베이크 영향 없음 (4단계에서 정적 + 베이크)
//  로그 Logs/pyrite_bedroom.txt, 렌더 Assets/_preview/bedroom/*.jpg (960x540)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomBuild
{
    public static readonly Vector3 ORIGIN = new Vector3(2000f, 0f, 0f);
    public const float R = 3.0f;       // 바닥 반지름
    public const float H = 3.5f;       // 꼭대기 높이
    const string DIR = "Assets/Bedroom";
    const string LOG = "Logs/pyrite_bedroom.txt";
    const string TENT = "Camp/camp01_tent_BRN";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z49b. Bedroom Shell Build", false, 4901)]
    public static void Build()
    {
        sb = new StringBuilder("[Z49b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49c. Bedroom Revert", false, 4902)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z49c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        foreach (var n in new[] { "TentBedroom", "TentDoor" }) { var g = RootByName(n); if (g != null) { Object.DestroyImmediate(g); sb.AppendLine(n + " 삭제"); } }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (Assets/Bedroom 에셋은 남김)");
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49d. Bedroom Renders", false, 4903)]
    public static void RenderOnly()
    {
        sb = new StringBuilder("[Z49d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static GameObject RootByName(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

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
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Ctrl+R 후 Z49b 다시");
        return false;
    }

    static bool Inner()
    {
        if (!EnsureProgram("PyriteTeleportDoor")) return false;
        PyriteSpawnAudit.CompileErrors(sb);
        var tentT = GameObject.Find(TENT);
        if (tentT == null) { sb.AppendLine("!! 텐트 없음 " + TENT); return false; }
        var tentR = tentT.GetComponent<Renderer>();

        foreach (var n in new[] { "TentBedroom", "TentDoor" }) { var g = RootByName(n); if (g != null) { Object.DestroyImmediate(g); sb.AppendLine("이전 " + n + " 삭제"); } }
        Directory.CreateDirectory(DIR + "/Meshes");

        var mCanvas = Mat("M_TentCanvas", new Color(0.62f, 0.47f, 0.31f), 0.15f, 0f);
        var mFloor = Mat("M_TentFloor", new Color(0.22f, 0.20f, 0.17f), 0.10f, 0f);
        var mPole = Mat("M_TentPole", new Color(0.18f, 0.18f, 0.19f), 0.55f, 0.6f);
        var mDoor = Mat("M_TentDoorFlap", new Color(0.45f, 0.33f, 0.21f), 0.12f, 0f);

        var root = new GameObject("TentBedroom"); Undo.RegisterCreatedObjectUndo(root, "bedroom");
        root.transform.position = ORIGIN;

        // 돔 (안쪽을 보는 면, 그림자는 양면 → 해·달 직사광을 막는다)
        var dome = Part(root.transform, "Dome", SaveMesh(DomeMesh(48, 16), "TentDome"), mCanvas);
        dome.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        dome.AddComponent<MeshCollider>().sharedMesh = dome.GetComponent<MeshFilter>().sharedMesh;

        // 바닥
        var floor = Part(root.transform, "Floor", SaveMesh(DiscMesh(48), "TentFloor"), mFloor);
        var fc = floor.AddComponent<BoxCollider>(); fc.center = new Vector3(0, -0.05f, 0); fc.size = new Vector3(2 * R + 0.4f, 0.1f, 2 * R + 0.4f);

        // X자 폴 2개 (방위 45° / 135° 평면의 반타원, 돔 안쪽 3 cm)
        var poleMesh = SaveMesh(ArchTube(0.985f, 0.02f, 40, 8), "TentPoleArch");
        foreach (var yaw in new[] { 45f, 135f })
        {
            var p = Part(root.transform, "Pole_" + yaw.ToString("0"), poleMesh, mPole);
            p.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 입구 천막 (−Z): 돔 곡면에 붙인 패널, 누르면 캠프로
        var door = Part(root.transform, "DoorFlap", SaveMesh(DoorMesh(1.1f, 1.9f), "TentDoorFlap"), mDoor);
        var dcol = door.AddComponent<BoxCollider>(); dcol.isTrigger = true;
        dcol.center = new Vector3(0, 0.95f, -R + 0.35f); dcol.size = new Vector3(1.2f, 1.9f, 0.5f);

        // Spawn: 입구 안쪽 1 m, +Z(가운데·창 쪽)를 봄
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root.transform, false);
        spawn.localPosition = new Vector3(0, 0.02f, -R + 1.0f); spawn.localRotation = Quaternion.identity;

        // 임시 조명 (4단계에서 교체)
        var tl = new GameObject("TempLight"); tl.transform.SetParent(root.transform, false); tl.transform.localPosition = new Vector3(0, 2.3f, 0);
        var L = tl.AddComponent<Light>(); L.type = LightType.Point; L.color = new Color(1f, 0.72f, 0.45f); L.intensity = 1.2f; L.range = 6f; L.shadows = LightShadows.None;
        L.lightmapBakeType = LightmapBakeType.Realtime;

        // 캠프 쪽: 귀환 지점 + 텐트 트리거
        var b = tentR.bounds;
        var fire = new Vector3(-10.5f, 0f, 51.5f);
        var toFire = fire - b.center; toFire.y = 0; toFire.Normalize();
        var ret = b.center + toFire * (Mathf.Max(b.extents.x, b.extents.z) + 1.2f);
        ret.y = Ground(ret) + 0.02f;
        var doorRoot = new GameObject("TentDoor"); Undo.RegisterCreatedObjectUndo(doorRoot, "tent door");
        doorRoot.transform.position = b.center;
        var tc = doorRoot.AddComponent<BoxCollider>(); tc.isTrigger = true; tc.size = b.size + new Vector3(0.1f, 0.1f, 0.1f);
        var campReturn = new GameObject("CampReturn").transform; campReturn.SetParent(doorRoot.transform, true);
        campReturn.position = ret; campReturn.rotation = Quaternion.LookRotation(toFire, Vector3.up);

        // Udon (새 오브젝트에만)
        var toRoom = UdonSharpUndo.AddComponent<PyriteTeleportDoor>(doorRoot);
        toRoom.target = spawn; UdonSharpEditorUtility.CopyProxyToUdon(toRoom); EditorUtility.SetDirty(toRoom);
        var ub1 = UdonSharpEditorUtility.GetBackingUdonBehaviour(toRoom); if (ub1 != null) { ub1.interactText = "Enter tent"; ub1.proximity = 3f; EditorUtility.SetDirty(ub1); }
        var toCamp = UdonSharpUndo.AddComponent<PyriteTeleportDoor>(door);
        toCamp.target = campReturn; UdonSharpEditorUtility.CopyProxyToUdon(toCamp); EditorUtility.SetDirty(toCamp);
        var ub2 = UdonSharpEditorUtility.GetBackingUdonBehaviour(toCamp); if (ub2 != null) { ub2.interactText = "Leave tent"; ub2.proximity = 2.5f; EditorUtility.SetDirty(ub2); }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        // 리포트
        int tris = root.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh ? m.sharedMesh.triangles.Length / 3 : 0);
        sb.AppendLine("TentBedroom at " + V(ORIGIN) + " | dome R " + R + " H " + H + " | tris " + tris);
        sb.AppendLine("spawn " + V(spawn.position) + " yaw " + spawn.eulerAngles.y.ToString("0"));
        sb.AppendLine("tent bounds " + V(b.min) + " .. " + V(b.max) + " → TentDoor trigger size " + V(tc.size));
        sb.AppendLine("CampReturn " + V(campReturn.position) + " yaw " + campReturn.eulerAngles.y.ToString("0") + " (불 쪽) | 지면 " + Ground(ret).ToString("F2"));
        var hits = Physics.OverlapSphere(campReturn.position + Vector3.up * 0.9f, 0.4f).Where(h => !h.isTrigger).Select(h => h.name).ToArray();
        sb.AppendLine("CampReturn 주변 0.4 m 고체 콜라이더: " + (hits.Length == 0 ? "없음" : string.Join(", ", hits)));
        sb.AppendLine("udon toRoom " + (ub1 != null) + " toCamp " + (ub2 != null));
        return true;
    }

    // ── 메시 ─────────────────────────────────────────
    static Mesh DomeMesh(int seg, int rings)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int j = 0; j <= rings; j++)
        {
            float phi = (Mathf.PI / 2f) * j / rings;
            for (int i = 0; i <= seg; i++)
            {
                float th = 2f * Mathf.PI * i / seg;
                var p = new Vector3(R * Mathf.Cos(phi) * Mathf.Cos(th), H * Mathf.Sin(phi), R * Mathf.Cos(phi) * Mathf.Sin(th));
                v.Add(p);
                var g = new Vector3(p.x / (R * R), p.y / (H * H), p.z / (R * R)); // 바깥 방향 → 안쪽 법선
                n.Add(-g.normalized);
                uv.Add(new Vector2((float)i / seg * 6f, (float)j / rings * 2f));
            }
        }
        int w = seg + 1;
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                t.AddRange(new[] { a, b, c, b, d, c }); // 안쪽에서 보이는 감김
            }
        return Build("TentDome", v, n, uv, t);
    }

    static Mesh DiscMesh(int seg)
    {
        var v = new List<Vector3> { Vector3.zero }; var n = new List<Vector3> { Vector3.up }; var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) }; var t = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float th = 2f * Mathf.PI * i / seg;
            v.Add(new Vector3(R * Mathf.Cos(th), 0, R * Mathf.Sin(th))); n.Add(Vector3.up);
            uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(th), 0.5f + 0.5f * Mathf.Sin(th)));
        }
        for (int i = 1; i <= seg; i++) t.AddRange(new[] { 0, i + 1, i });
        return Build("TentFloor", v, n, uv, t);
    }

    // x 축 방향 반타원 (−R..R), 돔 표면 × k 안쪽
    static Mesh ArchTube(float k, float rad, int steps, int sides)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int s = 0; s <= steps; s++)
        {
            float a = Mathf.PI * s / steps;
            var c = new Vector3(R * k * Mathf.Cos(a), H * k * Mathf.Sin(a), 0);
            var tan = new Vector3(-R * Mathf.Sin(a), H * Mathf.Cos(a), 0).normalized;
            var nrm = Vector3.Cross(tan, Vector3.forward).normalized;
            for (int i = 0; i <= sides; i++)
            {
                float b = 2f * Mathf.PI * i / sides;
                var d = nrm * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                v.Add(c + d * rad); n.Add(d); uv.Add(new Vector2((float)i / sides, (float)s / steps));
            }
        }
        int w = sides + 1;
        for (int s = 0; s < steps; s++)
            for (int i = 0; i < sides; i++)
            {
                int a = s * w + i, b = a + 1, c = a + w, d = c + 1;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
        return Build("TentPoleArch", v, n, uv, t);
    }

    // −Z 쪽 돔 곡면을 따라 붙인 아치형 천막 패널 (폭 wd, 높이 ht), 안쪽 1.5 cm
    static Mesh DoorMesh(float wd, float ht)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int cols = 12, rows = 16;
        for (int j = 0; j <= rows; j++)
        {
            float y = ht * j / rows;
            float half = wd * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Max(0f, (y - ht * 0.55f) / (ht * 0.45f)), 2f))); // 위쪽이 둥근 아치
            for (int i = 0; i <= cols; i++)
            {
                float x = Mathf.Lerp(-half, half, (float)i / cols);
                float rr = R * Mathf.Sqrt(Mathf.Max(0.0001f, 1f - (y * y) / (H * H))); // 그 높이의 돔 반지름
                float z = -Mathf.Sqrt(Mathf.Max(0.0001f, rr * rr - x * x)) + 0.015f;
                var p = new Vector3(x, y, z);
                v.Add(p);
                var g = new Vector3(p.x / (R * R), p.y / (H * H), p.z / (R * R));
                n.Add(-g.normalized);
                uv.Add(new Vector2((float)i / cols, (float)j / rows));
            }
        }
        int w = cols + 1;
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
        return Build("TentDoorFlap", v, n, uv, t);
    }

    static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
    {
        // 감김 방향을 법선에 맞춘다 (Unity 앞면: cross(b-a, c-a) 가 보는 쪽)
        double agree = 0;
        for (int i = 0; i < t.Count; i += 3)
        {
            var fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            agree += Vector3.Dot(fn, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]);
        }
        if (agree < 0) for (int i = 0; i < t.Count; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
        if (sb != null) sb.AppendLine("  mesh " + name + " verts " + v.Count + " tris " + t.Count / 3 + (agree < 0 ? " (감김 뒤집음)" : ""));
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        Unwrapping.GenerateSecondaryUVSet(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + "/Meshes/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(m, old); EditorUtility.SetDirty(old); AssetDatabase.SaveAssets(); return old; }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material Mat(string name, Color c, float gloss, float metal)
    {
        string path = DIR + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Part(Transform parent, string name, Mesh mesh, Material mat)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    static float Ground(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        return t ? t.SampleHeight(p) + t.transform.position.y : 1.81f;
    }

    // ── 확인 렌더 (JPG 960x540) ─────────────────────
    static void Renders()
    {
        var root = RootByName("TentBedroom"); if (root == null) { sb.AppendLine("TentBedroom 없음"); return; }
        Directory.CreateDirectory("Assets/_preview/bedroom/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            cam.fieldOfView = 75f;
            var o = ORIGIN;
            Shot(cam, o + new Vector3(0, 1.6f, -R + 1.0f), o + new Vector3(0, 1.3f, R), "in_spawn");          // 스폰에서 안쪽
            Shot(cam, o + new Vector3(0, 1.6f, 1.5f), o + new Vector3(0, 1.0f, -R), "in_door");              // 입구 천막
            Shot(cam, o + new Vector3(1.8f, 0.4f, 0.8f), o + new Vector3(-0.5f, 2.6f, -0.3f), "in_lie");     // 누운 시선(천장·폴)
            var tent = GameObject.Find(TENT); var td = RootByName("TentDoor");
            if (tent != null && td != null)
            {
                var ret = td.transform.Find("CampReturn");
                var c = tent.GetComponent<Renderer>().bounds.center;
                cam.fieldOfView = 60f;
                Shot(cam, ret.position + Vector3.up * 1.6f - ret.forward * 0.1f, c, "camp_return");            // 귀환 지점에서 텐트
            }
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            cam.fieldOfView = 75f;
            Shot(cam, o + new Vector3(0, 1.6f, -R + 1.0f), o + new Vector3(0, 1.3f, R), "in_spawn_noon");    // 정오: 햇빛이 새는지
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        sb.AppendLine("shots Assets/_preview/bedroom/{in_spawn,in_door,in_lie,camp_return,in_spawn_noon}.jpg");
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
        var px = tex.GetPixels32(); double sum = 0; foreach (var p in px) sum += (p.r + p.g + p.b) / 3.0;
        File.WriteAllBytes("Assets/_preview/bedroom/" + tag + ".jpg", tex.EncodeToJPG(80));
        sb.AppendLine("  shot " + tag + " mean " + (sum / px.Length).ToString("F1"));
        Object.DestroyImmediate(tex);
    }

    static string V(Vector3 v) { return "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")"; }
    static void Flush()
    {
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
