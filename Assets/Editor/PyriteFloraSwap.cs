// TsukinoStore FlowersGrassland 을 Terrain Detail 프로토타입으로 전환 (에디터 전용)
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PyriteFloraSwap
{
    const string TS  = "Assets/つきのすとあ/FlowersGrassland/";
    const string OUT = "Assets/Flora/";

    // 소스 프리팹 → 만들 프로토타입 이름. Day 자식만 쓴다(Night는 시간대 작업에서).
    static readonly (string src, string outName)[] SRC = new[]
    {
        ("1_Prefabs/Parts/Flower (5) Nemophila.prefab",         "P_TS_Nemophila"),
        ("1_Prefabs/Parts/Flower (1) Myosotis_Meadow.prefab",   "P_TS_Myosotis"),
        ("1_Prefabs/Parts/Flower (2) Dandelion_Puffball.prefab","P_TS_Dandelion"),
    };

    [MenuItem("Tools/Pyrite/H. Build TS Flower Prototypes &#8")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Flora"))
            AssetDatabase.CreateFolder("Assets", "Flora");

        foreach (var s in SRC)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(TS + s.src);
            if (src == null) { Debug.LogError("[Pyrite] 소스 없음: " + TS + s.src); continue; }

            // Day 자식 찾기 (이름에 night 없는 쪽)
            MeshFilter dayMF = null; MeshRenderer dayMR = null;
            MeshFilter nightMF = null; MeshRenderer nightMR = null;
            foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                bool isNight = mf.name.ToLower().Contains("night");
                if (isNight) { nightMF = mf; nightMR = mr; }
                else if (dayMF == null) { dayMF = mf; dayMR = mr; }
            }
            if (dayMF == null) { Debug.LogError("[Pyrite] Day 메시 못 찾음: " + s.src); continue; }

            var go = new GameObject(s.outName);
            var nmf = go.AddComponent<MeshFilter>();
            nmf.sharedMesh = dayMF.sharedMesh;
            var nmr = go.AddComponent<MeshRenderer>();
            nmr.sharedMaterial = dayMR.sharedMaterial;
            nmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            nmr.receiveShadows = false;
            nmr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            // 원본 자식의 로컬 변환을 루트에 흡수
            go.transform.localScale = dayMF.transform.lossyScale;

            if (dayMR.sharedMaterial != null && !dayMR.sharedMaterial.enableInstancing)
            { dayMR.sharedMaterial.enableInstancing = true; EditorUtility.SetDirty(dayMR.sharedMaterial); }
            if (nightMR != null && nightMR.sharedMaterial != null && !nightMR.sharedMaterial.enableInstancing)
            { nightMR.sharedMaterial.enableInstancing = true; EditorUtility.SetDirty(nightMR.sharedMaterial); }

            var path = OUT + s.outName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            var m = dayMF.sharedMesh;
            var b = m.bounds;
            Debug.Log(string.Format(
                "[Pyrite] {0}  tris {1}  verts {2}  bounds w{3:0.00} h{4:0.00} d{5:0.00}  mat={6} / night={7}  shader={8}",
                s.outName, m.triangles.Length / 3, m.vertexCount,
                b.size.x, b.size.y, b.size.z,
                dayMR.sharedMaterial == null ? "<none>" : dayMR.sharedMaterial.name,
                nightMR == null || nightMR.sharedMaterial == null ? "<none>" : nightMR.sharedMaterial.name,
                dayMR.sharedMaterial == null ? "?" : dayMR.sharedMaterial.shader.name));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Pyrite] TS 꽃 프로토타입 생성 완료");
    }
}
#endif
