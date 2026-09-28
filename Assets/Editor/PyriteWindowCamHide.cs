// PyriteWindowCamHide.cs — 창 카메라(BedroomWindowCam)에서만 안 보일 물체를 레이어 25 'WindowCamHide' 로 (Z49y 적용 / Z49yr 되돌리기)
//  대상: 캠프 텐트 Camp/camp01_tent_BRN (메시·머티리얼 그대로, 레이어만). 원래 레이어는 Logs/pyrite_windowcam_hide.txt
//  메인 카메라(mask −1)는 그대로 보이고, 다른 카메라·VRC 거울 중 레이어 0 을 보는 것에는 25 를 추가해 계속 보이게
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteWindowCamHide
{
    public const int LAYER = 25;
    static readonly string[] TARGETS = { "Camp/camp01_tent_BRN" };
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z49y. Window Cam Hide Tent", false, 4923)]
    public static void Apply()
    {
        sb = new StringBuilder("[Z49y] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(true); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49yr. Window Cam Hide Revert", false, 4924)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z49yr] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(false); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static void Inner(bool on)
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var l = tm.FindProperty("layers").GetArrayElementAtIndex(LAYER);
        if (string.IsNullOrEmpty(l.stringValue)) { l.stringValue = "WindowCamHide"; tm.ApplyModifiedProperties(); sb.AppendLine("레이어 25 = WindowCamHide"); }
        else if (l.stringValue != "WindowCamHide") { sb.AppendLine("!! 레이어 25 이름이 이미 '" + l.stringValue + "' → 중단"); return; }

        foreach (var path in TARGETS)
        {
            var go = GameObject.Find(path);
            if (go == null) { sb.AppendLine("!! 없음 " + path); continue; }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                int before = t.gameObject.layer;
                t.gameObject.layer = on ? LAYER : 0;
                EditorUtility.SetDirty(t.gameObject);
                sb.AppendLine("  " + path + (t == go.transform ? "" : "/" + t.name) + " layer " + before + " → " + t.gameObject.layer);
            }
        }

        var wc = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "BedroomWindowCam");
        var wcam = wc ? wc.GetComponent<Camera>() : null;
        if (wcam) { int b = wcam.cullingMask; if (on) wcam.cullingMask &= ~(1 << LAYER); else wcam.cullingMask |= 1 << LAYER; EditorUtility.SetDirty(wcam); sb.AppendLine("창 카메라 " + V(wc.transform.position) + " yaw " + wc.transform.eulerAngles.y.ToString("F1") + " mask " + b + " → " + wcam.cullingMask); }
        else sb.AppendLine("!! BedroomWindowCam 없음");

        if (on)
        {
            foreach (var c in Object.FindObjectsOfType<Camera>(true))
            {
                if (c == wcam || (c.cullingMask & 1) == 0 || (c.cullingMask & (1 << LAYER)) != 0) continue;
                int b = c.cullingMask; c.cullingMask |= 1 << LAYER; EditorUtility.SetDirty(c);
                sb.AppendLine("  카메라 " + c.name + " mask " + b + " → " + c.cullingMask);
            }
            var mirrorType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                .FirstOrDefault(t => t.Name == "VRCMirrorReflection" && typeof(Component).IsAssignableFrom(t));
            if (mirrorType != null)
                foreach (var m in Object.FindObjectsOfType(mirrorType, true).Cast<Component>())
                {
                    var so = new SerializedObject(m); var p = so.FindProperty("m_ReflectLayers");
                    if (p == null) { sb.AppendLine("  거울 " + m.name + " m_ReflectLayers 없음"); continue; }
                    int b = p.intValue;
                    if ((b & 1) != 0 && (b & (1 << LAYER)) == 0) { p.intValue = b | (1 << LAYER); so.ApplyModifiedPropertiesWithoutUndo(); }
                    sb.AppendLine("  거울 " + m.name + " reflect " + b + " → " + p.intValue);
                }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_windowcam_hide.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
