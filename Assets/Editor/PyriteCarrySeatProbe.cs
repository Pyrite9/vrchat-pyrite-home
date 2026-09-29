// PyriteCarrySeatProbe.cs — 들고 다니는 자리(PyriteCarrySeat) 전수 점검, 읽기 전용 (Z52k)
//  2026-09-30 관리자: 캠프 의자가 앉아 있어도 들린다 — 코드는 앉으면 pickup.pickupable = false 인데?
//  보는 것: 같은 오브젝트에 VRCStation 이 있는지(Station 이벤트는 같은 오브젝트의 Udon 에만 옴), pickup 필드(프록시 · Udon 변수)가 무엇을 가리키는지,
//          그 pickup 이 이 자리의 조상(루트)의 VRCPickup 인지, 루트 VRCPickup 수, 활성/EditorOnly
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteCarrySeatProbe
{
    [MenuItem("Tools/Pyrite3/Z52k. Carry Seat Probe", false, 5137)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z52k] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var all = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PyriteCarrySeat>(true)).ToArray();
            sb.AppendLine("PyriteCarrySeat " + all.Length + "개");
            foreach (var cs in all)
            {
                var t = cs.transform;
                string path = t.name; for (var p = t.parent; p; p = p.parent) path = p.name + "/" + path;
                var st = cs.GetComponent<VRCStation>();
                var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(cs);
                object udonPk = null; bool has = ub != null && ub.publicVariables.TryGetVariableValue("pickup", out udonPk);
                object udonSt = null; if (ub != null) ub.publicVariables.TryGetVariableValue("station", out udonSt);
                var rootPk = t.GetComponentsInParent<VRCPickup>(true);
                var proxyPk = cs.pickup;
                string Desc(Object o) => o == null ? "null" : (o is Component c ? c.gameObject.name : o.name);
                bool match = proxyPk != null && rootPk.Contains(proxyPk);
                sb.AppendLine("- " + path + (t.gameObject.activeInHierarchy ? "" : " (꺼짐)") + (t.root.CompareTag("EditorOnly") || t.GetComponentsInParent<Transform>(true).Any(x => x.CompareTag("EditorOnly")) ? " [EditorOnly]" : ""));
                sb.AppendLine("    같은 오브젝트 VRCStation " + (st != null) + " · Udon 수 " + t.GetComponents<VRC.Udon.UdonBehaviour>().Length
                              + " · 프록시 pickup " + Desc(proxyPk) + " · Udon 변수 pickup " + (has ? Desc(udonPk as Object) : "(없음)") + " · station " + Desc(udonSt as Object)
                              + " · 조상 VRCPickup " + rootPk.Length + " (" + string.Join(", ", rootPk.Select(p => p.gameObject.name + (p.pickupable ? "" : " pickupable=false"))) + ")"
                              + " · pickup 이 조상과 같음 " + match);
            }
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_carryseat_probe.txt", sb.ToString(), new UTF8Encoding(false));
    }
}
#endif
