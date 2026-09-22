// 호수 부두(잔교) — 에디터 전용  [dock v1]
//  · 메시는 파이썬 생성(PyriteDock.obj). 캠프 화로와 같은 축(x=-10)에서
//    물가 z43.9 → 수심 2.3m 지점 z31.0까지 12.9m.
//  · 덱 상면 y=0.50. 뭍 끝 지면이 0.51이라 올라서는 턱이 없다.
//  · 판자 사이 3cm 틈으로 빠지지 않게 충돌은 별도 BoxCollider 하나로 덮는다.
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteDockTools
{
    const string MESH_DIR = "Assets/Meshes/";
    const string MAT_DIR  = "Assets/Materials/";
    const string TEX_DIR  = "Assets/TerrainAssets/";

    static Material EnsureWoodMaterial()
    {
        var path = MAT_DIR + "M_DockWood.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        var texPath = TEX_DIR + "T_DockWood.png";
        var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null && ti.wrapMode != TextureWrapMode.Repeat)
        { ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) Debug.LogWarning("[Pyrite] 부두 텍스처 없음: " + texPath);
        mat.mainTexture = tex;
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.18f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    [MenuItem("Tools/Pyrite/C. Place Dock &#3")]
    public static void PlaceDock()
    {
        var objPath = MESH_DIR + "PyriteDock.obj";

        // 라이트맵 UV는 임포터가 굽는다
        var mi = AssetImporter.GetAtPath(objPath) as ModelImporter;
        if (mi == null) { Debug.LogError("[Pyrite] 모델 없음: " + objPath); return; }
        if (!mi.generateSecondaryUV || mi.secondaryUVPackMargin < 20)
        {
            mi.generateSecondaryUV = true;
            mi.secondaryUVPackMargin = 24;
            mi.SaveAndReimport();
        }

        GameObject root = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == "Dock") root = r;
        if (root != null) Undo.DestroyObjectImmediate(root);
        root = new GameObject("Dock");
        Undo.RegisterCreatedObjectUndo(root, "dock");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "DockMesh";
        inst.transform.SetParent(root.transform, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        var mat = EnsureWoodMaterial();
        int tris = 0;
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = mat;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
        }

        var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
                  | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic
                  | StaticEditorFlags.ReflectionProbeStatic;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

        // 덱 전체를 덮는 단일 충돌판 (판자 틈으로 빠지는 것 방지)
        Bounds db = new Bounds(); bool hd = false;
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
        { if (!hd) { db = r.bounds; hd = true; } else db.Encapsulate(r.bounds); }

        var col = new GameObject("Deck_Collider");
        Undo.RegisterCreatedObjectUndo(col, "dock collider");
        col.transform.SetParent(root.transform, false);
        // 메시 바운즈에서 길이를 가져온다 — 부두 길이를 바꿔도 따라온다
        col.transform.position = new Vector3(db.center.x, 0.43f, db.center.z);
        var bc = col.AddComponent<BoxCollider>();
        bc.size = new Vector3(2.04f, 0.16f, db.size.z + 0.08f);

        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf2 = r.GetComponent<MeshFilter>();
            var lb = mf2 != null && mf2.sharedMesh != null ? mf2.sharedMesh.bounds : new Bounds();
            Debug.Log(string.Format("[Pyrite] 부두 바운즈  렌더러 c({0:0.00},{1:0.00},{2:0.00}) s({3:0.00},{4:0.00},{5:0.00})  / 메시로컬 c({6:0.00},{7:0.00},{8:0.00}) s({9:0.00},{10:0.00},{11:0.00})",
                r.bounds.center.x, r.bounds.center.y, r.bounds.center.z,
                r.bounds.size.x, r.bounds.size.y, r.bounds.size.z,
                lb.center.x, lb.center.y, lb.center.z, lb.size.x, lb.size.y, lb.size.z));
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pyrite] 부두 배치 완료 — 삼각형 " + tris
                  + " / 덱 x(-11.0~-9.0) z(31.0~43.9) 상면 y0.50 / 충돌판 1개");
    }
}
#endif
