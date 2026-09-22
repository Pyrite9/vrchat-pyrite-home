using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PyriteProTVScan
{
    [MenuItem("Tools/Pyrite/N. Scan ProTV", false, 270)]
    public static void Scan()
    {
        var lines = new List<string>();
        var prefabs = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.IndexOf("architech", System.StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(p => p).ToArray();
        lines.Add("── Retro 테마 ──");
        foreach (var p in prefabs.Where(p => p.Contains("/Retro/"))) lines.Add("  " + p);
        lines.Add("── TV 본체 ──");
        foreach (var p in prefabs.Where(p => p.Contains("/TVs/") || p.EndsWith("(ProTV).prefab"))) lines.Add("  " + p);
        Debug.Log("[ProTV]\n" + string.Join("\n", lines));
    }

    // 프리팹 하나를 트리로 펼쳐서 컴포넌트까지 찍는다. 배선 대상 이름을 알아야 스크립트를 쓸 수 있다.
    static void Dump(string path, List<string> outp, int maxDepth)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        outp.Add("══ " + path + (go == null ? "  ← 없음" : "") + " ══");
        if (go == null) return;
        Walk(go.transform, 0, outp, maxDepth);
    }

    static void Walk(Transform t, int d, List<string> outp, int maxDepth)
    {
        var comps = t.GetComponents<Component>()
            .Where(c => c != null && !(c is Transform))
            .Select(c => {
                var n = c.GetType().Name;
                if (n == "UdonBehaviour")
                {
                    var so = new SerializedObject(c);
                    var p = so.FindProperty("programSource");
                    if (p != null && p.objectReferenceValue != null) n += ":" + p.objectReferenceValue.name;
                }
                return n;
            });
        outp.Add(new string(' ', d * 2) + "· " + t.name + "   [" + string.Join(", ", comps) + "]");
        if (d >= maxDepth) { if (t.childCount > 0) outp.Add(new string(' ', d * 2 + 2) + "… 자식 " + t.childCount + "개 생략"); return; }
        for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), d + 1, outp, maxDepth);
    }

    [MenuItem("Tools/Pyrite/N2. Dump ProTV Prefabs", false, 271)]
    public static void DumpPrefabs()
    {
        var o = new List<string>();
        Dump("Packages/dev.architech.protv/Simple (ProTV).prefab", o, 3);
        Dump("Packages/dev.architech.protv/Samples/Prefabs/TVs/Home Theater (ProTV).prefab", o, 2);
        Debug.Log("[ProTV dump A]\n" + string.Join("\n", o));

        var o2 = new List<string>();
        foreach (var p in AssetDatabase.FindAssets("t:Prefab")
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => p.Contains("/Retro/") && p.Contains("MediaControls"))
                     .OrderBy(p => p))
            Dump(p, o2, 2);
        Debug.Log("[ProTV dump B — Retro MediaControls]\n" + string.Join("\n", o2));
    }
}
