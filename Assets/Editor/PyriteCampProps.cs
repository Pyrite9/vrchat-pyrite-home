// Tools ▸ Pyrite2 ▸ Z31b. Build Camp Props / Z31c. Camp Props Revert / Z31d. Camp Props Renders
//  캠프 소품 6종을 절차적 메시로 만들어 새 루트 CampProps 아래에 둔다 (유료 에셋 안 씀 → 저장소에 들어가도 된다)
//   - 마시멜로 꼬치 3개 + 통나무 꽂이 : 테이블 왼쪽(+X, 의자에 앉아 불을 볼 때 왼쪽)
//   - 나무 상자 + 캠핑 스토브 + 주전자 : 테이블 오른쪽(-X) 바로 옆, 주전자는 스토브 위
//   - 법랑 머그 2개 : 테이블 가운데 의자 쪽 (프로젝터 둘은 불 쪽 절반에 있다), 여섯 모금
//  들었을 때 방향: 손 방향 대신 스크립트가 Visual(피벗 = 손잡이)을 세운다 — 랜턴과 같은 방식
//   - 망원경 : 화로 오른쪽(-X) 3 m
//   - 돗자리 : 타프 밑, 야전침대 앞 (Z31a 실측: 침대와 겹치지 않게)
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
    // 테이블 상판 (Z31a 실측): x -11.04..-10.17, z 52.64..53.09, y 2.187
    const float T_X0 = -11.04f, T_X1 = -10.17f, T_Z0 = 52.64f, T_Z1 = 53.09f, T_TOP = 2.187f;
    static readonly Vector3 RACK = new Vector3(-9.93f, 0f, 52.87f);
    static readonly Vector3 CRATE = new Vector3(-11.26f, 0f, 52.87f);
    const float CRATE_H = 0.36f, CRATE_W = 0.34f;
    static readonly Vector3[] MUGS = { new Vector3(-10.74f, 0f, 52.99f), new Vector3(-10.48f, 0f, 52.99f) };
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
    [MenuItem("Tools/Pyrite2/Z31b. Build Camp Props", false, 51)]
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
        var mCopper = Mat("M_PropCopper", new Color(0.80f, 0.46f, 0.30f), 0.9f, 0.62f);
        var mBrass = Mat("M_PropBrass", new Color(0.80f, 0.62f, 0.32f), 1f, 0.68f);
        var mNavy = Mat("M_PropEnamelNavy", new Color(0.10f, 0.17f, 0.30f), 0f, 0.78f);
        var mCream = Mat("M_PropEnamelCream", new Color(0.90f, 0.85f, 0.72f), 0f, 0.78f);
        var mCoffee = Mat("M_PropCoffee", new Color(0.16f, 0.08f, 0.035f), 0f, 0.92f);
        var mGlass = Mat("M_PropLens", new Color(0.04f, 0.07f, 0.11f), 0.2f, 0.95f);
        var mFlame = Mat("M_PropFlame", Color.black, 0f, 0.2f, new Color(0.35f, 0.62f, 1.6f) * 2.2f);
        var mSteam = SteamMat();
        var mMat = MatTex("M_PropMat", MatTexture());

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

        // 3) 상자 + 스토브
        var crate = new GameObject("StoveCrate");
        crate.transform.SetParent(root.transform, false);
        crate.transform.position = new Vector3(CRATE.x, Ground(CRATE), CRATE.z);
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var crateParts = new List<Part>();
        // 판자 느낌: 옆면 널 4줄씩 + 모서리 기둥 + 뚜껑
        for (int k = 0; k < 4; k++)
        {
            float y = 0.02f + k * 0.085f + 0.04f;
            foreach (var sgn in new[] { -1f, 1f })
            {
                crateParts.Add(new Part(cube, Matrix4x4.TRS(new Vector3(0, y, sgn * (CRATE_W * 0.5f - 0.01f)), Quaternion.identity, new Vector3(CRATE_W - 0.04f, 0.078f, 0.02f)), k % 2 == 0 ? mWood : mWoodDark));
                crateParts.Add(new Part(cube, Matrix4x4.TRS(new Vector3(sgn * (CRATE_W * 0.5f - 0.01f), y, 0), Quaternion.identity, new Vector3(0.02f, 0.078f, CRATE_W - 0.04f)), k % 2 == 0 ? mWoodDark : mWood));
            }
        }
        foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            crateParts.Add(new Part(cube, Matrix4x4.TRS(new Vector3(sx * (CRATE_W * 0.5f - 0.02f), CRATE_H * 0.5f - 0.01f, sz * (CRATE_W * 0.5f - 0.02f)), Quaternion.identity, new Vector3(0.04f, CRATE_H - 0.02f, 0.04f)), mWoodDark));
        crateParts.Add(new Part(cube, Matrix4x4.TRS(new Vector3(0, CRATE_H - 0.01f, 0), Quaternion.identity, new Vector3(CRATE_W, 0.02f, CRATE_W)), mWood));
        MeshOn(crate, "Crate", crateParts);
        var cb = crate.AddComponent<BoxCollider>(); cb.center = new Vector3(0, CRATE_H * 0.5f, 0); cb.size = new Vector3(CRATE_W, CRATE_H, CRATE_W);

        var stove = new GameObject("Stove");
        stove.transform.SetParent(crate.transform, false);
        stove.transform.localPosition = new Vector3(0, CRATE_H, 0);
        var stoveParts = new List<Part> {
            new Part(Lathe(new[] { V(0, 0), V(0.085f, 0), V(0.088f, 0.006f), V(0.082f, 0.045f), V(0.07f, 0.05f), V(0, 0.05f) }, 24), Matrix4x4.identity, mBlack),
            new Part(Lathe(new[] { V(0, 0.05f), V(0.036f, 0.05f), V(0.036f, 0.068f), V(0.03f, 0.072f), V(0, 0.072f) }, 20), Matrix4x4.identity, mSteel),
            new Part(Torus(0.027f, 0.0055f, 24, 8, 0f, 360f), Matrix4x4.TRS(new Vector3(0, 0.076f, 0), Quaternion.Euler(90, 0, 0), Vector3.one), mFlame),
            new Part(Lathe(new[] { V(0, 0), V(0.012f, 0), V(0.012f, 0.02f), V(0, 0.02f) }, 10), Matrix4x4.TRS(new Vector3(0.086f, 0.022f, 0), Quaternion.Euler(0, 0, -90), Vector3.one), mSteel),
        };
        for (int i = 0; i < 3; i++)
        {
            var q = Quaternion.Euler(0, 30f + i * 120f, 0);
            stoveParts.Add(new Part(cube, Matrix4x4.TRS(q * new Vector3(0, 0.074f, 0.058f), q, new Vector3(0.008f, 0.044f, 0.05f)), mSteel));
        }
        MeshOn(stove, "Stove", stoveParts);
        var sc = stove.AddComponent<BoxCollider>(); sc.center = new Vector3(0, 0.045f, 0); sc.size = new Vector3(0.18f, 0.09f, 0.18f);
        var slot = new GameObject("KettleSlot").transform;
        slot.SetParent(stove.transform, false); slot.localPosition = new Vector3(0, 0.096f, 0); slot.localRotation = Quaternion.Euler(0, 180f, 0);
        sb.AppendLine(string.Format("crate {0} top {1:F3} | kettle slot {2}", crate.transform.position.ToString("F2"), crate.transform.position.y + CRATE_H, slot.position.ToString("F3")));

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

        // 6) 망원경
        {
            var go = new GameObject("Telescope");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(SCOPE.x, Ground(SCOPE), SCOPE.z);
            const float HEAD = 1.30f, PIVOT = 1.38f;
            var legs = new List<Part> {
                new Part(Lathe(new[] { V(0, HEAD - 0.05f), V(0.05f, HEAD - 0.05f), V(0.05f, HEAD), V(0.03f, HEAD + 0.02f), V(0.03f, PIVOT - 0.03f), V(0, PIVOT - 0.03f) }, 16), Matrix4x4.identity, mBlack),
            };
            for (int i = 0; i < 3; i++)
            {
                float a = (30f + i * 120f) * Mathf.Deg2Rad;
                var rdir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var top = new Vector3(0, HEAD - 0.03f, 0) + rdir * 0.035f;
                var foot = rdir * 0.45f;
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
                new Part(Lathe(new[] { V(0, -0.43f), V(0.02f, -0.43f), V(0.02f, -0.40f), V(0.015f, -0.39f), V(0.015f, -0.29f), V(0, -0.29f) }, 14), alongZ, mBlack),
                new Part(Torus(0.043f, 0.004f, 24, 6, 0f, 360f), Matrix4x4.Translate(new Vector3(0, 0, -0.12f)), mBlack),
                new Part(Torus(0.043f, 0.004f, 24, 6, 0f, 360f), Matrix4x4.Translate(new Vector3(0, 0, 0.18f)), mBlack),
            });
            // 경통 끝 카메라 → RT
            var rtPath = DIR + "/RT_Telescope.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            bool rtNew = rt == null;
            if (rtNew) { rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32); rt.name = "RT_Telescope"; AssetDatabase.CreateAsset(rt, rtPath); }
            var camGo = new GameObject("ScopeCam"); camGo.transform.SetParent(pitch, false); camGo.transform.localPosition = new Vector3(0, 0, 0.57f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 18f; cam.nearClipPlane = 0.35f; cam.farClipPlane = 3000f; cam.targetTexture = rt; cam.depth = -5;   // 화각 6°·10° 는 하늘 별이 막대처럼 늘어났다 → 18° (약 3배)
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false; cam.allowMSAA = false;
            cam.cullingMask = ~((1 << 5) | (1 << 10) | (1 << 12) | (1 << 18) | (1 << 19));
            string tt0 = cam.targetTexture != null ? cam.targetTexture.name : "NULL";
            camGo.SetActive(false);
            sb.AppendLine(string.Format("scope rt {0} (new {1}) → cam.targetTexture active {2} / inactive {3}", rt != null ? rt.width + "x" + rt.height : "NULL", rtNew, tt0, cam.targetTexture != null ? cam.targetTexture.name : "NULL"));
            var view = GameObject.CreatePrimitive(PrimitiveType.Quad);
            view.name = "ScopeView"; Object.DestroyImmediate(view.GetComponent<Collider>());
            view.transform.SetParent(go.transform, false); view.transform.localScale = Vector3.one * 0.6f;
            var vm = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/Materials/M_ScopeView.mat");
            if (vm == null) { vm = new Material(Shader.Find("Pyrite/ScopeView")); AssetDatabase.CreateAsset(vm, DIR + "/Materials/M_ScopeView.mat"); }
            vm.shader = Shader.Find("Pyrite/ScopeView"); vm.SetTexture("_MainTex", rt); vm.SetFloat("_Radius", 0.1f); vm.SetFloat("_Soft", 0.006f); EditorUtility.SetDirty(vm);
            var vr = view.GetComponent<MeshRenderer>(); vr.sharedMaterial = vm; vr.shadowCastingMode = ShadowCastingMode.Off; vr.receiveShadows = false;
            view.SetActive(false);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, PIVOT - 0.02f, 0); bc.size = new Vector3(0.24f, 0.36f, 0.95f);
            var tel = UdonSharpUndo.AddComponent<PyriteTelescope>(go);
            tel.yawPivot = yaw; tel.pitchPivot = pitch; tel.cam = camGo; tel.view = view.transform;
            UdonSharpEditorUtility.CopyProxyToUdon(tel); EditorUtility.SetDirty(tel);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tel);
            if (ub != null) { ub.interactText = "Telescope (Jump to exit)"; ub.proximity = 2f; EditorUtility.SetDirty(ub); }
            sb.AppendLine(string.Format("telescope {0} | fire dist {1:F2} m | eyepiece y {2:F2} | rt {3}", go.transform.position.ToString("F2"),
                Vector2.Distance(new Vector2(go.transform.position.x, go.transform.position.z), new Vector2(FIRE.x, FIRE.z)), pitch.TransformPoint(0, 0, -0.43f).y - go.transform.position.y, rtPath));
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
            sb.AppendLine("mat " + go.transform.position.ToString("F3") + " size " + MAT_W + "x" + MAT_D);
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
                ("stove", new Vector3(-10.95f, 2.62f, 53.45f), new Vector3(-11.26f, 2.33f, 52.87f), 40f),
                ("rack", new Vector3(-9.45f, 2.75f, 53.45f), new Vector3(-9.93f, 2.45f, 52.87f), 42f),
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
                for (int k = 0; k < 3; k++)
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
            sb.AppendLine("  shots props_{overview,table,stove,rack,telescope,mat}_{13,20} + props_scope_{13,20}");
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
        // 돗자리: 볏짚색 바탕 + 가는 결 + 붉은·남색 줄 + 남색 테두리
        string p = DIR + "/Textures/T_PicnicMat.png";
        const int W = 512, H = 352;
        var t = new Texture2D(W, H, TextureFormat.RGB24, true);
        var straw = new Color(0.80f, 0.69f, 0.47f);
        var red = new Color(0.62f, 0.17f, 0.14f);
        var navy = new Color(0.13f, 0.18f, 0.32f);
        var rnd = new System.Random(7);
        var rowJit = Enumerable.Range(0, H).Select(_ => (float)rnd.NextDouble()).ToArray();
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            Color c = straw * (0.92f + 0.08f * rowJit[y]);
            if (y % 4 == 0) c *= 0.82f;                            // 짚 결
            if ((x / 3) % 2 == 0 && y % 4 == 2) c *= 0.93f;        // 엮은 날실
            int bx = x % 128;
            if (bx >= 20 && bx < 30) c = Color.Lerp(c, red, 0.85f);
            if (bx >= 34 && bx < 38) c = Color.Lerp(c, navy, 0.85f);
            if (bx >= 90 && bx < 94) c = Color.Lerp(c, navy, 0.85f);
            if (bx >= 98 && bx < 108) c = Color.Lerp(c, red, 0.85f);
            if (x < 12 || x >= W - 12 || y < 12 || y >= H - 12) c = navy * (y % 4 == 0 ? 0.85f : 1f);
            t.SetPixel(x, y, c);
        }
        return SaveTex(t, p, false, TextureWrapMode.Clamp);
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
