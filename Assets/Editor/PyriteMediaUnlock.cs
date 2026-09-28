// Tools ▸ Pyrite3 ▸ Z48a. TV Unlock (Everyone) / Z48b. TV Lock (= W, 원래대로)
//  2026-09-28 관리자: TV 조작을 모두가 할 수 있게. W(PyriteMediaLock) 는 lockedByDefault = true 로 인스턴스 마스터·주인·Pyrite9 만 바꿀 수 있게 잠갔다
//  → lockedByDefault false: 입장 때 잠겨 있지 않아 누구나 URL·재생·일시정지·탐색 가능
//    권한자(마스터·인스턴스 주인·Pyrite9)는 여전히 필요할 때 잠글 수 있다(allowMasterControl / superUserLockOverride 는 그대로)
//  TVManager 의 bool 필드를 전후로 전부 적는다 → Logs/pyrite_media.txt
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteMediaUnlock
{
    const string LOG = "Logs/pyrite_media.txt";

    static UdonSharpBehaviour FindTV()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
                if (b.GetType().Name == "TVManager") return b;
        return null;
    }

    static string Bools(SerializedObject so)
    {
        var sb = new StringBuilder();
        var it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyType == SerializedPropertyType.Boolean) sb.Append(it.name).Append('=').Append(it.boolValue ? 1 : 0).Append(' ');
        }
        return sb.ToString();
    }

    [MenuItem("Tools/Pyrite3/Z48a. TV Unlock (Everyone)", false, 180)]
    public static void Unlock()
    {
        var log = new StringBuilder("[Z48a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var tv = FindTV();
        if (tv == null) { log.AppendLine("!! TVManager 없음"); File.AppendAllText(LOG, log.ToString()); return; }
        var so = new SerializedObject(tv);
        log.AppendLine("  before: " + Bools(so));
        var p = so.FindProperty("lockedByDefault");
        if (p == null) log.AppendLine("  !! lockedByDefault 필드 없음");
        else { p.boolValue = false; so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(tv); EditorUtility.SetDirty(tv); }
        so.Update();
        log.AppendLine("  after:  " + Bools(so));
        log.AppendLine("  TV = " + tv.gameObject.name);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        log.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, log.ToString());
        Debug.Log(log.ToString());
    }

    [MenuItem("Tools/Pyrite3/Z48b. TV Lock (= W, revert)", false, 181)]
    public static void Lock()
    {
        PyriteMediaLock.Run();
        var tv = FindTV();
        File.AppendAllText(LOG, "[Z48b] " + System.DateTime.Now.ToString("HH:mm:ss") + " W 다시 적용\n  now: " + (tv != null ? Bools(new SerializedObject(tv)) : "TV 없음") + "\nRESULT: DONE\n");
    }
}
#endif
