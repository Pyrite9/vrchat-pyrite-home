// 베이크 전 정리 — 에디터 전용
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBakeTools
{
    [MenuItem("Tools/Pyrite/7. Fix Lightmap UVs on Props")]
    public static void FixPropUVs()
    {
        string[] roots = { "Assets/Noagami" };
        var guids = AssetDatabase.FindAssets("t:Model", roots);
        int changed = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) continue;
            if (mi.generateSecondaryUV) continue;
            mi.generateSecondaryUV = true;
            mi.secondaryUVAngleDistortion = 8;
            mi.secondaryUVAreaDistortion = 15;
            mi.secondaryUVHardAngle = 88;
            mi.secondaryUVPackMargin = 12;
            mi.SaveAndReimport();
            changed++;
            Debug.Log("[Pyrite] 라이트맵 UV 생성: " + path);
        }
        Debug.Log("[Pyrite] 모델 " + changed + "개에 라이트맵 UV 추가 (총 " + guids.Length + "개 검사)");
    }

    [MenuItem("Tools/Pyrite/8. Pre-bake Fixups")]
    public static void PreBakeFixups()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name == "RP_Lake")
            {
                Undo.RecordObject(r.transform, "rp");
                r.transform.position = new Vector3(0f, 6f, -14f);
                var p = r.GetComponent<ReflectionProbe>();
                if (p != null) { p.size = new Vector3(118f, 34f, 118f); p.center = Vector3.zero; }
                Debug.Log("[Pyrite] RP_Lake → (0,6,-14) size 118");
            }
            if (r.name == "RP_Shore")
            {
                var p = r.GetComponent<ReflectionProbe>();
                if (p != null) { p.size = new Vector3(120f, 34f, 66f); }
                Debug.Log("[Pyrite] RP_Shore size 120x34x66");
            }
            if (r.name == "RP_Far")
            {
                Undo.RecordObject(r.transform, "rp");
                r.transform.position = new Vector3(0f, 14f, -74f);
                var p = r.GetComponent<ReflectionProbe>();
                if (p != null) { p.size = new Vector3(120f, 54f, 46f); }
                Debug.Log("[Pyrite] RP_Far → (0,14,-74)");
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Pyrite] 베이크 전 정리 완료");
    }
}
#endif
