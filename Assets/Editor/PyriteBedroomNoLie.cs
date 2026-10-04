// PyriteBedroomNoLie.cs — 텐트 침실 매트 위 눕기 Station(Beds/Lie_1~4) 제거
// Tools ▸ Pyrite3 ▸ Z52k. Bedroom Lie Stations Remove / Z52l. Bedroom Lie Stations Restore
//  2026-10-01 04:12 관리자: "텐트 안에 침대에 눕기 상호작용 다 빼라. 실제로 누워보니까 컨트롤러에 걸려서 엄청나게 거슬린다. 다 로코모션으로 누우면 된다"
//  제거 대상: TentBedroom/Beds/Lie_* (VRCStation + PyriteCarrySeat \"Lie down\" + Pickup 레이어 판정 상자 + LiePoint/ExitPoint)
//  건드리지 않는 것: 매트 · 이불(PyriteBlanket — 뼈 위치만 읽고 Station 을 안 씀) · 빈백 앉기 · 캠프 야전침대/돗자리 눕기
//  재실행 안전(없으면 아무것도 안 함). 지오메트리·조명 변화 없음 → 라이트맵·큐브맵 재베이크 불필요
//  되돌림: Z52l (PyriteBedroomV3.RestoreLieStations) 또는 git. Z49i 는 BUILD_LIE=false 라 Lie 를 다시 만들지 않는다
//  로그 Logs/pyrite_bed_nolie.txt (+ 클립보드)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomNoLie
{
    const string LOG = "Logs/pyrite_bed_nolie.txt";
    static StringBuilder sb;

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static string Path(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static bool UnderLie(Transform t, Transform beds)
    {
        while (t != null && t != beds)
        {
            if (t.parent == beds && t.name.StartsWith("Lie_")) return true;
            t = t.parent;
        }
        return false;
    }

    [MenuItem("Tools/Pyrite3/Z52k. Bedroom Lie Stations Remove", false, 5137)]
    public static void Remove()
    {
        sb = new StringBuilder("[Z52k] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z52l. Bedroom Lie Stations Restore", false, 5138)]
    public static void Restore()
    {
        sb = new StringBuilder("[Z52l] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { PyriteBedroomV3.RestoreLieStations(sb); Report("복구 후"); sb.AppendLine("RESULT: DONE"); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static void Report(string tag)
    {
        var room = Root("TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
        var sts = room.GetComponentsInChildren<VRC.SDK3.Components.VRCStation>(true);
        sb.AppendLine("[" + tag + "] TentBedroom VRCStation " + sts.Length + "개");
        foreach (var st in sts)
        {
            var seat = st.GetComponent<VRC.Udon.UdonBehaviour>();
            sb.AppendLine("  " + Path(st.transform).Replace("TentBedroom/", "") + "  layer " + st.gameObject.layer + "  active " + st.gameObject.activeInHierarchy
                + "  interact '" + (seat ? seat.interactText : "-") + "'  controller " + (st.animatorController ? st.animatorController.name : "null"));
        }
    }

    static void Inner()
    {
        var room = Root("TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
        var beds = room.transform.Find("Beds");
        if (beds == null) { sb.AppendLine("!! Beds 없음"); return; }

        Report("제거 전");

        var lies = Enumerable.Range(0, beds.childCount).Select(i => beds.GetChild(i)).Where(t => t.name.StartsWith("Lie_")).ToList();
        sb.AppendLine("제거 대상 Lie_* " + lies.Count + "개: " + string.Join(", ", lies.Select(t => t.name)));
        if (lies.Count == 0) { sb.AppendLine("RESULT: NOTHING TO DO (이미 없음)"); return; }

        // 다른 Udon 이 Lie_* 를 변수로 잡고 있는지 (지우면 null 이 되는 참조)
        int refs = 0, scanned = 0;
        foreach (var ub in Object.FindObjectsOfType<VRC.Udon.UdonBehaviour>(true))
        {
            if (UnderLie(ub.transform, beds)) continue;
            scanned++;
            var tbl = ub.publicVariables;
            if (tbl == null) continue;
            foreach (var sym in tbl.VariableSymbols.ToArray())
            {
                UnityEngine.Object v;
                try { if (!tbl.TryGetVariableValue<UnityEngine.Object>(sym, out v) || v == null) continue; } catch { continue; }
                Transform tv = v is GameObject g ? g.transform : v is Component c ? c.transform : null;
                if (tv != null && UnderLie(tv, beds))
                {
                    refs++;
                    sb.AppendLine("  !! 참조: " + Path(ub.transform) + " . " + sym + " → " + Path(tv));
                }
            }
        }
        sb.AppendLine("Udon " + scanned + "개 검사 → Lie_* 를 잡은 변수 " + refs + "개");

        int layer13Before = room.GetComponentsInChildren<Collider>(true).Count(c => c.gameObject.layer == 13);
        foreach (var t in lies) Object.DestroyImmediate(t.gameObject);
        sb.AppendLine("삭제 완료. Pickup 레이어(13) 콜라이더 " + layer13Before + " → " + room.GetComponentsInChildren<Collider>(true).Count(c => c.gameObject.layer == 13));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Report("제거 후");
        var left = Enumerable.Range(0, beds.childCount).Select(i => beds.GetChild(i).name).ToArray();
        sb.AppendLine("Beds 자식: " + string.Join(", ", left));
        sb.AppendLine("남은 침실 Station 은 Beanbags 앉기만이어야 함 (눕기 0)");
        sb.AppendLine("RESULT: DONE");
    }

    static void Flush() { Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString() + "\n"); EditorGUIUtility.systemCopyBuffer = sb.ToString(); }
}
#endif
