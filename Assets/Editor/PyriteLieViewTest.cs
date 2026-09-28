// PyriteLieViewTest.cs — 눕기 시점이 땅으로 꺼지는 문제(데스크톱) 인게임 A/B 시험용 (Z49q 적용 / Z49r 되돌리기)
//  침실 Lie_1~4 를 서로 다른 설정으로:  A = 지금(Seated + Pose Space) / B = Seated + Pose Space 없음 / C = Seated 끔 + Pose Space / D = Seated 끔 + Pose Space 없음
//  Pose Space 없는 컨트롤러는 AC_LieDown 복사본(AC_LieDown_NoPS)에서 Temporary Pose Space 동작만 뺀 것. 원본 AC_LieDown 은 안 건드림
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteLieViewTest
{
    const string SRC = "Assets/Animations/AC_LieDown.controller";
    const string NOPS = "Assets/Animations/AC_LieDown_NoPS.controller";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z49q. Lie View Test (A-D)", false, 4916)]
    public static void Apply()
    {
        sb = new StringBuilder("[Z49q] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49r. Lie View Test Revert", false, 4917)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z49r] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var src = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SRC);
        foreach (var st in Stations())
        {
            st.animatorController = src; st.seated = true; EditorUtility.SetDirty(st);
            SetText(st, "Lie down");
            sb.AppendLine("  " + st.name + " → AC_LieDown, seated true");
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (AC_LieDown_NoPS 에셋은 남김)");
        Flush();
    }

    static VRC.SDK3.Components.VRCStation[] Stations()
    {
        var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        if (room == null) return new VRC.SDK3.Components.VRCStation[0];
        return Enumerable.Range(1, 4).Select(i => room.transform.Find("Beds/Lie_" + i)).Where(t => t)
            .Select(t => t.GetComponent<VRC.SDK3.Components.VRCStation>()).Where(s => s != null).ToArray();
    }

    static void SetText(VRC.SDK3.Components.VRCStation st, string text)
    {
        foreach (var ub in st.GetComponents<VRC.Udon.UdonBehaviour>()) { ub.interactText = text; EditorUtility.SetDirty(ub); }
    }

    static string Describe(AnimatorController c)
    {
        var s = new StringBuilder();
        foreach (var layer in c.layers)
            foreach (var cs in layer.stateMachine.states)
            {
                s.Append("    [" + layer.name + "] " + cs.state.name + " motion " + (cs.state.motion ? cs.state.motion.name : "-") + " behaviours:");
                foreach (var b in cs.state.behaviours)
                {
                    s.Append(" " + b.GetType().Name);
                    var so = new SerializedObject(b); var it = so.GetIterator(); it.NextVisible(true);
                    var parts = new System.Collections.Generic.List<string>();
                    do { if (it.name != "m_Script") parts.Add(it.name + "=" + Val(it)); } while (it.NextVisible(false));
                    s.Append("{" + string.Join(", ", parts) + "}");
                }
                s.AppendLine();
            }
        return s.ToString();
    }

    static string Val(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.Float: return p.floatValue.ToString("F2");
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString();
            default: return p.propertyType.ToString();
        }
    }

    static void Inner()
    {
        var src = AssetDatabase.LoadAssetAtPath<AnimatorController>(SRC);
        if (src == null) { sb.AppendLine("!! " + SRC + " 없음"); return; }
        sb.AppendLine("원본 " + SRC + ":\n" + Describe(src));

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(NOPS) != null) AssetDatabase.DeleteAsset(NOPS);
        if (!AssetDatabase.CopyAsset(SRC, NOPS)) { sb.AppendLine("!! 복사 실패"); return; }
        AssetDatabase.ImportAsset(NOPS);
        var nops = AssetDatabase.LoadAssetAtPath<AnimatorController>(NOPS);
        int removed = 0;
        foreach (var layer in nops.layers)
            foreach (var cs in layer.stateMachine.states)
            {
                var keep = cs.state.behaviours.Where(b => !b.GetType().Name.Contains("TemporaryPoseSpace")).ToArray();
                foreach (var b in cs.state.behaviours.Where(b => b.GetType().Name.Contains("TemporaryPoseSpace"))) { Object.DestroyImmediate(b, true); removed++; }
                cs.state.behaviours = keep;
            }
        EditorUtility.SetDirty(nops); AssetDatabase.SaveAssets();
        sb.AppendLine("복사본 " + NOPS + " (Pose Space 동작 " + removed + "개 뺌):\n" + Describe(nops));

        var sts = Stations();
        if (sts.Length != 4) { sb.AppendLine("!! Lie_1~4 중 " + sts.Length + "개만 찾음"); return; }
        var cfg = new[] {
            new { c = (RuntimeAnimatorController)src,  seated = true,  t = "Lie A (now)" },
            new { c = (RuntimeAnimatorController)nops, seated = true,  t = "Lie B (no pose space)" },
            new { c = (RuntimeAnimatorController)src,  seated = false, t = "Lie C (not seated)" },
            new { c = (RuntimeAnimatorController)nops, seated = false, t = "Lie D (not seated, no pose space)" },
        };
        for (int i = 0; i < 4; i++)
        {
            var st = sts[i];
            st.animatorController = cfg[i].c; st.seated = cfg[i].seated; EditorUtility.SetDirty(st);
            SetText(st, cfg[i].t);
            var lp = st.stationEnterPlayerLocation;
            sb.AppendLine("  " + st.name + " x " + st.transform.localPosition.x.ToString("F2") + " → " + cfg[i].t + " | controller " + cfg[i].c.name + " seated " + cfg[i].seated
                + " mobility " + st.PlayerMobility + " enter " + (lp ? lp.position.ToString("F2") + " rot " + lp.rotation.eulerAngles.ToString("F0") : "-"));
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_lie_view.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }
}
#endif
