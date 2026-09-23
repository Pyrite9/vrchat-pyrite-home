// Tools ▸ Pyrite ▸ W. Lock Media Player (Public)
// Public world: only the instance master / instance owner / whitelisted super users may change what the TV plays.
#if UNITY_EDITOR
using System.Text;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteMediaLock
{
    static readonly string[] SUPER_USERS = { "Pyrite9" };   // VRChat display name(s)

    [MenuItem("Tools/Pyrite/W. Lock Media Player (Public)")]
    public static void Run()
    {
        var log = new StringBuilder("[W] ");
        UdonSharpBehaviour tv = null, wl = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
            {
                string n = b.GetType().Name;
                if (n == "TVManager" && tv == null) tv = b;
                else if (n == "TVManagedWhitelist" && wl == null) wl = b;
            }
        if (tv == null) { Debug.LogError("[W] TVManager not found"); return; }

        var so = new SerializedObject(tv);
        Set(so, "lockedByDefault", true, log);          // starts locked: only authorized users can change media
        Set(so, "allowMasterControl", true, log);       // current instance master
        Set(so, "allowFirstMasterControl", true, log);  // whoever opened the instance
        Set(so, "instanceOwnerIsSuper", true, log);     // owner of invite/friends instances
        Set(so, "firstMasterIsSuper", false, log);
        Set(so, "superUserLockOverride", true, log);    // a super user's lock can't be broken by a plain master
        Set(so, "disallowUnauthorizedUsers", false, log); // others can still adjust their own local volume etc.
        so.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon(tv);
        log.Append("| TV=").Append(tv.gameObject.name);

        if (wl != null)
        {
            var sw = new SerializedObject(wl);
            var p = sw.FindProperty("superUsers");
            if (p != null && p.isArray)
            {
                p.arraySize = SUPER_USERS.Length;
                for (int i = 0; i < SUPER_USERS.Length; i++) p.GetArrayElementAtIndex(i).stringValue = SUPER_USERS[i];
                log.Append(" | superUsers=").Append(string.Join(",", SUPER_USERS));
            }
            sw.ApplyModifiedProperties();
            UdonSharpEditorUtility.CopyProxyToUdon(wl);
            // make sure the TV actually uses this auth plugin
            var ap = so.FindProperty("authPlugin");
            if (ap != null && ap.objectReferenceValue == null)
            {
                so.Update(); ap.objectReferenceValue = wl; so.ApplyModifiedProperties();
                UdonSharpEditorUtility.CopyProxyToUdon(tv);
                log.Append(" | authPlugin linked");
            }
            else log.Append(" | authPlugin=").Append(ap == null ? "(no field)" : ap.objectReferenceValue == null ? "null" : ap.objectReferenceValue.name);
        }
        else log.Append(" | TVManagedWhitelist not found");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(log.ToString());
    }

    static void Set(SerializedObject so, string name, bool v, StringBuilder log)
    {
        var p = so.FindProperty(name);
        if (p == null) { log.Append(name).Append("=MISSING "); return; }
        log.Append(name).Append(' ').Append(p.boolValue ? 1 : 0).Append("->").Append(v ? 1 : 0).Append(' ');
        p.boolValue = v;
    }
}
#endif
