// PyriteQvPenProbe.cs — QvPen 프리팹 구조 실측 (Z51e, 읽기 전용). Logs/pyrite_qvpen_probe.txt
//  패키지 프리팹 목록, QvPen.prefab 트리(깊이 3, 컴포넌트·U# 타입·크기), 캠프 인스턴스 위치·삼각형, U# 공개 필드 값(펜 개수·색 등)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteQvPenProbe
{
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z51e. QvPen Probe", false, 5105)]
    public static void Run()
    {
        sb = new StringBuilder("[Z51e] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        sb.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_qvpen_probe.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }

    static void Inner()
    {
        sb.AppendLine("== 패키지 프리팹");
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Packages/net.ureishi.qvpen" })) sb.AppendLine("  " + AssetDatabase.GUIDToAssetPath(g));
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/net.ureishi.qvpen/QvPen.prefab");
        if (asset == null) { sb.AppendLine("!! QvPen.prefab 없음"); return; }
        sb.AppendLine("== QvPen.prefab 트리");
        Tree(asset.transform, 0, 3);
        var inst = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "QvPen");
        if (inst)
        {
            int tris = inst.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).Sum(f => f.sharedMesh.triangles.Length / 3);
            sb.AppendLine("== 캠프 인스턴스 " + inst.transform.position.ToString("F2") + " yaw " + inst.transform.eulerAngles.y.ToString("F0") + ", 메시 삼각형(꺼진 것 포함) " + tris + ", UdonBehaviour " + inst.GetComponentsInChildren<Component>(true).Count(c => c && c.GetType().Name == "UdonBehaviour"));
            // U# 프록시(UdonSharpBehaviour) 공개 필드 중 숫자·bool·색·배열 길이
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m && m.GetType().BaseType != null && m.GetType().BaseType.Name == "UdonSharpBehaviour").GroupBy(m => m.GetType().Name).Select(g => g.First()))
            {
                var so = new SerializedObject(mb); var it = so.GetIterator(); var line = new StringBuilder("  [" + mb.GetType().Name + "] @ " + mb.name + ":");
                if (it.NextVisible(true)) do
                {
                    if (it.name == "m_Script") continue;
                    string v = it.propertyType switch
                    {
                        SerializedPropertyType.Boolean => it.boolValue.ToString(),
                        SerializedPropertyType.Integer => it.intValue.ToString(),
                        SerializedPropertyType.Float => it.floatValue.ToString("G4"),
                        SerializedPropertyType.Color => it.colorValue.ToString(),
                        SerializedPropertyType.ObjectReference => it.objectReferenceValue ? it.objectReferenceValue.name : "null",
                        SerializedPropertyType.Generic => it.isArray ? "[" + it.arraySize + "]" : null,
                        _ => null
                    };
                    if (v != null) line.Append(" " + it.name + "=" + v);
                } while (it.NextVisible(false));
                sb.AppendLine(line.ToString());
            }
        }
    }

    static void Tree(Transform t, int d, int max)
    {
        if (d > max) return;
        var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
        var r = t.GetComponent<Renderer>();
        sb.AppendLine("  " + new string(' ', d * 2) + t.name + (t.gameObject.activeSelf ? "" : " (꺼짐)") + " [" + comps + "] lp " + t.localPosition.ToString("F3") + (r ? " size " + r.bounds.size.ToString("F2") : "") + " 자식 " + t.childCount);
        int shown = 0;
        foreach (Transform c in t)
        {
            if (shown >= 4 && c.name.StartsWith(t.GetChild(0).name.Split(' ')[0])) { continue; }
            Tree(c, d + 1, max); shown++;
        }
        if (t.childCount > shown) sb.AppendLine("  " + new string(' ', (d + 1) * 2) + "… 같은 이름 계열 " + (t.childCount - shown) + "개 더");
    }
}
#endif
