// PyriteBeanbagSit.cs — 빈백 전용 앉기 클립 (Z51n 실측 / Z51o 빌드 / Z51p 되돌림). 재실행 안전
//  2026-09-30 관리자: 빈백 앉기 모션이 공중에 뜸(인게임 스크린샷) → 선택지 B = 빈백 전용 휴머노이드 클립
//  원인(추정): Seat 는 캠프 CarryChair Station 복사 = VRChat 기본 앉기(의자용). SitPoint y 0.30 은 빈백 윗면(가운데 ~0.54) 안쪽이고
//             기본 자세는 다리를 아래로 내리는 의자 자세라 빈백 앞 볼록면에 발이 걸려 뜬다.
//  방식: A_LieDown(PyriteCotStation) 과 같은 상수 커브 휴머노이드 클립. 값은 프로젝트 안 휴머노이드 모델에 샘플링해
//        뼈 각도를 재고 이분 탐색으로 목표에 맞춘다(근육 부호·범위를 추측하지 않음). 트래킹은 PyriteLieTracking.AddTo 재사용.
//  로그 Logs/pyrite_beanbag_sit.txt, 렌더 Assets/_preview/bedroom/bs_*.jpg
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBeanbagSit
{
    const string CLIP_PATH = "Assets/Animations/A_BeanbagSit.anim";
    const string CTRL_PATH = "Assets/Animations/AC_BeanbagSit.controller";
    const string PREV = "Assets/_preview/bedroom/";
    const string LOG = "Logs/pyrite_beanbag_sit.txt";

    // ── 목표 자세 (각도 °, 거리 = humanScale 배수) ───────────────
    // Z51n 실측(윗면 중심선): z −0.2 등받이 0.61 · z 0 솟음 0.54 · z +0.10~+0.35 평평한 좌석 0.38~0.39 · 앞끝 +0.40 0.36
    const float RECLINE = 38f;     // 골반→머리 벡터가 수직에서 뒤로 눕는 각도 (솟은 부분에 등을 기댐)
    const float SHIN    = -80f;    // 무릎→발목 옆면 각도 (0 = 앞 수평, −90 = 수직 아래, −180 = 뒤)
    const float REF_HS  = 0.85f;   // 기준 아바타 크기(humanScale, 약 1.5 m). 이 크기에서 발목이 바닥에 닿게 허벅지를 푼다
    static readonly float[] SIZES = { 0.65f, 0.85f, 1.06f };   // 크기별 확인 (작은/기준/큰)
    const float FACE    = 2f;      // 얼굴 방향 앙각 (TV 는 약 +5°)
    const float SIT_Z   = 0.15f;   // SitPoint 앞뒤 위치 = 평평한 좌석 (빈백 로컬, +Z = 앞)
    const float SINK    = 0.05f;   // 빈백 윗면에서 파묻히는 깊이 (m)
    const float HIP_CLEAR = 0.10f; // 골반 뼈를 SitPoint 위로 올리는 높이 (humanScale 배수)
    const float OLD_SIT_Y = 0.30f, OLD_SIT_Z = -0.02f;   // Z51l 원래 값 (되돌림용)

    // 보정 안 하는 고정 근육 (팔은 무릎/허벅지 위에 편하게)
    static readonly Dictionary<string, float> FIXED = new Dictionary<string, float>
    {
        { "Left Arm Down-Up",        -0.55f }, { "Right Arm Down-Up",        -0.55f },
        { "Left Arm Front-Back",      0.35f }, { "Right Arm Front-Back",      0.35f },
        { "Left Forearm Stretch",     0.10f }, { "Right Forearm Stretch",     0.10f },
        { "Left Upper Leg In-Out",    0.06f }, { "Right Upper Leg In-Out",    0.06f },
        { "Left Foot Up-Down",        0.00f }, { "Right Foot Up-Down",        0.00f },
        { "Spine Front-Back",         0.00f }, { "Chest Front-Back",          0.00f },
    };

    // 보정 대상 (휴머노이드가 없으면 이 기본값 그대로 — 로그에 표시)
    static float qx = -28f, legFB = 0.6f, knee = -0.6f, nod = 0f;
    static Vector3 rt = new Vector3(0f, 0.05f, 0f);

    static StringBuilder sb;
    static GameObject probe; static Animator an; static Transform sit, bagT; static Vector3 headFwdLocal; static float ankleBind;

    // ═════════════════════════════════════════════════════════════
    [MenuItem("Tools/Pyrite3/Z51n. Beanbag Sit Report", false, 5114)]
    public static void Report()
    {
        sb = new StringBuilder("[Z51n] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var bags = Bags(); if (bags == null) { Flush(); return; }
            foreach (var bag in bags) { Profile(bag); StationInfo(bag); }
            Humanoids(true);
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51o. Beanbag Sit Build", false, 5115)]
    public static void Build()
    {
        sb = new StringBuilder("[Z51o] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) sb.AppendLine("RESULT: DONE"); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally { if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); if (probe) Object.DestroyImmediate(probe); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51p. Beanbag Sit Revert", false, 5116)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z51p] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var bags = Bags(); if (bags == null) { Flush(); return; }
        var src = Object.FindObjectsOfType<VRC.SDK3.Components.VRCStation>(true)
            .FirstOrDefault(s => s.transform.parent && s.transform.parent.name.StartsWith("CarryChair"));
        var ctrl0 = src ? src.animatorController : null;
        foreach (var bag in bags)
        {
            var seat = bag.Find("Seat"); var st = seat ? seat.GetComponent<VRC.SDK3.Components.VRCStation>() : null;
            if (st == null) { sb.AppendLine("!! " + bag.name + " Station 없음"); continue; }
            st.animatorController = ctrl0; EditorUtility.SetDirty(st);
            var sp = seat.Find("SitPoint"); if (sp) sp.localPosition = new Vector3(0f, OLD_SIT_Y, OLD_SIT_Z);
            sb.AppendLine(bag.name + " → ctrl " + (ctrl0 ? ctrl0.name : "기본") + ", SitPoint " + V(sp.localPosition));
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    // ═════════════════════════════════════════════════════════════
    static bool Inner()
    {
        var bags = Bags(); if (bags == null) return false;
        var bag0 = bags[0];
        float surf = WithCollider(bag0, mc => Surf(bag0, mc, 0f, SIT_Z));
        if (float.IsNaN(surf)) { sb.AppendLine("!! 빈백 윗면 레이캐스트 실패"); return false; }
        float sitY = surf - SINK;
        sb.AppendLine("빈백 윗면 (x 0, z " + SIT_Z.ToString("F2") + ") = " + surf.ToString("F3") + " m → SitPoint y " + sitY.ToString("F3") + " (파묻힘 " + SINK + ")");

        // SitPoint 먼저 옮김 (보정 기준점)
        foreach (var bag in bags)
        {
            var sp = bag.Find("Seat/SitPoint");
            if (sp == null) { sb.AppendLine("!! " + bag.name + "/Seat/SitPoint 없음"); return false; }
            sb.AppendLine(bag.name + " SitPoint " + V(sp.localPosition) + " → " + V(new Vector3(0f, sitY, SIT_Z)));
            sp.localPosition = new Vector3(0f, sitY, SIT_Z); sp.localRotation = Quaternion.identity;
        }

        // ── 보정 ──
        var model = Humanoids(false);
        if (model != null)
        {
            Spawn(model, bag0.Find("Seat/SitPoint"), bag0, REF_HS);
            AnimationMode.StartAnimationMode();
            for (int pass = 0; pass < 3; pass++)
            {
                qx    = Solve(v => { qx = v; Sample(); return MRecline(); }, -80f, 10f, RECLINE, "RootQ x (등 기댐)");
                knee  = Solve(v => { knee = v; Sample(); return MShin(); }, -1f, 1f, SHIN, "Lower Leg Stretch");
                nod   = Solve(v => { nod = v; Sample(); return MFace(); }, -1f, 1f, FACE, "Neck/Head Nod");
                rt.y  = Solve(v => { rt.y = v; Sample(); return MHipUp(); }, -1.5f, 1.5f, HIP_CLEAR, "RootT.y");
                rt.z  = Solve(v => { rt.z = v; Sample(); return MHipFwd(); }, -1.5f, 1.5f, 0f, "RootT.z");
                legFB = Solve(v => { legFB = v; Sample(); return MAnkle(); }, -1f, 1f, 0.01f, "Upper Leg Front-Back (발목→바닥)");
            }
            Sample();
            sb.AppendLine("보정 결과: RootQ x " + qx.ToString("F1") + "° · RootT " + rt.ToString("F3") + " · 허벅지 " + legFB.ToString("F3") + " · 무릎 " + knee.ToString("F3") + " · 끄덕 " + nod.ToString("F3"));
            sb.AppendLine("  실측 (humanScale " + REF_HS + "): 기댐 " + MRecline().ToString("F1") + "° · 허벅지 " + MThigh().ToString("F1") + "° · 정강이 " + MShin().ToString("F1") + "° · 얼굴 " + MFace().ToString("F1") + "° · 골반 높이 " + MHipUp().ToString("F3") + " · 골반 앞뒤 " + MHipFwd().ToString("F3") + " · 발목 여유 " + MAnkle().ToString("F3") + " m (원본 humanScale " + an.humanScale.ToString("F3") + ")");
            Contacts(bag0, "기준 크기");
            foreach (var hs in SIZES)
            {
                Scale(hs); Sample();
                var hip = bag0.InverseTransformPoint(B(HumanBodyBones.Hips).position); var kn = bag0.InverseTransformPoint(B(HumanBodyBones.LeftLowerLeg).position);
                sb.AppendLine("  크기 " + hs.ToString("F2") + " (키 약 " + (hs * 1.75f).ToString("F2") + " m): 골반 y " + hip.y.ToString("F2") + " · 무릎 y " + kn.y.ToString("F2") + " z " + kn.z.ToString("F2") + " · 발목 여유 " + MAnkle().ToString("+0.00;-0.00") + " m (+ 뜸 / − 바닥 속)");
            }
            Scale(REF_HS);
            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(probe); probe = null;
        }
        else sb.AppendLine("!! 휴머노이드 없음 → 기본값으로 클립 생성 (검증 안 됨)");

        // ── 저장 ──
        Directory.CreateDirectory("Assets/Animations");
        var clip = MakeClip();
        AssetDatabase.DeleteAsset(CLIP_PATH); AssetDatabase.CreateAsset(clip, CLIP_PATH);
        AssetDatabase.DeleteAsset(CTRL_PATH);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(CTRL_PATH, clip);
        AssetDatabase.SaveAssets();
        var tsb = new StringBuilder();
        bool tr = PyriteLieTracking.AddTo(ctrl, tsb);
        sb.AppendLine("트래킹 제어 " + (tr ? "적용" : "실패") + "\n" + tsb.ToString().TrimEnd());

        foreach (var bag in bags)
        {
            var st = bag.Find("Seat").GetComponent<VRC.SDK3.Components.VRCStation>();
            sb.AppendLine(bag.name + " ctrl " + (st.animatorController ? st.animatorController.name : "기본") + " → " + ctrl.name + " · seated " + st.seated);
            st.animatorController = ctrl; EditorUtility.SetDirty(st);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        if (model != null) Renders(bags, model, clip);
        return true;
    }

    // ── 클립 ─────────────────────────────────────────────────────
    static AnimationClip MakeClip()
    {
        var c = new AnimationClip { name = "A_BeanbagSit", frameRate = 60f };
        var valid = new HashSet<string>(HumanTrait.MuscleName);
        var m = new Dictionary<string, float>(FIXED)
        {
            ["Left Upper Leg Front-Back"] = legFB, ["Right Upper Leg Front-Back"] = legFB,
            ["Left Lower Leg Stretch"] = knee,     ["Right Lower Leg Stretch"] = knee,
            ["Neck Nod Down-Up"] = nod,            ["Head Nod Down-Up"] = nod,
        };
        foreach (var kv in m)
        {
            if (!valid.Contains(kv.Key)) { if (sb != null && !sb.ToString().Contains("오타 " + kv.Key)) sb.AppendLine("!! 근육 이름 오타 " + kv.Key); continue; }
            Set(c, kv.Key, kv.Value);
        }
        var q = Quaternion.Euler(qx, 0f, 0f);
        Set(c, "RootQ.x", q.x); Set(c, "RootQ.y", q.y); Set(c, "RootQ.z", q.z); Set(c, "RootQ.w", q.w);
        Set(c, "RootT.x", rt.x); Set(c, "RootT.y", rt.y); Set(c, "RootT.z", rt.z);
        var s = AnimationUtility.GetAnimationClipSettings(c);
        s.loopTime = true;
        s.loopBlendOrientation = true; s.loopBlendPositionY = true; s.loopBlendPositionXZ = true;       // 루트 이동을 자세에 굽기
        s.keepOriginalOrientation = true; s.keepOriginalPositionY = true; s.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(c, s);
        return c;
    }

    static void Set(AnimationClip c, string prop, float v)
    {
        var b = EditorCurveBinding.FloatCurve("", typeof(Animator), prop);
        AnimationUtility.SetEditorCurve(c, b, new AnimationCurve(new Keyframe(0f, v), new Keyframe(1f / 60f, v)));
    }

    // ── 샘플링 · 측정 ────────────────────────────────────────────
    static void Spawn(GameObject model, Transform at, Transform bag, float hs)
    {
        probe = Object.Instantiate(model); probe.name = "_BeanbagSitProbe"; probe.hideFlags = HideFlags.DontSave;
        probe.transform.SetPositionAndRotation(at.position, at.rotation);
        an = probe.GetComponent<Animator>(); an.applyRootMotion = false;
        sit = at; bagT = bag;
        headFwdLocal = Quaternion.Inverse(an.GetBoneTransform(HumanBodyBones.Head).rotation) * at.forward;   // 바인드 포즈 = 정면
        ankleBind = (an.GetBoneTransform(HumanBodyBones.LeftFoot).position.y - probe.transform.position.y) / an.humanScale;   // 바인드 포즈 발목 높이 (humanScale 1 기준)
        Scale(hs);
        foreach (var t in probe.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PyriteBedroomV3.LAYER;
        foreach (var r in probe.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
    }

    static void Sample()
    {
        var c = MakeClip();
        AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(probe, c, 0f); AnimationMode.EndSampling();
        Object.DestroyImmediate(c);
    }

    static Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
    // 옆면(앞-위 평면) 부호 있는 각도: 0 = 앞 수평, +90 = 위, −90 = 아래, ±180 = 뒤. 무릎을 끝까지 굽혀도 단조롭다
    static float Elev(Transform a, Transform b) { var d = b.position - a.position; return Mathf.Atan2(d.y, Vector3.Dot(d, sit.forward)) * Mathf.Rad2Deg; }
    static float MRecline() { var v = B(HumanBodyBones.Head).position - B(HumanBodyBones.Hips).position; return Mathf.Atan2(-Vector3.Dot(v, sit.forward), v.y) * Mathf.Rad2Deg; }
    static float MThigh() => 0.5f * (Elev(B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg)) + Elev(B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg)));
    static float MShin() => 0.5f * (Elev(B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot)) + Elev(B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot)));
    static float MFace() { var f = B(HumanBodyBones.Head).rotation * headFwdLocal; return Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg; }
    static float HS => an.humanScale * probe.transform.localScale.x;   // 실제 크기
    static void Scale(float hs) { probe.transform.localScale = Vector3.one * (hs / an.humanScale); }
    static float MHipUp() => (B(HumanBodyBones.Hips).position.y - sit.position.y) / HS;
    static float MHipFwd() => Vector3.Dot(B(HumanBodyBones.Hips).position - sit.position, sit.forward) / HS;
    // 발목 뼈가 바닥(빈백 로컬 y 0) 위 '바인드 발목 높이' 에서 얼마나 떠 있나 (m). 0 = 발바닥이 바닥에 닿음
    static float MAnkle() => bagT.InverseTransformPoint(B(HumanBodyBones.LeftFoot).position).y - ankleBind * HS;

    static float Solve(System.Func<float, float> f, float lo, float hi, float target, string name)
    {
        float flo = f(lo) - target, fhi = f(hi) - target;
        if (flo * fhi > 0f)
        {
            float best = Mathf.Abs(flo) < Mathf.Abs(fhi) ? lo : hi;
            sb.AppendLine("  !! " + name + " 범위 [" + lo + ", " + hi + "] 에서 목표 " + target + " 못 맞춤 (끝값 " + (flo + target).ToString("F2") + " / " + (fhi + target).ToString("F2") + ") → " + best);
            f(best); return best;
        }
        for (int i = 0; i < 22; i++)
        {
            float mid = 0.5f * (lo + hi), fm = f(mid) - target;
            if (fm * flo <= 0f) { hi = mid; } else { lo = mid; flo = fm; }
        }
        float r = 0.5f * (lo + hi); f(r); return r;
    }

    // 뼈 위치 vs 빈백 윗면 (gap < 0 = 뼈가 빈백 속)
    static void Contacts(Transform bag, string tag)
    {
        sb.AppendLine("  접촉 (" + tag + ", 빈백 로컬 x,y,z · 그 자리 윗면 · 틈):");
        WithCollider(bag, mc =>
        {
            foreach (var hb in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.LeftHand })
            {
                var t = an.GetBoneTransform(hb); if (t == null) continue;
                var l = bag.InverseTransformPoint(t.position);
                float s = Surf(bag, mc, l.x, l.z);
                sb.AppendLine("    " + hb.ToString().PadRight(13) + V(l) + " · 윗면 " + (float.IsNaN(s) ? "밖(바닥 0)" : s.ToString("F3")) + " · 틈 " + (l.y - (float.IsNaN(s) ? 0f : s)).ToString("F3"));
            }
            return 0f;
        });
    }

    // ── 빈백 · 스테이션 ──────────────────────────────────────────
    static Transform[] Bags()
    {
        var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        var root = room ? room.transform.Find("Beanbags") : null;
        if (root == null) { sb.AppendLine("!! TentBedroom/Beanbags 없음 (Z51l 먼저)"); return null; }
        return root.Cast<Transform>().Where(t => t.name.StartsWith("Beanbag_")).OrderBy(t => t.name).ToArray();
    }

    static float WithCollider(Transform bag, System.Func<MeshCollider, float> fn)
    {
        var body = bag.Find("Body");
        var mc = body.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
        try { return fn(mc); } finally { Object.DestroyImmediate(mc); }
    }

    static float Surf(Transform bag, MeshCollider mc, float x, float z)
    {
        var o = bag.TransformPoint(new Vector3(x, 2f, z));
        return mc.Raycast(new Ray(o, -bag.up), out var h, 5f) ? bag.InverseTransformPoint(h.point).y : float.NaN;
    }

    static void Profile(Transform bag)
    {
        var line = new StringBuilder(bag.name + " 윗면 (x 0, z −0.45 → +0.55):");
        WithCollider(bag, mc =>
        {
            for (float z = -0.45f; z <= 0.551f; z += 0.05f)
            {
                float s = Surf(bag, mc, 0f, z);
                line.Append(" " + z.ToString("+0.00;-0.00") + ":" + (float.IsNaN(s) ? "-" : s.ToString("F2")));
            }
            return 0f;
        });
        sb.AppendLine(line.ToString());
    }

    static void StationInfo(Transform bag)
    {
        var seat = bag.Find("Seat"); var st = seat ? seat.GetComponent<VRC.SDK3.Components.VRCStation>() : null;
        if (st == null) { sb.AppendLine("  Station 없음"); return; }
        var bc = seat.GetComponent<BoxCollider>();
        sb.AppendLine("  Station ctrl " + (st.animatorController ? st.animatorController.name : "기본") + " · seated " + st.seated + " · " + st.PlayerMobility
                      + " · SitPoint " + V(st.stationEnterPlayerLocation.localPosition) + " · ExitPoint " + V(st.stationExitPlayerLocation.localPosition)
                      + (bc ? " · 판정 " + V(bc.center) + " / " + V(bc.size) : ""));
    }

    // 프로젝트 안 휴머노이드 모델 (Packages 포함). 첫 번째 유효 모델 반환
    static GameObject Humanoids(bool list)
    {
        var found = new List<(string path, Avatar av)>();
        foreach (var g in AssetDatabase.FindAssets("t:Avatar"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            foreach (var av in AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>())
                if (av.isHuman && av.isValid) found.Add((p, av));
        }
        sb.AppendLine("휴머노이드 모델 " + found.Count + (found.Count > 0 ? ": " + string.Join(" | ", found.Take(10).Select(f => f.path)) : ""));
        foreach (var f in found.OrderBy(f => f.path.StartsWith("Assets/") ? 0 : 1))
        {
            var go = AssetDatabase.LoadMainAssetAtPath(f.path) as GameObject;
            if (go == null) continue;
            var a = go.GetComponent<Animator>();
            if (a == null || a.avatar != f.av) continue;
            if (!list) sb.AppendLine("보정 모델: " + f.path);
            return go;
        }
        return null;
    }

    // ── 렌더 ─────────────────────────────────────────────────────
    static void Renders(Transform[] bags, GameObject model, AnimationClip clip)
    {
        var room = bags[0].parent.parent;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var tvT = room.Find("BedroomPanel/BedroomTV"); bool tv0 = tvT && tvT.gameObject.activeSelf;
        var inst = new List<GameObject>();
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            if (tvT) tvT.gameObject.SetActive(true);
            AnimationMode.StartAnimationMode();
            var heads = new List<Transform>();
            var anims = new List<Animator>();
            foreach (var bag in bags) { Spawn(model, bag.Find("Seat/SitPoint"), bag, REF_HS); inst.Add(probe); anims.Add(an); }
            AnimationMode.BeginSampling();                      // 한 번에 샘플 — 따로 하면 앞 인스턴스가 되돌아감
            foreach (var g in inst) AnimationMode.SampleAnimationClip(g, clip, 0f);
            AnimationMode.EndSampling();
            for (int i = 0; i < bags.Length; i++)
            {
                an = anims[i]; probe = inst[i]; sit = bags[i].Find("Seat/SitPoint"); bagT = bags[i];
                heads.Add(an.GetBoneTransform(HumanBodyBones.Head));
                if (i > 0) Contacts(bags[i], bags[i].name);
            }
            probe = null;
            cam.fieldOfView = 50f;
            Shot(cam, room.TransformPoint(new Vector3(1.6f, 1.5f, 2.3f)), room.TransformPoint(new Vector3(-0.6f, 0.45f, 1.0f)), "bs_room");
            for (int i = 0; i < bags.Length; i++)
            {
                var b = bags[i]; var c = b.position + Vector3.up * 0.5f;
                Shot(cam, c + b.right * 1.7f + Vector3.up * 0.15f, c, "bs_side" + (i + 1));
                Shot(cam, c + b.forward * 1.8f + b.right * 0.5f + Vector3.up * 0.3f, c, "bs_front" + (i + 1));
                var h = heads[i].position + b.forward * 0.12f;
                Shot(cam, h, room.TransformPoint(new Vector3(-2.67f, 1.20f, -0.90f)), "bs_eye" + (i + 1));
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            foreach (var g in inst) if (g) Object.DestroyImmediate(g);
            if (tvT) tvT.gameObject.SetActive(tv0);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rtex = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rtex; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rtex;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rtex);
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag);
        Object.DestroyImmediate(tex);
    }

    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, sb.ToString(), new UTF8Encoding(false));
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
