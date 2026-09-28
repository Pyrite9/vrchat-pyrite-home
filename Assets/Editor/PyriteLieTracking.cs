using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

// 눕기 스테이션(야전침대 Cot, 돗자리 Lie_A/B + 풀 복제본)의 트래킹 문제.
//
// 원인(gogoloco-idle-tracking.md 4절 "Seated 켬 + 커스텀 컨트롤러"):
//   Station.animatorController 는 아바타의 Sitting 컨트롤러를 대체한다.
//   GoGo Sitting 이 걸던 Tracking Control(전부 Animation) + Pose Space 가 빠지고,
//   Seated=true 라 GoGo Base 도 멈춘다 → 마지막으로 걸린 Stand 상태의 값
//   (Hip·발 = Tracking, 데스크톱은 Pose Space 해제)이 그대로 남는다.
//   AC_LieDown 의 유일한 상태 A_LieDown 에 VRC 동작이 하나도 없다(YAML 실측).
//
// 해결: A_LieDown 진입 시 Tracking Control(Hip·LFoot·RFoot = Animation) + Temporary Pose Space(enter, 0.5 s).
//   월드 SDK 3.10.5 의 SDKBase VRC_AnimatorTrackingControl / VRC_AnimatorTemporaryPoseSpace 는 abstract 다(Z44a 실측).
//   구체 타입 VRCAnimatorTrackingControl / VRCAnimatorTemporaryPoseSpace 는 아바타 SDK 의 VRCSDK3A.dll 에만 있다
//   → 아바타 프로젝트(같은 3.10.5)에서 Assets/VRCSDK3A/ 로 복사해 쓴다(저장소 제외, .gitignore + X2 금지).
//
// Z44a 실측 / Z44b 적용 / Z44c 되돌림. 로그 Logs/pyrite_lie_tracking.txt
public static class PyriteLieTracking
{
    const string CTRL_PATH = "Assets/Animations/AC_LieDown.controller";
    const string LOG = "Logs/pyrite_lie_tracking.txt";
    const string T_TRACK = "VRC_AnimatorTrackingControl";
    const string T_POSE  = "VRC_AnimatorTemporaryPoseSpace";
    // 2026-09-28 인게임 A/B(Z49q): Seated 켬 + Pose Space 켬 = 데스크톱 시점이 바닥 밑으로 꺼짐(앉기 보정과 이중으로 내려감).
    //   Seated 켬 + Pose Space 끔 = 정상 → Pose Space 는 넣지 않는다. 다시 넣으려면 true (그럼 Station Seated 를 꺼야 함)
    const bool USE_POSE_SPACE = false;

    // Animation 으로 잡을 부위. 나머지는 NoChange
    static readonly string[] ANIM_PARTS = { "trackingHip", "trackingLeftFoot", "trackingRightFoot" };
    const float POSE_DELAY = 0.5f;   // GoGo Sitting 과 같은 값

    [MenuItem("Tools/Pyrite3/Z44a. Lie Tracking Report", false, 160)]
    public static void Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Z44a Lie Tracking Report " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        foreach (var n in new[] { T_TRACK, T_POSE })
        {
            var t = FindType(n);
            if (t == null) { sb.AppendLine("[TYPE] " + n + " : 없음"); continue; }
            sb.AppendLine("[TYPE] " + t.FullName + " @ " + t.Assembly.GetName().Name
                          + " | abstract " + t.IsAbstract + " | base " + t.BaseType?.FullName);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var extra = f.FieldType.IsEnum ? " {" + string.Join(",", Enum.GetNames(f.FieldType)) + "}" : "";
                sb.AppendLine("    field " + f.Name + " : " + f.FieldType.Name + extra);
            }
            var subs = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .Where(x => x != t && t.IsAssignableFrom(x)).Select(x => x.FullName + (x.IsAbstract ? "(abstract)" : ""));
            sb.AppendLine("    subclasses: " + string.Join(", ", subs.DefaultIfEmpty("없음")));
        }

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (ctrl == null) sb.AppendLine("[CTRL] " + CTRL_PATH + " 없음");
        else
        {
            foreach (var layer in ctrl.layers)
                foreach (var cs in layer.stateMachine.states)
                {
                    var bs = cs.state.behaviours;
                    sb.AppendLine("[CTRL] " + layer.name + " / " + cs.state.name + " (default "
                                  + (layer.stateMachine.defaultState == cs.state) + ") behaviours " + bs.Length);
                    foreach (var b in bs) sb.AppendLine("    " + Describe(b));
                }
        }

        var stations = Resources.FindObjectsOfTypeAll<VRC.SDK3.Components.VRCStation>()
            .Where(s => s.gameObject.scene.IsValid() && s.gameObject.scene == SceneManager.GetActiveScene())
            .OrderBy(s => Path(s.transform)).ToArray();
        int lie = 0;
        foreach (var s in stations)
        {
            string c = s.animatorController ? s.animatorController.name : "-";
            if (c == "AC_LieDown") lie++;
            sb.AppendLine("[STATION] " + Path(s.transform) + " | active " + s.gameObject.activeInHierarchy
                          + " | seated " + s.seated + " | " + s.PlayerMobility + " | ctrl " + c);
        }
        sb.AppendLine("[SUM] stations " + stations.Length + " / AC_LieDown " + lie);
        Write(sb);
    }

    [MenuItem("Tools/Pyrite3/Z44b. Lie Tracking Apply", false, 161)]
    public static void Apply()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (ctrl == null) { Debug.LogError("[Z44b] " + CTRL_PATH + " 없음"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("# Z44b Apply " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        if (AddTo(ctrl, sb)) Report2(ctrl, sb);
        Write(sb);
    }

    [MenuItem("Tools/Pyrite3/Z44c. Lie Tracking Revert", false, 162)]
    public static void Revert()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (ctrl == null) { Debug.LogError("[Z44c] " + CTRL_PATH + " 없음"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("# Z44c Revert " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        int n = 0;
        foreach (var layer in ctrl.layers)
            foreach (var cs in layer.stateMachine.states) n += Strip(cs.state);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        sb.AppendLine("removed " + n);
        Report2(ctrl, sb);
        Write(sb);
    }

    // PyriteCotStation(R)이 컨트롤러를 새로 만들 때도 부른다
    public static bool AddTo(AnimatorController ctrl, StringBuilder sb = null)
    {
        sb = sb ?? new StringBuilder();
        var tTrack = FindConcrete(T_TRACK);
        var tPose  = FindConcrete(T_POSE);
        if (tTrack == null || tPose == null)
        {
            var msg = "[Z44b] 구체 타입이 없다 — track " + (tTrack?.FullName ?? "없음") + " / pose " + (tPose?.FullName ?? "없음")
                      + ". Assets/VRCSDK3A/VRCSDK3A.dll 이 있는지 확인. 적용 안 함";
            Debug.LogError(msg); sb.AppendLine(msg);
            return false;
        }

        foreach (var layer in ctrl.layers)
        {
            var state = layer.stateMachine.defaultState;
            if (state == null) continue;
            Strip(state);   // 재실행 안전

            var tc = state.AddStateMachineBehaviour(tTrack);
            foreach (var f in tTrack.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!f.FieldType.IsEnum || !f.Name.StartsWith("tracking")) continue;
                var want = ANIM_PARTS.Contains(f.Name) ? "Animation" : "NoChange";
                if (!Enum.GetNames(f.FieldType).Contains(want))
                { sb.AppendLine("  !! " + f.Name + " 에 " + want + " 값 없음"); continue; }
                f.SetValue(tc, Enum.Parse(f.FieldType, want));
            }

            EditorUtility.SetDirty(tc);
            string psDesc = "Pose Space 없음";
            if (USE_POSE_SPACE)
            {
                var ps = state.AddStateMachineBehaviour(tPose);
                SetField(ps, "enterPoseSpace", true, sb);
                SetField(ps, "fixedDelay", true, sb);
                SetField(ps, "delayTime", POSE_DELAY, sb);
                EditorUtility.SetDirty(ps); psDesc = Describe(ps);
            }
            sb.AppendLine("  " + layer.name + " / " + state.name + " ← " + Describe(tc) + " + " + psDesc);
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return true;
    }

    // ── 도우미 ───────────────────────────────────────────────────
    static int Strip(AnimatorState state)
    {
        var keep = new List<StateMachineBehaviour>(); int n = 0;
        foreach (var b in state.behaviours)
        {
            if (b == null) { n++; continue; }   // 스크립트 누락 항목도 정리
            var bt = b.GetType();
            if (IsA(bt, T_TRACK) || IsA(bt, T_POSE)) { UnityEngine.Object.DestroyImmediate(b, true); n++; }
            else keep.Add(b);
        }
        state.behaviours = keep.ToArray();
        return n;
    }

    static void Report2(AnimatorController ctrl, StringBuilder sb)
    {
        foreach (var layer in ctrl.layers)
            foreach (var cs in layer.stateMachine.states)
            {
                sb.AppendLine("[CTRL] " + cs.state.name + " behaviours " + cs.state.behaviours.Length);
                foreach (var b in cs.state.behaviours) sb.AppendLine("    " + Describe(b));
            }
    }

    static void SetField(object o, string name, object v, StringBuilder sb)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f == null) { sb.AppendLine("  !! 필드 없음 " + name); return; }
        f.SetValue(o, Convert.ChangeType(v, f.FieldType));
    }

    static string Describe(StateMachineBehaviour b)
    {
        if (b == null) return "(null — 스크립트 누락)";
        var t = b.GetType();
        var parts = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType.IsEnum || f.FieldType == typeof(bool) || f.FieldType == typeof(float))
            .Select(f => f.Name.Replace("tracking", "") + "=" + f.GetValue(b));
        return t.Name + " {" + string.Join(" ", parts) + "}";
    }

    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .FirstOrDefault(t => t.Name == name && typeof(StateMachineBehaviour).IsAssignableFrom(t));

    static bool IsA(Type t, string baseName)
    {
        for (; t != null; t = t.BaseType) if (t.Name == baseName) return true;
        return false;
    }

    // 추상 기반(SDKBase) 을 상속한 구체 타입 — 아바타 SDK 의 VRCAnimator* (Assets/VRCSDK3A/VRCSDK3A.dll)
    static Type FindConcrete(string baseName)
    {
        var bt = FindType(baseName);
        if (bt == null) return null;
        if (!bt.IsAbstract) return bt;
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .Where(x => !x.IsAbstract && bt.IsAssignableFrom(x))
            .OrderBy(x => x.Assembly.GetName().Name == "VRCSDK3A" ? 0 : 1)
            .FirstOrDefault();
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    static void Write(StringBuilder sb)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, sb.ToString(), new UTF8Encoding(false));
        Debug.Log(sb.ToString());
    }
}
