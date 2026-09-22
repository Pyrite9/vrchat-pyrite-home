// 수면 위를 걸을 수 있게 — 에디터 전용
//  호수 전체를 덮는 얇은 BoxCollider 한 장. 메시 콜라이더보다 싸고,
//  두께가 0.2m라 빠르게 움직여도 빠지지 않는다.
//  물 밖(뭍)에서는 지면이 y=0보다 높아서 이 판이 땅 아래로 묻힌다 → 영향 없음.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteWaterTools
{
    const float TOP    = 0.05f;    // 판 윗면 높이 (수면 y=0 바로 위)
    const float THICK  = 0.20f;
    const float EXTENT = 120f;     // 한 변. 호수 지름 112m를 덮고도 남는다

    [MenuItem("Tools/Pyrite/G. Make Water Walkable &6")]
    public static void MakeWalkable()
    {
        var scene = SceneManager.GetActiveScene();
        GameObject water = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "Water") water = r;
        if (water == null) { Debug.LogError("[Pyrite] Water 못 찾음"); return; }

        Transform t = null;
        foreach (Transform c in water.transform) if (c.name == "WaterWalk") t = c;
        if (t == null)
        {
            var go = new GameObject("WaterWalk");
            Undo.RegisterCreatedObjectUndo(go, "water collider");
            t = go.transform;
            t.SetParent(water.transform, true);   // worldPositionStays — 부모 스케일 11.6을 안 물려받게
        }
        t.position = new Vector3(water.transform.position.x,
                                 TOP - THICK * 0.5f,
                                 water.transform.position.z);
        t.rotation = Quaternion.identity;
        // 부모 스케일 11.6을 상쇄해서 로컬 스케일 1을 만든다
        Vector3 ps = water.transform.lossyScale;
        t.localScale = new Vector3(1f / Mathf.Max(ps.x, 1e-4f), 1f, 1f / Mathf.Max(ps.z, 1e-4f));

        var bc = t.GetComponent<BoxCollider>();
        if (bc == null) bc = Undo.AddComponent<BoxCollider>(t.gameObject);
        Undo.RecordObject(bc, "water box");
        bc.center = Vector3.zero;
        bc.size = new Vector3(EXTENT, THICK, EXTENT);
        bc.isTrigger = false;

        GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        var b = bc.bounds;
        Debug.Log(string.Format(
            "[Pyrite] 수면 콜라이더 — 윗면 y {0:0.000} / 월드 바운즈 c({1:0.0},{2:0.000},{3:0.0}) s({4:0.0},{5:0.00},{6:0.0})"
            + "   (끄려면 Water/WaterWalk 오브젝트를 비활성화)",
            b.max.y, b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z));

        EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
