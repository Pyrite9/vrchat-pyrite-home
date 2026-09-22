// 터레인 지면 레이어(스플랫) — 에디터 전용  [terrain v3: 3레이어 / 에셋 텍스처]
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteTerrainTools
{
    const string DIR = "Assets/TerrainAssets/";
    const string TS  = "Assets/つきのすとあ/FlowersGrassland/Materials/Ground/Texture/";

    static Terrain FindTerrain()
    {
        var t = Terrain.activeTerrain;
        if (t != null) return t;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        { var x = r.GetComponentInChildren<Terrain>(); if (x != null) return x; }
        return null;
    }

    static TerrainLayer MakeLayer(string texPath, string layerName, float tile, float smooth)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { Debug.LogError("[Pyrite] 텍스처 없음: " + texPath); return null; }
        var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null && ti.wrapMode != TextureWrapMode.Repeat)
        { ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.SaveAndReimport(); }

        var path = DIR + layerName + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        bool isNew = layer == null;
        if (isNew) layer = new TerrainLayer();
        layer.diffuseTexture = tex;
        layer.tileSize = new Vector2(tile, tile);
        layer.tileOffset = Vector2.zero;
        layer.metallic = 0f;
        layer.smoothness = smooth;
        layer.specular = new Color(0.06f, 0.06f, 0.06f, 1f);
        if (isNew) AssetDatabase.CreateAsset(layer, path);
        else EditorUtility.SetDirty(layer);
        return layer;
    }

    [MenuItem("Tools/Pyrite/A. Build Terrain Ground Layers &#7")]
    public static void BuildLayers()
    {
        if (!AssetDatabase.IsValidFolder("Assets/TerrainAssets"))
            AssetDatabase.CreateFolder("Assets", "TerrainAssets");

        var terrain = FindTerrain();
        if (terrain == null) { Debug.LogError("[Pyrite] Terrain 못 찾음"); return; }
        var td = terrain.terrainData;

        // 꽃 지면 레이어 제거 — 실제 꽃 메시가 깔리므로 거짓말하는 텍스처가 필요 없다
        var lGrass = MakeLayer(DIR + "T_grass_dusk.png",  "TL_Grass",  4.0f, 0.10f);
        var lDirt  = MakeLayer(DIR + "T_Ground_dusk.png", "TL_Dirt",   5.0f, 0.16f);
        var lRock  = MakeLayer(DIR + "T_Ground_Rock.png", "TL_Rock",   9.0f, 0.06f);
        // 호수 바닥 전용 — 물 밑을 흙으로 칠하면 반투명 수면으로 누렇게 비친다
        var lLake  = MakeLayer(DIR + "T_Lakebed.png",      "TL_Lakebed", 7.0f, 0.22f);
        if (lGrass == null || lDirt == null || lRock == null || lLake == null) return;

        var matPath = DIR + "M_TerrainGround.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var sh = Shader.Find("Nature/Terrain/Standard");
        if (sh == null) { Debug.LogError("[Pyrite] Nature/Terrain/Standard 셰이더 없음"); return; }
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = sh;
        EditorUtility.SetDirty(mat);
        Undo.RecordObject(terrain, "terrain material");
        terrain.materialTemplate = mat;

        Undo.RegisterCompleteObjectUndo(td, "terrain layers");
        td.terrainLayers = new[] { lGrass, lDirt, lRock, lLake };

        var binPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Splat4.bin");
        if (!File.Exists(binPath)) { Debug.LogError("[Pyrite] 없음: " + binPath); return; }
        var bytes = File.ReadAllBytes(binPath);
        int res = System.BitConverter.ToInt32(bytes, 0);
        int nl  = System.BitConverter.ToInt32(bytes, 4);
        int need = 8 + res * res * nl;
        if (bytes.Length != need || nl != 4)
        { Debug.LogError("[Pyrite] Splat4.bin 형식 불일치 len=" + bytes.Length + " res=" + res + " layers=" + nl); return; }

        td.alphamapResolution = res;
        var map = new float[res, res, nl];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                int o = 8 + (iz * res + ix) * nl;
                float sum = 0f;
                for (int l = 0; l < nl; l++) sum += bytes[o + l];
                if (sum < 1e-5f) { map[iz, ix, nl - 1] = 1f; continue; }
                for (int l = 0; l < nl; l++) map[iz, ix, l] = bytes[o + l] / sum;
            }
        td.SetAlphamaps(0, 0, map);

        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pyrite] 터레인 레이어 4종(풀/흙/암반/호수바닥) 적용 (alphamap " + res + ")");
    }
}
#endif
