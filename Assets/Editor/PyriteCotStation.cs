using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

// 야전침대에 "눕기" 스테이션을 붙인다.
//
// VRCStation 의 기본 포즈는 앉기다. 누우려면 휴머노이드 애니메이션 클립을 만들어
// animatorController 로 물려야 한다. 클립은 에디터에서 직접 생성한다.
//
//  · RootQ 로 몸 전체를 X축 -90° 회전  → 위를 보고 눕는 자세(머리는 스테이션의 -Z 쪽)
//  · RootT.y 로 몸을 천 위로 살짝 띄움
//  · 근육(muscle) 값으로 팔을 몸통 옆으로 내리고 다리를 모은다
//
// 근육 이름은 HumanTrait.MuscleName 과 대조해서 오타면 로그로 잡는다.
// 오타난 이름은 조용히 무시되기 때문에 "아무것도 안 바뀌는데 에러도 없는" 상태가 된다.
public static class PyriteCotStation
{
    const string CLIP_PATH = "Assets/Animations/A_LieDown.anim";
    const string CTRL_PATH = "Assets/Animations/AC_LieDown.controller";

    // 누운 자세 — 값 하나씩 조정 가능. 범위는 전부 -1 ~ +1
    static readonly Dictionary<string, float> POSE = new Dictionary<string, float>
    {
        { "Left Arm Down-Up",        -0.72f },   // -1 = 완전히 내림
        { "Right Arm Down-Up",       -0.72f },
        { "Left Arm Front-Back",      0.08f },
        { "Right Arm Front-Back",     0.08f },
        { "Left Forearm Stretch",     0.20f },
        { "Right Forearm Stretch",    0.20f },
        { "Left Upper Leg In-Out",   -0.12f },   // 다리 모으기
        { "Right Upper Leg In-Out",  -0.12f },
        { "Left Upper Leg Front-Back", 0.00f },
        { "Right Upper Leg Front-Back",0.00f },
        { "Left Lower Leg Stretch",   0.00f },
        { "Right Lower Leg Stretch",  0.00f },
        { "Left Foot Up-Down",       -0.20f },   // 발끝 살짝 펴기
        { "Right Foot Up-Down",      -0.20f },
        { "Spine Front-Back",         0.00f },
        { "Chest Front-Back",         0.00f },
        { "Neck Nod Down-Up",         0.10f },
        { "Head Nod Down-Up",         0.18f },   // 턱 살짝 당김
    };

    const float BODY_LIFT = 0.10f;   // 천 위로 몸을 띄우는 높이 (누운 몸 두께의 절반)

    [MenuItem("Tools/Pyrite/R. Setup Cot Lie Station", false, 282)]
    public static void Menu()
    {
        var cot = FindRoot("Cot");
        if (cot == null) { Debug.LogError("[COT] Cot 루트 없음 — 먼저 Q 를 실행할 것"); return; }
        Apply(cot);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    public static void Apply(GameObject cot)
    {
        var ctrl = BuildController();
        if (ctrl == null) return;

        // 좌석/퇴장 지점 — 침대 긴 축은 로컬 X. 머리가 -X 쪽으로 가게 90° 돌린다.
        var lie  = Child(cot.transform, "LiePoint",  new Vector3(0.10f, 0.19f, 0.00f), Quaternion.Euler(0f, 90f, 0f));
        var exit = Child(cot.transform, "ExitPoint", new Vector3(0.10f, 0.05f, -0.95f), Quaternion.Euler(0f, 180f, 0f));

        var st = cot.GetComponent<VRCStation>();
        if (st == null) st = Undo.AddComponent<VRCStation>(cot);
        Undo.RecordObject(st, "cot station");
        st.stationEnterPlayerLocation = lie;
        st.stationExitPlayerLocation  = exit;
        st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
        st.canUseStationFromStation = true;
        st.disableStationExit = false;
        st.seated = true;
        st.animatorController = ctrl;
        EditorUtility.SetDirty(cot);

        Debug.Log("[COT] 눕기 스테이션 — 머리 -X 쪽 / 몸 띄움 " + BODY_LIFT.ToString("F2")
                  + "m / 클립 " + CLIP_PATH);
    }

    // ── 휴머노이드 클립 + 컨트롤러 ────────────────────────────────
    static AnimatorController BuildController()
    {
        System.IO.Directory.CreateDirectory("Assets/Animations");

        var clip = new AnimationClip { name = "A_LieDown", frameRate = 60f };

        // 근육 이름 검증 — 오타는 조용히 무시되므로 반드시 대조한다
        var valid = new HashSet<string>(HumanTrait.MuscleName);
        var bad = POSE.Keys.Where(k => !valid.Contains(k)).ToArray();
        if (bad.Length > 0)
            Debug.LogError("[COT] 근육 이름 오타 " + bad.Length + "개: " + string.Join(", ", bad)
                           + "\n  (오타난 항목은 적용되지 않는다)");

        foreach (var kv in POSE)
            if (valid.Contains(kv.Key)) SetConst(clip, kv.Key, kv.Value);

        // 몸 전체를 X축 -90° 회전 → 등을 대고 눕는다(얼굴이 하늘을 봄)
        //   +90° 로 하면 엎드린 자세가 된다
        var q = Quaternion.Euler(-90f, 0f, 0f);
        SetConst(clip, "RootQ.x", q.x); SetConst(clip, "RootQ.y", q.y);
        SetConst(clip, "RootQ.z", q.z); SetConst(clip, "RootQ.w", q.w);
        SetConst(clip, "RootT.x", 0f);
        SetConst(clip, "RootT.y", BODY_LIFT);
        SetConst(clip, "RootT.z", 0f);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(CLIP_PATH);
        if (old != null) AssetDatabase.DeleteAsset(CLIP_PATH);
        AssetDatabase.CreateAsset(clip, CLIP_PATH);

        var oldC = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (oldC != null) AssetDatabase.DeleteAsset(CTRL_PATH);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(CTRL_PATH, clip);
        AssetDatabase.SaveAssets();

        if (ctrl == null) Debug.LogError("[COT] 컨트롤러 생성 실패");
        return ctrl;
    }

    static void SetConst(AnimationClip clip, string prop, float value)
    {
        var b = EditorCurveBinding.FloatCurve("", typeof(Animator), prop);
        var c = new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value));
        AnimationUtility.SetEditorCurve(clip, b, c);
    }

    // ── 도우미 ───────────────────────────────────────────────────
    static Transform Child(Transform parent, string name, Vector3 lp, Quaternion lr)
    {
        Transform t = null;
        foreach (Transform c in parent) if (c.name == name) { t = c; break; }
        if (t == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "cot point");
            t = go.transform;
            t.SetParent(parent, false);
        }
        t.localPosition = lp;
        t.localRotation = lr;
        t.localScale = Vector3.one;
        return t;
    }

    static GameObject FindRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) return r;
        return null;
    }
}
