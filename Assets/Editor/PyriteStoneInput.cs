// Tools ▸ Pyrite2 ▸ Z40a. Stone Desktop Throw / Z40b. Stone Throw Revert
//  물수제비 돌 5개: AutoHold Yes(클릭 한 번에 들기, 머그와 같음) + UseText. 던지기는 PyriteSkipStone 의 OnPickupUseDown/Up
//  되돌림 = AutoHold AutoDetect(원래 값, 데스크톱은 누르고 있어야 들림), UseText 비움
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

public static class PyriteStoneInput
{
    [MenuItem("Tools/Pyrite2/Z40a. Stone Desktop Throw", false, 140)]
    public static void Apply() { Run(true); }

    [MenuItem("Tools/Pyrite2/Z40b. Stone Throw Revert", false, 141)]
    public static void Revert() { Run(false); }

    static void Run(bool on)
    {
        var sb = new StringBuilder("[" + (on ? "Z40a" : "Z40b") + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var stones = Object.FindObjectsOfType<PyriteSkipStone>(true);
            foreach (var s in stones)
            {
                var pk = s.GetComponent<VRCPickup>();
                if (pk == null) { sb.AppendLine("  !! pickup 없음 " + s.name); continue; }
                var before = pk.AutoHold;
                Undo.RecordObject(pk, "stone input");
                pk.AutoHold = on ? VRC_Pickup.AutoHoldMode.Yes : VRC_Pickup.AutoHoldMode.AutoDetect;
                pk.UseText = on ? "Throw (hold)" : "";
                EditorUtility.SetDirty(pk);
                s.throwMin = 7f; s.throwMax = 17f; s.chargeTime = 0.9f; s.releaseHeight = 0.8f;
                UdonSharpEditorUtility.CopyProxyToUdon(s); EditorUtility.SetDirty(s);
                sb.AppendLine(string.Format("  {0} AutoHold {1}({2}) → {3}({4})", s.name, before, (int)before, pk.AutoHold, (int)pk.AutoHold));
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            sb.AppendLine("  stones " + stones.Length + "\nRESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_stone.txt", sb.ToString());
    }
}
#endif
