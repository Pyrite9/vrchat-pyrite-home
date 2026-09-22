using System.IO;
using UnityEditor;
using UnityEngine;

// 지면 dusk 텍스처 재생성
//
//  T_grass_dusk.png / T_Ground_dusk.png 는 つきのすとあ FlowersGrassland 의 T_grass.png / T_Ground.png 를
//  색만 누른 파생본이다(원본 잔디가 형광 연두라 황혼에 튀었다). 유료 에셋 파생물이라 git 에는 올리지 않고,
//  클론 후 팩을 임포트한 다음 이 메뉴로 다시 만든다.
//
//  변환식은 기존 결과물에서 역산했다 — 채널별 y = gain·x + offset (sRGB 0~1).
//  재현 오차: 평균 0.10 / 0.16 (0~255 단위), 사실상 동일.
public static class PyriteDuskTextures
{
    const string SRC = "Assets/つきのすとあ/FlowersGrassland/Materials/Ground/Texture/";
    const string DST = "Assets/TerrainAssets/";

    struct Job { public string src, dst, layer; public Vector3 g, o; }
    static readonly Job[] JOBS =
    {
        new Job { src = "T_grass.png",  dst = "T_grass_dusk.png",  layer = "TL_Grass",
                  g = new Vector3(0.5856f, 0.5463f, 0.5363f), o = new Vector3( 0.0242f, -0.0231f, 0.0512f) },
        new Job { src = "T_Ground.png", dst = "T_Ground_dusk.png", layer = "TL_Dirt",
                  g = new Vector3(0.6889f, 0.7085f, 0.8151f), o = new Vector3(-0.0007f,  0.0022f, 0.0048f) },
    };

    [MenuItem("Tools/Pyrite/A2. Rebuild Dusk Ground Textures", false, 120)]
    public static void Rebuild()
    {
        int ok = 0;
        foreach (var j in JOBS)
        {
            string sp = SRC + j.src;
            if (!File.Exists(sp)) { Debug.LogError("[DUSK] 원본 없음 — FlowersGrassland 팩을 먼저 임포트할 것: " + sp); continue; }

            // 임포트 설정(압축·크기 제한)을 타지 않게 PNG 바이트를 직접 디코드한다
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            tex.LoadImage(File.ReadAllBytes(sp));
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color32(
                    Map(c.r, j.g.x, j.o.x), Map(c.g, j.g.y, j.o.y), Map(c.b, j.g.z, j.o.z), 255);
            }
            var outp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, false);
            outp.SetPixels32(px);
            outp.Apply();
            File.WriteAllBytes(DST + j.dst, outp.EncodeToPNG());
            Object.DestroyImmediate(tex); Object.DestroyImmediate(outp);
            AssetDatabase.ImportAsset(DST + j.dst, ImportAssetOptions.ForceUpdate);

            // 지형 레이어에 다시 연결 (클론 직후엔 GUID 가 끊겨 있다)
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DST + j.layer + ".terrainlayer");
            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(DST + j.dst);
            if (layer != null && t2 != null) { layer.diffuseTexture = t2; EditorUtility.SetDirty(layer); }
            ok++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[DUSK] 재생성 " + ok + "/" + JOBS.Length + " — TL_Grass / TL_Dirt 에 다시 연결");
    }

    static byte Map(byte v, float g, float o)
    {
        float y = g * (v / 255f) + o;
        return (byte)Mathf.Clamp(Mathf.RoundToInt(y * 255f), 0, 255);
    }
}
