// 하이트맵 RAW 적용 — 에디터 전용
//  Terrain ▸ Import Raw 대화창은 손으로 눌러야 하고 축/바이트순서 옵션을 틀리기 쉽다.
//  직접 SetHeights 한다. 적용 후 알려진 좌표를 샘플링해서 방향이 맞는지 같이 찍는다.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteHeightTools
{
    const string RAW = "Terrain.raw";     // World/ 아래
    const int    RES = 513;
    const float  TY  = 60f;               // Terrain size.y
    const float  TBASE = -10f;            // Terrain position.y

    static Terrain FindTerrain()
    {
        var t = Terrain.activeTerrain;
        if (t != null) return t;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        { var x = r.GetComponentInChildren<Terrain>(); if (x != null) return x; }
        return null;
    }

    [MenuItem("Tools/Pyrite/D. Import Heightmap RAW &#1")]
    public static void ImportRaw()
    {
        var terrain = FindTerrain();
        if (terrain == null) { Debug.LogError("[Pyrite] Terrain 못 찾음"); return; }
        var td = terrain.terrainData;

        var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RAW);
        if (!File.Exists(path)) { Debug.LogError("[Pyrite] 없음: " + path); return; }
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length != RES * RES * 2)
        { Debug.LogError("[Pyrite] RAW 크기 불일치 " + bytes.Length + " (기대 " + RES*RES*2 + ")"); return; }

        if (td.heightmapResolution != RES)
        { Undo.RegisterCompleteObjectUndo(td, "res"); td.heightmapResolution = RES; }

        var h = new float[RES, RES];      // [z, x]
        for (int iz = 0; iz < RES; iz++)
            for (int ix = 0; ix < RES; ix++)
            {
                int o = (iz * RES + ix) * 2;
                int v = bytes[o] | (bytes[o + 1] << 8);       // little endian
                h[iz, ix] = v / 65535f;
            }

        Undo.RegisterCompleteObjectUndo(td, "heightmap");
        td.SetHeights(0, 0, h);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        // ── 방향 검증: 알려진 좌표를 샘플링한다 ──
        var probes = new[]
        {
            new Vector3(-10f, 0f, 53.5f),   // 캠프 패드 중심  기대 +1.81
            new Vector3(-10f, 0f, 43.0f),   // 부두 뭍끝 근처  기대 +0.64
            new Vector3(-10f, 0f, 47.0f),   // 새 비탈 중간    기대 +1.68
            new Vector3(  0f, 0f,-14.0f),   // 호수 바닥       기대 -8.00
            new Vector3( 78f, 0f,  0f),     // 절벽 안쪽       기대 큰 양수
        };
        foreach (var p in probes)
            Debug.Log(string.Format("[Pyrite] 샘플 ({0,6:0.0},{1,6:0.0}) → y {2:+0.000;-0.000}",
                      p.x, p.z, terrain.SampleHeight(p) + terrain.transform.position.y));

        Debug.Log("[Pyrite] 하이트맵 적용 완료 — " + RES + "x" + RES
                  + " / size.y " + td.size.y + " / pos.y " + terrain.transform.position.y);
    }
}
#endif
