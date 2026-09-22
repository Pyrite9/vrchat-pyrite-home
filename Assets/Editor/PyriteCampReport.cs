using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 현재 캠프 상태 진단 — 손으로 옮긴 오브젝트가 지면에 잘 붙었는지, 어디를 보고 있는지
public static class PyriteCampReport
{
    static readonly Vector2 FIRE = new Vector2(-10.5f, 51.5f);

    [MenuItem("Tools/Pyrite/P. Camp Report", false, 280)]
    public static void Report()
    {
        var t = Terrain.activeTerrain;
        var L = new List<string>();

        L.Add("── 주요 오브젝트 ──");
        foreach (var n in new[] { "Mirror", "MediaPlayer", "Dock", "Camp" })
            L.Add(Line(FindRoot(n), t, n));

        L.Add("── Camp 자식 ──");
        var camp = FindRoot("Camp");
        if (camp != null)
            foreach (Transform c in camp.transform)
                L.Add(Line(c.gameObject, t, c.name));

        L.Add("── 부두 자식 ──");
        var dock = FindRoot("Dock");
        if (dock != null)
            foreach (Transform c in dock.transform)
                L.Add("  " + c.name + "  " + c.position.ToString("F2"));

        Debug.Log("[REPORT]\n" + string.Join("\n", L));

        // 캠프 에셋 프리팹 목록 — 간이침대/랜턴 후보 찾기
        var P = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/") &&
                        p.IndexOf("architech", System.StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(p => p).ToArray();
        Debug.Log("[PREFABS] Assets 안 프리팹 " + P.Length + "개\n" + string.Join("\n", P));

        // fbx/모델도 같이 (프리팹이 아닌 에셋 팩은 모델로만 들어온다)
        var M = AssetDatabase.FindAssets("t:Model")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/"))
            .OrderBy(p => p).ToArray();
        Debug.Log("[MODELS] Assets 안 모델 " + M.Length + "개\n" + string.Join("\n", M));
    }

    static string Line(GameObject go, Terrain t, string label)
    {
        if (go == null) return "  " + label + "  ← 없음";
        var p = go.transform.position;
        float g = t != null ? t.SampleHeight(new Vector3(p.x, 0f, p.z)) + t.transform.position.y : 0f;
        Vector2 toFire = (FIRE - new Vector2(p.x, p.z));
        Vector3 f = go.transform.forward;
        float ang = Vector2.Angle(new Vector2(f.x, f.z), toFire.normalized);
        return string.Format("  {0,-26} pos {1}  y-지면 {2:+0.00;-0.00}  모닥불까지 {3:F1}m  모닥불과 각도 {4:F0}°",
            label, p.ToString("F2"), p.y - g, toFire.magnitude, ang);
    }

    static GameObject FindRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) return r;
        return null;
    }
}
