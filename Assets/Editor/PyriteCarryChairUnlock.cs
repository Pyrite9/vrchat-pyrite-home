// PyriteCarryChairUnlock.cs — 캠프 의자·돗자리를 앉아/누워 있어도 들 수 있게 (Z52l 적용 / Z52m 되돌림). 재실행 안전
//  2026-09-30 관리자: "캠프 의자도 앉아 있어도 들 수 있게" (침실 빈백과 같게)
//  방법: 의자 Seat · 돗자리 Lie_A/B 의 PyriteCarrySeat.pickup 을 비움 → 앉을·누울 때 pickupable = false 를 안 건다 (06:09 관리자: 돗자리도 동일하게)
//  개수 풀(PyriteCampPool)의 빼기·제자리는 PyriteCarrySeat.IsOccupied() 로 앉은 의자를 건너뜀
//  ⚠ Z28b(의자 재빌드)·Z31b(캠프 소품 = 돗자리)·Z43b(캠프 풀 재빌드)는 pickup 을 다시 연결한다 → 그 뒤 Z52l 다시
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteCarryChairUnlock
{
    [MenuItem("Tools/Pyrite3/Z52l. Camp Chair+Mat Unlock While Seated", false, 5138)]
    public static void Apply() => Run("Z52l", true);

    [MenuItem("Tools/Pyrite3/Z52m. Camp Chair+Mat Lock While Seated", false, 5139)]
    public static void Revert() => Run("Z52m", false);

    static void Run(string tag, bool unlock)
    {
        var sb = new StringBuilder("[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            int n = 0;
            var all = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PyriteCarrySeat>(true)).ToArray();
            foreach (var cs in all)
            {
                var root = cs.GetComponentsInParent<VRCPickup>(true).FirstOrDefault();
                if (root == null || !(root.gameObject.name.StartsWith("CarryChair") || root.gameObject.name.StartsWith("PicnicMat"))) continue;   // 의자·돗자리만 (침대·빈백 제외)
                var want = unlock ? null : root;
                sb.AppendLine("  " + root.gameObject.name + "/" + cs.gameObject.name + " pickup " + (cs.pickup ? cs.pickup.gameObject.name : "null") + " → " + (want ? want.gameObject.name : "null"));
                Undo.RecordObject(cs, tag);
                cs.pickup = want;
                UdonSharpEditorUtility.CopyProxyToUdon(cs);
                EditorUtility.SetDirty(cs);
                var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs); if (ub) EditorUtility.SetDirty(ub);
                n++;
            }
            sb.AppendLine("의자·돗자리 자리 " + n + "개 → " + (unlock ? "앉아 있어도 들기 가능" : "앉으면 들기 금지(원래)"));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_chair_unlock.txt", sb.ToString(), new UTF8Encoding(false));
    }
}
#endif
