// Tools ▸ Pyrite4 ▸ Z54a. Free-Grip Pickups (Mug·Kettle·Beer) / Z54b. Free-Grip Revert
//  2026-09-30 23:19 관리자 인게임: PC/VR 을 런타임에 나눈 주전자·머그·맥주가 "처음 잡을 땐 VR 방식, 다시 잡으면 PC 방식" → 구분을 없애고 전부 자유 회전
//  Z54a: 세 종류의 VRCPickup 을 orientation Any + ExactGrip 없음(잡은 자세 그대로), AutoHold Yes(누구나 같게), Visual 회전 identity
//  Z54b: orientation Grip + ExactGrip = 자식 "Grip" (Z31b · Z53b 가 만든 원래 값)
//  ⚠ Z31b(캠프 소품) 를 다시 돌리면 머그·주전자가 Grip 으로 돌아간다 → 그 뒤 Z54a 다시. Z53b(맥주)는 처음부터 Any 로 만든다
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

public static class PyriteFreeGrip
{
    [MenuItem("Tools/Pyrite4/Z54a. Free-Grip Pickups (Mug·Kettle·Beer)", false, 5311)]
    public static void Apply() { Run(true); }

    [MenuItem("Tools/Pyrite4/Z54b. Free-Grip Revert", false, 5312)]
    public static void Revert() { Run(false); }

    static void Run(bool free)
    {
        var sb = new StringBuilder("[" + (free ? "Z54a" : "Z54b") + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var roots = Object.FindObjectsOfType<PyriteMug>(true).Select(x => x.gameObject)
                .Concat(Object.FindObjectsOfType<PyriteKettle>(true).Select(x => x.gameObject))
                .Concat(Object.FindObjectsOfType<PyriteBeer>(true).Select(x => x.gameObject)).ToArray();
            foreach (var go in roots)
            {
                var pk = go.GetComponent<VRCPickup>();
                if (pk == null) { sb.AppendLine("  !! pickup 없음 " + go.name); continue; }
                var before = pk.orientation + "/" + (pk.ExactGrip ? pk.ExactGrip.name : "-") + "/" + pk.AutoHold;
                Undo.RecordObject(pk, "free grip");
                if (free) { pk.orientation = VRC_Pickup.PickupOrientation.Any; pk.ExactGrip = null; }
                else { pk.orientation = VRC_Pickup.PickupOrientation.Grip; pk.ExactGrip = go.transform.Find("Grip"); }
                pk.ExactGun = null;
                pk.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
                EditorUtility.SetDirty(pk);
                var vis = go.transform.Find("Visual");
                if (vis != null && vis.localRotation != Quaternion.identity) { Undo.RecordObject(vis, "free grip"); vis.localRotation = Quaternion.identity; }
                sb.AppendLine(string.Format("  {0}: {1} → {2}/{3}/{4}", go.name, before, pk.orientation, pk.ExactGrip ? pk.ExactGrip.name : "-", pk.AutoHold));
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            sb.AppendLine("  pickups " + roots.Length + "\nRESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_freegrip.txt", sb.ToString());
    }
}
#endif
