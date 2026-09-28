// PyriteBedroomBuild.cs — 텐트 침실 2단계: 빈 돔 + 텔레포트 왕복
// Tools ▸ Pyrite3 ▸ Z49b. Bedroom Shell Build  /  Z49c. Bedroom Revert  /  Z49d. Bedroom Renders
//  루트 TentBedroom (2000, 0, 0) — 직사각 돔 텐트(바닥 6.8×5.6 m 초타원 n=6, 꼭대기 3.3 m), 바닥, 대각 X자 폴 2개,
//    입구 천막(−X 짧은 벽, 귀환), Spawn(입구 안쪽, +X 를 봄). 앞 = +Z 긴 벽(창 자리), 뒤 = −Z 긴 벽(매트 머리)
//  v1(원형 지름 6 m, 15:36)은 관리자 "너무 동그랗다" → v2 직사각 (15:53)
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
    public const float A = 3.4f;       // 바닥 반폭 x (6.8 m)
    public const float B = 2.8f;       // 바닥 반폭 z (5.6 m)
    public const float H = 3.3f;       // 꼭대기 높이
    public const float N = 6f;         // 초타원 지수 (클수록 각짐)
    // 매트 자리 안내(3단계에서 실제 매트): 2.2×0.9 m, 간격 0.6, 머리 z −2.4, 발 z −0.2
    public const float DOOR_Z = 1.3f;  // 입구는 짧은 벽의 앞쪽 절반(매트 발끝 z 0 보다 앞, 통로 쪽)
    public static readonly float[] MAT_X = { -2.25f, -0.75f, 0.75f, 2.25f };
    public const float MAT_HEAD = -2.4f, MAT_LEN = 2.2f;
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
        // 이전 판 렌더 보존 (전후 비교)
        const string PV = "Assets/_preview/bedroom/";
        if (Directory.Exists(PV) && !Directory.Exists(PV + "v1") && Directory.GetFiles(PV, "*.jpg").Length > 0)
        {
            Directory.CreateDirectory(PV + "v1");
            foreach (var f in Directory.GetFiles(PV, "*.jpg")) File.Move(f, PV + "v1/" + Path.GetFileName(f));
            foreach (var f in Directory.GetFiles(PV, "*.jpg.meta")) File.Delete(f);
            sb.AppendLine("v1 렌더 → " + PV + "v1/");
        }

        var mCanvas = Mat("M_TentCanvas", new Color(0.62f, 0.47f, 0.31f), 0.15f, 0f);
        var mFloor = Mat("M_TentFloor", new Color(0.22f, 0.20f, 0.17f), 0.10f, 0f);
        var mPole = Mat("M_TentPole", new Color(0.18f, 0.18f, 0.19f), 0.55f, 0.6f);
        var mDoor = Mat("M_TentDoorFlap", new Color(0.45f, 0.33f, 0.21f), 0.12f, 0f);

        var root = new GameObject("TentBedroom"); Undo.RegisterCreatedObjectUndo(root, "bedroom");
        root.transform.position = ORIGIN;

        // 돔 (안쪽을 보는 면, 그림자는 양면 → 해·달 직사광을 막는다)
        var dome = Part(root.transform, "Dome", SaveMesh(DomeMesh(96, 20), "TentDome"), mCanvas);
        dome.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        dome.AddComponent<MeshCollider>().sharedMesh = dome.GetComponent<MeshFilter>().sharedMesh;

        // 바닥
        var floor = Part(root.transform, "Floor", SaveMesh(DiscMesh(96), "TentFloor"), mFloor);
        var fc = floor.AddComponent<BoxCollider>(); fc.center = new Vector3(0, -0.05f, 0); fc.size = new Vector3(2 * A + 0.4f, 0.1f, 2 * B + 0.4f);

        // 대각 X자 폴 2개 (모서리 → 꼭대기 → 반대 모서리, 표면 안쪽 3 cm)
        for (int k = 0; k < 2; k++)
        {
            float th = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
            var p = Part(root.transform, "Pole_" + (k == 0 ? "A" : "B"), SaveMesh(ArchTube(th, 0.03f, 0.02f, 48, 8), "TentPole" + (k == 0 ? "A" : "B")), mPole);
            p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 입구 천막 (−X 짧은 벽): 곡면에 붙인 패널, 누르면 캠프로
        var door = Part(root.transform, "DoorFlap", SaveMesh(DoorMesh(1.1f, 1.9f, DOOR_Z), "TentDoorFlap"), mDoor);
        var dcol = door.AddComponent<BoxCollider>(); dcol.isTrigger = true;
        dcol.center = new Vector3(-A + 0.35f, 0.95f, DOOR_Z); dcol.size = new Vector3(0.5f, 1.9f, 1.2f);

        // Spawn: 입구 안쪽 1 m, +X(방 안쪽)를 봄
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root.transform, false);
        spawn.localPosition = new Vector3(-A + 1.0f, 0.02f, DOOR_Z); spawn.localRotation = Quaternion.Euler(0, 90, 0);

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
        sb.AppendLine("TentBedroom at " + V(ORIGIN) + " | floor " + (2 * A) + "x" + (2 * B) + " m (n " + N + ") H " + H + " | 바닥 넓이 " + FloorArea().ToString("F1") + " m² | tris " + tris);
        foreach (var mx in MAT_X) { float zb = SurfZ(Mathf.Abs(mx) + 0.45f, 0.25f); sb.AppendLine("  매트 x " + mx.ToString("F3") + " : 바깥 모서리 높이 0.25 m 에서 뒤 벽 z −" + zb.ToString("F2") + " (머리 " + MAT_HEAD.ToString("F2") + " → 여유 " + (zb + MAT_HEAD).ToString("F2") + " m)"); }
        sb.AppendLine("spawn " + V(spawn.position) + " yaw " + spawn.eulerAngles.y.ToString("0"));
        sb.AppendLine("tent bounds " + V(b.min) + " .. " + V(b.max) + " → TentDoor trigger size " + V(tc.size));
        sb.AppendLine("CampReturn " + V(campReturn.position) + " yaw " + campReturn.eulerAngles.y.ToString("0") + " (불 쪽) | 지면 " + Ground(ret).ToString("F2"));
        var hits = Physics.OverlapSphere(campReturn.position + Vector3.up * 0.9f, 0.4f).Where(h => !h.isTrigger).Select(h => h.name).ToArray();
        sb.AppendLine("CampReturn 주변 0.4 m 고체 콜라이더: " + (hits.Length == 0 ? "없음" : string.Join(", ", hits)));
        sb.AppendLine("udon toRoom " + (ub1 != null) + " toCamp " + (ub2 != null));
        return true;
    }

    // ── 메시 ─────────────────────────────────────────
    // 초타원 방향 (각진 원): θ → (cx, cz), |cx|^N + |cz|^N = 1
    static Vector2 SE(float th)
    {
        float c = Mathf.Cos(th), s = Mathf.Sin(th);
        return new Vector2(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / N), Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / N));
    }
    // 돔 표면 점: θ(방위), φ(0 바닥 → π/2 꼭대기). 단면은 타원 → 벽은 바닥에서 거의 수직
    static Vector3 Surf(float th, float phi)
    {
        var d = SE(th); float r = Mathf.Cos(phi);
        return new Vector3(A * d.x * r, H * Mathf.Sin(phi), B * d.y * r);
    }
    // 높이 y 에서 x 위치의 벽 z (양수)
    public static float SurfZ(float x, float y)
    {
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - (y * y) / (H * H)));
        return B * Mathf.Pow(Mathf.Max(0f, Mathf.Pow(r, N) - Mathf.Pow(Mathf.Abs(x) / A, N)), 1f / N);
    }
    public static float SurfX(float z, float y)
    {
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - (y * y) / (H * H)));
        return A * Mathf.Pow(Mathf.Max(0f, Mathf.Pow(r, N) - Mathf.Pow(Mathf.Abs(z) / B, N)), 1f / N);
    }
    static float FloorArea()
    {
        float sum = 0; int n = 720;
        for (int i = 0; i < n; i++) { var a = SE(2 * Mathf.PI * i / n); var b = SE(2 * Mathf.PI * (i + 1) / n); sum += 0.5f * (A * a.x * B * b.y - A * b.x * B * a.y); }
        return Mathf.Abs(sum);
    }
    static readonly Vector3 INSIDE = new Vector3(0, H * 0.3f, 0);

    static Mesh DomeMesh(int seg, int rings)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int j = 0; j <= rings; j++)
        {
            float phi = (Mathf.PI / 2f) * j / rings;
            for (int i = 0; i <= seg; i++)
            {
                float th = 2f * Mathf.PI * i / seg;
                var p = Surf(th, phi);
                v.Add(p); n.Add((INSIDE - p).normalized);   // 감김 판정용 (안쪽), 음영 법선은 Build 에서 다시 계산
                uv.Add(new Vector2((float)i / seg * 8f, (float)j / rings * 2f));
            }
        }
        int w = seg + 1;
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                t.AddRange(new[] { a, b, c, b, d, c });
            }
        return Build("TentDome", v, n, uv, t, true, seg + 1);
    }

    static Mesh DiscMesh(int seg)
    {
        var v = new List<Vector3> { Vector3.zero }; var n = new List<Vector3> { Vector3.up }; var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) }; var t = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            var d = SE(2f * Mathf.PI * i / seg);
            v.Add(new Vector3(A * d.x, 0, B * d.y)); n.Add(Vector3.up);
            uv.Add(new Vector2(0.5f + 0.5f * d.x * A / 3f, 0.5f + 0.5f * d.y * B / 3f));
        }
        for (int i = 1; i <= seg; i++) t.AddRange(new[] { 0, i + 1, i });
        return Build("TentFloor", v, n, uv, t, false, 0);
    }

    // 모서리(θ) → 꼭대기 → 반대 모서리(θ+π), 표면에서 inset 만큼 안쪽
    static Mesh ArchTube(float th, float inset, float rad, int steps, int sides)
    {
        var path = new List<Vector3>();
        for (int s = 0; s <= steps; s++)
        {
            float u = (float)s / steps;           // 0 → 1
            float phi = u < 0.5f ? Mathf.PI * u : Mathf.PI * (1f - u);
            float tt = u < 0.5f ? th : th + Mathf.PI;
            var p = Surf(tt, phi);
            p += (INSIDE - p).normalized * inset;
            path.Add(p);
        }
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int s = 0; s < path.Count; s++)
        {
            var tan = (path[Mathf.Min(s + 1, path.Count - 1)] - path[Mathf.Max(s - 1, 0)]).normalized;
            var side = Vector3.Cross(tan, Vector3.up); if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(tan, Vector3.forward); side.Normalize();
            var nrm = Vector3.Cross(side, tan).normalized;
            for (int i = 0; i <= sides; i++)
            {
                float b = 2f * Mathf.PI * i / sides;
                var d = nrm * Mathf.Cos(b) + side * Mathf.Sin(b);
                v.Add(path[s] + d * rad); n.Add(d); uv.Add(new Vector2((float)i / sides, (float)s / steps));
            }
        }
        int w = sides + 1;
        for (int s = 0; s < steps; s++)
            for (int i = 0; i < sides; i++)
            {
                int a = s * w + i, b = a + 1, c = a + w, d = c + 1;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
        return Build("TentPole", v, n, uv, t, false, 0);
    }

    // −X 짧은 벽 곡면을 따라 붙인 아치형 천막 패널 (폭 wd, 높이 ht), 안쪽 1.5 cm
    static Mesh DoorMesh(float wd, float ht, float zc)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int cols = 12, rows = 16;
        for (int j = 0; j <= rows; j++)
        {
            float y = ht * j / rows;
            float half = wd * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Max(0f, (y - ht * 0.55f) / (ht * 0.45f)), 2f)));
            for (int i = 0; i <= cols; i++)
            {
                float z = zc + Mathf.Lerp(-half, half, (float)i / cols);
                var p = new Vector3(-SurfX(z, y) + 0.015f, y, z);
                v.Add(p); n.Add(Vector3.right);
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
        return Build("TentDoorFlap", v, n, uv, t, true, 0);
    }

    static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, bool recalc, int seamW)
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
        if (recalc)
        {
            m.RecalculateNormals();
            if (seamW > 1)   // θ=0 / 2π 이음매 법선 평균 (같은 위치 두 정점)
            {
                var nn = m.normals;
                for (int k = 0; k + seamW - 1 < nn.Length; k += seamW) { var avg = (nn[k] + nn[k + seamW - 1]).normalized; nn[k] = avg; nn[k + seamW - 1] = avg; }
                m.normals = nn;
            }
        }
        m.RecalculateBounds(); m.RecalculateTangents();
        Unwrapping.GenerateSecondaryUVSet(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + "/Meshes/" + name + ".asset";
        // 🔴 CopySerialized 로 덮으면 정점이 안 바뀌었다(v2 첫 빌드가 v1 원형 메시 그대로) → 지우고 새로 만든다 (씬 오브젝트는 매번 새로 만드니 참조 문제 없음)
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
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
            Shot(cam, o + new Vector3(-A + 1.0f, 1.6f, DOOR_Z), o + new Vector3(A, 1.2f, -0.3f), "in_spawn");                 // 입구 안쪽에서 방 안
            Shot(cam, o + new Vector3(0, 1.6f, -1.2f), o + new Vector3(0, 1.1f, B), "in_front");                     // 뒤에서 앞 긴 벽(창 자리)
            Shot(cam, o + new Vector3(0.3f, 1.6f, 1.8f), o + new Vector3(0, 0.6f, -B), "in_back");                    // 앞에서 뒤 벽(매트 머리 쪽)
            Shot(cam, o + new Vector3(MAT_X[1], 0.35f, MAT_HEAD + 0.3f), o + new Vector3(MAT_X[1], 1.3f, B), "in_lie");         // 매트에 누운 시선(발끝 = 창 쪽)
            Shot(cam, o + new Vector3(1.5f, 1.6f, DOOR_Z), o + new Vector3(-A, 1.0f, DOOR_Z), "in_door");                       // 입구 천막
            // 위에서: 돔을 끄고 매트 자리 안내판을 잠깐 깔아 찍는다
            var dome = root.transform.Find("Dome").gameObject; dome.SetActive(false);
            var guides = new List<GameObject>();
            var gm = new Material(Shader.Find("Unlit/Color")); gm.color = new Color(0.25f, 0.55f, 0.85f);
            foreach (var mx in MAT_X)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube); q.transform.SetParent(root.transform, false);
                q.transform.localPosition = new Vector3(mx, 0.12f, MAT_HEAD + MAT_LEN / 2f); q.transform.localScale = new Vector3(0.9f, 0.24f, MAT_LEN);
                q.GetComponent<Renderer>().sharedMaterial = gm; Object.DestroyImmediate(q.GetComponent<Collider>()); guides.Add(q);
            }
            bool o0 = cam.orthographic; float s0 = cam.orthographicSize;
            cam.orthographic = true; cam.orthographicSize = 3.6f;
            Shot(cam, o + new Vector3(0, 30f, 0.001f), o, "top_mats");
            cam.orthographic = o0; cam.orthographicSize = s0;
            foreach (var g in guides) Object.DestroyImmediate(g);
            Object.DestroyImmediate(gm);
            dome.SetActive(true);
            var tent = GameObject.Find(TENT); var td = RootByName("TentDoor");
            if (tent != null && td != null)
            {
                var ret = td.transform.Find("CampReturn");
                var c = tent.GetComponent<Renderer>().bounds.center;
                cam.fieldOfView = 60f;
                Shot(cam, ret.position + Vector3.up * 1.6f - ret.forward * 0.1f, c, "camp_return");
            }
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            cam.fieldOfView = 75f;
            Shot(cam, o + new Vector3(-A + 1.0f, 1.6f, DOOR_Z), o + new Vector3(A, 1.2f, -0.3f), "in_spawn_noon");
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        sb.AppendLine("shots Assets/_preview/bedroom/{in_spawn,in_front,in_back,in_lie,in_door,top_mats,camp_return,in_spawn_noon}.jpg (v1 은 v1/)");
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        var fwd = at - eye; cam.transform.SetPositionAndRotation(eye, Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.99f ? Quaternion.LookRotation(fwd, Vector3.forward) : Quaternion.LookRotation(fwd));
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
