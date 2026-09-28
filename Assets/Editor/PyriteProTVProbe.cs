// PyriteProTVProbe.cs — 씬 ProTV 구조 실측 (Z50f, 읽기 전용). Logs/pyrite_protv_probe.txt
//  TVManager 가 붙은 오브젝트: 경로, 원본 프리팹, 자식 트리(깊이 4, 컴포넌트), TVManager 직렬화 값 / 패키지 안 ProTV 프리팹 목록 / 침실 −X 벽 치수
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteProTVProbe
{
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z50f. ProTV Probe", false, 5006)]
    public static void Run()
    {
        sb = new StringBuilder("[Z50f] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var all = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
            var tvs = all.Where(c => c.GetType().Name == "TVManager").ToArray();
            sb.AppendLine("TVManager " + tvs.Length + "개");
            foreach (var tv in tvs)
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(tv.gameObject);
                sb.AppendLine("== " + Path(tv.transform) + " | 프리팹 루트 " + (root ? Path(root.transform) + " ← " + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) : "없음"));
                var so = new SerializedObject(tv); var it = so.GetIterator();
                var line = new StringBuilder("  TVManager:");
                if (it.NextVisible(true)) do
                {
                    string v = it.propertyType switch
                    {
                        SerializedPropertyType.Boolean => it.boolValue.ToString(),
                        SerializedPropertyType.Integer => it.intValue.ToString(),
                        SerializedPropertyType.Float => it.floatValue.ToString("G4"),
                        SerializedPropertyType.String => "\"" + it.stringValue + "\"",
                        SerializedPropertyType.ObjectReference => it.objectReferenceValue ? it.objectReferenceValue.name : "null",
                        SerializedPropertyType.Enum => it.intValue.ToString(),
                        _ => null
                    };
                    if (v != null) line.Append(" " + it.name + "=" + v);
                } while (it.NextVisible(false));
                sb.AppendLine(line.ToString());
                Tree(root ? root.transform : tv.transform, 0);
            }
            sb.AppendLine("-- ProTV 관련 프리팹(에셋)");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.IndexOf("protv", System.StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("architech", System.StringComparison.OrdinalIgnoreCase) >= 0) sb.AppendLine("  " + p);
            }
            sb.AppendLine("-- 침실 −X 벽: y 0.2/1.0/1.8 에서 z −2..1 SurfX");
            foreach (float y in new[] { 0.2f, 1.0f, 1.8f })
            {
                var l = new StringBuilder("  y " + y + ":");
                for (float z = -2f; z <= 1.01f; z += 0.5f) l.Append(" z" + z.ToString("F1") + "=" + PyriteBedroomBuild.SurfX(z, y).ToString("F2"));
                sb.AppendLine(l.ToString());
            }
            var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
            foreach (Transform c in room.transform) { var r = c.GetComponentsInChildren<Renderer>(true); if (r.Length == 0) { sb.AppendLine("  " + c.name + " (렌더러 없음) " + c.localPosition.ToString("F2")); continue; } var b = r[0].bounds; foreach (var x in r) b.Encapsulate(x.bounds); sb.AppendLine("  " + c.name + " bounds(로컬) min " + (b.min - room.transform.position).ToString("F2") + " max " + (b.max - room.transform.position).ToString("F2")); }
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        sb.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_protv_probe.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }

    static void Tree(Transform t, int d)
    {
        if (d > 4) return;
        var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
        sb.AppendLine(new string(' ', 2 + d * 2) + t.name + (t.gameObject.activeSelf ? "" : " (꺼짐)") + " [" + comps + "] pos " + t.position.ToString("F2") + " scale " + t.lossyScale.ToString("F2"));
        foreach (Transform c in t) Tree(c, d + 1);
    }

    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
