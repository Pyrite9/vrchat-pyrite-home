// Tools ▸ Pyrite2 ▸ Z31b. Build Camp Props / Z31c. Camp Props Revert / Z31d. Camp Props Renders
//  캠프 소품 6종을 절차적 메시로 만들어 새 루트 CampProps 아래에 둔다 (유료 에셋 안 씀 → 저장소에 들어가도 된다)
//   - 마시멜로 꼬치 3개 + 통나무 꽂이 : 테이블 왼쪽(+X, 의자에 앉아 불을 볼 때 왼쪽)
//   - 가스 캔 스토브 + 주전자 : 커진 테이블 위 오른쪽(-X), 주전자는 스토브 위 (나무 상자는 폐기)
//   - 법랑 머그 2개 : 테이블 가운데 의자 쪽 (프로젝터 둘은 불 쪽 절반에 있다), 여섯 모금
//  들었을 때 방향: 손 방향 대신 스크립트가 Visual(피벗 = 손잡이)을 세운다 — 랜턴과 같은 방식
//   - 망원경 : 화로 오른쪽(-X) 3 m
//   - 돗자리 : 타프 밑, 야전침대 앞 (Z31a 실측: 침대와 겹치지 않게), 녹색 단색, 눕기 두 자리
//   - 테이블 콜라이더 (camp03_table 에는 콜라이더가 없어 떨어뜨린 머그가 바닥까지 빠진다)
//  조작: VRChat 데스크톱은 든 채 우클릭 = 놓기(클라이언트 고정) → 먹기·따르기·마시기·스토브 올리기는 좌클릭(사용)
//  재실행 안전: CampProps 를 지우고 다시 만든다. Z31c 는 CampProps 만 지운다(생성 에셋은 Assets/Props 에 남김)
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
using VRC.SDK3.Components;
using VRC.SDKBase;

public static class PyriteCampProps
{
    const string DIR = "Assets/Props";
    const string ROOT = "CampProps";
    const int PICKUP_LAYER = 13;
    static readonly Vector3 FIRE = new Vector3(-10.5f, 2.15f, 51.5f);
    // 테이블: camp03_table 을 가로(로컬 X) 1.6배·세로(로컬 Z) 1.5배로 키운다(높이 그대로 → 위의 프로젝터 둘은 안 움직인다)
    //  원래 상판 (Z31a 실측): x -11.04..-10.17, z 52.64..53.09, y 2.187 → 빌드 때 메시 정점으로 다시 잰다
    const float TABLE_SX = 1.6f, TABLE_SZ = 1.5f;
    const string TABLE_REVERT = "Logs/pyrite_table_revert.txt";
    static float T_X0 = -11.04f, T_X1 = -10.17f, T_Z0 = 52.64f, T_Z1 = 53.09f, T_TOP = 2.187f;
    static readonly Vector3 RACK = new Vector3(-9.72f, 0f, 52.87f);        // 커진 테이블(x 끝 -9.91) 왼쪽
    static readonly Vector3 STOVE = new Vector3(-11.14f, 0f, 52.97f);      // 테이블 위 오른쪽 끝 (영상 프로젝터 x -10.98..-10.75, z 52.63..52.84 와 안 겹침)
    static readonly Vector3[] MUGS = { new Vector3(-10.30f, 0f, 53.00f), new Vector3(-10.08f, 0f, 52.96f) };
    static readonly float[] MUG_YAW = { -70f, -110f };
    static readonly Vector3 SCOPE = new Vector3(-13.6f, 0f, 51.6f);
    static readonly Vector3 MAT = new Vector3(-8.3f, 0f, 56.62f);   // 야전침대(x -9.25..-7.35, z 57.69..58.31) 앞, 타프 앞 끝(z≈55.7) 안쪽
    const float MAT_W = 1.9f, MAT_D = 1.3f;

    static StringBuilder sb;

    class Part
    {
        public Mesh mesh; public Matrix4x4 m; public Material mat;
        public Part(Mesh a, Matrix4x4 b, Material c) { mesh = a; m = b; mat = c; }
    }

    // ───────────────────────── 메뉴 ─────────────────────────
    [MenuItem("Tools/Pyrite2/Z31b. Build Camp Props %&#9", false, 51)]   // Ctrl+Alt+Shift+9 (Alt+Shift+9 는 E 와 충돌) — 메뉴가 캡처에서 가려질 때 단축키로
    public static void Build()
    {
        sb = new StringBuilder("[Z31b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { BuildInner(); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite2/Z31c. Camp Props Revert", false, 52)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z31c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = FindRoot();
        if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("CampProps 삭제"); }
        else sb.AppendLine("CampProps 없음");
        var table = GameObject.Find("Camp/camp03_table");
        if (table != null && File.Exists(TABLE_REVERT))
        {
            table.transform.localScale = ParseV(File.ReadAllText(TABLE_REVERT));
            File.Delete(TABLE_REVERT);
            sb.AppendLine("table scale 복구 " + table.transform.localScale.ToString("F3") + " (라이트맵 재베이크 F → T 필요)");
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush();
    }

    [MenuItem("Tools/Pyrite2/Z31d. Camp Props Renders", false, 53)]
    public static void RendersMenu()
    {
        sb = new StringBuilder("[Z31d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static GameObject FindRoot()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == ROOT);
    }

    // ───────────────────────── 빌드 ─────────────────────────
    static void BuildInner()
    {
        foreach (var d in new[] { DIR, DIR + "/Meshes", DIR + "/Materials", DIR + "/Textures" })
            if (!AssetDatabase.IsValidFolder(d)) AssetDatabase.CreateFolder(Path.GetDirectoryName(d).Replace('\\', '/'), Path.GetFileName(d));

        var old = FindRoot();
        if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("이전 CampProps 지움"); }
        var root = new GameObject(ROOT);

        // 재질
        var mWood = Mat("M_PropWood", new Color(0.50f, 0.33f, 0.19f), 0f, 0.30f);
        var mWoodDark = Mat("M_PropWoodDark", new Color(0.30f, 0.20f, 0.12f), 0f, 0.25f);
        var mSteel = Mat("M_PropSteel", new Color(0.74f, 0.74f, 0.76f), 0.9f, 0.70f);
        var mBlack = Mat("M_PropBlack", new Color(0.05f, 0.05f, 0.055f), 0.4f, 0.55f);
        var mMarsh = Mat("M_PropMarsh", new Color(0.97f, 0.95f, 0.91f), 0f, 0.15f);
        var mCopper = Mat("M_PropStainless", new Color(0.80f, 0.81f, 0.82f), 0.92f, 0.58f);   // 주전자: 관리자 요청으로 구리 → 스테인리스 (헤어라인 느낌으로 광택 조금 낮춤)
        var mBrass = Mat("M_PropBrass", new Color(0.80f, 0.62f, 0.32f), 1f, 0.68f);
        var mNavy = Mat("M_PropEnamelNavy", new Color(0.10f, 0.17f, 0.30f), 0f, 0.78f);
        var mCream = Mat("M_PropEnamelCream", new Color(0.90f, 0.85f, 0.72f), 0f, 0.78f);
        var mCoffee = Mat("M_PropCoffee", new Color(0.16f, 0.08f, 0.035f), 0f, 0.92f);
        var mGlass = Mat("M_PropLens", new Color(0.04f, 0.07f, 0.11f), 0.2f, 0.95f);
        var mFlame = Mat("M_PropFlame", Color.black, 0f, 0.2f, new Color(0.35f, 0.62f, 1.6f) * 2.2f);
        var mSteam = SteamMat();
        var mMat = MatTex("M_PropMat", MatTexture());

        // 0) 테이블 키우기 (원래 스케일은 한 번만 기록)
        {
            var table = GameObject.Find("Camp/camp03_table");
            if (table == null) { sb.AppendLine("Camp/camp03_table 없음"); return; }
            Vector3 orig;
            if (File.Exists(TABLE_REVERT)) orig = ParseV(File.ReadAllText(TABLE_REVERT));
            else { orig = table.transform.localScale; Directory.CreateDirectory("Logs"); File.WriteAllText(TABLE_REVERT, V3(orig)); }
            table.transform.localScale = new Vector3(orig.x * TABLE_SX, orig.y, orig.z * TABLE_SZ);
            EditorUtility.SetDirty(table.transform);
            var vs = table.GetComponentsInChildren<MeshFilter>().SelectMany(mf => mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v))).ToArray();
            float top = vs.Max(v => v.y);
            var tv = vs.Where(v => v.y > top - 0.01f).ToArray();
            T_TOP = top; T_X0 = tv.Min(v => v.x); T_X1 = tv.Max(v => v.x); T_Z0 = tv.Min(v => v.z); T_Z1 = tv.Max(v => v.z);
            sb.AppendLine(string.Format("table scale {0} → {1} | top y {2:F3} x {3:F2}..{4:F2} z {5:F2}..{6:F2} ({7:F2}×{8:F2} m) — 라이트맵 재베이크 F → T 필요",
                orig.ToString("F3"), table.transform.localScale.ToString("F3"), T_TOP, T_X0, T_X1, T_Z0, T_Z1, T_X1 - T_X0, T_Z1 - T_Z0));
        }

        // 1) 테이블 콜라이더
        var tc = new GameObject("TableCollider");
        tc.transform.SetParent(root.transform, false);
        var tcb = tc.AddComponent<BoxCollider>();
        tc.transform.position = new Vector3((T_X0 + T_X1) * 0.5f, 0f, (T_Z0 + T_Z1) * 0.5f);
        float tg = Ground(tc.transform.position);
        tcb.center = new Vector3(0f, (tg + T_TOP) * 0.5f, 0f);
        tcb.size = new Vector3(T_X1 - T_X0, T_TOP - tg, T_Z1 - T_Z0);
        sb.AppendLine(string.Format("table collider: ground {0:F3} top {1:F3} size {2}", tg, T_TOP, tcb.size.ToString("F3")));

        var pkTemplate = GameObject.Find("Camp/Lantern_Carry")?.GetComponent<VRCPickup>();
        sb.AppendLine("pickup template: " + (pkTemplate != null ? "Lantern_Carry" : "없음(기본값)"));

        // 2) 마시멜로 꽂이 + 꼬치 3개
        var rack = new GameObject("MarshmallowRack");
        rack.transform.SetParent(root.transform, false);
        rack.transform.position = new Vector3(RACK.x, Ground(RACK), RACK.z);
        var logProf = new[] { V(0, 0), V(0.075f, 0), V(0.078f, 0.01f), V(0.078f, 0.11f), V(0.072f, 0.12f), V(0, 0.12f) };
        MeshOn(rack, "Rack", new List<Part> { new Part(Lathe(logProf, 20), Matrix4x4.identity, mWoodDark) });
        var rb0 = rack.AddComponent<BoxCollider>(); rb0.center = new Vector3(0, 0.06f, 0); rb0.size = new Vector3(0.156f, 0.12f, 0.156f);
        var slots = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            float a = (90f + i * 120f) * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var dir = (Vector3.up * Mathf.Cos(12f * Mathf.Deg2Rad) + radial * Mathf.Sin(12f * Mathf.Deg2Rad)).normalized;
            var s = new GameObject("Slot_" + i).transform;
            s.SetParent(rack.transform, false);
            s.localPosition = new Vector3(0, 0.12f, 0) + radial * 0.035f - dir * 0.035f;
            s.localRotation = Quaternion.LookRotation(dir, radial);
            slots[i] = s;
        }
        var skewerMesh = Combine("Skewer", new List<Part> {
            new Part(Lathe(new[] { V(0, 0), V(0.011f, 0), V(0.012f, 0.01f), V(0.012f, 0.13f), V(0.009f, 0.14f), V(0, 0.14f) }, 12), Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)), mWood),
            new Part(Lathe(new[] { V(0, 0.13f), V(0.0028f, 0.13f), V(0.0028f, 0.755f), V(0.0012f, 0.765f), V(0, 0.766f) }, 8), Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)), mSteel),
        }, out var skewerMats);
        var marshMesh = SaveMesh(Lathe(new[] { V(0, -0.017f), V(0.014f, -0.017f), V(0.0175f, -0.013f), V(0.0175f, 0.013f), V(0.014f, 0.017f), V(0, 0.017f) }, 16), "Marshmallow");
        var fire = new GameObject("FirePoint").transform;
        fire.SetParent(root.transform, false); fire.position = FIRE;
        var skewers = new Transform[3];
        var skewerScripts = new PyriteSkewer[3];
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Skewer_" + i);
            go.layer = PICKUP_LAYER;
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(slots[i].position, slots[i].rotation);
            // 손 방향과 무관하게 꼬치 방향을 스크립트가 정한다 → 피벗 = 손잡이(Grip)
            var sVis = new GameObject("Visual").transform; sVis.SetParent(go.transform, false); sVis.localPosition = new Vector3(0, 0, 0.07f);
            var vis = new GameObject("Mesh"); vis.transform.SetParent(sVis, false); vis.transform.localPosition = new Vector3(0, 0, -0.07f);
            vis.AddComponent<MeshFilter>().sharedMesh = skewerMesh; vis.AddComponent<MeshRenderer>().sharedMaterials = skewerMats;
            var marsh = new GameObject("Marshmallow"); marsh.transform.SetParent(vis.transform, false);
            marsh.transform.localPosition = new Vector3(0, 0, 0.705f); marsh.transform.localRotation = Quaternion.Euler(90, 0, 0);
            marsh.AddComponent<MeshFilter>().sharedMesh = marshMesh;
            var mr = marsh.AddComponent<MeshRenderer>(); mr.sharedMaterial = mMarsh;
            var grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false); grip.localPosition = new Vector3(0, 0, 0.07f);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0, 0.40f); col.size = new Vector3(0.045f, 0.045f, 0.80f);
            var pk = Pickup(go, pkTemplate, grip, VRC_Pickup.PickupOrientation.Grip, "Marshmallow", "Eat / New");
            go.AddComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;
            var sk = UdonSharpUndo.AddComponent<PyriteSkewer>(go);
            var st = new GameObject("State"); st.transform.SetParent(go.transform, false);
            var ss = UdonSharpUndo.AddComponent<PyriteSkewerState>(st);
            ss.marsh = mr;
            UdonSharpEditorUtility.CopyProxyToUdon(ss);
            sk.state = ss; sk.tip = marsh.transform; sk.fire = fire; sk.slots = slots; sk.visual = sVis;
            skewers[i] = go.transform; skewerScripts[i] = sk;
        }
        foreach (var sk in skewerScripts) { sk.others = skewers; UdonSharpEditorUtility.CopyProxyToUdon(sk); EditorUtility.SetDirty(sk); }
        sb.AppendLine(string.Format("rack {0} | skewer tris {1} + marsh {2} | slot0 {3}", rack.transform.position.ToString("F2"), Tris(skewerMesh), Tris(marshMesh), slots[0].position.ToString("F3")));

        // 3) 가스 캔 스토브 (관리자 참고 이미지: 파란 캔 + 은색 밸브·버너 + 톱니 받침 4개 + 검은 노브) — 테이블 위
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var mCan = Mat("M_PropCanBlue", new Color(0.10f, 0.33f, 0.55f), 0.35f, 0.45f);
        var stove = new GameObject("Stove");
        stove.transform.SetParent(root.transform, false);
        stove.transform.SetPositionAndRotation(new Vector3(STOVE.x, T_TOP, STOVE.z), Quaternion.Euler(0f, 200f, 0f));
        var stoveParts = new List<Part> {
            // 캔: 은색 바닥 테 + 파란 몸통(어깨 둥글게) + 은색 윗테
            new Part(Lathe(new[] { V(0, 0), V(0.053f, 0), V(0.055f, 0.004f), V(0.055f, 0.010f), V(0.052f, 0.012f), V(0, 0.012f) }, 32), Matrix4x4.identity, mSteel),
            new Part(Lathe(new[] { V(0.052f, 0.010f), V(0.054f, 0.014f), V(0.054f, 0.118f), V(0.050f, 0.132f), V(0.038f, 0.142f), V(0.026f, 0.146f), V(0, 0.146f) }, 32), Matrix4x4.identity, mCan),
            new Part(Lathe(new[] { V(0.027f, 0.144f), V(0.029f, 0.150f), V(0.024f, 0.156f), V(0, 0.156f) }, 24), Matrix4x4.identity, mSteel),
            // 육각 너트 + 밸브 몸통 + 버너까지 기둥
            new Part(Lathe(new[] { V(0, 0.156f), V(0.023f, 0.156f), V(0.023f, 0.176f), V(0, 0.176f) }, 6), Matrix4x4.identity, mSteel),
            new Part(Lathe(new[] { V(0, 0.176f), V(0.014f, 0.176f), V(0.014f, 0.206f), V(0, 0.206f) }, 16), Matrix4x4.identity, mSteel),
            new Part(Lathe(new[] { V(0, 0.206f), V(0.009f, 0.206f), V(0.009f, 0.246f), V(0, 0.246f) }, 12), Matrix4x4.identity, mSteel),
            // 노브: 옆으로 나온 팔 + 검은 톱니 노브
            new Part(Lathe(new[] { V(0, 0), V(0.008f, 0), V(0.008f, 0.042f), V(0, 0.042f) }, 12), Matrix4x4.TRS(new Vector3(0.010f, 0.19f, 0), Quaternion.Euler(0, 0, -90), Vector3.one), mSteel),
            new Part(Lathe(new[] { V(0, 0), V(0.017f, 0), V(0.019f, 0.003f), V(0.019f, 0.020f), V(0.016f, 0.024f), V(0, 0.024f) }, 14), Matrix4x4.TRS(new Vector3(0.050f, 0.19f, 0), Quaternion.Euler(0, 0, -90), Vector3.one), mBlack),
            // 버너: 은색 컵 + 구멍 뚫린 윗면(어두운 원판) + 파란 불꽃 고리
            new Part(Lathe(new[] { V(0.009f, 0.244f), V(0.028f, 0.257f), V(0.036f, 0.261f), V(0.036f, 0.268f), V(0.031f, 0.272f), V(0, 0.272f) }, 28), Matrix4x4.identity, mSteel),
            new Part(Lathe(new[] { V(0.026f, 0.2725f), V(0, 0.2725f) }, 28), Matrix4x4.identity, mBlack),
            new Part(Torus(0.032f, 0.0045f, 28, 8, 0f, 360f), Matrix4x4.TRS(new Vector3(0, 0.274f, 0), Quaternion.Euler(90, 0, 0), Vector3.one), mFlame),
        };
        // 받침 4개: 기둥에서 비스듬히 올라가는 판 + 위 가로대 + 톱니 + 안쪽 버팀
        for (int i = 0; i < 4; i++)
        {
            var q = Quaternion.Euler(0, 45f + i * 90f, 0);
            Vector3 a = new Vector3(0, 0.215f, 0.016f), b = new Vector3(0, 0.285f, 0.088f);
            var mid = (a + b) * 0.5f; var dir = b - a;
            stoveParts.Add(new Part(cube, Matrix4x4.TRS(q * mid, q * Quaternion.LookRotation(dir, Vector3.up), new Vector3(0.003f, 0.011f, dir.magnitude)), mSteel));
            stoveParts.Add(new Part(cube, Matrix4x4.TRS(q * new Vector3(0, 0.287f, 0.070f), q, new Vector3(0.003f, 0.010f, 0.062f)), mSteel));
            for (int t = 0; t < 4; t++)
                stoveParts.Add(new Part(cube, Matrix4x4.TRS(q * new Vector3(0, 0.2935f, 0.046f + t * 0.016f), q, new Vector3(0.003f, 0.005f, 0.006f)), mSteel));
            Vector3 c = new Vector3(0, 0.284f, 0.044f), d = new Vector3(0, 0.250f, 0.030f);
            var dir2 = c - d;
            stoveParts.Add(new Part(cube, Matrix4x4.TRS(q * ((c + d) * 0.5f), q * Quaternion.LookRotation(dir2, Vector3.up), new Vector3(0.003f, 0.007f, dir2.magnitude)), mSteel));
        }
        MeshOn(stove, "Stove", stoveParts);
        var sc = stove.AddComponent<BoxCollider>(); sc.center = new Vector3(0, 0.145f, 0); sc.size = new Vector3(0.12f, 0.29f, 0.12f);
        var slot = new GameObject("KettleSlot").transform;
        slot.SetParent(stove.transform, false); slot.localPosition = new Vector3(0, 0.297f, 0); slot.localRotation = Quaternion.identity;
        sb.AppendLine(string.Format("stove {0} on table | tris {1} | kettle slot {2}", stove.transform.position.ToString("F3"), Tris(stove.GetComponent<MeshFilter>().sharedMesh), slot.position.ToString("F3")));

        // 4) 머그 2개
        var mugRoots = new Transform[2];
        var mugStates = new PyriteMugState[2];
        var mugBody = Lathe(new[] { V(0, 0), V(0.039f, 0), V(0.042f, 0.004f), V(0.042f, 0.09f), V(0.038f, 0.09f), V(0.038f, 0.008f), V(0, 0.008f) }, 24);
        var mugRim = Torus(0.040f, 0.0032f, 24, 6, 0f, 360f);
        var mugHandle = Torus(0.026f, 0.0055f, 14, 8, -90f, 90f);
        var liquidMesh = SaveMesh(Lathe(new[] { V(0.0375f, 0), V(0, 0) }, 24), "MugLiquid");
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("Mug_" + i);
            go.layer = PICKUP_LAYER;
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(new Vector3(MUGS[i].x, T_TOP, MUGS[i].z), Quaternion.Euler(0, MUG_YAW[i], 0));
            // 피벗 = 손잡이(Grip). Body 는 잔 바닥 기준 → 스크립트가 Visual 을 세워도 손잡이는 손에 남는다
            var gripPos = new Vector3(0.068f, 0.048f, 0);
            var pivot = new GameObject("Visual").transform; pivot.SetParent(go.transform, false); pivot.localPosition = gripPos;
            var vis = new GameObject("Body").transform; vis.SetParent(pivot, false); vis.localPosition = -gripPos;
            var enamel = i == 0 ? mNavy : mCream;
            var meshGo = new GameObject("Mesh"); meshGo.transform.SetParent(vis, false);
            MeshOn(meshGo, "Mug_" + i, new List<Part> {
                new Part(mugBody, Matrix4x4.identity, enamel),
                new Part(mugRim, Matrix4x4.TRS(new Vector3(0, 0.09f, 0), Quaternion.Euler(90, 0, 0), Vector3.one), mSteel),
                new Part(mugHandle, Matrix4x4.Translate(new Vector3(0.042f, 0.048f, 0)), enamel),
            });
            var liq = new GameObject("Liquid"); liq.transform.SetParent(vis, false); liq.transform.localPosition = new Vector3(0, 0.072f, 0);
            liq.AddComponent<MeshFilter>().sharedMesh = liquidMesh; var lr = liq.AddComponent<MeshRenderer>(); lr.sharedMaterial = mCoffee; lr.shadowCastingMode = ShadowCastingMode.Off;
            liq.SetActive(false);
            var steam = Steam(vis, new Vector3(0, 0.09f, 0), mSteam, 5f, 0.035f, 0.25f);
            var grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false); grip.localPosition = new Vector3(0.068f, 0.048f, 0);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0.012f, 0.045f, 0); col.size = new Vector3(0.11f, 0.09f, 0.086f);
            Pickup(go, pkTemplate, grip, VRC_Pickup.PickupOrientation.Grip, "Mug", "Drink");
            go.AddComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;
            var mg = UdonSharpUndo.AddComponent<PyriteMug>(go);
            var st = new GameObject("State"); st.transform.SetParent(go.transform, false);
            var ms = UdonSharpUndo.AddComponent<PyriteMugState>(st);
            ms.liquid = liq.transform; ms.steam = steam; ms.emptyY = 0.012f; ms.fullY = 0.072f; ms.sip = 1f / 6f;   // 여섯 모금
            UdonSharpEditorUtility.CopyProxyToUdon(ms); EditorUtility.SetDirty(ms);
            mg.state = ms; mg.visual = pivot;
            UdonSharpEditorUtility.CopyProxyToUdon(mg); EditorUtility.SetDirty(mg);
            mugRoots[i] = go.transform; mugStates[i] = ms;
        }
        sb.AppendLine(string.Format("mugs {0} / {1} | tris {2}", mugRoots[0].position.ToString("F3"), mugRoots[1].position.ToString("F3"), Tris(mugRoots[0].GetComponentInChildren<MeshFilter>().sharedMesh)));

        // 5) 주전자 (스토브 위)
        {
            var go = new GameObject("Kettle");
            go.layer = PICKUP_LAYER;
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(slot.position, slot.rotation);
            const float GY = 0.19f;
            var vis = new GameObject("Visual").transform; vis.SetParent(go.transform, false); vis.localPosition = new Vector3(0, GY, 0);
            var meshGo = new GameObject("Mesh"); meshGo.transform.SetParent(vis, false); meshGo.transform.localPosition = new Vector3(0, -GY, 0);
            var spoutDir = new Vector3(0, Mathf.Sin(50f * Mathf.Deg2Rad), Mathf.Cos(50f * Mathf.Deg2Rad));
            var spoutBase = new Vector3(0, 0.05f, 0.072f);
            var spoutTip = spoutBase + spoutDir * 0.11f;
            MeshOn(meshGo, "Kettle", new List<Part> {
                new Part(Lathe(new[] { V(0, 0), V(0.07f, 0), V(0.078f, 0.01f), V(0.085f, 0.045f), V(0.08f, 0.09f), V(0.06f, 0.12f), V(0.036f, 0.135f), V(0.03f, 0.15f), V(0.012f, 0.156f), V(0.013f, 0.165f), V(0, 0.168f) }, 28), Matrix4x4.identity, mCopper),
                new Part(Lathe(new[] { V(0, 0.165f), V(0.017f, 0.166f), V(0.018f, 0.174f), V(0, 0.178f) }, 14), Matrix4x4.identity, mBlack),
                new Part(Lathe(new[] { V(0, 0), V(0.016f, 0), V(0.007f, 0.11f), V(0, 0.11f) }, 12), Matrix4x4.TRS(spoutBase, Quaternion.FromToRotation(Vector3.up, spoutDir), Vector3.one), mCopper),
                new Part(Torus(0.075f, 0.0075f, 28, 8, 0f, 180f), Matrix4x4.TRS(new Vector3(0, 0.115f, 0), Quaternion.Euler(0, -90, 0), Vector3.one), mBlack),
            });
            var spout = new GameObject("SpoutTip").transform; spout.SetParent(meshGo.transform, false); spout.localPosition = spoutTip;
            var steam = Steam(meshGo.transform, spoutTip, mSteam, 10f, 0.05f, 0.35f);
            var stream = new GameObject("Stream"); stream.transform.SetParent(go.transform, false);
            stream.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Lathe(new[] { V(0, -0.22f), V(0.0035f, -0.22f), V(0.0045f, 0), V(0, 0) }, 8), "KettleStream");
            var sr = stream.AddComponent<MeshRenderer>(); sr.sharedMaterial = mCoffee; sr.shadowCastingMode = ShadowCastingMode.Off;
            stream.SetActive(false);
            var grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false); grip.localPosition = new Vector3(0, GY, 0);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.095f, 0.02f); col.size = new Vector3(0.18f, 0.19f, 0.25f);
            Pickup(go, pkTemplate, grip, VRC_Pickup.PickupOrientation.Grip, "Kettle", "Pour / Put on stove");
            go.AddComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;
            var kt = UdonSharpUndo.AddComponent<PyriteKettle>(go);
            kt.stoveSlot = slot; kt.spout = spout; kt.mugRoots = mugRoots; kt.mugStates = mugStates; kt.steam = steam; kt.visual = vis; kt.stream = stream.transform;
            UdonSharpEditorUtility.CopyProxyToUdon(kt); EditorUtility.SetDirty(kt);
            sb.AppendLine(string.Format("kettle {0} | spout tip {1} | tris {2}", go.transform.position.ToString("F3"), spout.position.ToString("F3"), Tris(meshGo.GetComponent<MeshFilter>().sharedMesh)));
        }

        // 6) 망원경 — 접안렌즈 들여다보기 (머리 앞 화면 방식 폐기: 사진에 찍혀 불쾌)
        {
            var go = new GameObject("Telescope");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(SCOPE.x, Ground(SCOPE), SCOPE.z);
            const float HEAD = 1.46f, PIVOT = 1.54f;          // 접안부가 눈높이(약 1.4 m)에 오도록 전보다 16 cm 높임
            var legs = new List<Part> {
                new Part(Lathe(new[] { V(0, HEAD - 0.05f), V(0.05f, HEAD - 0.05f), V(0.05f, HEAD), V(0.03f, HEAD + 0.02f), V(0.03f, PIVOT - 0.03f), V(0, PIVOT - 0.03f) }, 16), Matrix4x4.identity, mBlack),
            };
            for (int i = 0; i < 3; i++)
            {
                float a = (30f + i * 120f) * Mathf.Deg2Rad;
                var rdir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var top = new Vector3(0, HEAD - 0.03f, 0) + rdir * 0.035f;
                var foot = rdir * 0.48f;
                float L = (top - foot).magnitude;
                legs.Add(new Part(Lathe(new[] { V(0, 0), V(0.011f, 0), V(0.014f, L), V(0, L) }, 8), Matrix4x4.TRS(foot, Quaternion.FromToRotation(Vector3.up, top - foot), Vector3.one), mWood));
                legs.Add(new Part(cube, Matrix4x4.TRS(foot + Vector3.up * 0.012f, Quaternion.identity, new Vector3(0.03f, 0.024f, 0.03f)), mBlack));
            }
            MeshOn(go, "Tripod", legs);
            var yaw = new GameObject("Yaw").transform; yaw.SetParent(go.transform, false); yaw.localPosition = new Vector3(0, PIVOT, 0); yaw.rotation = Quaternion.Euler(0, 180f, 0);
            var mount = new GameObject("Mount"); mount.transform.SetParent(yaw, false);
            MeshOn(mount, "TelescopeMount", new List<Part> {
                new Part(cube, Matrix4x4.TRS(new Vector3(0, -0.04f, 0), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.06f)), mBlack),
                new Part(cube, Matrix4x4.TRS(new Vector3(0.05f, -0.005f, 0), Quaternion.identity, new Vector3(0.012f, 0.07f, 0.05f)), mBlack),
                new Part(cube, Matrix4x4.TRS(new Vector3(-0.05f, -0.005f, 0), Quaternion.identity, new Vector3(0.012f, 0.07f, 0.05f)), mBlack),
            });
            var pitch = new GameObject("Tube").transform; pitch.SetParent(yaw, false); pitch.localRotation = Quaternion.Euler(-18f, 0, 0);
            var alongZ = Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0));
            MeshOn(pitch.gameObject, "TelescopeTube", new List<Part> {
                new Part(Lathe(new[] { V(0, -0.30f), V(0.038f, -0.30f), V(0.042f, -0.28f), V(0.042f, 0.40f), V(0, 0.40f) }, 24), alongZ, mBrass),
                new Part(Lathe(new[] { V(0.043f, 0.39f), V(0.05f, 0.40f), V(0.05f, 0.56f), V(0.046f, 0.56f), V(0.046f, 0.42f) }, 24), alongZ, mBlack),
                new Part(Lathe(new[] { V(0.046f, 0.52f), V(0, 0.52f) }, 24), alongZ, mGlass),
                // 접안부: 가는 통 + 넓은 눈받이(속이 빈 고리) + 안쪽 바닥판 → 그 앞에 화면
                // 🔴 회전체 프로필은 높이가 커지는 쪽으로 그려야 법선이 바깥을 본다 (거꾸로 그리면 안팎이 뒤집힘 — 관리자 스크린샷)
                new Part(Lathe(new[] { V(0.030f, -0.395f), V(0.018f, -0.385f), V(0.018f, -0.30f), V(0.015f, -0.29f) }, 16), alongZ, mBlack),
                new Part(Lathe(new[] { V(0.031f, -0.41f), V(0.031f, -0.447f), V(0.035f, -0.445f), V(0.035f, -0.40f), V(0.030f, -0.395f) }, 24), alongZ, mBlack),
                new Part(Lathe(new[] { V(0, -0.405f), V(0.031f, -0.405f) }, 24), alongZ, mBlack),
                new Part(Torus(0.043f, 0.004f, 24, 6, 0f, 360f), Matrix4x4.Translate(new Vector3(0, 0, -0.12f)), mBlack),
                new Part(Torus(0.043f, 0.004f, 24, 6, 0f, 360f), Matrix4x4.Translate(new Vector3(0, 0, 0.18f)), mBlack),
            });
            // 경통 끝 카메라 → RT (작은 화면이라 512² + MSAA 2)
            var rtPath = DIR + "/RT_Telescope.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt != null && (rt.width != 512 || rt.antiAliasing != 2)) { AssetDatabase.DeleteAsset(rtPath); rt = null; }
            bool rtNew = rt == null;
            if (rtNew)
            {
                rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2, useMipMap = false, filterMode = FilterMode.Bilinear };
                rt.name = "RT_Telescope"; AssetDatabase.CreateAsset(rt, rtPath);
            }
            var camGo = new GameObject("ScopeCam"); camGo.transform.SetParent(pitch, false); camGo.transform.localPosition = new Vector3(0, 0, 0.57f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 18f; cam.nearClipPlane = 0.35f; cam.farClipPlane = 3000f; cam.targetTexture = rt; cam.depth = -5;   // 화각 6°·10° 는 하늘 별이 막대처럼 늘어났다 → 18° (약 3배)
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false; cam.allowMSAA = true;
            cam.cullingMask = ~((1 << 5) | (1 << 12) | (1 << 18) | (1 << 19));
            camGo.SetActive(false);
            // 접안 화면: 지름 6 cm, 경통 뒤(-Z)를 향함 → 경통 방향(+Z)으로 보는 사람에게 앞면
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "EyepieceScreen"; Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(pitch, false); screen.transform.localPosition = new Vector3(0, 0, -0.407f); screen.transform.localScale = Vector3.one * 0.062f;
            var em = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/Materials/M_ScopeEyepiece.mat");
            if (em == null) { em = new Material(Shader.Find("Pyrite/ScopeEyepiece")); AssetDatabase.CreateAsset(em, DIR + "/Materials/M_ScopeEyepiece.mat"); }
            em.shader = Shader.Find("Pyrite/ScopeEyepiece"); em.SetTexture("_MainTex", rt); em.SetFloat("_Brightness", 1.0f); EditorUtility.SetDirty(em);
            var sr = screen.GetComponent<MeshRenderer>(); sr.sharedMaterial = em; sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, PIVOT - 0.02f, 0); bc.size = new Vector3(0.24f, 0.36f, 0.95f);
            var tel = UdonSharpUndo.AddComponent<PyriteTelescope>(go);
            tel.yawPivot = yaw; tel.pitchPivot = pitch; tel.cam = camGo; tel.eyepiece = screen.transform;
            tel.yaw = 180f; tel.pitch = 18f;
            UdonSharpEditorUtility.CopyProxyToUdon(tel); EditorUtility.SetDirty(tel);
            sb.AppendLine(string.Format("telescope {0} | fire dist {1:F2} m | eyepiece y {2:F2} (지면 기준) | screen Ø0.062 m | rt {3} {4}x{5} aa{6}", go.transform.position.ToString("F2"),
                Vector2.Distance(new Vector2(go.transform.position.x, go.transform.position.z), new Vector2(FIRE.x, FIRE.z)), screen.transform.position.y - go.transform.position.y, rtPath, rt.width, rt.height, rt.antiAliasing));
        }

        // 7) 돗자리 (타프 밑)
        {
            var go = new GameObject("PicnicMat");
            go.layer = PICKUP_LAYER;
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(MAT.x, Ground(MAT) + 0.002f, MAT.z);
            MeshOn(go, "PicnicMat", new List<Part> { new Part(cube, Matrix4x4.TRS(new Vector3(0, 0.004f, 0), Quaternion.identity, new Vector3(MAT_W, 0.008f, MAT_D)), mMat) });
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.006f, 0); col.size = new Vector3(MAT_W, 0.012f, MAT_D);
            Pickup(go, pkTemplate, null, VRC_Pickup.PickupOrientation.Any, "Mat", "");
            go.AddComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;
            var cc = UdonSharpUndo.AddComponent<PyriteCarryChair>(go);
            UdonSharpEditorUtility.CopyProxyToUdon(cc);
            // 눕기 두 자리: 야전침대(Cot) 의 VRCStation(누운 자세 컨트롤러)을 복사. 머리 -X, 발 +X
            //  판정: 발 쪽 끝 0.3 m 띠 = 들기(돗자리 콜라이더), 나머지 = 눕기(돗자리 위 0.09 m 상자, 레이어 Pickup → 몸에 안 걸림)
            var cotSt = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Cot")?.GetComponent<VRC.SDK3.Components.VRCStation>();
            var matPk = go.GetComponent<VRCPickup>();
            int nLie = 0;
            foreach (var zs in new[] { -1f, 1f })
            {
                var lie = new GameObject(zs < 0 ? "Lie_A" : "Lie_B"); lie.layer = PICKUP_LAYER;
                lie.transform.SetParent(go.transform, false);
                var lrb = lie.AddComponent<Rigidbody>(); lrb.isKinematic = true; lrb.useGravity = false;
                var lbc = lie.AddComponent<BoxCollider>(); lbc.center = new Vector3(-0.15f, 0.057f, zs * MAT_D * 0.25f); lbc.size = new Vector3(MAT_W - 0.3f, 0.09f, MAT_D * 0.5f - 0.02f);
                var st = lie.AddComponent<VRC.SDK3.Components.VRCStation>();
                if (cotSt != null) EditorUtility.CopySerialized(cotSt, st);
                st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize; st.seated = true; st.disableStationExit = false; st.canUseStationFromStation = true;
                var lp = new GameObject("LiePoint").transform; lp.SetParent(lie.transform, false);
                lp.localPosition = new Vector3(-0.05f, 0.012f, zs * MAT_D * 0.25f); lp.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var ep = new GameObject("ExitPoint").transform; ep.SetParent(lie.transform, false);
                ep.localPosition = new Vector3(-0.05f, 0.05f, zs * (MAT_D * 0.5f + 0.45f)); ep.localRotation = Quaternion.Euler(0f, zs < 0 ? 180f : 0f, 0f);
                st.stationEnterPlayerLocation = lp; st.stationExitPlayerLocation = ep;
                EditorUtility.SetDirty(st);
                var cs = UdonSharpUndo.AddComponent<PyriteCarrySeat>(lie);
                cs.station = st; cs.pickup = matPk;
                UdonSharpEditorUtility.CopyProxyToUdon(cs);
                var lub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs);
                if (lub != null) { lub.interactText = "Lie down"; lub.proximity = 2f; EditorUtility.SetDirty(lub); }
                nLie++;
            }
            sb.AppendLine("mat " + go.transform.position.ToString("F3") + " size " + MAT_W + "x" + MAT_D + " | lie stations " + nLie + " (cot station " + (cotSt != null ? "copied, ctrl " + (cotSt.animatorController ? cotSt.animatorController.name : "null") : "NOT FOUND") + ")");
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("syncs " + root.GetComponentsInChildren<VRCObjectSync>(true).Length + " | udon " + root.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true).Length
            + " | renderers " + root.GetComponentsInChildren<Renderer>(true).Length + " | tris " + root.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh ? Tris(f.sharedMesh) : 0));
        Renders();
        sb.AppendLine("RESULT: DONE");
    }

    // ───────────────────────── 확인 렌더 ─────────────────────────
    static void Renders()
    {
        var root = FindRoot();
        if (root == null) { sb.AppendLine("CampProps 없음"); return; }
        Directory.CreateDirectory("Assets/_preview/props/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var steams = root.GetComponentsInChildren<ParticleSystem>(true);
        var liquids = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Liquid").ToArray();
        var marsh = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Marshmallow").Select(t => t.GetComponent<MeshRenderer>()).ToArray();
        var saved = marsh.Select(m => m.sharedMaterial).ToArray();
        var tmp = new List<Material>();
        try
        {
            // 미리보기용: 머그 하나 채우고, 꼬치 셋을 생·노릇·탄 색으로, 김 3초 진행
            if (liquids.Length > 0) liquids[0].gameObject.SetActive(true);
            var cols = new[] { new Color(0.97f, 0.95f, 0.91f), new Color(0.86f, 0.58f, 0.26f), new Color(0.07f, 0.055f, 0.05f) };
            for (int i = 0; i < marsh.Length && i < 3; i++) { var m = new Material(saved[i]); m.color = cols[i]; tmp.Add(m); marsh[i].sharedMaterial = m; }
            foreach (var ps in steams) { ps.gameObject.SetActive(true); ps.Simulate(3f, true, true); }
            var shots = new (string n, Vector3 eye, Vector3 at, float fov)[] {
                ("overview", new Vector3(-10.6f, 3.35f, 55.4f), new Vector3(-10.9f, 2.0f, 52.4f), 55f),
                ("table", new Vector3(-10.05f, 2.78f, 53.75f), new Vector3(-10.6f, 2.22f, 52.9f), 48f),
                ("stove", new Vector3(-10.72f, 2.66f, 53.55f), new Vector3(-11.14f, 2.36f, 52.97f), 40f),
                ("rack", new Vector3(-9.25f, 2.75f, 53.45f), new Vector3(-9.72f, 2.45f, 52.87f), 42f),
                ("telescope", new Vector3(-12.2f, 2.85f, 53.1f), new Vector3(-13.6f, 2.85f, 51.6f), 45f),
                ("mat", new Vector3(-8.3f, 3.5f, 53.9f), new Vector3(-8.3f, 1.85f, 56.9f), 58f),
            };
            foreach (var h in new[] { 13f, 20.5f })
            {
                if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(h); }
                foreach (var s in shots)
                {
                    cam.fieldOfView = s.fov;
                    cam.transform.SetPositionAndRotation(s.eye, Quaternion.LookRotation(s.at - s.eye));
                    Shot(cam, string.Format("Assets/_preview/props/props_{0}_{1:00}.png", s.n, h), 1280, 720);
                }
            }
            // 들고 있는 모습 흉내: 루트를 손처럼 아무렇게나 돌리고(Euler 60,40,120), Udon 과 같은 식으로 Visual 을 세운다
            {
                if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(13f); }
                var head = new Vector3(-10.0f, 3.35f, 54.4f);
                var look = Quaternion.Euler(28f, 180f, 0f);                   // 불 쪽(-Z)을 28° 내려다봄
                var hand = new[] { new Vector3(0.22f, -0.32f, 0.42f), new Vector3(0.18f, -0.30f, 0.45f), new Vector3(0.16f, -0.28f, 0.36f) };
                var names = new[] { "Skewer_0", "Kettle", "Mug_0" };
                var q = Quaternion.Euler(60f, 40f, 120f);
                for (int k = 1; k < 3; k++)   // 꼬치(0)는 손 방향 그대로라 흉내 대상 아님
                {
                    var r = root.transform.Find(names[k]); if (r == null) continue;
                    var vis = r.Find("Visual"); if (vis == null) continue;
                    var p1 = r.position; var q1 = r.rotation;
                    var target = head + look * hand[k];
                    r.SetPositionAndRotation(target - q * vis.localPosition, q);
                    if (k == 0)
                    {
                        var d = vis.position - head;
                        float down = Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                        vis.rotation = Quaternion.Euler(Mathf.Clamp(down, -10f, 35f), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                    }
                    else { var f = look * Vector3.forward; vis.rotation = Quaternion.Euler(0f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f); }
                    // 옆에서 본다 (머리 위치엔 대역 구체를 두지 않는다 — 손 쪽만 보면 된다)
                    var side = look * new Vector3(0.9f, 0.25f, 0.35f);
                    cam.fieldOfView = 45f;
                    cam.transform.SetPositionAndRotation(target + side, Quaternion.LookRotation(-side + look * new Vector3(0f, 0f, 0.15f)));
                    Shot(cam, "Assets/_preview/props/props_held_" + k + ".png", 960, 540);
                    sb.AppendLine(string.Format("  held {0}: grip→target err {1:F4} m, visual up·Y {2:F2}", names[k], Vector3.Distance(vis.position, target), Vector3.Dot(vis.up, Vector3.up)));
                    r.SetPositionAndRotation(p1, q1); vis.localRotation = Quaternion.identity;
                }
            }
            // 망원경 속 (기본 방향: 호수, 18° 위)
            var scope = root.transform.Find("Telescope/Yaw/Tube/ScopeCam");
            if (scope != null)
            {
                var sc = scope.GetComponent<Camera>();
                foreach (var h in new[] { 13f, 20.5f })
                {
                    if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(h); }
                    var rt = sc.targetTexture;
                    sb.AppendLine("  scope cam targetTexture " + (rt != null ? rt.name : "NULL"));
                    if (rt == null) continue;
                    scope.gameObject.SetActive(true); sc.Render(); scope.gameObject.SetActive(false);
                    RenderTexture.active = rt; var tx = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tx.Apply(); RenderTexture.active = null;
                    File.WriteAllBytes(string.Format("Assets/_preview/props/props_scope_{0:00}.png", h), tx.EncodeToPNG()); Object.DestroyImmediate(tx);
                }
            }
            // 접안 화면 가까이 (얼굴 대고 보는 거리 15 cm)
            var ep = root.transform.Find("Telescope/Yaw/Tube/EyepieceScreen");
            if (ep != null && scope != null)
            {
                if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(20.5f); }
                scope.gameObject.SetActive(true); scope.GetComponent<Camera>().Render();
                var tube = ep.parent;
                var eye = ep.position - tube.forward * 0.15f + Vector3.up * 0.01f;
                cam.fieldOfView = 60f;
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(tube.forward, Vector3.up));
                Shot(cam, "Assets/_preview/props/props_eyepiece_21.png", 1280, 720);
                var side = ep.position + tube.right * 0.9f - tube.forward * 0.5f + Vector3.up * 0.1f;
                cam.fieldOfView = 45f;
                cam.transform.SetPositionAndRotation(side, Quaternion.LookRotation(ep.position + tube.forward * 0.1f - side));
                Shot(cam, "Assets/_preview/props/props_eyepiece_side_21.png", 1280, 720);
                var back = ep.position - tube.forward * 0.55f + tube.right * 0.12f + Vector3.up * 0.12f;   // 관리자 스크린샷과 비슷한 뒤쪽 시점
                cam.fieldOfView = 50f;
                cam.transform.SetPositionAndRotation(back, Quaternion.LookRotation(ep.position + tube.forward * 0.2f - back));
                Shot(cam, "Assets/_preview/props/props_eyepiece_back_21.png", 1280, 720);
                scope.gameObject.SetActive(false);
            }
            sb.AppendLine("  shots props_{overview,table,stove,rack,telescope,mat}_{13,20} + props_scope_{13,20} + props_eyepiece(_side)_21");
        }
        finally
        {
            foreach (var ps in steams) ps.Clear(true);
            if (liquids.Length > 0) liquids[0].gameObject.SetActive(false);
            for (int i = 0; i < marsh.Length; i++) marsh[i].sharedMaterial = saved[i];
            foreach (var m in tmp) Object.DestroyImmediate(m);
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(w, h, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, w, h), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    // ───────────────────────── 구성 요소 ─────────────────────────
    static VRCPickup Pickup(GameObject go, VRCPickup template, Transform grip, VRC_Pickup.PickupOrientation o, string text, string use)
    {
        var pk = go.AddComponent<VRCPickup>();
        if (template != null) EditorUtility.CopySerialized(template, pk);
        pk.ExactGrip = grip; pk.ExactGun = null;
        pk.orientation = o;
        pk.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
        pk.InteractionText = text; pk.UseText = use;
        pk.pickupable = true;
        EditorUtility.SetDirty(pk);
        return pk;
    }

    static ParticleSystem Steam(Transform parent, Vector3 lpos, Material mat, float rate, float size, float alpha)
    {
        var go = new GameObject("Steam");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lpos;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);        // 방출 방향(+Z) = 위
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = true; main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.gravityModifier = -0.01f;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.005f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
        var no = ps.noise; no.enabled = true; no.strength = 0.04f; no.frequency = 0.7f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    static float Ground(Vector3 p)
    {
        Physics.SyncTransforms();
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out hit, 120f, 1 | (1 << 11), QueryTriggerInteraction.Ignore)) return hit.point.y;
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.transform.position.y : 1.81f;
    }

    // ───────────────────────── 재질 · 텍스처 ─────────────────────────
    static Material Mat(string name, Color c, float metal, float gloss, Color? emis = null)
    {
        string p = DIR + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Standard");
        m.color = c; m.SetFloat("_Metallic", metal); m.SetFloat("_Glossiness", gloss);
        if (emis.HasValue)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emis.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatTex(string name, Texture2D tex)
    {
        var m = Mat(name, Color.white, 0f, 0.12f);
        m.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SteamMat()
    {
        string p = DIR + "/Materials/M_PropSteam.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var sh = Shader.Find("Particles/Standard Unlit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh;
        m.SetTexture("_MainTex", SoftDot());
        m.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
        m.SetFloat("_Mode", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D SoftDot()
    {
        string p = DIR + "/Textures/T_SoftDot.png";
        const int N = 64;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        return SaveTex(t, p, true, TextureWrapMode.Clamp);
    }

    static Texture2D MatTexture()
    {
        // 돗자리: 녹색 단색 (관리자: 줄무늬가 너무 튄다) — 같은 색의 옅은 짚 결과 조금 어두운 같은 색 테두리만
        string p = DIR + "/Textures/T_PicnicMat.png";
        const int W = 512, H = 352;
        var t = new Texture2D(W, H, TextureFormat.RGB24, true);
        var green = new Color(0.26f, 0.38f, 0.23f);
        var rnd = new System.Random(7);
        var rowJit = Enumerable.Range(0, H).Select(_ => (float)rnd.NextDouble()).ToArray();
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            Color c = green * (0.95f + 0.05f * rowJit[y]);
            if (y % 4 == 0) c *= 0.90f;                            // 짚 결
            if ((x / 3) % 2 == 0 && y % 4 == 2) c *= 0.96f;        // 엮은 날실
            if (x < 12 || x >= W - 12 || y < 12 || y >= H - 12) c *= 0.78f;   // 같은 색 테두리
            c.a = 1f;
            t.SetPixel(x, y, c);
        }
        return SaveTex(t, p, false, TextureWrapMode.Clamp);
    }

    static string V3(Vector3 v) { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2}", v.x, v.y, v.z); }
    static Vector3 ParseV(string s)
    {
        var a = s.Trim().Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(a[0], a[1], a[2]);
    }

    static Texture2D SaveTex(Texture2D t, string p, bool alpha, TextureWrapMode wrap)
    {
        t.Apply();
        File.WriteAllBytes(p, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(p);
        ti.alphaIsTransparency = alpha; ti.wrapMode = wrap; ti.mipmapEnabled = true; ti.sRGBTexture = true;
        ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    // ───────────────────────── 메시 ─────────────────────────
    static Vector2 V(float r, float y) { return new Vector2(r, y); }
    static int Tris(Mesh m) { return m == null ? 0 : (int)Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i)) / 3; }

    // Y 축 회전체. 프로필 (반지름, 높이). 인접 선분 각이 35° 미만이면 부드럽게, 넘으면 각진 모서리
    static Mesh Lathe(Vector2[] p, int seg)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int np = p.Length;
        var sn = new Vector2[np - 1];
        for (int i = 0; i < np - 1; i++) { var d = p[i + 1] - p[i]; sn[i] = new Vector2(d.y, -d.x).normalized; }
        var acc = new float[np]; for (int i = 1; i < np; i++) acc[i] = acc[i - 1] + (p[i] - p[i - 1]).magnitude;
        float total = Mathf.Max(acc[np - 1], 1e-5f);
        for (int s = 0; s < np - 1; s++)
        {
            Vector2 n0 = sn[s], n1 = sn[s];
            if (s > 0 && Vector2.Angle(sn[s - 1], sn[s]) < 35f) n0 = (sn[s - 1] + sn[s]).normalized;
            if (s < np - 2 && Vector2.Angle(sn[s + 1], sn[s]) < 35f) n1 = (sn[s + 1] + sn[s]).normalized;
            int b = v.Count;
            for (int k = 0; k <= seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg; float c = Mathf.Cos(a), si = Mathf.Sin(a);
                v.Add(new Vector3(p[s].x * c, p[s].y, p[s].x * si)); n.Add(new Vector3(n0.x * c, n0.y, n0.x * si)); uv.Add(new Vector2((float)k / seg, acc[s] / total));
                v.Add(new Vector3(p[s + 1].x * c, p[s + 1].y, p[s + 1].x * si)); n.Add(new Vector3(n1.x * c, n1.y, n1.x * si)); uv.Add(new Vector2((float)k / seg, acc[s + 1] / total));
            }
            for (int k = 0; k < seg; k++)
            {
                int i0 = b + k * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                Tri(t, v, n, i0, i1, i2); Tri(t, v, n, i2, i1, i3);
            }
        }
        var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    // 원환 (XY 평면, 중심 원점). a0~a1 도 구간만 (손잡이 = 반원)
    static Mesh Torus(float R, float r, int segU, int segV, float a0, float a1)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= segU; i++)
        {
            float th = Mathf.Lerp(a0, a1, (float)i / segU) * Mathf.Deg2Rad;
            var c = new Vector3(R * Mathf.Cos(th), R * Mathf.Sin(th), 0f);
            var n1 = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f);
            for (int j = 0; j <= segV; j++)
            {
                float ph = j * Mathf.PI * 2f / segV;
                var nn = n1 * Mathf.Cos(ph) + Vector3.forward * Mathf.Sin(ph);
                v.Add(c + nn * r); n.Add(nn);
            }
        }
        int row = segV + 1;
        for (int i = 0; i < segU; i++) for (int j = 0; j < segV; j++)
        {
            int a = i * row + j, b = a + row, c = a + 1, d = b + 1;
            Tri(t, v, n, a, b, c); Tri(t, v, n, c, b, d);
        }
        var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    // 면 방향을 원하는 법선 쪽으로 맞춘다 (Unity 앞면: Cross(b-a, c-a) 방향). 넓이 0 삼각형은 버린다
    static void Tri(List<int> t, List<Vector3> v, List<Vector3> n, int a, int b, int c)
    {
        var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (fn.sqrMagnitude < 1e-16f) return;
        if (Vector3.Dot(fn, n[a] + n[b] + n[c]) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
        else { t.Add(a); t.Add(b); t.Add(c); }
    }

    static Mesh Combine(string name, List<Part> parts, out Material[] mats)
    {
        var groups = parts.GroupBy(p => p.mat).ToList();
        var subs = new List<CombineInstance>();
        foreach (var g in groups)
        {
            var gm = new Mesh();
            gm.CombineMeshes(g.Select(p => new CombineInstance { mesh = p.mesh, transform = p.m, subMeshIndex = 0 }).ToArray(), true, true);
            subs.Add(new CombineInstance { mesh = gm, transform = Matrix4x4.identity });
        }
        var m = new Mesh();
        m.CombineMeshes(subs.ToArray(), false, false);
        m.RecalculateBounds();
        mats = groups.Select(g => g.Key).ToArray();
        return SaveMesh(m, name);
    }

    static void MeshOn(GameObject go, string name, List<Part> parts)
    {
        var mesh = Combine(name, parts, out var mats);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string p = DIR + "/Meshes/" + name + ".asset";
        m.name = name;
        if (AssetDatabase.LoadAssetAtPath<Mesh>(p) != null) AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText("Logs/pyrite_props.txt", sb.ToString());
        Debug.Log("[CampProps] " + sb.ToString().Split('\n').FirstOrDefault(l => l.StartsWith("RESULT")));
    }
}
#endif
