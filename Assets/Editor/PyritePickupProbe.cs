// Tools ▸ Pyrite4 ▸ Z54c. Pickup Rigidbody Probe (읽기 전용)
//  2026-10-01 01:32 관리자 인게임(업로드 판): 잡았다가 땅에 떨어뜨리면 수직으로 굳고, 다시 들어도 각도가 안 변함
//  가설: Rigidbody.constraints = FreezeRotation. 처음엔 키네마틱이라 무관 → 놓기 처리(SetKinematic(false)) 뒤 동적 몸체가 되면
//        들고 있는 동안에도 회전이 잠김. 씬의 모든 VRCPickup 의 리지드바디·픽업·스크립트를 표로
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

public static class PyritePickupProbe
{
    [MenuItem("Tools/Pyrite4/Z54c. Pickup Rigidbody Probe", false, 5313)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z54c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var pks = Object.FindObjectsOfType<VRCPickup>(true).OrderBy(p => Path(p.transform)).ToArray();
            sb.AppendLine("pickups " + pks.Length);
            sb.AppendLine("이름 | kin/grav | constraints | orient/exact/auto | sync | 스크립트");
            foreach (var p in pks)
            {
                var rb = p.GetComponent<Rigidbody>();
                var sync = p.GetComponent<VRCObjectSync>();
                var ub = p.GetComponents<MonoBehaviour>().Where(m => m != null && m.GetType().Name == "UdonBehaviour")
                    .Select(m => { var so = new SerializedObject(m); var pr = so.FindProperty("serializedProgramAsset"); return pr != null && pr.objectReferenceValue ? pr.objectReferenceValue.name : "?"; });
                var ush = p.GetComponents<MonoBehaviour>().Where(m => m != null && m.GetType().BaseType != null && m.GetType().BaseType.Name == "UdonSharpBehaviour").Select(m => m.GetType().Name);
                sb.AppendLine(string.Format("{0} | {1} | {2} | {3}/{4}/{5} | {6} | {7}",
                    Path(p.transform),
                    rb ? (rb.isKinematic ? "K" : "d") + "/" + (rb.useGravity ? "G" : "-") : "no rb",
                    rb ? rb.constraints.ToString() : "-",
                    p.orientation, p.ExactGrip ? p.ExactGrip.name : "-", p.AutoHold,
                    sync ? "sync" : "-",
                    string.Join(",", ush.Concat(ub))));
            }
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_pickupprobe.txt", sb.ToString(), new UTF8Encoding(false));
    }

    static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
