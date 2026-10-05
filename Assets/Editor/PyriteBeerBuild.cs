// PyriteBeerBuild.cs — 캠프 맥주 박스 (Tools ▸ Pyrite4 ▸ Z53b 빌드 / Z53c 되돌림 / Z53d 렌더). 재실행 안전 (BeerCooler 를 지우고 다시 만든다)
//  2026-09-30 관리자: 텐트 침실 폴딩 컨테이너를 재활용해 병맥주 + 얼음, 뚜껑 여닫기, 캠프 탁자 오른쪽(스토브·주전자 쪽). 첫 사용 = 뽕 + 탄산, 그 뒤 = 마시기
//  Z53a 실측: 탁자 x −11.34~−9.87 · z 52.53~53.21, 옆 땅 1.809 평평 · 꽃 없음. 탁자 끝에 붙이면(A1) CarryChair_2 앞 20 cm 라 앉은 무릎과 겹침
//   (15:4x 첫 빌드: 긴 변 x, 중심 (−11.70, 52.73))
//  20:26 관리자 인게임: 세로(긴 변 z)로, 뚜껑은 탁자 쪽으로 열리게 → yaw 0, 경첩 = 로컬 +x = 탁자 쪽. 중심 (−11.64, 52.73)
//   탁자 끝과 10 cm (5 cm 면 −100° 연 뚜껑이 상판 모서리를 6 mm 파고듦), z 는 그대로 → 의자 앞면(53.38)까지 34 cm, 탁자 앞선보다 11 cm 모닥불 쪽
//  구성 (루트 BeerCooler, 정적 플래그 없음 → 라이트맵 재베이크 불필요)
//   Container: 침실 컨테이너(PyriteBedside.BuildContainer)와 같은 모양·재질, 파트를 재질별로 합친 메시. 뚜껑 경첩 = 탁자 쪽(+x), 사람은 −x 쪽에서 꺼냄
//    콜라이더는 벽 4 + 바닥 (통짜 상자면 안의 병이 가려져 집히지 않는다)
//   Ice: 얼음판 + 얼음 조각 110 (합친 메시 1개). 병은 3 × 4 = 12, 제자리 = Container/Slots/Slot_n
//   Bottles/Beer_n: 반투명 갈색 유리 330 ml(Ø 6 cm · 높이 22.7 cm) + 속 맥주(Pyrite/BeerLiquid, 모금마다 수면이 내려감) + 라벨(가상 상표 PYRITE LAGER) + 왕관 병뚜껑
//    Pickup(Mug_0 설정 복사) + ObjectSync + PyriteBeer, 자식 State = PyriteBeerState(Manual) + AudioSource(따기·마시기)
//   CapFly/CapFly_n: 딸 때 날아가는 병뚜껑 (각자 물리, 꺼 둠, 레이어 17 Walkthrough)
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
using VRC.SDK3.Components;
using VRC.SDKBase;

public static class PyriteBeerBuild
{
    const string ROOT = "BeerCooler";
    const string DIR = "Assets/Props/Beer/";
    const string SFX = "Assets/Audio/SFX/";
    const string PREV = "Assets/_preview/beer/";
    static readonly Vector3 POS = new Vector3(-11.64f, 0f, 52.73f);
    const float YAW = 0f;                                    // 컨테이너 긴 변(로컬 z) = 월드 z, 경첩(로컬 +x) = 탁자 쪽 → 뚜껑이 탁자 쪽으로 넘어가며 열림
    const float CT_W = 0.40f, CT_L = 0.62f, CT_H = 0.32f, WT = 0.012f, FLOOR = 0.032f;
    static readonly float[] SX = { -0.115f, 0f, 0.115f };
    static readonly float[] SZ = { -0.21f, -0.07f, 0.07f, 0.21f };
    const float GRIP_Y = 0.11f, LIP_Y = 0.2235f;
    const float ICE_Y = 0.098f;                               // 얼음 윗면 (컨테이너 바닥에서). 라벨(병 0.034~0.106 + 바닥 0.032) 위쪽 절반이 보이게
    const int PICKUP_LAYER = 13, WALKTHROUGH = 17;
    static StringBuilder sb;
    static int tris;

    [MenuItem("Tools/Pyrite4/Z53b. Beer Cooler Build", false, 5302)]
    public static void Build()
    {
        sb = new StringBuilder("[Z53b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) sb.AppendLine("RESULT: DONE"); else sb.AppendLine("RESULT: STOPPED"); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite4/Z53c. Beer Cooler Revert", false, 5303)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z53c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var r = Root(ROOT);
        if (r != null) { Object.DestroyImmediate(r); sb.AppendLine("BeerCooler 삭제 (에셋 Assets/Props/Beer 는 남김)"); } else sb.AppendLine("BeerCooler 없음");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    [MenuItem("Tools/Pyrite4/Z53d. Beer Cooler Renders", false, 5304)]
    public static void RenderOnly()
    {
        sb = new StringBuilder("[Z53d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    // ═════════════════════════════════════════════════════════════
    static bool Inner()
    {
        bool p1 = EnsureProgram("PyriteBeer"), p2 = EnsureProgram("PyriteBeerState");
        if (!p1 || !p2) return false;
        Directory.CreateDirectory(DIR);
        var old = Root(ROOT); if (old != null) Object.DestroyImmediate(old);
        tris = 0;

        // 재질
        var pp = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/Bedside/M_ContainerPP.mat");
        var ppd = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/Bedside/M_ContainerPPDark.mat");
        var blk = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/Bedside/M_ContainerBlack.mat");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_TableWood.mat");
        if (!pp || !ppd || !blk || !wood) { sb.AppendLine("!! 침실 컨테이너 재질 없음 (pp " + (pp != null) + " ppd " + (ppd != null) + " blk " + (blk != null) + " wood " + (wood != null) + ") — Z51r 먼저"); return false; }
        var mGlass = GlassMat("M_BeerGlass", new Color(0.30f, 0.13f, 0.03f, 0.52f));          // 20:26 관리자: 속 액체가 보이게 → 반투명 (Standard Transparent)
        var mGlassE = GlassMat("M_BeerGlassEmpty", new Color(0.40f, 0.22f, 0.08f, 0.38f));
        var mLiquid = LiquidMat();
        var mCap = Mat("M_BeerCap", new Color(0.80f, 0.64f, 0.30f), 0.90f, 0.62f);
        var mIce = Mat("M_BeerIce", new Color(0.50f, 0.62f, 0.70f), 0f, 0.96f);        // 15:43 렌더: 0.80~0.95 는 흰 종이처럼 평평 → 어둡고 매끈하게
        var mLabel = LabelMat();
        var mFoam = AssetDatabase.LoadAssetAtPath<Material>("Assets/Props/Materials/M_PropSteam.mat");
        if (mLabel == null) { sb.AppendLine("!! " + DIR + "T_BeerLabel.png 없음"); return false; }

        // 루트
        float g = Ground(POS);
        var root = new GameObject(ROOT).transform;
        root.SetPositionAndRotation(new Vector3(POS.x, g, POS.z), Quaternion.Euler(0f, YAW, 0f));
        sb.AppendLine(string.Format("루트 ({0:F2}, {1:F3}, {2:F2}) yaw {3} — 긴 변 z ({4:F2}~{5:F2}), 탁자 쪽 벽 x {6:F2}", POS.x, g, POS.z, YAW, POS.z - CT_L / 2f, POS.z + CT_L / 2f, POS.x + CT_W / 2f));

        // ── 컨테이너 몸통 (침실 것과 같은 모양, 재질별로 합침) ──
        var ct = new GameObject("Container").transform; ct.SetParent(root, false);
        var body = new List<Part>();
        void B(Vector3 c, Vector3 s, Material m) => body.Add(new Part(Cube(), Matrix4x4.TRS(c, Quaternion.identity, s), m));
        B(new Vector3(0, 0.02f, 0), new Vector3(CT_W, 0.02f, CT_L), pp);
        B(new Vector3(0, 0.008f, 0), new Vector3(CT_W + 0.006f, 0.016f, CT_L + 0.006f), ppd);
        B(new Vector3(-CT_W / 2f + WT / 2f, CT_H / 2f, 0), new Vector3(WT, CT_H, CT_L), pp);
        B(new Vector3(CT_W / 2f - WT / 2f, CT_H / 2f, 0), new Vector3(WT, CT_H, CT_L), pp);
        foreach (var sz in new[] { -1, 1 }) B(new Vector3(0, CT_H / 2f, sz * (CT_L / 2f - WT / 2f)), new Vector3(CT_W - 2f * WT, CT_H, WT), pp);
        B(new Vector3(0, 0.031f, 0), new Vector3(CT_W - 2f * WT, 0.002f, CT_L - 2f * WT), ppd);
        float sy = CT_H * 0.45f;
        foreach (var sx in new[] { -1, 1 }) B(new Vector3(sx * (CT_W / 2f + 0.002f), sy, 0), new Vector3(0.004f, 0.008f, CT_L - 0.04f), ppd);
        foreach (var sz in new[] { -1, 1 }) B(new Vector3(0, sy, sz * (CT_L / 2f + 0.002f)), new Vector3(CT_W - 0.04f, 0.008f, 0.004f), ppd);
        foreach (var sx in new[] { -1, 1 }) foreach (var z in new[] { -0.19f, 0f, 0.19f })
            B(new Vector3(sx * (CT_W / 2f + 0.003f), CT_H * 0.55f, z), new Vector3(0.006f, CT_H * 0.70f, 0.022f), pp);
        foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            B(new Vector3(sx * (CT_W / 2f - 0.008f), CT_H / 2f, sz * (CT_L / 2f - 0.008f)), new Vector3(0.028f, CT_H, 0.028f), pp);
        foreach (var sz in new[] { -1, 1 }) B(new Vector3(0, CT_H * 0.80f, sz * (CT_L / 2f + 0.001f)), new Vector3(0.13f, 0.030f, 0.004f), blk);
        Emit(ct, "Body", body);
        // 콜라이더: 벽 4 + 바닥 (안은 비움)
        Col(ct, "ColFront", new Vector3(-CT_W / 2f + WT / 2f, CT_H / 2f, 0), new Vector3(WT, CT_H, CT_L));
        Col(ct, "ColBack", new Vector3(CT_W / 2f - WT / 2f, CT_H / 2f, 0), new Vector3(WT, CT_H, CT_L));
        foreach (var sz in new[] { -1, 1 }) Col(ct, "ColEnd", new Vector3(0, CT_H / 2f, sz * (CT_L / 2f - WT / 2f)), new Vector3(CT_W, CT_H, WT));
        Col(ct, "ColFloor", new Vector3(0, FLOOR / 2f, 0), new Vector3(CT_W, FLOOR, CT_L));

        // ── 뚜껑 (경첩 = 로컬 +x 윗모서리 = 월드 의자 쪽) ──
        var lidGo = new GameObject("Lid"); lidGo.transform.SetParent(ct, false); lidGo.transform.localPosition = new Vector3(CT_W / 2f + 0.006f, CT_H, 0f);
        var lidT = lidGo.transform; float cx = -(CT_W + 0.012f) / 2f;
        var lp = new List<Part>();
        void L(Vector3 c, Vector3 s, Material m) => lp.Add(new Part(Cube(), Matrix4x4.TRS(c, Quaternion.identity, s), m));
        L(new Vector3(cx, 0.011f, 0), new Vector3(CT_W + 0.012f, 0.022f, CT_L + 0.012f), pp);
        L(new Vector3(cx, -0.006f, 0), new Vector3(CT_W + 0.012f, 0.012f, CT_L + 0.012f), ppd);
        L(new Vector3(cx, 0.022f + 0.009f, 0), new Vector3(CT_W - 0.03f, 0.018f, CT_L - 0.05f), wood);
        foreach (var z in new[] { -0.17f, 0.17f }) L(new Vector3(-(CT_W + 0.012f) - 0.004f, -0.012f, z), new Vector3(0.008f, 0.05f, 0.045f), blk);
        Emit(lidT, "Lid", lp);
        var lbc = lidGo.AddComponent<BoxCollider>(); lbc.center = new Vector3(cx, 0.02f, 0); lbc.size = new Vector3(CT_W + 0.012f, 0.045f, CT_L + 0.012f);
        var lid = UdonSharpUndo.AddComponent<PyriteTrunkLid>(lidGo);
        UdonSharpEditorUtility.CopyProxyToUdon(lid);
        var lub = UdonSharpEditorUtility.GetBackingUdonBehaviour(lid);
        if (lub != null) { lub.interactText = "Open / Close"; lub.proximity = 2f; EditorUtility.SetDirty(lub); }

        // ── 얼음 ──
        var ice = new List<Part>();
        ice.Add(new Part(Cube(), Matrix4x4.TRS(new Vector3(0, (FLOOR + ICE_Y) / 2f, 0), Quaternion.identity, new Vector3(CT_W - 2f * WT - 0.002f, ICE_Y - FLOOR, CT_L - 2f * WT - 0.002f)), mIce));
        var rnd = new System.Random(53);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        for (int i = 0; i < 110; i++)
        {
            float s = R(0.020f, 0.036f);
            var c = new Vector3(R(-0.17f, 0.17f), R(ICE_Y - 0.006f, ICE_Y + 0.011f), R(-0.28f, 0.28f));
            ice.Add(new Part(Cube(), Matrix4x4.TRS(c, Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)), Vector3.one * s), mIce));
        }
        Emit(ct, "Ice", ice);

        // ── 병 메시 ──
        var glassMesh = SaveMesh(Lathe(new[] {
            V(0, 0), V(0.026f, 0), V(0.0295f, 0.004f), V(0.030f, 0.012f), V(0.030f, 0.140f), V(0.028f, 0.156f), V(0.020f, 0.176f),
            V(0.0145f, 0.192f), V(0.0135f, 0.208f), V(0.0138f, 0.214f), V(0.0150f, 0.216f), V(0.0150f, 0.2215f), V(0.0132f, 0.2230f), V(0, 0.2232f) }, 16), "BeerGlass");
        var labelMesh = SaveMesh(Lathe(new[] { V(0.0304f, 0.034f), V(0.0304f, 0.106f) }, 24), "BeerLabel");
        var liquidMesh = SaveMesh(Lathe(new[] {
            V(0, 0.006f), V(0.0262f, 0.006f), V(0.0283f, 0.012f), V(0.0283f, 0.139f), V(0.0264f, 0.155f), V(0.0187f, 0.175f),
            V(0.0131f, 0.191f), V(0.0121f, 0.207f), V(0, 0.2072f) }, 16), "BeerLiquid");   // 유리 안쪽 1.7 mm
        var capMesh = SaveMesh(Lathe(new[] { V(0.0162f, -0.004f), V(0.0166f, -0.0025f), V(0.0163f, 0.0018f), V(0.0128f, 0.0035f), V(0, 0.0038f) }, 16), "BeerCap");   // 가운데 = 원점 (날아가는 뚜껑과 같이 씀)

        // ── 제자리 ──
        var slots = new GameObject("Slots").transform; slots.SetParent(ct, false); slots.localPosition = new Vector3(0, FLOOR, 0);
        var homes = new List<Transform>();
        foreach (var z in SZ) foreach (var x in SX)
        {
            var s = new GameObject("Slot_" + homes.Count).transform; s.SetParent(slots, false);
            s.localPosition = new Vector3(x, 0f, z); s.localRotation = Quaternion.Euler(0f, 90f, 0f);   // 병 −z(라벨 앞) → 월드 −z(모닥불 쪽)
            homes.Add(s);
        }

        // ── 효과음 ──
        var openClip = Clip("SFX_BeerOpen"); var sipClip = Clip("SFX_Sip");
        var mug = GameObject.Find("CampProps/Mug_0"); var tpl = mug != null ? mug.GetComponent<VRCPickup>() : null;
        sb.AppendLine("pickup 설정 복사 원본: " + (tpl != null ? "CampProps/Mug_0" : "없음(기본값)"));

        // ── 날아가는 뚜껑 ──
        var flyRoot = new GameObject("CapFly").transform; flyRoot.SetParent(root, false);
        // ── 병 12 ──
        var bRoot = new GameObject("Bottles").transform; bRoot.SetParent(root, false);
        for (int i = 0; i < homes.Count; i++)
        {
            var h = homes[i];
            var fly = new GameObject("CapFly_" + i); fly.transform.SetParent(flyRoot, false); fly.layer = WALKTHROUGH;
            fly.AddComponent<MeshFilter>().sharedMesh = capMesh; var fr = fly.AddComponent<MeshRenderer>(); fr.sharedMaterial = mCap; fr.shadowCastingMode = ShadowCastingMode.Off;
            var fbc = fly.AddComponent<BoxCollider>(); fbc.size = new Vector3(0.032f, 0.008f, 0.032f);
            var frb = fly.AddComponent<Rigidbody>(); frb.mass = 0.005f; frb.drag = 0.4f; frb.angularDrag = 0.6f; frb.interpolation = RigidbodyInterpolation.Interpolate; frb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            fly.SetActive(false);

            var go = new GameObject("Beer_" + i); go.layer = PICKUP_LAYER;
            go.transform.SetParent(bRoot, true);
            go.transform.SetPositionAndRotation(h.position, h.rotation);
            var vis = new GameObject("Visual").transform; vis.SetParent(go.transform, false); vis.localPosition = new Vector3(0, GRIP_Y, 0);
            var bodyT = new GameObject("Body").transform; bodyT.SetParent(vis, false); bodyT.localPosition = new Vector3(0, -GRIP_Y, 0);
            var gf = MeshObj(bodyT, "GlassFull", glassMesh, mGlass);
            var ge = MeshObj(bodyT, "GlassEmpty", glassMesh, mGlassE); ge.SetActive(false); tris -= glassMesh.triangles.Length / 3;
            var liq = MeshObj(bodyT, "Liquid", liquidMesh, mLiquid); liq.transform.SetSiblingIndex(0);
            MeshObj(bodyT, "Label", labelMesh, mLabel);
            var cap = MeshObj(bodyT, "Cap", capMesh, mCap); cap.transform.localPosition = new Vector3(0, 0.2232f, 0);
            var foam = Foam(bodyT, new Vector3(0, LIP_Y, 0), mFoam);
            var grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false); grip.localPosition = new Vector3(0, GRIP_Y, 0);

            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.mass = 0.5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var cc = go.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 0.1125f, 0); cc.radius = 0.031f; cc.height = 0.226f; cc.direction = 1;
            var pk = go.AddComponent<VRCPickup>();
            if (tpl != null) EditorUtility.CopySerialized(tpl, pk);
            pk.ExactGrip = null; pk.ExactGun = null; pk.orientation = VRC_Pickup.PickupOrientation.Any;   // 23:19 PC/VR 구분 제거 → 누구나 잡은 자세 그대로 (Z54a 와 같은 값)
            pk.AutoHold = VRC_Pickup.AutoHoldMode.Yes; pk.InteractionText = "Beer"; pk.UseText = "Open / Drink"; pk.pickupable = true;
            EditorUtility.SetDirty(pk);
            go.AddComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;

            var st = new GameObject("State"); st.transform.SetParent(go.transform, false); st.transform.localPosition = new Vector3(0, 0.2f, 0);
            var bs = UdonSharpUndo.AddComponent<PyriteBeerState>(st);
            bs.opened = false; bs.sips = 8; bs.maxSips = 8; bs.audioSrc = Src(st, 10f); bs.openClip = openClip; bs.sipClip = sipClip;
            var b = UdonSharpUndo.AddComponent<PyriteBeer>(go);
            b.state = bs; b.visual = vis; b.home = h; b.lid = lid; b.glassFull = gf; b.glassEmpty = ge; b.cap = cap;
            b.capFly = frb; b.foam = foam; b.liquid = liq.GetComponent<MeshRenderer>();
            b.drunk = Object.FindObjectOfType<PyriteDrunk>(true);   // 취기 효과 (Z57a). 없으면 null — Z57a 가 다시 연결
            bs.beer = b;
            UdonSharpEditorUtility.CopyProxyToUdon(bs); EditorUtility.SetDirty(bs);
            UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b);
        }

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            r.shadowCastingMode = r.name.StartsWith("CapFly") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.BlendProbes; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var rs = root.GetComponentsInChildren<MeshRenderer>(false);
        sb.AppendLine(string.Format("병 {0} · 삼각형(보이는 것) {1} · 켜진 MeshRenderer {2} · ObjectSync {3} · UdonBehaviour {4}",
            homes.Count, tris, rs.Length, root.GetComponentsInChildren<VRCObjectSync>(true).Length, root.GetComponentsInChildren<VRC.Udon.UdonBehaviour>(true).Length));
        sb.AppendLine(string.Format("병 메시 {0} tris · 라벨 {1} · 뚜껑 {2}", glassMesh.triangles.Length / 3, labelMesh.triangles.Length / 3, capMesh.triangles.Length / 3));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    // ───────────── 부품 ─────────────
    struct Part { public Mesh m; public Matrix4x4 x; public Material mat; public Part(Mesh m, Matrix4x4 x, Material mat) { this.m = m; this.x = x; this.mat = mat; } }

    static Mesh cube;
    static Mesh Cube() { if (cube == null) cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"); return cube; }

    static void Emit(Transform parent, string name, List<Part> parts)
    {
        foreach (var grp in parts.GroupBy(p => p.mat))
        {
            var ci = grp.Select(p => new CombineInstance { mesh = p.m, transform = p.x }).ToArray();
            var m = new Mesh(); m.CombineMeshes(ci, true, true); m.RecalculateBounds();
            var mesh = SaveMesh(m, "Cooler_" + name + "_" + grp.Key.name);
            MeshObj(parent, name + "_" + grp.Key.name.Replace("M_", ""), mesh, grp.Key);
        }
    }

    static void Col(Transform p, string n, Vector3 c, Vector3 s)
    {
        var g = new GameObject(n); g.transform.SetParent(p, false); g.layer = 0;
        var bc = g.AddComponent<BoxCollider>(); bc.center = c; bc.size = s;
    }

    static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        tris += mesh.triangles.Length / 3;
        return g;
    }

    static ParticleSystem Foam(Transform parent, Vector3 lpos, Material mat)
    {
        var go = new GameObject("Foam"); go.transform.SetParent(parent, false);
        go.transform.localPosition = lpos; go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 방출 +Z = 병 위쪽
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.duration = 1.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.28f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.010f, 0.022f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60; main.gravityModifier = 0.05f;
        var em = ps.emission; em.rateOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 70f, 1f, 5f));
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.008f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.95f, 0.93f, 0.86f), 1f) },
                   new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.Facing;
        r.allowRoll = false;   // SDK 경고 '카메라와 같이 도는 파티클' (VR 멀미) — Auto Fix 와 같은 값
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    static Material LabelMat()
    {
        string tp = DIR + "T_BeerLabel.png";
        var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
        if (ti == null) return null;
        ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
        var m = Mat("M_BeerLabel", Color.white, 0f, 0.35f);
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
        EditorUtility.SetDirty(m);
        return m;
    }

    // Standard Transparent (반사는 남고 알파만 비침) — 병 속 맥주가 보이게
    static Material GlassMat(string name, Color c)
    {
        var m = Mat(name, c, 0.05f, 0.95f);
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LiquidMat()
    {
        string p = DIR + "M_BeerLiquid.mat";
        var sh = Shader.Find("Pyrite/BeerLiquid");
        if (sh == null) { sb.AppendLine("!! 셰이더 Pyrite/BeerLiquid 없음"); return null; }
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh;
        m.SetColor("_Color", new Color(0.46f, 0.23f, 0.035f)); m.SetColor("_TopColor", new Color(0.78f, 0.58f, 0.24f));   // 20:3x 1차(0.72/0.40/0.07, 발광 0.35)는 오렌지 주스처럼 밝고 밤에 빛남
        m.SetFloat("_Level", 0.192f); m.SetFloat("_Glossiness", 0.85f); m.SetFloat("_Emission", 0.08f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Mat(string name, Color c, float metal, float gloss)
    {
        string p = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Standard");
        m.color = c; m.SetFloat("_Metallic", metal); m.SetFloat("_Glossiness", gloss);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static AudioClip Clip(string name)
    {
        string p = SFX + name + ".wav";
        var ai = AssetImporter.GetAtPath(p) as AudioImporter;
        if (ai == null) { sb.AppendLine("  !! 없음 " + p); return null; }
        ai.forceToMono = true; ai.loadInBackground = false;
        var st = ai.defaultSampleSettings; st.loadType = AudioClipLoadType.DecompressOnLoad; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.6f; st.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        st.preloadAudioData = true;
        ai.defaultSampleSettings = st; ai.SaveAndReimport();
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        sb.AppendLine(string.Format("  clip {0} {1:0.00}s preload {2}", name, c != null ? c.length : 0f, ai.defaultSampleSettings.preloadAudioData));
        return c;
    }

    // PyriteSfx.Src 와 같은 설정 (들리는 환경음 AMB_* 와 같게 — 8절 무음 함정)
    static AudioSource Src(GameObject go, float maxDist)
    {
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false; a.loop = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic;
        a.minDistance = 0.6f; a.maxDistance = maxDist; a.dopplerLevel = 0f; a.volume = 1f; a.clip = null;
        var sp = go.AddComponent<VRCSpatialAudioSource>();
        var so = new SerializedObject(sp);
        foreach (var (n, v) in new[] { ("Gain", 0f), ("Near", 0f), ("Far", maxDist) }) { var pr = so.FindProperty(n); if (pr != null) pr.floatValue = v; }
        var e = so.FindProperty("EnableSpatialization"); if (e != null) e.boolValue = false;
        var u = so.FindProperty("UseAudioSourceVolumeCurve"); if (u != null) u.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Vector2 V(float r, float y) => new Vector2(r, y);

    // 회전체 — 프로필은 높이가 커지는 쪽으로 (법선이 바깥, 8절 함정)
    static Mesh Lathe(Vector2[] prof, int seg)
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
        for (int i = 0; i < prof.Length; i++)
            for (int s = 0; s <= seg; s++)
            {
                float a = 2f * Mathf.PI * s / seg;
                vs.Add(new Vector3(Mathf.Cos(a) * prof[i].x, prof[i].y, Mathf.Sin(a) * prof[i].x));
                uv.Add(new Vector2((float)s / seg, (float)i / (prof.Length - 1)));
            }
        int row = seg + 1;
        for (int i = 0; i < prof.Length - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                ts.Add(a); ts.Add(c); ts.Add(b); ts.Add(b); ts.Add(c); ts.Add(d);
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(ts, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

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
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Assets ▸ Refresh 후 Z53b 다시");
        return false;
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static float Ground(Vector3 p)
    {
        Physics.SyncTransforms();
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out hit, 120f, 1 | (1 << 11), QueryTriggerInteraction.Ignore)) return hit.point.y;
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.transform.position.y : 1.81f;
    }

    // ───────────── 렌더 ─────────────
    //  전후: 박스 끔/켬 × 18:20 · 21:00 (모닥불 쪽 눈높이), 뚜껑 연 모습, 병 가까이, 딴 병(뚜껑 없음) · 빈 병
    static void Renders()
    {
        var root = Root(ROOT); if (root == null) { sb.AppendLine("!! BeerCooler 없음 — Z53b 먼저"); return; }
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var lid = root.transform.Find("Container/Lid");
        var fire = new Vector3(-10.5f, 0f, 51.5f);
        Directory.CreateDirectory(PREV);
        float g = root.transform.position.y;
        var c = root.transform.position;
        var eye = new Vector3(-10.9f, g + 1.55f, 51.55f);                       // 모닥불 옆에 서서 탁자 오른쪽을 봄
        var at = new Vector3(-11.45f, g + 0.25f, 52.85f);
        var near = new Vector3(-12.30f, g + 0.95f, 52.45f);                      // 박스 바깥쪽(−x, 탁자 반대편)에서 내려다봄
        try
        {
            foreach (var hour in new[] { 18.33f, 21f })
            {
                if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(hour); }
                string hs = hour < 20f ? "1820" : "2100";
                root.SetActive(false); Shot(cam, eye, at, 60f, "b_before_" + hs);
                root.SetActive(true); Shot(cam, eye, at, 60f, "b_after_" + hs);
            }
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            if (lid)
            {
                var lr = lid.localRotation; lid.localRotation = Quaternion.Euler(0, 0, -100f);
                Shot(cam, near, c + Vector3.up * 0.15f, 55f, "b_open_2100");
                if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(18.33f); }
                Shot(cam, near, c + Vector3.up * 0.15f, 55f, "b_open_1820");
                Shot(cam, c + new Vector3(-0.34f, 0.42f, -0.06f), c + new Vector3(0.02f, 0.16f, 0f), 45f, "b_bottles_1820");
                // 딴 병 · 빈 병 흉내: 앞줄 가운데 병 뚜껑 숨김, 그 옆 병 빈 유리
                var b1 = root.transform.Find("Bottles/Beer_1/Visual/Body"); var b0 = root.transform.Find("Bottles/Beer_0/Visual/Body");
                if (b1 && b0)
                {
                    var cap = b1.Find("Cap"); cap.gameObject.SetActive(false);
                    b0.Find("GlassFull").gameObject.SetActive(false); b0.Find("GlassEmpty").gameObject.SetActive(true); b0.Find("Cap").gameObject.SetActive(false);
                    Shot(cam, c + new Vector3(-0.34f, 0.42f, -0.06f), c + new Vector3(0.02f, 0.16f, 0f), 45f, "b_bottles_open_empty_1820");
                    cap.gameObject.SetActive(true);
                    b0.Find("GlassFull").gameObject.SetActive(true); b0.Find("GlassEmpty").gameObject.SetActive(false); b0.Find("Cap").gameObject.SetActive(true);
                }
                // 병 넷을 들어 올려 나란히: 가득 · 반(4/8) · 한 모금 · 반을 35° 기울임 (수면이 수평인지). 수면은 MaterialPropertyBlock 으로 흉내
                var names = new[] { "Bottles/Beer_4", "Bottles/Beer_5", "Bottles/Beer_6", "Bottles/Beer_7" };
                var lv = new[] { 0.192f, Mathf.Lerp(0.022f, 0.192f, 3f / 7f), 0.022f, Mathf.Lerp(0.022f, 0.192f, 3f / 7f) };
                var saved = new List<(Transform t, Vector3 p, Quaternion q)>();
                var mpb = new MaterialPropertyBlock();
                var basePos = c + new Vector3(0f, 0.55f, -0.55f);
                for (int i = 0; i < names.Length; i++)
                {
                    var bt = root.transform.Find(names[i]); if (!bt) continue;
                    saved.Add((bt, bt.position, bt.rotation));
                    bt.position = basePos + new Vector3(-0.12f + i * 0.085f, 0f, 0f);
                    bt.rotation = i == 3 ? Quaternion.Euler(0f, 0f, -35f) : Quaternion.identity;
                    var liqR = bt.Find("Visual/Body/Liquid")?.GetComponent<Renderer>();
                    if (liqR) { mpb.Clear(); mpb.SetFloat("_Level", lv[i]); liqR.SetPropertyBlock(mpb); }
                    if (i == 0) Shot(cam, bt.position + new Vector3(0f, 0.12f, -0.34f), bt.position + new Vector3(0f, 0.11f, 0f), 40f, "b_label_1820");
                }
                Shot(cam, basePos + new Vector3(0.01f, 0.13f, -0.52f), basePos + new Vector3(0.01f, 0.10f, 0f), 42f, "b_liquid_1820");
                if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
                Shot(cam, basePos + new Vector3(0.01f, 0.13f, -0.52f), basePos + new Vector3(0.01f, 0.10f, 0f), 42f, "b_liquid_2100");
                foreach (var s in saved)
                {
                    s.t.SetPositionAndRotation(s.p, s.q);
                    var liqR = s.t.Find("Visual/Body/Liquid")?.GetComponent<Renderer>(); if (liqR) liqR.SetPropertyBlock(null);
                }
                lid.localRotation = lr;
            }
        }
        finally
        {
            root.SetActive(true);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        sb.AppendLine("  shots " + PREV + "b_*.jpg");
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, float fov, string tag)
    {
        cam.fieldOfView = fov;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double sum = 0; foreach (var q in px) sum += q.r + q.g + q.b;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(88));
        sb.AppendLine("  shot " + tag + " 평균 " + (sum / px.Length / 3).ToString("F1"));
        Object.DestroyImmediate(tex);
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText("Logs/pyrite_beer.txt", sb.ToString(), new UTF8Encoding(false));
    }
}
#endif
