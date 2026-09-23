// Tools ▸ Pyrite ▸ A3 — recreate the two tuned materials that live inside paid package folders.
// The .mat files are not in git (they sit in Booth package folders). This writes them back with the
// original GUIDs so the scene references reconnect. Run after importing Sakana-Water and Sorafield Atmosphere Sky.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PyriteTunedMaterials
{
    struct Item { public string path, guid, shaderGuid, yaml; }

    static readonly Item[] Items =
    {
        new Item { path = "Assets/Sakana-Water/M_LakeWater.mat", guid = "80c3c38a0cc6d0641a34a4419c9a6668",
                   shaderGuid = "68f6778c80c0bbf4582d612faae2fe4d", yaml = LAKE },
        new Item { path = "Assets/Sorafield Atmosphere Sky/Materials/M_Sky_PyriteDusk.mat", guid = "89e9793cd7a0db74f97c30da12a72fb3",
                   shaderGuid = "3bcf0d0bff74f424a9ffc58d75ae6e0b", yaml = SKY },
    };

    const string META = "fileFormatVersion: 2\nguid: {0}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

    [MenuItem("Tools/Pyrite/A3. Recreate Tuned Materials")]
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        foreach (var it in Items)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(it.shaderGuid)))
            { Debug.LogWarning("[A3] shader not found (import the Booth package first): " + it.path); continue; }
            string full = Path.Combine(root, it.path);
            if (File.Exists(full)) { Debug.Log("[A3] exists, skipped: " + it.path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full + ".meta", string.Format(META, it.guid));
            File.WriteAllText(full, it.yaml.Replace("\r\n", "\n"));
            AssetDatabase.ImportAsset(it.path, ImportAssetOptions.ForceUpdate);
            Debug.Log("[A3] created: " + it.path);
        }
        AssetDatabase.Refresh();
    }

    const string LAKE = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: M_LakeWater
  m_Shader: {fileID: 4800000, guid: 68f6778c80c0bbf4582d612faae2fe4d, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 3000
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BumpMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailAlbedoMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailMask:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailNormalMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _EmissionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MainTex:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MetallicGlossMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _NormalMap:
        m_Texture: {fileID: 2800000, guid: df14a30500fad634da4312f1ad0ea5e0, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _OcclusionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _ParallaxMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _Alpha: 1
    - _BumpScale: 1
    - _Cutoff: 0.5
    - _DetailNormalMapScale: 1
    - _DstBlend: 0
    - _FresnelPower: 3
    - _GlossMapScale: 1
    - _Glossiness: 0.59999996
    - _GlossyReflections: 1
    - _Metallic: 0
    - _Mode: 0
    - _NormalScale: 0.25
    - _OcclusionStrength: 1
    - _Parallax: 0.02
    - _RefractionStrength: 0.03
    - _Smoothness: 0.95
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _UVSec: 0
    - _WaterDepth: 0.521
    - _WaveAmplitude: 0
    - _WaveFrequency: 15
    - _WaveSpeed: 0.4
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {r: 0.2901961, g: 0.36862746, b: 0.3764706, a: 1}
    - _Color: {r: 0.9063317, g: 0.9063317, b: 0.9063317, a: 1}
    - _DeepColor: {r: 0.078431375, g: 0.13333334, b: 0.16470589, a: 1}
    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}
  m_BuildTextureStacks: []
";

    const string SKY = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: M_Sky_PyriteDusk
  m_Shader: {fileID: 4800000, guid: 3bcf0d0bff74f424a9ffc58d75ae6e0b, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords:
  - _INSHADERTONEMAP_ON
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _CloudCoverage: 0.4
    - _CloudSpeed: 0.012
    - _Exposure: 1.1
    - _HorizonHaze: 1.5
    - _InShaderTonemap: 1
    - _MieG: 0.85
    - _MieStrength: 2.4
    - _SunAzimuth: 184
    - _SunElevation: 0.17
    m_Colors: []
  m_BuildTextureStacks: []
";
}
#endif
